using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinPerf.Core.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    InvalidResponse,
    Error
}

public sealed record WinPerfUpdateManifest(
    int SchemaVersion,
    string ProductId,
    string Channel,
    string LatestVersion,
    string ReleaseTag,
    long ReleaseId,
    long AssetId,
    string AssetName,
    long AssetSize,
    string Sha256);

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version? LatestVersion,
    WinPerfUpdateManifest? Manifest,
    string? ErrorCode);

public sealed record SponsorProSession(
    string SessionId,
    string SessionToken,
    string? GithubLogin,
    string? SponsorAccount,
    string? SponsorTier,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsUsable =>
        !string.IsNullOrWhiteSpace(SessionId)
        && !string.IsNullOrWhiteSpace(SessionToken)
        && ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(5);
}

public sealed record SponsorProAuthStart(
    string AuthSessionId,
    string PollToken,
    Uri LoginUrl,
    Uri PollUrl,
    DateTimeOffset ExpiresAtUtc);

public sealed record SponsorProLoginResult(
    bool Success,
    SponsorProSession? Session,
    string? SponsorStatus,
    string? ErrorCode);

public sealed record UpdateDownloadTicket(Uri DownloadUrl, DateTimeOffset ExpiresAtUtc);

public sealed record StagedWinPerfUpdate(
    string StagingDirectory,
    string ExecutablePath,
    IReadOnlyList<string> RelativeFiles);

public sealed class WinPerfUpdateService : IDisposable
{
    public const string ProductId = "winperf";
    public const string Channel = "sponsor-pro";
    public const string AssetName = "WinPerf.zip";
    public const long MaximumAssetSize = 268_435_456;
    public const long MaximumExpandedSize = 536_870_912;
    public const string DefaultBaseUrl = "https://updates.watel.cloud";
    public const string LatestPath = "/v1/products/winperf/update/latest";
    public const string DownloadTokenPath = "/v1/products/winperf/update/download-token";
    public const string AuthStartPath = "/v1/auth/github/start";

    private const int MaximumJsonBytes = 1_048_576;
    private static readonly string[] RequiredPackageFiles = ["WinPerf.exe"];
    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex DownloadTokenPattern = new("^[A-Za-z0-9_-]{32,256}$", RegexOptions.CultureInvariant);

    private readonly HttpClient _client;
    private readonly Uri _baseUri;
    private readonly bool _ownsClient;

    public static IReadOnlyList<string> PackageFiles { get; } = Array.AsReadOnly(RequiredPackageFiles);

    public WinPerfUpdateService()
        : this(CreateClient(), new Uri(DefaultBaseUrl), true)
    {
    }

    public WinPerfUpdateService(HttpClient client, Uri? baseUri = null)
        : this(client, baseUri ?? new Uri(DefaultBaseUrl), false)
    {
    }

