using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinPerf.Core.Product;
using WinPerf.Core.Updates;

namespace WinPerf.App.Updates;

internal sealed class SponsorProSessionStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinPerf Sponsor Pro session v1");
    private readonly string _sessionFile;

    public SponsorProSessionStore()
        : this(Path.Combine(AppContext.BaseDirectory, WinPerfProductEdition.DataDirectoryName))
    {
    }

    public SponsorProSessionStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _sessionFile = Path.Combine(dataDirectory, "sponsor-pro-session.dat");
    }

    public SponsorProSession? Load()
    {
        try
        {
            if (!File.Exists(_sessionFile))
            {
                return null;
            }

            var encrypted = File.ReadAllBytes(_sessionFile);
            var json = ProtectedData.Unprotect(
                encrypted,
                Entropy,
                DataProtectionScope.CurrentUser);
            var session = JsonSerializer.Deserialize<SponsorProSession>(json);

            if (session?.IsUsable == true)
            {
                return session;
            }
        }
        catch (CryptographicException)
        {
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        Clear();
        return null;
    }

    public void Save(SponsorProSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsUsable)
        {
            throw new InvalidOperationException("session_invalid");
        }

        var directory = Path.GetDirectoryName(_sessionFile)!;
        Directory.CreateDirectory(directory);

        var encrypted = ProtectedData.Protect(
            JsonSerializer.SerializeToUtf8Bytes(session),
            Entropy,
            DataProtectionScope.CurrentUser);
        var temporary = _sessionFile + $".tmp-{Guid.NewGuid():N}";

        try
        {
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, _sessionFile, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch
            {
            }
        }
    }

    public bool Clear()
    {
        try
        {
            if (File.Exists(_sessionFile))
            {
                File.Delete(_sessionFile);
            }

            return !File.Exists(_sessionFile);
        }
        catch
        {
            return false;
        }
    }
}
