using WinPerf.Core.Iperf;
using WinPerf.Core.Profiles;

namespace WinPerf.Tests.Profiles;

public sealed class JsonSavedIperfProfileStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileIsMissing_ReturnsEmptyDocument()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new JsonSavedIperfProfileStore(Path.Combine(directory, "missing", "profiles.json"));

            var document = await store.LoadAsync();

            Assert.Equal(1, document.SchemaVersion);
            Assert.Empty(document.Profiles);
            Assert.Null(document.DefaultProfileId);
            Assert.Null(document.LastSelectedProfileId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripProfiles()
    {
        var directory = CreateTempDirectory();

        try
        {
            var path = Path.Combine(directory, "profiles.json");
            var profile = CreateProfile();
            var store = new JsonSavedIperfProfileStore(path);

            var document = new SavedIperfProfilesDocument
            {
                DefaultProfileId = profile.Id,
                LastSelectedProfileId = profile.Id,
                Profiles = new List<SavedIperfProfile> { profile }
            };

            await store.SaveAsync(document);

            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("\"schemaVersion\": 1", json);
            Assert.Contains("\"protocol\": \"Tcp\"", json);
            Assert.Contains("\"runMode\": \"Client\"", json);

            var loaded = await store.LoadAsync();

            Assert.Equal(profile.Id, loaded.DefaultProfileId);
            Assert.Equal(profile.Id, loaded.LastSelectedProfileId);
            Assert.Single(loaded.Profiles);
            Assert.Equal(profile, loaded.Profiles[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesParentDirectory()
    {
        var directory = CreateTempDirectory();

        try
        {
            var path = Path.Combine(directory, "nested", "profiles", "profiles.json");
            var store = new JsonSavedIperfProfileStore(path);

            await store.SaveAsync(new SavedIperfProfilesDocument());

            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsInvalid_ThrowsInvalidDataException()
    {
        var directory = CreateTempDirectory();

        try
        {
            var path = Path.Combine(directory, "profiles.json");
            await File.WriteAllTextAsync(path, "{");
            var store = new JsonSavedIperfProfileStore(path);

            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenProfileIsInvalid_ThrowsInvalidDataException()
    {
        var directory = CreateTempDirectory();

        try
        {
            var path = Path.Combine(directory, "profiles.json");
            var store = new JsonSavedIperfProfileStore(path);

            var invalidProfile = CreateProfile() with
            {
                Port = 0
            };

            var document = new SavedIperfProfilesDocument
            {
                Profiles = new List<SavedIperfProfile> { invalidProfile }
            };

            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(document));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolveLastSelectedProfile_FallsBackToDefaultThenFirstProfile()
    {
        var first = CreateProfile(
            id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            name: "First");

        var second = CreateProfile(
            id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            name: "Second");

        var document = new SavedIperfProfilesDocument
        {
            DefaultProfileId = second.Id,
            LastSelectedProfileId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Profiles = new List<SavedIperfProfile> { first, second }
        };

        Assert.Equal(second.Id, document.ResolveLastSelectedProfile()?.Id);

        document = document with
        {
            DefaultProfileId = Guid.Parse("44444444-4444-4444-4444-444444444444")
        };

        Assert.Equal(first.Id, document.ResolveLastSelectedProfile()?.Id);
    }

    [Fact]
    public void ToClientTestOptions_MapsSavedProfileToExistingIperfOptions()
    {
        var profile = CreateProfile() with
        {
            Protocol = SavedIperfProtocol.Tcp,
            Reverse = false,
            Bidirectional = true,
            Streams = 8,
            DurationSeconds = 30,
            AddressFamily = IperfAddressFamily.IPv6
        };

        var options = profile.ToClientTestOptions();

        Assert.Equal(profile.Server, options.Server);
        Assert.Equal(profile.Port, options.Port);
        Assert.Equal(IperfMode.TcpBidirectional, options.Mode);
        Assert.Equal(8, options.Streams);
        Assert.Equal(30, options.DurationSeconds);
        Assert.Equal(IperfAddressFamily.IPv6, options.AddressFamily);
    }

    [Fact]
    public void FromClientTestOptions_CreatesSavedProfile()
    {
        var now = new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero);
        var options = new IperfTestOptions
        {
            Server = "10.100.100.1",
            Port = 5201,
            Mode = IperfMode.UdpDownload,
            Streams = 10,
            DurationSeconds = 15,
            AddressFamily = IperfAddressFamily.IPv4,
            UdpBandwidth = "100M"
        };

        var profile = SavedIperfProfile.FromClientTestOptions(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "UDP download",
            options,
            now);

        Assert.Equal("UDP download", profile.Name);
        Assert.Equal(SavedIperfProtocol.Udp, profile.Protocol);
        Assert.True(profile.Reverse);
        Assert.False(profile.Bidirectional);
        Assert.Equal("100M", profile.UdpBandwidth);
        Assert.Equal(now, profile.CreatedAtUtc);
        Assert.Equal(now, profile.UpdatedAtUtc);
    }


    [Fact]
    public async Task SaveAsync_WhenProfileIdsAreDuplicated_ThrowsInvalidDataException()
    {
        var directory = CreateTempDirectory();

        try
        {
            var path = Path.Combine(directory, "profiles.json");
            var store = new JsonSavedIperfProfileStore(path);
            var id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            var document = new SavedIperfProfilesDocument
            {
                Profiles = new List<SavedIperfProfile>
                {
                    CreateProfile(id, "First"),
                    CreateProfile(id, "Second")
                }
            };

            var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(document));

            Assert.Contains("Duplicate profile id", ex.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validate_AllowsServerModeWithoutServerAddress()
    {
        var profile = CreateProfile() with
        {
            RunMode = SavedIperfRunMode.Server,
            Server = null,
            ServerOneOff = true
        };

        var errors = SavedIperfProfileValidation.Validate(profile);

        Assert.Empty(errors);
    }

    [Fact]
    public void GetDefaultFilePath_UsesWinPerfProfilesJson()
    {
        var path = JsonSavedIperfProfileStore.GetDefaultFilePath();

        Assert.EndsWith(Path.Combine("WinPerf", "profiles.json"), path);
    }

    private static SavedIperfProfile CreateProfile(
        Guid? id = null,
        string name = "LAN TCP 10s x10")
    {
        var now = new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero);

        return new SavedIperfProfile
        {
            Id = id ?? Guid.Parse("5a8ab1d1-3f92-4d9e-a87a-4c72d6a4fc16"),
            Name = name,
            RunMode = SavedIperfRunMode.Client,
            Protocol = SavedIperfProtocol.Tcp,
            AddressFamily = IperfAddressFamily.IPv4,
            Server = "10.100.100.1",
            Port = 5201,
            Streams = 10,
            DurationSeconds = 10,
            ReportIntervalSeconds = 1,
            UdpBandwidth = "0",
            ReportFormat = "M",
            UseJsonStream = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WinPerf.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