    private WinPerfUpdateService(HttpClient client, Uri baseUri, bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _baseUri = RequireSecureBaseUri(baseUri);
        _ownsClient = ownsClient;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, LatestPath));
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new(UpdateCheckStatus.Error, null, null, $"http_{(int)response.StatusCode}");
            }

            var payload = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            var manifest = ParseManifest(payload);
            var validationError = ValidateManifest(manifest);

            if (validationError is not null || !Version.TryParse(manifest.LatestVersion, out var latest))
            {
                return new(UpdateCheckStatus.InvalidResponse, null, null, validationError ?? "version_invalid");
            }

            var status = Normalize(latest) > Normalize(currentVersion)
                ? UpdateCheckStatus.UpdateAvailable
                : UpdateCheckStatus.UpToDate;

            return new(status, latest, manifest, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(UpdateCheckStatus.Error, null, null, "request_failed");
        }
    }

    public async Task<SponsorProAuthStart> StartLoginAsync(CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new { productId = ProductId });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, AuthStartPath))
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        var authSessionId = Text(payload, "authSessionId");
        var pollToken = Text(payload, "pollToken");
        var loginUrl = RequireLoginUri(Text(payload, "loginUrl"));
        var pollUrl = RequireSameOriginUri(Text(payload, "pollUrl"));
        var expiresAt = UnixTime(payload, "expiresAt");

        if (string.IsNullOrWhiteSpace(authSessionId)
            || string.IsNullOrWhiteSpace(pollToken)
            || expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidDataException("auth_start_invalid");
        }

        return new(authSessionId, pollToken, loginUrl, pollUrl, expiresAt);
    }

    public async Task<SponsorProLoginResult> PollLoginAsync(
        SponsorProAuthStart start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        while (DateTimeOffset.UtcNow < start.ExpiresAtUtc)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                authSessionId = start.AuthSessionId,
                pollToken = start.PollToken
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, start.PollUrl)
            {
                Content = new ByteArrayContent(body)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new(false, null, null, $"http_{(int)response.StatusCode}");
            }

            var payload = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            var status = Text(payload, "status");

            if (status is "github_authenticated" or "authenticated")
            {
                var sessionElement = payload.TryGetProperty("sponsorProSession", out var shared)
                    ? shared
                    : payload.TryGetProperty("mpmSession", out var legacy)
                        ? legacy
                        : default;

                if (payload.TryGetProperty("sessionTokenIssued", out var issued)
                    && issued.ValueKind == JsonValueKind.True
                    && sessionElement.ValueKind == JsonValueKind.Object)
                {
                    var session = new SponsorProSession(
                        Text(sessionElement, "sessionId"),
                        Text(sessionElement, "sessionToken"),
                        TextOrNull(payload, "githubLogin"),
                        TextOrNull(payload, "sponsorAccount"),
                        TextOrNull(payload, "sponsorTier"),
                        UnixTime(sessionElement, "expiresAt"));

                    return session.IsUsable
                        ? new(true, session, TextOrNull(payload, "sponsorStatus"), null)
                        : new(false, null, TextOrNull(payload, "sponsorStatus"), "session_invalid");
                }

                return new(
                    false,
                    null,
                    TextOrNull(payload, "sponsorStatus"),
                    TextOrNull(payload, "error") ?? "entitlement_inactive");
            }

            if (status is "failed" or "github_auth_failed" or "expired" or "consumed")
            {
                return new(
                    false,
                    null,
                    TextOrNull(payload, "sponsorStatus"),
                    TextOrNull(payload, "error") ?? status);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        return new(false, null, null, "auth_timeout");
    }

    public async Task<UpdateDownloadTicket> RequestDownloadTicketAsync(
        SponsorProSession session,
        WinPerfUpdateManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(manifest);

        if (!session.IsUsable)
        {
            throw new InvalidOperationException("session_invalid");
        }

        var manifestError = ValidateManifest(manifest);
        if (manifestError is not null)
        {
            throw new InvalidDataException(manifestError);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, DownloadTokenPath))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("SponsorPro-Session", session.SessionToken);

        using var response = await _client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(Text(payload, "status"), "ok", StringComparison.Ordinal)
            || !string.Equals(Text(payload, "productId"), ProductId, StringComparison.Ordinal)
            || !string.Equals(Text(payload, "tokenType"), "SponsorPro-Download", StringComparison.Ordinal))
        {
            throw new InvalidDataException("download_ticket_invalid");
        }

        var expiresAt = UnixTime(payload, "expiresAt");
        var downloadUrl = RequireDownloadUri(Text(payload, "downloadUrl"));

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidDataException("download_ticket_expired");
        }

        return new(downloadUrl, expiresAt);
    }

    public async Task<StagedWinPerfUpdate> DownloadAndStageAsync(
        UpdateDownloadTicket ticket,
        WinPerfUpdateManifest manifest,
        string stagingRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(manifest);

        if (ticket.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("download_ticket_expired");
        }

        var manifestError = ValidateManifest(manifest);
        if (manifestError is not null)
        {
            throw new InvalidDataException(manifestError);
        }

        var downloadUri = RequireDownloadUri(ticket.DownloadUrl.AbsoluteUri);
        var root = Path.GetFullPath(stagingRoot ?? throw new ArgumentNullException(nameof(stagingRoot)));
        Directory.CreateDirectory(root);

        var stagingDirectory = Path.Combine(root, $"winperf-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        var archivePath = Path.Combine(stagingDirectory, "WinPerf.zip.part");

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUri))
            using (var response = await _client
                       .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is long contentLength
                    && contentLength != manifest.AssetSize)
                {
                    throw new InvalidDataException("download_size_invalid");
                }

                if (response.Content.Headers.ContentType?.MediaType is not "application/zip")
                {
                    throw new InvalidDataException("download_content_type_invalid");
                }

                await DownloadFileAsync(response, archivePath, manifest.AssetSize, cancellationToken)
                    .ConfigureAwait(false);
            }

            await ValidateAndExtractPackageAsync(archivePath, stagingDirectory, manifest, cancellationToken)
                .ConfigureAwait(false);

            File.Delete(archivePath);

            return new(
                stagingDirectory,
                Path.Combine(stagingDirectory, "WinPerf.exe"),
                PackageFiles);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public static string? ValidateManifest(WinPerfUpdateManifest manifest)
    {
        if (manifest.SchemaVersion != 1)
        {
            return "schema_invalid";
        }

        if (!string.Equals(manifest.ProductId, ProductId, StringComparison.Ordinal))
        {
            return "product_invalid";
        }

        if (!string.Equals(manifest.Channel, Channel, StringComparison.Ordinal))
        {
            return "channel_invalid";
        }

        if (!Version.TryParse(manifest.LatestVersion, out var version))
        {
            return "version_invalid";
        }

        if (!string.Equals(manifest.ReleaseTag, $"winperf/v{Normalize(version)}", StringComparison.Ordinal))
        {
            return "tag_invalid";
        }

        if (manifest.ReleaseId <= 0 || manifest.AssetId <= 0)
        {
            return "release_identity_invalid";
        }

        if (!string.Equals(manifest.AssetName, AssetName, StringComparison.Ordinal))
        {
            return "asset_name_invalid";
        }

        if (manifest.AssetSize <= 0 || manifest.AssetSize > MaximumAssetSize)
        {
            return "asset_size_invalid";
        }

        return Sha256Pattern.IsMatch(manifest.Sha256) ? null : "sha256_invalid";
    }

    private static WinPerfUpdateManifest ParseManifest(JsonElement root)
    {
        if (!root.TryGetProperty("asset", out var asset)
            || asset.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("asset_missing");
        }

        return new(
            Number(root, "schemaVersion"),
            Text(root, "productId"),
            Text(root, "channel"),
            Text(root, "latestVersion"),
            Text(root, "tagName"),
            Long(root, "releaseId"),
            Long(asset, "id"),
            Text(asset, "name"),
            Long(asset, "size"),
            Text(asset, "sha256").ToLowerInvariant());
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaximumJsonBytes)
        {
            throw new InvalidDataException("json_too_large");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (bytes.Length > MaximumJsonBytes)
        {
            throw new InvalidDataException("json_too_large");
        }

        using var document = JsonDocument.Parse(bytes);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("json_invalid");
        }

        return document.RootElement.Clone();
    }

    private Uri RequireSameOriginUri(string value)
    {
        var result = Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
                ? absolute
                : new Uri(_baseUri, value);

        if (result.Scheme != Uri.UriSchemeHttps
            || result.Host != _baseUri.Host
            || result.Port != _baseUri.Port)
        {
            throw new InvalidDataException("poll_url_invalid");
        }

        return result;
    }

    private Uri RequireDownloadUri(string value)
    {
        var result = RequireSameOriginUri(value);
        var prefix = $"/v1/products/{ProductId}/update/download/";
        var token = result.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)
            ? result.AbsolutePath[prefix.Length..]
            : string.Empty;

        if (!DownloadTokenPattern.IsMatch(token)
            || !string.IsNullOrEmpty(result.Query)
            || !string.IsNullOrEmpty(result.Fragment)
            || !string.IsNullOrEmpty(result.UserInfo))
        {
            throw new InvalidDataException("download_url_invalid");
        }

        return result;
    }

    private static async Task DownloadFileAsync(
        HttpResponseMessage response,
        string destination,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1_048_576,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[1_048_576];
        long total = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > expectedSize || total > MaximumAssetSize)
            {
                throw new InvalidDataException("download_too_large");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (total != expectedSize)
        {
            throw new InvalidDataException("download_size_invalid");
        }
    }

    private static async Task ValidateAndExtractPackageAsync(
        string archivePath,
        string destination,
        WinPerfUpdateManifest manifest,
        CancellationToken cancellationToken)
    {
        await using (var stream = File.OpenRead(archivePath))
        {
            var actualHash = Convert
                .ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();

            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("download_hash_invalid");
            }
        }

        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.ToDictionary(
            entry => entry.FullName.Replace('\\', '/'),
            StringComparer.Ordinal);

        if (entries.Count != RequiredPackageFiles.Length
            || !entries.Keys
                .Order(StringComparer.Ordinal)
                .SequenceEqual(RequiredPackageFiles.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("package_contract_invalid");
        }

        if (entries.Values.Any(entry => string.IsNullOrEmpty(entry.Name)))
        {
            throw new InvalidDataException("package_directory_entry_invalid");
        }

        if (entries.Values.Any(entry => entry.Length > MaximumExpandedSize))
        {
            throw new InvalidDataException("package_expanded_size_invalid");
        }

        var expandedSize = entries.Values.Aggregate(0L, (total, entry) => checked(total + entry.Length));

        if (expandedSize <= 0 || expandedSize > MaximumExpandedSize)
        {
            throw new InvalidDataException("package_expanded_size_invalid");
        }

        foreach (var relativePath in RequiredPackageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.GetFullPath(Path.Combine(destination, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException("package_path_invalid");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entries[relativePath].Open();
            await using var output = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous);

            await CopyExactEntryAsync(input, output, entries[relativePath].Length, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task CopyExactEntryAsync(
        Stream input,
        Stream output,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81_920];
        long total = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > expectedSize || total > MaximumExpandedSize)
            {
                throw new InvalidDataException("package_entry_size_invalid");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (total != expectedSize)
        {
            throw new InvalidDataException("package_entry_size_invalid");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static Uri RequireSecureBaseUri(Uri value) =>
        value.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(value.UserInfo)
            ? value
            : throw new ArgumentException("base_url_invalid", nameof(value));

    private static Uri RequireLoginUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host == "github.com"
            && uri.AbsolutePath == "/login/oauth/authorize"
                ? uri
                : throw new InvalidDataException("login_url_invalid");

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? TextOrNull(JsonElement element, string name)
    {
        var value = Text(element, name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int Number(JsonElement element, string name) => checked((int)Long(element, name));

    private static long Long(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var result)
            ? result
            : 0;

    private static DateTimeOffset UnixTime(JsonElement element, string name) =>
        DateTimeOffset.FromUnixTimeSeconds(Long(element, name));

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinPerf-SponsorPro/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
