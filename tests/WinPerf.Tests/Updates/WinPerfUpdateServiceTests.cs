using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinPerf.Core.Updates;

namespace WinPerf.Tests.Updates;

public sealed class WinPerfUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_AcceptsExactWinPerfSponsorProContract()
    {
        const string json = """
        {"schemaVersion":1,"productId":"winperf","channel":"sponsor-pro","latestVersion":"0.2.0","tagName":"winperf/v0.2.0","releaseId":10,"asset":{"id":11,"name":"WinPerf.zip","size":1234,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}
        """;
        using var service = new WinPerfUpdateService(
            new HttpClient(new StubHandler(json)),
            new Uri("https://updates.example"));

        var result = await service.CheckAsync(new Version(0, 1, 4));

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(0, 2, 0), result.LatestVersion);
        Assert.Equal("winperf", result.Manifest!.ProductId);
        Assert.Equal(11, result.Manifest.AssetId);
    }

    [Theory]
    [InlineData("other", "sponsor-pro", "winperf/v0.2.0", "WinPerf.zip", "product_invalid")]
    [InlineData("winperf", "public", "winperf/v0.2.0", "WinPerf.zip", "channel_invalid")]
    [InlineData("winperf", "sponsor-pro", "winperf/v9.9.9", "WinPerf.zip", "tag_invalid")]
    [InlineData("winperf", "sponsor-pro", "winperf/v0.2.0", "Other.zip", "asset_name_invalid")]
    public void ValidateManifest_RejectsIdentityDrift(
        string product,
        string channel,
        string tag,
        string asset,
        string error)
    {
        var manifest = new WinPerfUpdateManifest(
            1,
            product,
            channel,
            "0.2.0",
            tag,
            1,
            2,
            asset,
            100,
            new string('a', 64));

        Assert.Equal(error, WinPerfUpdateService.ValidateManifest(manifest));
    }

    [Fact]
    public void Constructor_RejectsNonHttpsUpdateService()
    {
        Assert.Throws<ArgumentException>(() =>
            new WinPerfUpdateService(
                new HttpClient(new StubHandler("{}")),
                new Uri("http://updates.example")));
    }

    [Fact]
    public async Task StartLoginAsync_SendsWinPerfProductIdAndAcceptsSameOriginPollUrl()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new
        {
            authSessionId = "auth-1",
            pollToken = "poll-1",
            loginUrl = "https://github.com/login/oauth/authorize?client_id=test",
            pollUrl = "/v1/auth/github/poll",
            expiresAt
        });
        string? requestBody = null;
        using var service = new WinPerfUpdateService(
            new HttpClient(new DelegateHandler(request =>
            {
                requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse(json);
            })),
            new Uri("https://updates.example"));

        var result = await service.StartLoginAsync();

        Assert.Equal("auth-1", result.AuthSessionId);
        Assert.Equal(new Uri("https://updates.example/v1/auth/github/poll"), result.PollUrl);
        Assert.Equal(
            "winperf",
            JsonDocument.Parse(requestBody!).RootElement.GetProperty("productId").GetString());
    }

    [Fact]
    public async Task StartLoginAsync_PreservesServerErrorCode()
    {
        using var service = new WinPerfUpdateService(
            new HttpClient(new DelegateHandler(_ =>
                JsonResponse("{\"error\":\"product_not_found\"}", HttpStatusCode.ServiceUnavailable))),
            new Uri("https://updates.example"));

        var error = await Assert.ThrowsAsync<WinPerfUpdateServiceException>(() => service.StartLoginAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal("product_not_found", error.ErrorCode);
    }

    [Theory]
    [InlineData("https://example.com/login/oauth/authorize", "/v1/auth/github/poll")]
    [InlineData("https://github.com/login/oauth/authorize", "https://evil.example/v1/auth/github/poll")]
    public async Task StartLoginAsync_RejectsUntrustedRedirects(string loginUrl, string pollUrl)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new
        {
            authSessionId = "auth-1",
            pollToken = "poll-1",
            loginUrl,
            pollUrl,
            expiresAt
        });
        using var service = new WinPerfUpdateService(
            new HttpClient(new StubHandler(json)),
            new Uri("https://updates.example"));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.StartLoginAsync());
    }

    [Fact]
    public async Task PollLoginAsync_AcceptsSharedSponsorProSessionContract()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new
        {
            status = "github_authenticated",
            sessionTokenIssued = true,
            sponsorStatus = "active",
            githubLogin = "octocat",
            sponsorProSession = new
            {
                sessionId = "session-1",
                sessionToken = "secret-token",
                expiresAt
            }
        });
        using var service = new WinPerfUpdateService(
            new HttpClient(new StubHandler(json)),
            new Uri("https://updates.example"));
        var start = new SponsorProAuthStart(
            "auth-1",
            "poll-1",
            new Uri("https://github.com/login/oauth/authorize"),
            new Uri("https://updates.example/v1/auth/github/poll"),
            DateTimeOffset.UtcNow.AddMinutes(10));

        var result = await service.PollLoginAsync(start);

        Assert.True(result.Success);
        Assert.Equal("session-1", result.Session!.SessionId);
        Assert.Equal("secret-token", result.Session.SessionToken);
    }

    [Fact]
    public async Task RequestDownloadTicketAsync_UsesSponsorProSessionAndAcceptsBoundUrl()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new
        {
            status = "ok",
            productId = "winperf",
            tokenType = "SponsorPro-Download",
            downloadUrl = "https://updates.example/v1/products/winperf/update/download/abcdefghijklmnopqrstuvwxyz0123456789_-",
            expiresAt
        });
        string? authorization = null;
        using var service = new WinPerfUpdateService(
            new HttpClient(new DelegateHandler(request =>
            {
                authorization = request.Headers.Authorization?.ToString();
                return JsonResponse(json);
            })),
            new Uri("https://updates.example"));

        var ticket = await service.RequestDownloadTicketAsync(
            ValidSession(),
            ValidManifest(1234, new string('a', 64)));

        Assert.Equal("SponsorPro-Session secret-token", authorization);
        Assert.Equal(
            "abcdefghijklmnopqrstuvwxyz0123456789_-",
            ticket.DownloadUrl.Segments[^1]);
    }

    [Fact]
    public async Task DownloadAndStageAsync_AcceptsOnlyExactSingleExePackage()
    {
        var zip = CreatePackage();
        var hash = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        using var service = new WinPerfUpdateService(
            new HttpClient(new DelegateHandler(_ => ZipResponse(zip))),
            new Uri("https://updates.example"));
        var root = CreateTemporaryDirectory();

        try
        {
            var staged = await service.DownloadAndStageAsync(
                ValidTicket(),
                ValidManifest(zip.Length, hash),
                root);

            Assert.Equal(new[] { "WinPerf.exe" }, staged.RelativeFiles);
            Assert.Equal("app", await File.ReadAllTextAsync(staged.ExecutablePath));
            Assert.Single(Directory.GetFiles(staged.StagingDirectory, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAndStageAsync_RejectsExtraPackageEntryAndCleansStaging()
    {
        var zip = CreatePackage(("data/settings.json", "must-not-install"));
        var hash = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        using var service = new WinPerfUpdateService(
            new HttpClient(new DelegateHandler(_ => ZipResponse(zip))),
            new Uri("https://updates.example"));
        var root = CreateTemporaryDirectory();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.DownloadAndStageAsync(
                    ValidTicket(),
                    ValidManifest(zip.Length, hash),
                    root));

            Assert.Empty(Directory.GetDirectories(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static SponsorProSession ValidSession() =>
        new("session-1", "secret-token", "octocat", "owner", "tier", DateTimeOffset.UtcNow.AddDays(1));

    private static UpdateDownloadTicket ValidTicket() =>
        new(
            new Uri("https://updates.example/v1/products/winperf/update/download/abcdefghijklmnopqrstuvwxyz0123456789_-"),
            DateTimeOffset.UtcNow.AddMinutes(10));

    private static WinPerfUpdateManifest ValidManifest(long size, string hash) =>
        new(
            1,
            "winperf",
            "sponsor-pro",
            "0.2.0",
            "winperf/v0.2.0",
            10,
            11,
            "WinPerf.zip",
            size,
            hash);

    private static byte[] CreatePackage(params (string Name, string Content)[] extraEntries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "WinPerf.exe", "app");

            foreach (var entry in extraEntries)
            {
                WriteEntry(archive, entry.Name, entry.Content);
            }
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "WinPerf.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage ZipResponse(byte[] zip)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(zip)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        return response;
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
