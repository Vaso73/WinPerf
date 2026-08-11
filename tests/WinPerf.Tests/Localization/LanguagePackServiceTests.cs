using WinPerf.Core.Localization;

namespace WinPerf.Tests.Localization;

public sealed class LanguagePackServiceTests
{
    [Fact]
    public void EnsureSeedLanguagePacks_CreatesSlovakPackBesidePortableRuntime()
    {
        var baseDirectory = CreateTemporaryDirectory();

        try
        {
            var service = new LanguagePackService();

            service.EnsureSeedLanguagePacks(baseDirectory);

            var languagePackPath = Path.Combine(
                baseDirectory,
                LanguagePackService.LangDirectoryName,
                $"{LanguagePackService.SlovakLanguageCode}.lang");

            Assert.True(File.Exists(languagePackPath));

            var document = LanguagePackService.Parse(File.ReadAllText(languagePackPath), languagePackPath);
            Assert.Equal(LanguagePackService.SlovakLanguageCode, document.Info.LanguageCode);
            Assert.Equal("Slovenčina", document.Info.NativeName);
            Assert.Equal("Nastavenia", document.Texts["Settings"]);
            Assert.Equal("Posledné", document.Texts["Last"]);
        }
        finally
        {
            DeleteTemporaryDirectory(baseDirectory);
        }
    }

    [Fact]
    public void EnsureSeedLanguagePacks_KeepsSlovakPackInSyncWithBuiltInEnglishKeys()
    {
        var baseDirectory = CreateTemporaryDirectory();

        try
        {
            var service = new LanguagePackService();

            service.EnsureSeedLanguagePacks(baseDirectory);

            var languagePackPath = Path.Combine(
                baseDirectory,
                LanguagePackService.LangDirectoryName,
                $"{LanguagePackService.SlovakLanguageCode}.lang");
            var document = LanguagePackService.Parse(File.ReadAllText(languagePackPath), languagePackPath);

            var missingKeys = service.EnglishTexts.Keys
                .Except(document.Texts.Keys, StringComparer.Ordinal)
                .ToList();

            Assert.Empty(missingKeys);
        }
        finally
        {
            DeleteTemporaryDirectory(baseDirectory);
        }
    }

    [Fact]
    public void EnsureSeedLanguagePacks_AddsMissingKeysWithoutOverwritingExistingTranslations()
    {
        var baseDirectory = CreateTemporaryDirectory();

        try
        {
            var languageDirectory = LanguagePackService.GetLanguageDirectory(baseDirectory);
            Directory.CreateDirectory(languageDirectory);
            var languagePackPath = Path.Combine(languageDirectory, $"{LanguagePackService.SlovakLanguageCode}.lang");

            File.WriteAllText(
                languagePackPath,
                """
                # app-id := winperf
                # language-code := sk-SK
                # language-name := Slovak
                # native-name := Slovenčina
                # direction := ltr

                KEY := Settings || TEXT := Vlastné nastavenia
                """);

            var service = new LanguagePackService();

            service.EnsureSeedLanguagePacks(baseDirectory);

            var document = LanguagePackService.Parse(File.ReadAllText(languagePackPath), languagePackPath);

            Assert.Equal("Vlastné nastavenia", document.Texts["Settings"]);
            Assert.True(document.Texts.ContainsKey("Dashboard"));
        }
        finally
        {
            DeleteTemporaryDirectory(baseDirectory);
        }
    }

    [Fact]
    public void EnsureSeedLanguagePacks_RefreshesStaleEnglishSeedValuesWithoutOverwritingCustomTranslations()
    {
        var baseDirectory = CreateTemporaryDirectory();

        try
        {
            var languageDirectory = LanguagePackService.GetLanguageDirectory(baseDirectory);
            Directory.CreateDirectory(languageDirectory);
            var languagePackPath = Path.Combine(languageDirectory, $"{LanguagePackService.SlovakLanguageCode}.lang");

            File.WriteAllText(
                languagePackPath,
                """
                # app-id := winperf
                # language-code := sk-SK
                # language-name := Slovak
                # native-name := Slovenčina
                # direction := ltr

                KEY := Settings || TEXT := Vlastné nastavenia
                KEY := Advanced iperf3 builder || TEXT := Advanced iperf3 builder
                KEY := TCP Upload || TEXT := TCP Upload
                KEY := iperf2 executable · fallback tools\iperf2\iperf.exe or iperf2.exe || TEXT := iperf2 executable · fallback tools\iperf2\iperf.exe alebo iperf2.exe
                """);

            var service = new LanguagePackService();

            service.EnsureSeedLanguagePacks(baseDirectory);

            var document = LanguagePackService.Parse(File.ReadAllText(languagePackPath), languagePackPath);

            Assert.Equal("Vlastné nastavenia", document.Texts["Settings"]);
            Assert.Equal("Pokročilý tvorca iperf3 príkazu", document.Texts["Advanced iperf3 builder"]);
            Assert.Equal("TCP upload", document.Texts["TCP Upload"]);
            Assert.Equal(
                "iperf2 spustiteľný súbor · záloha tools\\iperf2\\iperf.exe alebo iperf2.exe",
                document.Texts["iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe"]);
        }
        finally
        {
            DeleteTemporaryDirectory(baseDirectory);
        }
    }

    [Fact]
    public void UseLanguage_LoadsExternalPackAndFallsBackToEnglishPerMissingKey()
    {
        var baseDirectory = CreateTemporaryDirectory();

        try
        {
            var languageDirectory = LanguagePackService.GetLanguageDirectory(baseDirectory);
            Directory.CreateDirectory(languageDirectory);
            var languagePackPath = Path.Combine(languageDirectory, "test.lang");
            File.WriteAllText(
                languagePackPath,
                """
                # app-id := winperf
                # language-code := x-test
                # language-name := Test
                # native-name := Test
                # direction := ltr

                KEY := Settings || TEXT := Test settings
                """);

            var service = new LanguagePackService();

            service.UseLanguage(baseDirectory, "x-test");

            Assert.Equal("x-test", service.CurrentLanguage.LanguageCode);
            Assert.Equal("Test settings", service.Text("Settings"));
            Assert.Equal("Dashboard", service.Text("Dashboard"));
        }
        finally
        {
            DeleteTemporaryDirectory(baseDirectory);
        }
    }

    [Fact]
    public void UseLanguage_FallsBackToBuiltInEnglishWhenLanguageIsMissing()
    {
        var baseDirectory = CreateTemporaryDirectory();
        var service = new LanguagePackService();

        try
        {
            service.UseLanguage(baseDirectory, "missing-language");

            Assert.Equal(LanguagePackService.DefaultLanguageCode, service.CurrentLanguage.LanguageCode);
            Assert.Equal("Settings", service.Text("Settings"));
        }
        finally
        {
            DeleteTemporaryDirectory(baseDirectory);
        }
    }

    [Fact]
    public void Parse_RoundTripsEscapedMultilineValues()
    {
        var text = LanguagePackService.CreateLanguagePackText(
            new LanguagePackInfo("x-test", "Test", "Test", "ltr", false, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Line"] = "First line\nSecond line \\ path",
            });

        var document = LanguagePackService.Parse(text);

        Assert.Equal("First line\nSecond line \\ path", document.Texts["Line"]);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "winperf-lang-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
