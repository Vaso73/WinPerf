using System.Text;

namespace WinPerf.Core.Localization;

public sealed class LanguagePackService
{
    public const string AppId = "winperf";
    public const string LangDirectoryName = "lang";
    public const string DefaultLanguageCode = "en-US";
    public const string SlovakLanguageCode = "sk-SK";

    private const string FormatVersion = "1";

    private static readonly LanguagePackInfo BuiltInEnglishInfo = new(
        DefaultLanguageCode,
        "English",
        "English",
        "ltr",
        IsBuiltIn: true,
        FilePath: null);

    private readonly Dictionary<string, string> _englishTexts;
    private Dictionary<string, string> _activeTexts;

    public LanguagePackService()
    {
        _englishTexts = CreateBuiltInEnglishTexts();
        _activeTexts = new Dictionary<string, string>(_englishTexts, StringComparer.Ordinal);
        CurrentLanguage = BuiltInEnglishInfo;
    }

    public LanguagePackInfo CurrentLanguage { get; private set; }

    public IReadOnlyDictionary<string, string> EnglishTexts => _englishTexts;

    public string Text(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (_activeTexts.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return _englishTexts.TryGetValue(key, out value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value
            : key;
    }

    public IReadOnlyList<LanguagePackInfo> GetAvailableLanguages(string baseDirectory)
    {
        var languages = new List<LanguagePackInfo> { BuiltInEnglishInfo };

        foreach (var document in ReadExternalLanguagePacks(baseDirectory))
        {
            if (string.Equals(document.Info.LanguageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (languages.Any(language =>
                    string.Equals(language.LanguageCode, document.Info.LanguageCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            languages.Add(document.Info);
        }

        return languages
            .OrderBy(language => language.IsBuiltIn ? 0 : 1)
            .ThenBy(language => language.NativeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void UseLanguage(string baseDirectory, string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            CurrentLanguage = BuiltInEnglishInfo;
            _activeTexts = new Dictionary<string, string>(_englishTexts, StringComparer.Ordinal);
            return;
        }

        var document = ReadExternalLanguagePacks(baseDirectory)
            .FirstOrDefault(pack => string.Equals(
                pack.Info.LanguageCode,
                languageCode,
                StringComparison.OrdinalIgnoreCase));

        if (document is null)
        {
            CurrentLanguage = BuiltInEnglishInfo;
            _activeTexts = new Dictionary<string, string>(_englishTexts, StringComparer.Ordinal);
            return;
        }

        var texts = new Dictionary<string, string>(_englishTexts, StringComparer.Ordinal);
        foreach (var item in document.Texts)
        {
            if (_englishTexts.ContainsKey(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            {
                texts[item.Key] = item.Value;
            }
        }

        CurrentLanguage = document.Info;
        _activeTexts = texts;
    }

    public void EnsureSeedLanguagePacks(string baseDirectory)
    {
        var langDirectory = GetLanguageDirectory(baseDirectory);
        Directory.CreateDirectory(langDirectory);

        var slovakPath = Path.Combine(langDirectory, $"{SlovakLanguageCode}.lang");
        var slovakInfo = new LanguagePackInfo(SlovakLanguageCode, "Slovak", "Slovenčina", "ltr", false, slovakPath);
        var builtInSlovakTexts = CreateBuiltInSlovakTexts();

        if (!File.Exists(slovakPath))
        {
            File.WriteAllText(
                slovakPath,
                CreateLanguagePackText(slovakInfo, builtInSlovakTexts),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        else
        {
            TryMergeMissingLanguagePackKeys(slovakPath, slovakInfo, builtInSlovakTexts);
        }

        var readmePath = Path.Combine(langDirectory, "README.lang.md");
        if (!File.Exists(readmePath))
        {
            File.WriteAllText(
                readmePath,
                CreateLanguageReadme(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private void TryMergeMissingLanguagePackKeys(
        string path,
        LanguagePackInfo fallbackInfo,
        IReadOnlyDictionary<string, string> fallbackTexts)
    {
        try
        {
            var document = Parse(File.ReadAllText(path), path);
            var missingKeys = _englishTexts.Keys
                .Where(key => !document.Texts.ContainsKey(key))
                .ToList();

            if (missingKeys.Count == 0)
            {
                return;
            }

            var mergedTexts = new Dictionary<string, string>(fallbackTexts, StringComparer.Ordinal);
            foreach (var item in document.Texts)
            {
                if (_englishTexts.ContainsKey(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                {
                    mergedTexts[item.Key] = item.Value;
                }
            }

            File.WriteAllText(
                path,
                CreateLanguagePackText(document.Info with { FilePath = path }, mergedTexts),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            File.WriteAllText(
                path,
                CreateLanguagePackText(fallbackInfo, fallbackTexts),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    public static string GetLanguageDirectory(string baseDirectory)
    {
        return Path.Combine(baseDirectory, LangDirectoryName);
    }

    public static LanguagePackDocument Parse(string content, string? filePath = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                ParseMetadataLine(line, metadata);
                continue;
            }

            var parts = ParseParts(line);
            if (parts.TryGetValue("KEY", out var key) &&
                parts.TryGetValue("TEXT", out var text) &&
                !string.IsNullOrWhiteSpace(key))
            {
                texts[key.Trim()] = text;
            }
        }

        var appId = metadata.GetValueOrDefault("app-id", AppId);
        if (!string.Equals(appId, AppId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Language pack app-id '{appId}' is not supported.");
        }

        var languageCode = metadata.GetValueOrDefault("language-code");
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new InvalidDataException("Language pack is missing language-code metadata.");
        }

        var languageName = metadata.GetValueOrDefault("language-name", languageCode);
        var nativeName = metadata.GetValueOrDefault("native-name", languageName);
        var direction = metadata.GetValueOrDefault("direction", "ltr");

        return new LanguagePackDocument(
            new LanguagePackInfo(languageCode, languageName, nativeName, direction, false, filePath),
            texts);
    }

    public static string CreateLanguagePackText(LanguagePackInfo info, IReadOnlyDictionary<string, string> texts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# WinPerf language pack");
        builder.AppendLine($"# app-id := {AppId}");
        builder.AppendLine($"# format-version := {FormatVersion}");
        builder.AppendLine($"# language-code := {info.LanguageCode}");
        builder.AppendLine($"# language-name := {info.LanguageName}");
        builder.AppendLine($"# native-name := {info.NativeName}");
        builder.AppendLine($"# direction := {info.Direction}");
        builder.AppendLine("#");
        builder.AppendLine("# Format:");
        builder.AppendLine("# KEY := stable-text-key || TEXT := translated text");
        builder.AppendLine("# Keep KEY unchanged. Edit only TEXT.");
        builder.AppendLine();

        foreach (var item in texts.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            builder.Append("KEY := ");
            builder.Append(item.Key);
            builder.Append(" || TEXT := ");
            builder.AppendLine(EscapeLineValue(item.Value));
        }

        return builder.ToString();
    }

    private IEnumerable<LanguagePackDocument> ReadExternalLanguagePacks(string baseDirectory)
    {
        var langDirectory = GetLanguageDirectory(baseDirectory);
        if (!Directory.Exists(langDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(langDirectory, "*.lang", SearchOption.TopDirectoryOnly))
        {
            LanguagePackDocument? document = null;
            try
            {
                document = Parse(File.ReadAllText(path), path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidDataException)
            {
            }

            if (document is not null)
            {
                yield return document;
            }
        }
    }

    private static void ParseMetadataLine(string line, IDictionary<string, string> metadata)
    {
        var body = line.TrimStart('#').Trim();
        var separator = body.IndexOf(":=", StringComparison.Ordinal);
        if (separator < 0)
        {
            return;
        }

        var key = body[..separator].Trim();
        var value = UnescapeLineValue(body[(separator + 2)..].Trim());
        if (key.Length > 0)
        {
            metadata[key] = value;
        }
    }

    private static Dictionary<string, string> ParseParts(string line)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawPart in line.Split("||", StringSplitOptions.TrimEntries))
        {
            var separator = rawPart.IndexOf(":=", StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var key = rawPart[..separator].Trim();
            var value = UnescapeLineValue(rawPart[(separator + 2)..].Trim());
            if (key.Length > 0)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string EscapeLineValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string UnescapeLineValue(string value)
    {
        return value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string CreateLanguageReadme()
    {
        return """
               # WinPerf language packs

               Default language is built-in English.

               Additional languages live beside WinPerf.exe in this folder:

               - `sk-SK.lang` is the Slovak starter pack.
               - Keep `KEY` values unchanged.
               - Edit only `TEXT` values.
               - Missing or invalid translations fall back to English.

               Restart WinPerf or switch language in Settings after editing a language file.
               """;
    }

    private static Dictionary<string, string> CreateBuiltInEnglishTexts()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["About WinPerf"] = "About WinPerf",
            ["Active"] = "Active",
            ["Advanced command preview:"] = "Advanced command preview:",
            ["Advanced iperf3 builder"] = "Advanced iperf3 builder",
            ["Apply"] = "Apply",
            ["Available"] = "Available",
            ["Average"] = "Average",
            ["Browse"] = "Browse",
            ["Cancel"] = "Cancel",
            ["Check for updates"] = "Check for updates",
            ["Checking"] = "Checking",
            ["Checking..."] = "Checking...",
            ["Checking Sponsor Pro update channel..."] = "Checking Sponsor Pro update channel...",
            ["Clear"] = "Clear",
            ["Clear all"] = "Clear all",
            ["Clear all history?"] = "Clear all history?",
            ["Clear failed: {0}"] = "Clear failed: {0}",
            ["Close"] = "Close",
            ["Command"] = "Command",
            ["Command copied."] = "Command copied.",
            ["Command preview:"] = "Command preview:",
            ["Command unavailable."] = "Command unavailable.",
            ["Changes applied for this session. Save to keep them after restart."] = "Changes applied for this session. Save to keep them after restart.",
            ["Configure portable iperf engines used by WinPerf."] = "Configure portable iperf engines used by WinPerf.",
            ["Connected to {0} / {1}."] = "Connected to {0} / {1}.",
            ["Copy command"] = "Copy command",
            ["Core"] = "Core",
            ["Could not remove the local Sponsor Pro session."] = "Could not remove the local Sponsor Pro session.",
            ["Could not open {0}: {1}"] = "Could not open {0}: {1}",
            ["Configured"] = "Configured",
            ["Current"] = "Current",
            ["Current version is up to date"] = "Current version is up to date",
            ["Dashboard"] = "Dashboard",
            ["Delete"] = "Delete",
            ["Delete all {0} saved history results from this portable runtime?"] = "Delete all {0} saved history results from this portable runtime?",
            ["Delete failed: {0}"] = "Delete failed: {0}",
            ["Delete history result?"] = "Delete history result?",
            ["Delete this saved result?"] = "Delete this saved result?",
            ["Details"] = "Details",
            ["Duration"] = "Duration",
            ["Engine"] = "Engine",
            ["Engine  ●  {0}  ●  Ready  ●  {1}"] = "Engine  ●  {0}  ●  Ready  ●  {1}",
            ["Engine  ●  Not configured"] = "Engine  ●  Not configured",
            ["Engine  ●  {0}  ●  Not configured"] = "Engine  ●  {0}  ●  Not configured",
            ["Engine Output"] = "Engine Output",
            ["Engines"] = "Engines",
            ["Enter a server to preview the iperf command."] = "Enter a server to preview the iperf command.",
            ["Enter iperf3 arguments first."] = "Enter iperf3 arguments first.",
            ["Error"] = "Error",
            ["Export"] = "Export",
            ["Export failed: {0}"] = "Export failed: {0}",
            ["Export WinPerf history"] = "Export WinPerf history",
            ["Exported 1 result."] = "Exported 1 result.",
            ["Exported {0} results."] = "Exported {0} results.",
            ["Exit code"] = "Exit code",
            ["Failed"] = "Failed",
            ["Failed to run server:"] = "Failed to run server:",
            ["History"] = "History",
            ["History cleared."] = "History cleared.",
            ["History could not be loaded. Check the portable data folder."] = "History could not be loaded. Check the portable data folder.",
            ["History is already empty."] = "History is already empty.",
            ["History detail"] = "History detail",
            ["Idle"] = "Idle",
            ["Import"] = "Import",
            ["Import failed: {0}"] = "Import failed: {0}",
            ["Import WinPerf history"] = "Import WinPerf history",
            ["Imported history."] = "Imported history.",
            ["Imported portable engine: {0}"] = "Imported portable engine: {0}",
            ["Imported 1 result. History now has {0} results."] = "Imported 1 result. History now has {0} results.",
            ["Imported {0} results. History now has {1} results."] = "Imported {0} results. History now has {1} results.",
            ["Integrations"] = "Integrations",
            ["Invalid"] = "Invalid",
            ["Jitter"] = "Jitter",
            ["Language"] = "Language",
            ["Loaded"] = "Loaded",
            ["Local server"] = "Local server",
            ["Login"] = "Login",
            ["Loss"] = "Loss",
            ["Maximum"] = "Maximum",
            ["Minimum"] = "Minimum",
            ["Missing"] = "Missing",
            ["Mode"] = "Mode",
            ["Manual iperf2 path cleared. WinPerf will use fallback detection."] = "Manual iperf2 path cleared. WinPerf will use fallback detection.",
            ["Manual iperf3 path cleared. WinPerf will use fallback detection."] = "Manual iperf3 path cleared. WinPerf will use fallback detection.",
            ["Network Performance Toolkit"] = "Network Performance Toolkit",
            ["No command saved for this result."] = "No command saved for this result.",
            ["No saved history yet. Run a test and WinPerf will save the result here."] = "No saved history yet. Run a test and WinPerf will save the result here.",
            ["No saved profile loaded."] = "No saved profile loaded.",
            ["No summary saved."] = "No summary saved.",
            ["Not checked yet"] = "Not checked yet",
            ["Not configured"] = "Not configured",
            ["Not loaded yet"] = "Not loaded yet",
            ["Not signed in"] = "Not signed in",
            ["OK"] = "OK",
            ["Omit"] = "Omit",
            ["Open data"] = "Open data",
            ["Open iperf2"] = "Open iperf2",
            ["Open iperf3"] = "Open iperf3",
            ["Opened {0}: {1}"] = "Opened {0}: {1}",
            ["Opening GitHub Sponsor Pro login..."] = "Opening GitHub Sponsor Pro login...",
            ["Port"] = "Port",
            ["Portable import failed: {0}"] = "Portable import failed: {0}",
            ["Portable data folder"] = "Portable data folder",
            ["Portable folders"] = "Portable folders",
            ["Portable iperf2 engine folder"] = "Portable iperf2 engine folder",
            ["Portable iperf3 engine folder"] = "Portable iperf3 engine folder",
            ["Delete profile?"] = "Delete profile?",
            ["Delete profile '{0}'?"] = "Delete profile '{0}'?",
            ["Fix the advanced command options first."] = "Fix the advanced command options first.",
            ["Profile save failed:"] = "Profile save failed:",
            ["Profile values are invalid:"] = "Profile values are invalid:",
            ["Ready"] = "Ready",
            ["Ready. Sign in to enable Sponsor Pro update checks."] = "Ready. Sign in to enable Sponsor Pro update checks.",
            ["Remove"] = "Remove",
            ["Reverse average"] = "Reverse average",
            ["Result deleted."] = "Result deleted.",
            ["Result details"] = "Result details",
            ["Result was not found."] = "Result was not found.",
            ["Run a local iperf server for LAN, routed, VPN or public-client testing."] = "Run a local iperf server for LAN, routed, VPN or public-client testing.",
            ["Run iperf3 and iperf2 throughput tests with live visualization and saved results."] = "Run iperf3 and iperf2 throughput tests with live visualization and saved results.",
            ["Running"] = "Running",
            ["Running server command:"] = "Running server command:",
            ["Save"] = "Save",
            ["Saved local test results from this portable WinPerf runtime."] = "Saved local test results from this portable WinPerf runtime.",
            ["{0} saved results"] = "{0} saved results",
            ["Server"] = "Server",
            ["{0} is not configured. Open Settings and select the executable first."] = "{0} is not configured. Open Settings and select the executable first.",
            ["{0} not configured"] = "{0} not configured",
            ["Select a profile first."] = "Select a profile first.",
            ["Select an existing {0} first."] = "Select an existing {0} first.",
            ["Selected iperf.exe / iperf2.exe path does not exist."] = "Selected iperf.exe / iperf2.exe path does not exist.",
            ["Selected iperf3.exe path does not exist."] = "Selected iperf3.exe path does not exist.",
            ["Server cannot start: engine missing."] = "Server cannot start: engine missing.",
            ["Server command preview:"] = "Server command preview:",
            ["Server command preview unavailable:"] = "Server command preview unavailable:",
            ["Server configuration is invalid."] = "Server configuration is invalid.",
            ["Server failed to start or stopped unexpectedly."] = "Server failed to start or stopped unexpectedly.",
            ["Server Mode"] = "Server Mode",
            ["Server Output"] = "Server Output",
            ["Server process exited with code {0}."] = "Server process exited with code {0}.",
            ["Server stopped."] = "Server stopped.",
            ["Server stopped by user."] = "Server stopped by user.",
            ["Server stopped with exit code {0}."] = "Server stopped with exit code {0}.",
            ["Settings"] = "Settings",
            ["Sign in with GitHub Sponsor Pro before checking for updates."] = "Sign in with GitHub Sponsor Pro before checking for updates.",
            ["Sign in with GitHub"] = "Sign in with GitHub",
            ["Sign in with GitHub to use Sponsor Pro updates."] = "Sign in with GitHub to use Sponsor Pro updates.",
            ["Sign out"] = "Sign out",
            ["Signed out locally. Your GitHub browser session is unchanged."] = "Signed out locally. Your GitHub browser session is unchanged.",
            ["Single portable WinPerf.exe. Updates use the private Sponsor Pro channel."] = "Single portable WinPerf.exe. Updates use the private Sponsor Pro channel.",
            ["Sponsor Pro / Updates"] = "Sponsor Pro / Updates",
            ["Sponsor Pro account is connected. You can check for private updates."] = "Sponsor Pro account is connected. You can check for private updates.",
            ["Sponsor Pro login failed or was not authorized."] = "Sponsor Pro login failed or was not authorized.",
            ["Sponsor Pro login failed. Check your connection and try again."] = "Sponsor Pro login failed. Check your connection and try again.",
            ["Sponsor Pro login was cancelled."] = "Sponsor Pro login was cancelled.",
            ["Speed Test"] = "Speed Test",
            ["Start"] = "Start",
            ["Start server"] = "Start server",
            ["Stop"] = "Stop",
            ["Stop after one client (--one-off)"] = "Stop after one client (--one-off)",
            ["Stop server"] = "Stop server",
            ["Stopped"] = "Stopped",
            ["Stopped. Ready to start local server."] = "Stopped. Ready to start local server.",
            ["Streams"] = "Streams",
            ["Status"] = "Status",
            ["Summary"] = "Summary",
            ["Test Configuration"] = "Test Configuration",
            ["Test profile"] = "Test profile",
            ["Total bandwidth"] = "Total bandwidth",
            ["UDP bandwidth"] = "UDP bandwidth",
            ["UDP only"] = "UDP only",
            ["Update check failed"] = "Update check failed",
            ["Update check failed or server response was invalid."] = "Update check failed or server response was invalid.",
            ["Update check failed. Check your connection and try again."] = "Update check failed. Check your connection and try again.",
            ["Update check was cancelled."] = "Update check was cancelled.",
            ["Update found. Install button will be enabled after helper launcher/startup wiring."] = "Update found. Install button will be enabled after helper launcher/startup wiring.",
            ["Use Command"] = "Use Command",
            ["Waiting for GitHub authorization..."] = "Waiting for GitHub authorization...",
            ["Waiting for test..."] = "Waiting for test...",
            ["Waiting for throughput samples..."] = "Waiting for throughput samples...",
            ["WinPerf is up to date on the Sponsor Pro channel."] = "WinPerf is up to date on the Sponsor Pro channel.",
            ["1 saved result"] = "1 saved result",
            ["WinPerf Settings"] = "WinPerf Settings",
            ["WinPerfLanguage.Description"] = "Choose the app language. English is built in; other languages are loaded from the lang folder beside WinPerf.exe.",
            ["WinPerfLanguage.EnglishDisplay"] = "English",
            ["WinPerfLanguage.SlovakDisplay"] = "Slovenčina",
            ["WinPerfLanguage.Status"] = "Language packs are loaded from the portable lang folder.",
            ["data folder"] = "data folder",
            ["iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe"] = "iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe",
            ["iperf3 executable · fallback tools\\iperf3\\iperf3.exe"] = "iperf3 executable · fallback tools\\iperf3\\iperf3.exe",
            ["portable iperf2 engine folder"] = "portable iperf2 engine folder",
            ["portable iperf3 engine folder"] = "portable iperf3 engine folder",
        };
    }

    private static Dictionary<string, string> CreateBuiltInSlovakTexts()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["About WinPerf"] = "O aplikácii WinPerf",
            ["Active"] = "Aktívne",
            ["Advanced command preview:"] = "Náhľad pokročilého príkazu:",
            ["Advanced iperf3 builder"] = "Pokročilý iperf3 builder",
            ["Apply"] = "Použiť",
            ["Available"] = "Dostupné",
            ["Average"] = "Priemer",
            ["Browse"] = "Vybrať",
            ["Cancel"] = "Zrušiť",
            ["Check for updates"] = "Skontrolovať aktualizácie",
            ["Checking"] = "Kontrola",
            ["Checking..."] = "Kontrolujem...",
            ["Checking Sponsor Pro update channel..."] = "Kontrolujem Sponsor Pro aktualizačný kanál...",
            ["Clear"] = "Vyčistiť",
            ["Clear all"] = "Vymazať všetko",
            ["Clear all history?"] = "Vymazať celú históriu?",
            ["Clear failed: {0}"] = "Vymazanie zlyhalo: {0}",
            ["Close"] = "Zavrieť",
            ["Command"] = "Príkaz",
            ["Command copied."] = "Príkaz bol skopírovaný.",
            ["Command preview:"] = "Náhľad príkazu:",
            ["Command unavailable."] = "Príkaz nie je dostupný.",
            ["Changes applied for this session. Save to keep them after restart."] = "Zmeny sú použité pre túto reláciu. Ulož ich tlačidlom Uložiť, aby ostali aj po reštarte.",
            ["Configure portable iperf engines used by WinPerf."] = "Nastavenie prenosných iperf nástrojov používaných aplikáciou WinPerf.",
            ["Connected to {0} / {1}."] = "Pripojené k {0} / {1}.",
            ["Copy command"] = "Kopírovať príkaz",
            ["Core"] = "Jadro",
            ["Could not remove the local Sponsor Pro session."] = "Lokálnu Sponsor Pro reláciu sa nepodarilo odstrániť.",
            ["Could not open {0}: {1}"] = "Nepodarilo sa otvoriť {0}: {1}",
            ["Configured"] = "Nastavené",
            ["Current"] = "Aktuálne",
            ["Current version is up to date"] = "Aktuálna verzia je najnovšia",
            ["Dashboard"] = "Prehľad",
            ["Delete"] = "Vymazať",
            ["Delete all {0} saved history results from this portable runtime?"] = "Vymazať všetkých {0} uložených výsledkov z tejto prenosnej inštancie?",
            ["Delete failed: {0}"] = "Mazanie zlyhalo: {0}",
            ["Delete history result?"] = "Vymazať výsledok z histórie?",
            ["Delete this saved result?"] = "Vymazať tento uložený výsledok?",
            ["Details"] = "Detaily",
            ["Duration"] = "Trvanie",
            ["Engine"] = "Engine",
            ["Engine  ●  {0}  ●  Ready  ●  {1}"] = "Engine  ●  {0}  ●  Pripravené  ●  {1}",
            ["Engine  ●  Not configured"] = "Engine  ●  Nenastavené",
            ["Engine  ●  {0}  ●  Not configured"] = "Engine  ●  {0}  ●  Nenastavené",
            ["Engine Output"] = "Výstup enginu",
            ["Engines"] = "Enginy",
            ["Enter a server to preview the iperf command."] = "Zadaj server pre náhľad iperf príkazu.",
            ["Enter iperf3 arguments first."] = "Najprv zadaj iperf3 argumenty.",
            ["Error"] = "Chyba",
            ["Export"] = "Export",
            ["Export failed: {0}"] = "Export zlyhal: {0}",
            ["Export WinPerf history"] = "Export histórie WinPerf",
            ["Exported 1 result."] = "Exportovaný 1 výsledok.",
            ["Exported {0} results."] = "Exportovaných {0} výsledkov.",
            ["Exit code"] = "Návratový kód",
            ["Failed"] = "Zlyhalo",
            ["Failed to run server:"] = "Server sa nepodarilo spustiť:",
            ["History"] = "História",
            ["History cleared."] = "História bola vymazaná.",
            ["History could not be loaded. Check the portable data folder."] = "Históriu sa nepodarilo načítať. Skontroluj priečinok prenosných dát.",
            ["History is already empty."] = "História je už prázdna.",
            ["History detail"] = "Detail histórie",
            ["Idle"] = "Nečinné",
            ["Import"] = "Import",
            ["Import failed: {0}"] = "Import zlyhal: {0}",
            ["Import WinPerf history"] = "Import histórie WinPerf",
            ["Imported history."] = "História bola importovaná.",
            ["Imported portable engine: {0}"] = "Importovaný prenosný engine: {0}",
            ["Imported 1 result. History now has {0} results."] = "Importovaný 1 výsledok. História teraz obsahuje {0} výsledkov.",
            ["Imported {0} results. History now has {1} results."] = "Importovaných {0} výsledkov. História teraz obsahuje {1} výsledkov.",
            ["Integrations"] = "Integrácie",
            ["Invalid"] = "Neplatné",
            ["Jitter"] = "Jitter",
            ["Language"] = "Jazyk",
            ["Loaded"] = "Načítané",
            ["Local server"] = "Lokálny server",
            ["Login"] = "Login",
            ["Loss"] = "Straty",
            ["Maximum"] = "Maximum",
            ["Minimum"] = "Minimum",
            ["Missing"] = "Chýba",
            ["Mode"] = "Režim",
            ["Manual iperf2 path cleared. WinPerf will use fallback detection."] = "Manuálna cesta k iperf2 bola vyčistená. WinPerf použije fallback detekciu.",
            ["Manual iperf3 path cleared. WinPerf will use fallback detection."] = "Manuálna cesta k iperf3 bola vyčistená. WinPerf použije fallback detekciu.",
            ["Network Performance Toolkit"] = "Nástroj na meranie výkonu siete",
            ["No command saved for this result."] = "Pre tento výsledok nie je uložený príkaz.",
            ["No saved history yet. Run a test and WinPerf will save the result here."] = "Zatiaľ nie je uložená história. Spusti test a WinPerf sem uloží výsledok.",
            ["No saved profile loaded."] = "Nie je načítaný uložený profil.",
            ["No summary saved."] = "Súhrn nie je uložený.",
            ["Not checked yet"] = "Zatiaľ nekontrolované",
            ["Not configured"] = "Nenastavené",
            ["Not loaded yet"] = "Zatiaľ nenačítané",
            ["Not signed in"] = "Neprihlásené",
            ["OK"] = "OK",
            ["Omit"] = "Omit",
            ["Open data"] = "Otvoriť dáta",
            ["Open iperf2"] = "Otvoriť iperf2",
            ["Open iperf3"] = "Otvoriť iperf3",
            ["Opened {0}: {1}"] = "Otvorené {0}: {1}",
            ["Opening GitHub Sponsor Pro login..."] = "Otváram GitHub Sponsor Pro prihlásenie...",
            ["Port"] = "Port",
            ["Portable import failed: {0}"] = "Import prenosného enginu zlyhal: {0}",
            ["Portable data folder"] = "Priečinok prenosných dát",
            ["Portable folders"] = "Prenosné priečinky",
            ["Portable iperf2 engine folder"] = "Priečinok prenosného iperf2 enginu",
            ["Portable iperf3 engine folder"] = "Priečinok prenosného iperf3 enginu",
            ["Delete profile?"] = "Vymazať profil?",
            ["Delete profile '{0}'?"] = "Vymazať profil „{0}“?",
            ["Fix the advanced command options first."] = "Najprv oprav pokročilé nastavenia príkazu.",
            ["Profile save failed:"] = "Uloženie profilu zlyhalo:",
            ["Profile values are invalid:"] = "Hodnoty profilu sú neplatné:",
            ["Ready"] = "Pripravené",
            ["Ready. Sign in to enable Sponsor Pro update checks."] = "Pripravené. Prihlás sa pre kontrolu Sponsor Pro aktualizácií.",
            ["Remove"] = "Odobrať",
            ["Reverse average"] = "Reverzný priemer",
            ["Result deleted."] = "Výsledok bol vymazaný.",
            ["Result details"] = "Detaily výsledku",
            ["Result was not found."] = "Výsledok sa nenašiel.",
            ["Run a local iperf server for LAN, routed, VPN or public-client testing."] = "Spusti lokálny iperf server pre LAN, routované, VPN alebo verejné klientské testy.",
            ["Run iperf3 and iperf2 throughput tests with live visualization and saved results."] = "Spúšťaj iperf3 a iperf2 testy priepustnosti so živým grafom a uloženými výsledkami.",
            ["Running"] = "Beží",
            ["Running server command:"] = "Spúšťam serverový príkaz:",
            ["Save"] = "Uložiť",
            ["Saved local test results from this portable WinPerf runtime."] = "Lokálne uložené výsledky z tejto prenosnej WinPerf inštancie.",
            ["{0} saved results"] = "{0} uložených výsledkov",
            ["Server"] = "Server",
            ["{0} is not configured. Open Settings and select the executable first."] = "{0} nie je nastavený. Otvor Nastavenia a najprv vyber spustiteľný súbor.",
            ["{0} not configured"] = "{0} nie je nastavené",
            ["Select a profile first."] = "Najprv vyber profil.",
            ["Select an existing {0} first."] = "Najprv vyber existujúci {0}.",
            ["Selected iperf.exe / iperf2.exe path does not exist."] = "Vybraná cesta k iperf.exe / iperf2.exe neexistuje.",
            ["Selected iperf3.exe path does not exist."] = "Vybraná cesta k iperf3.exe neexistuje.",
            ["Server cannot start: engine missing."] = "Server sa nedá spustiť: chýba engine.",
            ["Server command preview:"] = "Náhľad serverového príkazu:",
            ["Server command preview unavailable:"] = "Náhľad serverového príkazu nie je dostupný:",
            ["Server configuration is invalid."] = "Nastavenie servera je neplatné.",
            ["Server failed to start or stopped unexpectedly."] = "Server sa nespustil alebo sa neočakávane zastavil.",
            ["Server Mode"] = "Server režim",
            ["Server Output"] = "Výstup servera",
            ["Server process exited with code {0}."] = "Serverový proces skončil s kódom {0}.",
            ["Server stopped."] = "Server zastavený.",
            ["Server stopped by user."] = "Server zastavil používateľ.",
            ["Server stopped with exit code {0}."] = "Server sa zastavil s kódom {0}.",
            ["Settings"] = "Nastavenia",
            ["Sign in with GitHub Sponsor Pro before checking for updates."] = "Pred kontrolou aktualizácií sa prihlás cez GitHub Sponsor Pro.",
            ["Sign in with GitHub"] = "Prihlásiť cez GitHub",
            ["Sign in with GitHub to use Sponsor Pro updates."] = "Prihlás sa cez GitHub pre Sponsor Pro aktualizácie.",
            ["Sign out"] = "Odhlásiť",
            ["Signed out locally. Your GitHub browser session is unchanged."] = "Lokálne odhlásené. Tvoja GitHub relácia v prehliadači ostala nezmenená.",
            ["Single portable WinPerf.exe. Updates use the private Sponsor Pro channel."] = "Jedno prenosné WinPerf.exe. Aktualizácie používajú súkromný Sponsor Pro kanál.",
            ["Sponsor Pro / Updates"] = "Sponsor Pro / Aktualizácie",
            ["Sponsor Pro account is connected. You can check for private updates."] = "Sponsor Pro účet je pripojený. Môžeš skontrolovať súkromné aktualizácie.",
            ["Sponsor Pro login failed or was not authorized."] = "Sponsor Pro prihlásenie zlyhalo alebo nebolo autorizované.",
            ["Sponsor Pro login failed. Check your connection and try again."] = "Sponsor Pro prihlásenie zlyhalo. Skontroluj pripojenie a skús znova.",
            ["Sponsor Pro login was cancelled."] = "Sponsor Pro prihlásenie bolo zrušené.",
            ["Speed Test"] = "Speed Test",
            ["Start"] = "Štart",
            ["Start server"] = "Spustiť server",
            ["Stop"] = "Stop",
            ["Stop after one client (--one-off)"] = "Zastaviť po jednom klientovi (--one-off)",
            ["Stop server"] = "Zastaviť server",
            ["Stopped"] = "Zastavené",
            ["Stopped. Ready to start local server."] = "Zastavené. Lokálny server je pripravený na spustenie.",
            ["Streams"] = "Streamy",
            ["Status"] = "Stav",
            ["Summary"] = "Súhrn",
            ["Test Configuration"] = "Nastavenie testu",
            ["Test profile"] = "Testovací profil",
            ["Total bandwidth"] = "Celková priepustnosť",
            ["UDP bandwidth"] = "UDP bandwidth",
            ["UDP only"] = "Iba UDP",
            ["Update check failed"] = "Kontrola aktualizácie zlyhala",
            ["Update check failed or server response was invalid."] = "Kontrola aktualizácie zlyhala alebo odpoveď servera bola neplatná.",
            ["Update check failed. Check your connection and try again."] = "Kontrola aktualizácie zlyhala. Skontroluj pripojenie a skús znova.",
            ["Update check was cancelled."] = "Kontrola aktualizácie bola zrušená.",
            ["Update found. Install button will be enabled after helper launcher/startup wiring."] = "Aktualizácia nájdená. Inštalácia sa zapne po dopojení pomocného spúšťača.",
            ["Use Command"] = "Použiť príkaz",
            ["Waiting for GitHub authorization..."] = "Čakám na GitHub autorizáciu...",
            ["Waiting for test..."] = "Čakám na test...",
            ["Waiting for throughput samples..."] = "Čakám na vzorky priepustnosti...",
            ["WinPerf is up to date on the Sponsor Pro channel."] = "WinPerf je na Sponsor Pro kanáli aktuálny.",
            ["1 saved result"] = "1 uložený výsledok",
            ["WinPerf Settings"] = "Nastavenia WinPerf",
            ["WinPerfLanguage.Description"] = "Vyber jazyk aplikácie. Angličtina je vstavaná; ďalšie jazyky sa načítajú z priečinka lang vedľa WinPerf.exe.",
            ["WinPerfLanguage.EnglishDisplay"] = "English",
            ["WinPerfLanguage.SlovakDisplay"] = "Slovenčina",
            ["WinPerfLanguage.Status"] = "Jazykové balíky sa načítajú z prenosného priečinka lang.",
            ["data folder"] = "priečinok dát",
            ["iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe"] = "iperf2 executable · fallback tools\\iperf2\\iperf.exe alebo iperf2.exe",
            ["iperf3 executable · fallback tools\\iperf3\\iperf3.exe"] = "iperf3 executable · fallback tools\\iperf3\\iperf3.exe",
            ["portable iperf2 engine folder"] = "priečinok prenosného iperf2 enginu",
            ["portable iperf3 engine folder"] = "priečinok prenosného iperf3 enginu",
        };
    }
}
