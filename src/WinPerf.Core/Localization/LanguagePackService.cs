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
            var mergedTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            var changed = false;

            foreach (var item in document.Texts)
            {
                if (_englishTexts.ContainsKey(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                {
                    mergedTexts[item.Key] = item.Value;
                }
            }

            foreach (var item in fallbackTexts)
            {
                if (!_englishTexts.ContainsKey(item.Key))
                {
                    continue;
                }

                if (!mergedTexts.TryGetValue(item.Key, out var existingValue))
                {
                    mergedTexts[item.Key] = item.Value;
                    changed = true;
                    continue;
                }

                if (_englishTexts.TryGetValue(item.Key, out var englishValue) &&
                    string.Equals(existingValue, englishValue, StringComparison.Ordinal) &&
                    !string.Equals(item.Value, englishValue, StringComparison.Ordinal))
                {
                    mergedTexts[item.Key] = item.Value;
                    changed = true;
                    continue;
                }

                if (IsKnownStaleBuiltInTranslation(item.Key, existingValue, item.Value))
                {
                    mergedTexts[item.Key] = item.Value;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
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

    private static bool IsKnownStaleBuiltInTranslation(string key, string existingValue, string updatedValue)
    {
        if (string.Equals(existingValue, updatedValue, StringComparison.Ordinal))
        {
            return false;
        }

        return key switch
        {
            "iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe" =>
                string.Equals(
                    existingValue,
                    "iperf2 executable · fallback tools\\iperf2\\iperf.exe alebo iperf2.exe",
                    StringComparison.Ordinal),
            _ => false
        };
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
            ["Appended after generated arguments. When JSON stream is enabled, --json-stream remains last."] = "Appended after generated arguments. When JSON stream is enabled, --json-stream remains last.",
            ["Available"] = "Available",
            ["Available in WinPerf Sponsor Pro."] = "Available in WinPerf Sponsor Pro.",
            ["Bidirectional (--bidir)"] = "Bidirectional (--bidir)",
            ["Bind address, optional"] = "Bind address, optional",
            ["Average"] = "Average",
            ["Browse"] = "Browse",
            ["Buffer length"] = "Buffer length",
            ["Bundled"] = "Bundled",
            ["Cancel"] = "Cancel",
            ["Check for updates"] = "Check for updates",
            ["Check for updates before installing."] = "Check for updates before installing.",
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
            ["Command preview unavailable:"] = "Command preview unavailable:",
            ["Command unavailable."] = "Command unavailable.",
            ["Changes applied for this session. Save to keep them after restart."] = "Changes applied for this session. Save to keep them after restart.",
            ["Client mode"] = "Client mode",
            ["Client mode requires a server address."] = "Client mode requires a server address.",
            ["Client port (--cport, optional)"] = "Client port (--cport, optional)",
            ["Client port must be empty or between 1 and 65535."] = "Client port must be empty or between 1 and 65535.",
            ["Client test"] = "Client test",
            ["Click parameters and WinPerf will generate clean iperf3 arguments before running them."] = "Click parameters and WinPerf will generate clean iperf3 arguments before running them.",
            ["Configure portable iperf engines used by WinPerf."] = "Configure portable iperf engines used by WinPerf.",
            ["Connected to {0} / {1}."] = "Connected to {0} / {1}.",
            ["Copy command"] = "Copy command",
            ["Core"] = "Core",
            ["Could not remove the local Sponsor Pro session."] = "Could not remove the local Sponsor Pro session.",
            ["Could not open {0}: {1}"] = "Could not open {0}: {1}",
            ["Configured"] = "Configured",
            ["Current"] = "Current",
            ["Current version is up to date"] = "Current version is up to date",
            ["Custom"] = "Custom",
            ["Custom command preview:"] = "Custom command preview:",
            ["Custom command..."] = "Custom command...",
            ["Custom iperf3 command"] = "Custom iperf3 command",
            ["Dashboard"] = "Dashboard",
            ["Default"] = "Default",
            ["Default profile set to '{0}'."] = "Default profile set to '{0}'.",
            ["Delete"] = "Delete",
            ["Delete all {0} saved history results from this portable runtime?"] = "Delete all {0} saved history results from this portable runtime?",
            ["Delete failed: {0}"] = "Delete failed: {0}",
            ["Delete history result?"] = "Delete history result?",
            ["Deleted profile '{0}'."] = "Deleted profile '{0}'.",
            ["Delete this saved result?"] = "Delete this saved result?",
            ["Details"] = "Details",
            ["DSCP, optional"] = "DSCP, optional",
            ["Duration"] = "Duration",
            ["Duration must be a positive number."] = "Duration must be a positive number.",
            ["Duration, sec"] = "Duration, sec",
            ["Engine"] = "Engine",
            ["Engine  ●  {0}  ●  Ready  ●  {1}"] = "Engine  ●  {0}  ●  Ready  ●  {1}",
            ["Engine  ●  Not configured"] = "Engine  ●  Not configured",
            ["Engine  ●  {0}  ●  Not configured"] = "Engine  ●  {0}  ●  Not configured",
            ["Engine Output"] = "Engine Output",
            ["Engines"] = "Engines",
            ["Endpoint"] = "Endpoint",
            ["Enter a server to preview the iperf command."] = "Enter a server to preview the iperf command.",
            ["Enter iperf3 arguments first."] = "Enter iperf3 arguments first.",
            ["Error"] = "Error",
            ["Examples"] = "Examples",
            ["Export"] = "Export",
            ["Export failed: {0}"] = "Export failed: {0}",
            ["Export WinPerf history"] = "Export WinPerf history",
            ["Exported 1 result."] = "Exported 1 result.",
            ["Exported {0} results."] = "Exported {0} results.",
            ["Exit code"] = "Exit code",
            ["Extra arguments, optional"] = "Extra arguments, optional",
            ["Failed"] = "Failed",
            ["Failed to run server:"] = "Failed to run server:",
            ["For live charts, prefer --json-stream."] = "For live charts, prefer --json-stream.",
            ["Free"] = "Free",
            ["Generated arguments"] = "Generated arguments",
            ["Get server output (--get-server-output)"] = "Get server output (--get-server-output)",
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
            ["iperf2 is available in WinPerf Sponsor Pro."] = "iperf2 is available in WinPerf Sponsor Pro.",
            ["iperf2 compatibility engine"] = "iperf2 compatibility engine",
            ["iperf3 throughput engine"] = "iperf3 throughput engine",
            ["IP version"] = "IP version",
            ["Jitter"] = "Jitter",
            ["Language"] = "Language",
            ["Last"] = "Last",
            ["Last selected profile saved."] = "Last selected profile saved.",
            ["Loaded"] = "Loaded",
            ["Loaded profile '{0}'."] = "Loaded profile '{0}'.",
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
            ["No saved profiles found."] = "No saved profiles found.",
            ["No saved profiles yet."] = "No saved profiles yet.",
            ["No summary saved."] = "No summary saved.",
            ["Not checked yet"] = "Not checked yet",
            ["Not configured"] = "Not configured",
            ["Not loaded yet"] = "Not loaded yet",
            ["Not signed in"] = "Not signed in",
            ["OK"] = "OK",
            ["Omit"] = "Omit",
            ["Omit first seconds (-O)"] = "Omit first seconds (-O)",
            ["Omit seconds must be empty, zero, or a positive number."] = "Omit seconds must be empty, zero, or a positive number.",
            ["One-off server, stop after one client (-1)"] = "One-off server, stop after one client (-1)",
            ["Open data"] = "Open data",
            ["Open iperf2"] = "Open iperf2",
            ["Open iperf3"] = "Open iperf3",
            ["Opened {0}: {1}"] = "Opened {0}: {1}",
            ["Opening GitHub Sponsor Pro login..."] = "Opening GitHub Sponsor Pro login...",
            ["Port"] = "Port",
            ["Port must be between 1 and 65535."] = "Port must be between 1 and 65535.",
            ["Portable import failed: {0}"] = "Portable import failed: {0}",
            ["Portable data folder"] = "Portable data folder",
            ["Portable folders"] = "Portable folders",
            ["Portable iperf2 engine folder"] = "Portable iperf2 engine folder",
            ["Portable iperf3 engine folder"] = "Portable iperf3 engine folder",
            ["Delete profile?"] = "Delete profile?",
            ["Delete profile '{0}'?"] = "Delete profile '{0}'?",
            ["Fix the advanced command options first."] = "Fix the advanced command options first.",
            ["Profile load failed: {0}"] = "Profile load failed: {0}",
            ["Profile name"] = "Profile name",
            ["Profile selection was not saved: {0}"] = "Profile selection was not saved: {0}",
            ["Profile save failed:"] = "Profile save failed:",
            ["Profile save failed: {0}"] = "Profile save failed: {0}",
            ["Profile values are invalid:"] = "Profile values are invalid:",
            ["Profiles are stored in %APPDATA%\\WinPerf\\profiles.json"] = "Profiles are stored in %APPDATA%\\WinPerf\\profiles.json",
            ["Ready"] = "Ready",
            ["Ready. Sign in to enable Sponsor Pro update checks."] = "Ready. Sign in to enable Sponsor Pro update checks.",
            ["Remove"] = "Remove",
            ["Report format"] = "Report format",
            ["Report interval must be empty or a positive number."] = "Report interval must be empty or a positive number.",
            ["Report interval, sec"] = "Report interval, sec",
            ["Reverse (-R)"] = "Reverse (-R)",
            ["Reverse average"] = "Reverse average",
            ["Reverse and bidirectional cannot be enabled together."] = "Reverse and bidirectional cannot be enabled together.",
            ["Result deleted."] = "Result deleted.",
            ["Result details"] = "Result details",
            ["Result was not found."] = "Result was not found.",
            ["Run a local iperf server for LAN, routed, VPN or public-client testing."] = "Run a local iperf server for LAN, routed, VPN or public-client testing.",
            ["Run iperf3 and iperf2 throughput tests with live visualization and saved results."] = "Run iperf3 and iperf2 throughput tests with live visualization and saved results.",
            ["Running"] = "Running",
            ["Running server command:"] = "Running server command:",
            ["Save"] = "Save",
            ["Save as new"] = "Save as new",
            ["Saved new profile '{0}'."] = "Saved new profile '{0}'.",
            ["Saved local test results from this portable WinPerf runtime."] = "Saved local test results from this portable WinPerf runtime.",
            ["Saved profile"] = "Saved profile",
            ["Saved profile '{0}'."] = "Saved profile '{0}'.",
            ["{0} saved results"] = "{0} saved results",
            ["Server"] = "Server",
            ["Server address / host"] = "Server address / host",
            ["{0} is not configured. Open Settings and select the executable first."] = "{0} is not configured. Open Settings and select the executable first.",
            ["{0} not configured"] = "{0} not configured",
            ["Select a profile first."] = "Select a profile first.",
            ["Select an existing {0} first."] = "Select an existing {0} first.",
            ["Selected profile '{0}'."] = "Selected profile '{0}'.",
            ["Selected iperf.exe / iperf2.exe path does not exist."] = "Selected iperf.exe / iperf2.exe path does not exist.",
            ["Selected iperf3.exe path does not exist."] = "Selected iperf3.exe path does not exist.",
            ["Server cannot start: engine missing."] = "Server cannot start: engine missing.",
            ["Server command preview:"] = "Server command preview:",
            ["Server command preview unavailable:"] = "Server command preview unavailable:",
            ["Server configuration is invalid."] = "Server configuration is invalid.",
            ["Server failed to start or stopped unexpectedly."] = "Server failed to start or stopped unexpectedly.",
            ["Server mode"] = "Server mode",
            ["Server Mode"] = "Server Mode",
            ["Server mode:    -s -p 5201"] = "Server mode:    -s -p 5201",
            ["Server options"] = "Server options",
            ["Server Output"] = "Server Output",
            ["Server process exited with code {0}."] = "Server process exited with code {0}.",
            ["Server stopped."] = "Server stopped.",
            ["Server stopped by user."] = "Server stopped by user.",
            ["Server stopped with exit code {0}."] = "Server stopped with exit code {0}.",
            ["Set default"] = "Set default",
            ["Settings"] = "Settings",
            ["Sign in with GitHub Sponsor Pro before checking for updates."] = "Sign in with GitHub Sponsor Pro before checking for updates.",
            ["Sign in with GitHub Sponsor Pro before installing updates."] = "Sign in with GitHub Sponsor Pro before installing updates.",
            ["Sign in with GitHub"] = "Sign in with GitHub",
            ["Sign in with GitHub to use Sponsor Pro updates."] = "Sign in with GitHub to use Sponsor Pro updates.",
            ["Sign out"] = "Sign out",
            ["Signed out locally. Your GitHub browser session is unchanged."] = "Signed out locally. Your GitHub browser session is unchanged.",
            ["Single portable WinPerf.exe. Updates use the private Sponsor Pro channel."] = "Single portable WinPerf.exe. Updates use the private Sponsor Pro channel.",
            ["Sponsor Pro / Updates"] = "Sponsor Pro / Updates",
            ["Sponsor Pro account is connected. You can check for private updates."] = "Sponsor Pro account is connected. You can check for private updates.",
            ["Sponsor Pro login failed or was not authorized."] = "Sponsor Pro login failed or was not authorized.",
            ["Sponsor Pro login failed: {0}"] = "Sponsor Pro login failed: {0}",
            ["Sponsor Pro login failed. Check your connection and try again."] = "Sponsor Pro login failed. Check your connection and try again.",
            ["Sponsor Pro login was cancelled."] = "Sponsor Pro login was cancelled.",
            ["Sponsor Pro session expired. Sign in again and retry the update."] = "Sponsor Pro session expired. Sign in again and retry the update.",
            ["Sponsor Pro updates are available only in WinPerf Sponsor Pro."] = "Sponsor Pro updates are available only in WinPerf Sponsor Pro.",
            ["Speed Test"] = "Speed Test",
            ["Start"] = "Start",
            ["Start will run these generated/custom arguments instead of the dashboard fields."] = "Start will run these generated/custom arguments instead of the dashboard fields.",
            ["Start server"] = "Start server",
            ["Stop"] = "Stop",
            ["Stop after one client (--one-off)"] = "Stop after one client (--one-off)",
            ["Stop server"] = "Stop server",
            ["Stopped"] = "Stopped",
            ["Stopped. Ready to start local server."] = "Stopped. Ready to start local server.",
            ["Streams"] = "Streams",
            ["Streams must be a positive number."] = "Streams must be a positive number.",
            ["Status"] = "Status",
            ["Summary"] = "Summary",
            ["TCP download:   -c <server> -p 5201 -4 -R -t 10 -P 10 --json-stream"] = "TCP download:   -c <server> -p 5201 -4 -R -t 10 -P 10 --json-stream",
            ["TCP MSS"] = "TCP MSS",
            ["TCP MSS must be empty or a positive number."] = "TCP MSS must be empty or a positive number.",
            ["TCP no delay (-N)"] = "TCP no delay (-N)",
            ["TCP upload"] = "TCP upload",
            ["TCP upload:     -c <server> -p 5201 -4 -t 10 -P 10 --json-stream"] = "TCP upload:     -c <server> -p 5201 -4 -t 10 -P 10 --json-stream",
            ["TCP window"] = "TCP window",
            ["Test Configuration"] = "Test Configuration",
            ["Test profile"] = "Test profile",
            ["This edition does not use the private Sponsor Pro update channel."] = "This edition does not use the private Sponsor Pro update channel.",
            ["This test mode is available in WinPerf Sponsor Pro."] = "This test mode is available in WinPerf Sponsor Pro.",
            ["Time (sec)"] = "Time (sec)",
            ["Total bandwidth"] = "Total bandwidth",
            ["Transport tuning"] = "Transport tuning",
            ["UDP bandwidth"] = "UDP bandwidth",
            ["UDP upload:     -c <server> -p 5201 -4 -u -b 0 -t 10 -P 10 --json-stream"] = "UDP upload:     -c <server> -p 5201 -4 -u -b 0 -t 10 -P 10 --json-stream",
            ["UDP only"] = "UDP only",
            ["Update check failed"] = "Update check failed",
            ["Update check failed or server response was invalid."] = "Update check failed or server response was invalid.",
            ["Update check failed. Check your connection and try again."] = "Update check failed. Check your connection and try again.",
            ["Update check was cancelled."] = "Update check was cancelled.",
            ["Update failed"] = "Update failed",
            ["Update found. Install button will be enabled after helper launcher/startup wiring."] = "Update found. Install button will be enabled after helper launcher/startup wiring.",
            ["Update found. You can install it now."] = "Update found. You can install it now.",
            ["Update installation failed and automatic rollback needs manual recovery from {0}."] = "Update installation failed and automatic rollback needs manual recovery from {0}.",
            ["Update installation failed. WinPerf was rolled back to the previous version."] = "Update installation failed. WinPerf was rolled back to the previous version.",
            ["Update installation failed. WinPerf.exe was not changed."] = "Update installation failed. WinPerf.exe was not changed.",
            ["Update installation was cancelled."] = "Update installation was cancelled.",
            ["Update installed"] = "Update installed",
            ["Update recovery required"] = "Update recovery required",
            ["Use Command"] = "Use Command",
            ["Use --json-stream for live chart parsing"] = "Use --json-stream for live chart parsing",
            ["Use this for advanced flags, public servers, server mode, UDP bitrate tuning, or manual troubleshooting."] = "Use this for advanced flags, public servers, server mode, UDP bitrate tuning, or manual troubleshooting.",
            ["Verbose output"] = "Verbose output",
            ["Waiting for GitHub authorization..."] = "Waiting for GitHub authorization...",
            ["Waiting for test..."] = "Waiting for test...",
            ["Waiting for throughput samples..."] = "Waiting for throughput samples...",
            ["WinPerf is up to date on the Sponsor Pro channel."] = "WinPerf is up to date on the Sponsor Pro channel.",
            ["WinPerf Free allows tests up to {0} seconds."] = "WinPerf Free allows tests up to {0} seconds.",
            ["WinPerf Free allows up to {0} stream."] = "WinPerf Free allows up to {0} stream.",
            ["WinPerf Free includes iperf3 TCP upload/download, 1 stream and 10 second tests."] = "WinPerf Free includes iperf3 TCP upload/download, 1 stream and 10 second tests.",
            ["WinPerf is not enabled on the Sponsor Pro update server yet."] = "WinPerf is not enabled on the Sponsor Pro update server yet.",
            ["WinPerf was updated successfully."] = "WinPerf was updated successfully.",
            ["WinPerf will close, replace only WinPerf.exe, and restart. Portable data, tools, language packs, profiles and history stay in place."] = "WinPerf will close, replace only WinPerf.exe, and restart. Portable data, tools, language packs, profiles and history stay in place.",
            ["Zero copy (-Z, OS-dependent)"] = "Zero copy (-Z, OS-dependent)",
            ["{0} command override active"] = "{0} command override active",
            ["{0} ignored"] = "{0} ignored",
            ["sent avg {0}"] = "sent avg {0}",
            ["{0}s · {1} stream(s) · {2}"] = "{0}s · {1} stream(s) · {2}",
            ["1 saved result"] = "1 saved result",
            ["Download last {0} · min {1} · avg {2} · max {3}"] = "Download last {0} · min {1} · avg {2} · max {3}",
            ["Download last"] = "Download last",
            ["download {0}"] = "download {0}",
            ["Exit {0}"] = "Exit {0}",
            ["Failed to run {0}:"] = "Failed to run {0}:",
            ["History save failed: {0}"] = "History save failed: {0}",
            ["Ignoring warm-up samples. Live chart starts after warm-up."] = "Ignoring warm-up samples. Live chart starts after warm-up.",
            ["Interval"] = "Interval",
            ["Interval {0}s"] = "Interval {0}s",
            ["Invalid test configuration:"] = "Invalid test configuration:",
            ["iperf3 error: {0}"] = "iperf3 error: {0}",
            ["iperf3 event received."] = "iperf3 event received.",
            ["iperf3 event: {0}"] = "iperf3 event: {0}",
            ["jitter {0}"] = "jitter {0}",
            ["jitter {0} ms"] = "jitter {0} ms",
            ["Last {0} · min {1} · avg {2} · max {3}"] = "Last {0} · min {1} · avg {2} · max {3}",
            ["loss {0}"] = "loss {0}",
            ["loss {0} %"] = "loss {0} %",
            ["No throughput samples."] = "No throughput samples.",
            ["Process exited with code {0}."] = "Process exited with code {0}.",
            ["Received {0}{1}"] = "Received {0}{1}",
            ["Running command:"] = "Running command:",
            ["Server result unavailable ({0}/{1} streams)."] = "Server result unavailable ({0}/{1} streams).",
            ["TCP Bidirectional"] = "TCP Bidirectional",
            ["TCP Download"] = "TCP Download",
            ["TCP Upload"] = "TCP Upload",
            ["Test completed"] = "Test completed",
            ["Test completed."] = "Test completed.",
            ["Test completed with warning."] = "Test completed with warning.",
            ["Test completed with warning: {0}"] = "Test completed with warning: {0}",
            ["Test failed."] = "Test failed.",
            ["Test failed: {0}"] = "Test failed: {0}",
            ["Test failed: incomplete iperf2 UDP server report ({0}/{1} streams)."] = "Test failed: incomplete iperf2 UDP server report ({0}/{1} streams).",
            ["Test failed: process exited with code {0}."] = "Test failed: process exited with code {0}.",
            ["Test failed: the iperf executable could not start because a required Windows DLL is missing. Re-import the portable engine from its full folder so WinPerf can copy the companion .dll files."] = "Test failed: the iperf executable could not start because a required Windows DLL is missing. Re-import the portable engine from its full folder so WinPerf can copy the companion .dll files.",
            ["Test started."] = "Test started.",
            ["Test stopped by user."] = "Test stopped by user.",
            ["UDP Download"] = "UDP Download",
            ["UDP Upload"] = "UDP Upload",
            ["unknown error"] = "unknown error",
            ["Upload last {0} · min {1} · avg {2} · max {3}"] = "Upload last {0} · min {1} · avg {2} · max {3}",
            ["Upload last"] = "Upload last",
            ["upload {0}"] = "upload {0}",
            ["Warm-up: omitting first {0}s before live metrics."] = "Warm-up: omitting first {0}s before live metrics.",
            ["Warm-up: omitting first {0}s..."] = "Warm-up: omitting first {0}s...",
            ["Warm-up {0}/{1}s"] = "Warm-up {0}/{1}s",
            ["Warm-up {0}/{1}s omitted{2}"] = "Warm-up {0}/{1}s omitted{2}",
            ["Warm-up {0}/{1}s omitted{2}."] = "Warm-up {0}/{1}s omitted{2}.",
            ["Warm-up sample omitted{0}"] = "Warm-up sample omitted{0}",
            ["WinPerf Settings"] = "WinPerf Settings",
            ["WinPerfLanguage.Description"] = "Choose the app language. English is built in; other languages are loaded from the lang folder beside WinPerf.exe.",
            ["WinPerfLanguage.EnglishDisplay"] = "English",
            ["WinPerfLanguage.SlovakDisplay"] = "Slovenčina",
            ["WinPerfLanguage.Status"] = "Language packs are loaded from the portable lang folder.",
            ["0 Mbps"] = "0 Mbps",
            ["0.00 ms"] = "0.00 ms",
            ["Advanced builder..."] = "Advanced builder...",
            ["Awaiting server result"] = "Awaiting server result",
            ["Bandwidth / stream"] = "Bandwidth / stream",
            ["Client, manifest validation and installer contracts loaded"] = "Client, manifest validation and installer contracts loaded",
            ["Command override active"] = "Command override active",
            ["Command ▾"] = "Command ▾",
            ["Confirm"] = "Confirm",
            ["Confirm action"] = "Confirm action",
            ["Continue?"] = "Continue?",
            ["Download"] = "Download",
            ["Downloading and validating WinPerf update..."] = "Downloading and validating WinPerf update...",
            ["Finished"] = "Finished",
            ["GitHub Sponsor Pro account, private update channel and WinPerf update package status."] = "GitHub Sponsor Pro account, private update channel and WinPerf update package status.",
            ["Install update"] = "Install update",
            ["Install WinPerf update?"] = "Install WinPerf update?",
            ["Installer launcher/startup wiring is the next updater slice."] = "Installer launcher/startup wiring is the next updater slice.",
            ["Installed"] = "Installed",
            ["Invalid server configuration:"] = "Invalid server configuration:",
            ["Last Summary"] = "Last Summary",
            ["Last sample {0}s"] = "Last sample {0}s",
            ["Last {0}"] = "Last {0}",
            ["Latest"] = "Latest",
            ["Live Total Throughput"] = "Live Total Throughput",
            ["Live total average"] = "Live total average",
            ["No completed test yet."] = "No completed test yet.",
            ["One-off is iperf3 only. iperf2 runs until stopped."] = "One-off is iperf3 only. iperf2 runs until stopped.",
            ["Output"] = "Output",
            ["Per-stream: {0} streams · scale 0-{1}"] = "Per-stream: {0} streams · scale 0-{1}",
            ["Per-stream: {0} streams · avg {1} · min {2} · max {3} · scale 0-{4}"] = "Per-stream: {0} streams · avg {1} · min {2} · max {3} · scale 0-{4}",
            ["Portable single-EXE runtime"] = "Portable single-EXE runtime",
            ["Private Sponsor Pro updater"] = "Private Sponsor Pro updater",
            ["Preparing Sponsor Pro update download..."] = "Preparing Sponsor Pro update download...",
            ["Product"] = "Product",
            ["Protocol"] = "Protocol",
            ["Receiving samples..."] = "Receiving samples...",
            ["Run mode"] = "Run mode",
            ["Enter target server address."] = "Enter target server address.",
            ["Open app settings, updates and information"] = "Open app settings, updates and information",
            ["Speed Test page will be added later."] = "Speed Test page will be added later.",
            ["Enter server address or select one from recent servers."] = "Enter server address or select one from recent servers.",
            ["Warm-up seconds to ignore. Use 10–15 for routed, VLAN, VPN, or public server download tests."] = "Warm-up seconds to ignore. Use 10–15 for routed, VLAN, VPN, or public server download tests.",
            ["Target UDP bandwidth per stream. With 10 streams, 10M is about 100M total."] = "Target UDP bandwidth per stream. With 10 streams, 10M is about 100M total.",
            ["Application version from the running executable."] = "Application version from the running executable.",
            ["Selected engine integration status"] = "Selected engine integration status",
            ["Server command preview will appear here."] = "Server command preview will appear here.",
            ["Server received total"] = "Server received total",
            ["Server received total {0} · chart shows sent rate"] = "Server received total {0} · chart shows sent rate",
            ["Server result missing"] = "Server result missing",
            ["Starting update helper. WinPerf will restart after installation."] = "Starting update helper. WinPerf will restart after installation.",
            ["Incomplete server report: {0}/{1} streams"] = "Incomplete server report: {0}/{1} streams",
            ["Sponsor Pro planned · Free edition will be reduced"] = "Sponsor Pro planned · Free edition will be reduced",
            ["Start uses these arguments instead of dashboard fields."] = "Start uses these arguments instead of dashboard fields.",
            ["Update channel"] = "Update channel",
            ["Version"] = "Version",
            ["Waiting for samples..."] = "Waiting for samples...",
            ["iperf output will appear here."] = "iperf output will appear here.",
            ["iperf result"] = "iperf result",
            ["pending"] = "pending",
            ["unavailable"] = "unavailable",
            ["Engine  ●  {0}  ●  {1}  ●  {2}"] = "Engine  ●  {0}  ●  {1}  ●  {2}",
            ["Engine  ●  {0}  ●  {1}"] = "Engine  ●  {0}  ●  {1}",
            ["total {0}   min {1} · avg {2} · max {3}"] = "total {0}   min {1} · avg {2} · max {3}",
            ["↑ {0} · ↓ {1}   ↑ avg {2} · ↓ avg {3}"] = "↑ {0} · ↓ {1}   ↑ avg {2} · ↓ avg {3}",
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
            ["Advanced iperf3 builder"] = "Pokročilý tvorca iperf3 príkazu",
            ["Apply"] = "Použiť",
            ["Appended after generated arguments. When JSON stream is enabled, --json-stream remains last."] = "Pridá sa za vygenerované argumenty. Pri zapnutom JSON streame zostane --json-stream posledný.",
            ["Available"] = "Dostupné",
            ["Available in WinPerf Sponsor Pro."] = "Dostupné vo WinPerf Sponsor Pro.",
            ["Average"] = "Priemer",
            ["Bidirectional (--bidir)"] = "Obojsmerne (--bidir)",
            ["Bind address, optional"] = "Lokálna adresa, voliteľné",
            ["Browse"] = "Vybrať",
            ["Buffer length"] = "Veľkosť buffera",
            ["Bundled"] = "V balíku",
            ["Cancel"] = "Zrušiť",
            ["Check for updates"] = "Skontrolovať aktualizácie",
            ["Check for updates before installing."] = "Pred inštaláciou najprv skontroluj aktualizácie.",
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
            ["Command preview unavailable:"] = "Náhľad príkazu nie je dostupný:",
            ["Command unavailable."] = "Príkaz nie je dostupný.",
            ["Changes applied for this session. Save to keep them after restart."] = "Zmeny sú použité pre túto reláciu. Ulož ich tlačidlom Uložiť, aby ostali aj po reštarte.",
            ["Client mode"] = "Klientský režim",
            ["Client mode requires a server address."] = "Klientský režim vyžaduje adresu servera.",
            ["Client port (--cport, optional)"] = "Klientsky port (--cport, voliteľné)",
            ["Client port must be empty or between 1 and 65535."] = "Klientsky port musí byť prázdny alebo od 1 do 65535.",
            ["Client test"] = "Klientský test",
            ["Click parameters and WinPerf will generate clean iperf3 arguments before running them."] = "Vyber parametre a WinPerf pred spustením vytvorí čisté iperf3 argumenty.",
            ["Configure portable iperf engines used by WinPerf."] = "Nastavenie prenosných iperf nástrojov používaných aplikáciou WinPerf.",
            ["Connected to {0} / {1}."] = "Pripojené k {0} / {1}.",
            ["Copy command"] = "Kopírovať príkaz",
            ["Core"] = "Jadro",
            ["Could not remove the local Sponsor Pro session."] = "Lokálnu Sponsor Pro reláciu sa nepodarilo odstrániť.",
            ["Could not open {0}: {1}"] = "Nepodarilo sa otvoriť {0}: {1}",
            ["Configured"] = "Nastavené",
            ["Current"] = "Aktuálne",
            ["Current version is up to date"] = "Aktuálna verzia je najnovšia",
            ["Custom"] = "Vlastný",
            ["Custom command preview:"] = "Náhľad vlastného príkazu:",
            ["Custom command..."] = "Vlastný príkaz...",
            ["Custom iperf3 command"] = "Vlastný iperf3 príkaz",
            ["Dashboard"] = "Prehľad",
            ["Default"] = "Predvolené",
            ["Default profile set to '{0}'."] = "Predvolený profil je „{0}“.",
            ["Delete"] = "Vymazať",
            ["Delete all {0} saved history results from this portable runtime?"] = "Vymazať všetkých {0} uložených výsledkov z tejto prenosnej inštancie?",
            ["Delete failed: {0}"] = "Mazanie zlyhalo: {0}",
            ["Delete history result?"] = "Vymazať výsledok z histórie?",
            ["Deleted profile '{0}'."] = "Profil „{0}“ bol vymazaný.",
            ["Delete this saved result?"] = "Vymazať tento uložený výsledok?",
            ["Details"] = "Detaily",
            ["DSCP, optional"] = "DSCP, voliteľné",
            ["Duration"] = "Trvanie",
            ["Duration must be a positive number."] = "Trvanie musí byť kladné číslo.",
            ["Duration, sec"] = "Trvanie, s",
            ["Engine"] = "Engine",
            ["Engine  ●  {0}  ●  Ready  ●  {1}"] = "Engine  ●  {0}  ●  Pripravené  ●  {1}",
            ["Engine  ●  Not configured"] = "Engine  ●  Nenastavené",
            ["Engine  ●  {0}  ●  Not configured"] = "Engine  ●  {0}  ●  Nenastavené",
            ["Engine Output"] = "Výstup enginu",
            ["Engines"] = "Enginy",
            ["Endpoint"] = "Cieľ",
            ["Enter a server to preview the iperf command."] = "Zadaj server pre náhľad iperf príkazu.",
            ["Enter iperf3 arguments first."] = "Najprv zadaj iperf3 argumenty.",
            ["Error"] = "Chyba",
            ["Examples"] = "Príklady",
            ["Export"] = "Export",
            ["Export failed: {0}"] = "Export zlyhal: {0}",
            ["Export WinPerf history"] = "Export histórie WinPerf",
            ["Exported 1 result."] = "Exportovaný 1 výsledok.",
            ["Exported {0} results."] = "Exportovaných {0} výsledkov.",
            ["Exit code"] = "Návratový kód",
            ["Extra arguments, optional"] = "Ďalšie argumenty, voliteľné",
            ["Failed"] = "Zlyhalo",
            ["Failed to run server:"] = "Server sa nepodarilo spustiť:",
            ["For live charts, prefer --json-stream."] = "Pre živé grafy odporúčame --json-stream.",
            ["Free"] = "Free",
            ["Generated arguments"] = "Vygenerované argumenty",
            ["Get server output (--get-server-output)"] = "Načítať výstup servera (--get-server-output)",
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
            ["iperf2 is available in WinPerf Sponsor Pro."] = "iperf2 je dostupný vo WinPerf Sponsor Pro.",
            ["iperf2 compatibility engine"] = "iperf2 kompatibilný engine",
            ["iperf3 throughput engine"] = "iperf3 engine priepustnosti",
            ["IP version"] = "Verzia IP",
            ["Jitter"] = "Jitter",
            ["Language"] = "Jazyk",
            ["Last"] = "Posledné",
            ["Last selected profile saved."] = "Posledný vybraný profil bol uložený.",
            ["Loaded"] = "Načítané",
            ["Loaded profile '{0}'."] = "Načítaný profil „{0}“.",
            ["Local server"] = "Lokálny server",
            ["Login"] = "Prihlásenie",
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
            ["No saved profiles found."] = "Nenašli sa uložené profily.",
            ["No saved profiles yet."] = "Zatiaľ nie sú uložené profily.",
            ["No summary saved."] = "Súhrn nie je uložený.",
            ["Not checked yet"] = "Zatiaľ nekontrolované",
            ["Not configured"] = "Nenastavené",
            ["Not loaded yet"] = "Zatiaľ nenačítané",
            ["Not signed in"] = "Neprihlásené",
            ["OK"] = "OK",
            ["Omit"] = "Vynechať",
            ["Omit first seconds (-O)"] = "Vynechať prvé sekundy (-O)",
            ["Omit seconds must be empty, zero, or a positive number."] = "Vynechané sekundy musia byť prázdne, nula alebo kladné číslo.",
            ["One-off server, stop after one client (-1)"] = "Jednorazový server, zastaviť po jednom klientovi (-1)",
            ["Open data"] = "Otvoriť dáta",
            ["Open iperf2"] = "Otvoriť iperf2",
            ["Open iperf3"] = "Otvoriť iperf3",
            ["Opened {0}: {1}"] = "Otvorené {0}: {1}",
            ["Opening GitHub Sponsor Pro login..."] = "Otváram GitHub Sponsor Pro prihlásenie...",
            ["Port"] = "Port",
            ["Port must be between 1 and 65535."] = "Port musí byť od 1 do 65535.",
            ["Portable import failed: {0}"] = "Import prenosného enginu zlyhal: {0}",
            ["Portable data folder"] = "Priečinok prenosných dát",
            ["Portable folders"] = "Prenosné priečinky",
            ["Portable iperf2 engine folder"] = "Priečinok prenosného iperf2 enginu",
            ["Portable iperf3 engine folder"] = "Priečinok prenosného iperf3 enginu",
            ["Delete profile?"] = "Vymazať profil?",
            ["Delete profile '{0}'?"] = "Vymazať profil „{0}“?",
            ["Fix the advanced command options first."] = "Najprv oprav pokročilé nastavenia príkazu.",
            ["Profile load failed: {0}"] = "Načítanie profilu zlyhalo: {0}",
            ["Profile name"] = "Názov profilu",
            ["Profile selection was not saved: {0}"] = "Výber profilu sa neuložil: {0}",
            ["Profile save failed:"] = "Uloženie profilu zlyhalo:",
            ["Profile save failed: {0}"] = "Uloženie profilu zlyhalo: {0}",
            ["Profile values are invalid:"] = "Hodnoty profilu sú neplatné:",
            ["Profiles are stored in %APPDATA%\\WinPerf\\profiles.json"] = "Profily sa ukladajú do %APPDATA%\\WinPerf\\profiles.json",
            ["Ready"] = "Pripravené",
            ["Ready. Sign in to enable Sponsor Pro update checks."] = "Pripravené. Prihlás sa pre kontrolu Sponsor Pro aktualizácií.",
            ["Remove"] = "Odobrať",
            ["Report format"] = "Formát výstupu",
            ["Report interval must be empty or a positive number."] = "Interval výstupu musí byť prázdny alebo kladné číslo.",
            ["Report interval, sec"] = "Interval výstupu, s",
            ["Reverse (-R)"] = "Download režim (-R)",
            ["Reverse average"] = "Reverzný priemer",
            ["Reverse and bidirectional cannot be enabled together."] = "Reverse a obojsmerný režim nemôžu byť zapnuté naraz.",
            ["Result deleted."] = "Výsledok bol vymazaný.",
            ["Result details"] = "Detaily výsledku",
            ["Result was not found."] = "Výsledok sa nenašiel.",
            ["Run a local iperf server for LAN, routed, VPN or public-client testing."] = "Spusti lokálny iperf server pre LAN, routované, VPN alebo verejné klientské testy.",
            ["Run iperf3 and iperf2 throughput tests with live visualization and saved results."] = "Spúšťaj iperf3 a iperf2 testy priepustnosti so živým grafom a uloženými výsledkami.",
            ["Running"] = "Beží",
            ["Running server command:"] = "Spúšťam serverový príkaz:",
            ["Save"] = "Uložiť",
            ["Save as new"] = "Uložiť ako nový",
            ["Saved new profile '{0}'."] = "Nový profil „{0}“ bol uložený.",
            ["Saved local test results from this portable WinPerf runtime."] = "Lokálne uložené výsledky z tejto prenosnej WinPerf inštancie.",
            ["Saved profile"] = "Uložený profil",
            ["Saved profile '{0}'."] = "Profil „{0}“ bol uložený.",
            ["{0} saved results"] = "{0} uložených výsledkov",
            ["Server"] = "Server",
            ["Server address / host"] = "Adresa servera / hostiteľ",
            ["{0} is not configured. Open Settings and select the executable first."] = "{0} nie je nastavený. Otvor Nastavenia a najprv vyber spustiteľný súbor.",
            ["{0} not configured"] = "{0} nie je nastavené",
            ["Select a profile first."] = "Najprv vyber profil.",
            ["Select an existing {0} first."] = "Najprv vyber existujúci {0}.",
            ["Selected profile '{0}'."] = "Vybraný profil „{0}“.",
            ["Selected iperf.exe / iperf2.exe path does not exist."] = "Vybraná cesta k iperf.exe / iperf2.exe neexistuje.",
            ["Selected iperf3.exe path does not exist."] = "Vybraná cesta k iperf3.exe neexistuje.",
            ["Server cannot start: engine missing."] = "Server sa nedá spustiť: chýba engine.",
            ["Server command preview:"] = "Náhľad serverového príkazu:",
            ["Server command preview unavailable:"] = "Náhľad serverového príkazu nie je dostupný:",
            ["Server configuration is invalid."] = "Nastavenie servera je neplatné.",
            ["Server failed to start or stopped unexpectedly."] = "Server sa nespustil alebo sa neočakávane zastavil.",
            ["Server mode"] = "Serverový režim",
            ["Server Mode"] = "Server režim",
            ["Server mode:    -s -p 5201"] = "Server režim:   -s -p 5201",
            ["Server options"] = "Možnosti servera",
            ["Server Output"] = "Výstup servera",
            ["Server process exited with code {0}."] = "Serverový proces skončil s kódom {0}.",
            ["Server stopped."] = "Server zastavený.",
            ["Server stopped by user."] = "Server zastavil používateľ.",
            ["Server stopped with exit code {0}."] = "Server sa zastavil s kódom {0}.",
            ["Set default"] = "Nastaviť predvolený",
            ["Settings"] = "Nastavenia",
            ["Sign in with GitHub Sponsor Pro before checking for updates."] = "Pred kontrolou aktualizácií sa prihlás cez GitHub Sponsor Pro.",
            ["Sign in with GitHub Sponsor Pro before installing updates."] = "Pred inštaláciou aktualizácií sa prihlás cez GitHub Sponsor Pro.",
            ["Sign in with GitHub"] = "Prihlásiť cez GitHub",
            ["Sign in with GitHub to use Sponsor Pro updates."] = "Prihlás sa cez GitHub pre Sponsor Pro aktualizácie.",
            ["Sign out"] = "Odhlásiť",
            ["Signed out locally. Your GitHub browser session is unchanged."] = "Lokálne odhlásené. Tvoja GitHub relácia v prehliadači ostala nezmenená.",
            ["Single portable WinPerf.exe. Updates use the private Sponsor Pro channel."] = "Jedno prenosné WinPerf.exe. Aktualizácie používajú súkromný Sponsor Pro kanál.",
            ["Sponsor Pro / Updates"] = "Sponsor Pro / Aktualizácie",
            ["Sponsor Pro account is connected. You can check for private updates."] = "Sponsor Pro účet je pripojený. Môžeš skontrolovať súkromné aktualizácie.",
            ["Sponsor Pro login failed or was not authorized."] = "Sponsor Pro prihlásenie zlyhalo alebo nebolo autorizované.",
            ["Sponsor Pro login failed: {0}"] = "Sponsor Pro prihlásenie zlyhalo: {0}",
            ["Sponsor Pro login failed. Check your connection and try again."] = "Sponsor Pro prihlásenie zlyhalo. Skontroluj pripojenie a skús znova.",
            ["Sponsor Pro login was cancelled."] = "Sponsor Pro prihlásenie bolo zrušené.",
            ["Sponsor Pro session expired. Sign in again and retry the update."] = "Sponsor Pro relácia vypršala. Prihlás sa znova a zopakuj aktualizáciu.",
            ["Sponsor Pro updates are available only in WinPerf Sponsor Pro."] = "Sponsor Pro aktualizácie sú dostupné iba vo WinPerf Sponsor Pro.",
            ["Speed Test"] = "Test rýchlosti",
            ["Start"] = "Štart",
            ["Start will run these generated/custom arguments instead of the dashboard fields."] = "Štart spustí tieto vygenerované alebo vlastné argumenty namiesto polí v prehľade.",
            ["Start server"] = "Spustiť server",
            ["Stop"] = "Stop",
            ["Stop after one client (--one-off)"] = "Zastaviť po jednom klientovi (--one-off)",
            ["Stop server"] = "Zastaviť server",
            ["Stopped"] = "Zastavené",
            ["Stopped. Ready to start local server."] = "Zastavené. Lokálny server je pripravený na spustenie.",
            ["Streams"] = "Streamy",
            ["Streams must be a positive number."] = "Počet streamov musí byť kladné číslo.",
            ["Status"] = "Stav",
            ["Summary"] = "Súhrn",
            ["TCP download:   -c <server> -p 5201 -4 -R -t 10 -P 10 --json-stream"] = "TCP download:   -c <server> -p 5201 -4 -R -t 10 -P 10 --json-stream",
            ["TCP MSS"] = "TCP MSS",
            ["TCP MSS must be empty or a positive number."] = "TCP MSS musí byť prázdne alebo kladné číslo.",
            ["TCP no delay (-N)"] = "TCP bez oneskorenia (-N)",
            ["TCP upload"] = "TCP upload",
            ["TCP upload:     -c <server> -p 5201 -4 -t 10 -P 10 --json-stream"] = "TCP upload:     -c <server> -p 5201 -4 -t 10 -P 10 --json-stream",
            ["TCP window"] = "TCP okno",
            ["Test Configuration"] = "Nastavenie testu",
            ["Test profile"] = "Testovací profil",
            ["This edition does not use the private Sponsor Pro update channel."] = "Táto edícia nepoužíva súkromný Sponsor Pro aktualizačný kanál.",
            ["This test mode is available in WinPerf Sponsor Pro."] = "Tento režim testu je dostupný vo WinPerf Sponsor Pro.",
            ["Time (sec)"] = "Čas (s)",
            ["Total bandwidth"] = "Celková priepustnosť",
            ["Transport tuning"] = "Ladenie prenosu",
            ["UDP bandwidth"] = "UDP priepustnosť",
            ["UDP upload:     -c <server> -p 5201 -4 -u -b 0 -t 10 -P 10 --json-stream"] = "UDP upload:     -c <server> -p 5201 -4 -u -b 0 -t 10 -P 10 --json-stream",
            ["UDP only"] = "Iba UDP",
            ["Update check failed"] = "Kontrola aktualizácie zlyhala",
            ["Update check failed or server response was invalid."] = "Kontrola aktualizácie zlyhala alebo odpoveď servera bola neplatná.",
            ["Update check failed. Check your connection and try again."] = "Kontrola aktualizácie zlyhala. Skontroluj pripojenie a skús znova.",
            ["Update check was cancelled."] = "Kontrola aktualizácie bola zrušená.",
            ["Update failed"] = "Aktualizácia zlyhala",
            ["Update found. Install button will be enabled after helper launcher/startup wiring."] = "Aktualizácia nájdená. Inštalácia sa zapne po dopojení pomocného spúšťača.",
            ["Update found. You can install it now."] = "Aktualizácia je dostupná. Môžeš ju nainštalovať.",
            ["Update installation failed and automatic rollback needs manual recovery from {0}."] = "Inštalácia aktualizácie zlyhala a automatický rollback potrebuje ručnú obnovu z {0}.",
            ["Update installation failed. WinPerf was rolled back to the previous version."] = "Inštalácia aktualizácie zlyhala. WinPerf bol vrátený na predchádzajúcu verziu.",
            ["Update installation failed. WinPerf.exe was not changed."] = "Inštalácia aktualizácie zlyhala. WinPerf.exe sa nezmenil.",
            ["Update installation was cancelled."] = "Inštalácia aktualizácie bola zrušená.",
            ["Update installed"] = "Aktualizácia nainštalovaná",
            ["Update recovery required"] = "Vyžaduje sa obnova aktualizácie",
            ["Use Command"] = "Použiť príkaz",
            ["Use --json-stream for live chart parsing"] = "Použiť --json-stream pre živý graf",
            ["Use this for advanced flags, public servers, server mode, UDP bitrate tuning, or manual troubleshooting."] = "Použi to pre pokročilé prepínače, verejné servery, serverový režim, ladenie UDP bitrate alebo ručné riešenie problémov.",
            ["Verbose output"] = "Podrobný výstup",
            ["Waiting for GitHub authorization..."] = "Čakám na GitHub autorizáciu...",
            ["Waiting for test..."] = "Čakám na test...",
            ["Waiting for throughput samples..."] = "Čakám na vzorky priepustnosti...",
            ["WinPerf is up to date on the Sponsor Pro channel."] = "WinPerf je na Sponsor Pro kanáli aktuálny.",
            ["WinPerf Free allows tests up to {0} seconds."] = "WinPerf Free umožňuje testy najviac na {0} sekúnd.",
            ["WinPerf Free allows up to {0} stream."] = "WinPerf Free umožňuje najviac {0} stream.",
            ["WinPerf Free includes iperf3 TCP upload/download, 1 stream and 10 second tests."] = "WinPerf Free obsahuje iperf3 TCP upload/download, 1 stream a 10-sekundové testy.",
            ["WinPerf is not enabled on the Sponsor Pro update server yet."] = "WinPerf ešte nie je povolený na Sponsor Pro aktualizačnom serveri.",
            ["WinPerf was updated successfully."] = "WinPerf bol úspešne aktualizovaný.",
            ["WinPerf will close, replace only WinPerf.exe, and restart. Portable data, tools, language packs, profiles and history stay in place."] = "WinPerf sa zatvorí, vymení iba WinPerf.exe a znova sa spustí. Prenosné dáta, nástroje, jazykové balíky, profily a história ostanú zachované.",
            ["Zero copy (-Z, OS-dependent)"] = "Zero copy (-Z, podľa OS)",
            ["{0} command override active"] = "Aktívny override príkazu: {0}",
            ["{0} ignored"] = "{0} ignorované",
            ["sent avg {0}"] = "odoslané avg {0}",
            ["1 saved result"] = "1 uložený výsledok",
            ["{0}s · {1} stream(s) · {2}"] = "{0}s · {1} stream(ov) · {2}",
            ["Download last {0} · min {1} · avg {2} · max {3}"] = "Download posledné {0} · min {1} · avg {2} · max {3}",
            ["Download last"] = "Download posledné",
            ["download {0}"] = "download {0}",
            ["Exit {0}"] = "Kód {0}",
            ["Failed to run {0}:"] = "Spustenie {0} zlyhalo:",
            ["History save failed: {0}"] = "Uloženie histórie zlyhalo: {0}",
            ["Ignoring warm-up samples. Live chart starts after warm-up."] = "Ignorujem warm-up vzorky. Živý graf začne po warm-upe.",
            ["Interval"] = "Interval",
            ["Interval {0}s"] = "Interval {0}s",
            ["Invalid test configuration:"] = "Neplatné nastavenie testu:",
            ["iperf3 error: {0}"] = "iperf3 chyba: {0}",
            ["iperf3 event received."] = "iperf3 udalosť prijatá.",
            ["iperf3 event: {0}"] = "iperf3 udalosť: {0}",
            ["jitter {0}"] = "jitter {0}",
            ["jitter {0} ms"] = "jitter {0} ms",
            ["Last {0} · min {1} · avg {2} · max {3}"] = "Posledné {0} · min {1} · avg {2} · max {3}",
            ["loss {0}"] = "straty {0}",
            ["loss {0} %"] = "straty {0} %",
            ["No throughput samples."] = "Žiadne vzorky priepustnosti.",
            ["Process exited with code {0}."] = "Proces skončil s kódom {0}.",
            ["Received {0}{1}"] = "Prijaté {0}{1}",
            ["Running command:"] = "Spúšťam príkaz:",
            ["Server result unavailable ({0}/{1} streams)."] = "Výsledok servera nie je dostupný ({0}/{1} streamov).",
            ["TCP Bidirectional"] = "TCP obojsmerne",
            ["TCP Download"] = "TCP download",
            ["TCP Upload"] = "TCP upload",
            ["Test completed"] = "Test dokončený",
            ["Test completed."] = "Test dokončený.",
            ["Test completed with warning."] = "Test dokončený s upozornením.",
            ["Test completed with warning: {0}"] = "Test dokončený s upozornením: {0}",
            ["Test failed."] = "Test zlyhal.",
            ["Test failed: {0}"] = "Test zlyhal: {0}",
            ["Test failed: incomplete iperf2 UDP server report ({0}/{1} streams)."] = "Test zlyhal: neúplný iperf2 UDP report servera ({0}/{1} streamov).",
            ["Test failed: process exited with code {0}."] = "Test zlyhal: proces skončil s kódom {0}.",
            ["Test failed: the iperf executable could not start because a required Windows DLL is missing. Re-import the portable engine from its full folder so WinPerf can copy the companion .dll files."] = "Test zlyhal: iperf sa nedal spustiť, pretože chýba potrebná Windows DLL. Importuj znovu celý prenosný engine priečinok, aby WinPerf skopíroval aj sprievodné .dll súbory.",
            ["Test started."] = "Test spustený.",
            ["Test stopped by user."] = "Test zastavil používateľ.",
            ["UDP Download"] = "UDP download",
            ["UDP Upload"] = "UDP upload",
            ["unknown error"] = "neznáma chyba",
            ["Upload last {0} · min {1} · avg {2} · max {3}"] = "Upload posledné {0} · min {1} · avg {2} · max {3}",
            ["Upload last"] = "Upload posledné",
            ["upload {0}"] = "upload {0}",
            ["Warm-up: omitting first {0}s before live metrics."] = "Warm-up: prvých {0}s ignorujem pred živými metrikami.",
            ["Warm-up: omitting first {0}s..."] = "Warm-up: ignorujem prvých {0}s...",
            ["Warm-up {0}/{1}s"] = "Warm-up {0}/{1}s",
            ["Warm-up {0}/{1}s omitted{2}"] = "Warm-up {0}/{1}s ignorované{2}",
            ["Warm-up {0}/{1}s omitted{2}."] = "Warm-up {0}/{1}s ignorované{2}.",
            ["Warm-up sample omitted{0}"] = "Warm-up vzorka ignorovaná{0}",
            ["WinPerf Settings"] = "Nastavenia WinPerf",
            ["WinPerfLanguage.Description"] = "Vyber jazyk aplikácie. Angličtina je vstavaná; ďalšie jazyky sa načítajú z priečinka lang vedľa WinPerf.exe.",
            ["WinPerfLanguage.EnglishDisplay"] = "English",
            ["WinPerfLanguage.SlovakDisplay"] = "Slovenčina",
            ["WinPerfLanguage.Status"] = "Jazykové balíky sa načítajú z prenosného priečinka lang.",
            ["0 Mbps"] = "0 Mbps",
            ["0.00 ms"] = "0.00 ms",
            ["Advanced builder..."] = "Pokročilý builder...",
            ["Awaiting server result"] = "Čakám na výsledok servera",
            ["Bandwidth / stream"] = "Priepustnosť / stream",
            ["Client, manifest validation and installer contracts loaded"] = "Klient, validácia manifestu a inštalačné kontrakty sú načítané",
            ["Command override active"] = "Aktívny vlastný príkaz",
            ["Command ▾"] = "Príkaz ▾",
            ["Confirm"] = "Potvrdenie",
            ["Confirm action"] = "Potvrdiť akciu",
            ["Continue?"] = "Pokračovať?",
            ["Download"] = "Download",
            ["Downloading and validating WinPerf update..."] = "Sťahujem a overujem aktualizáciu WinPerf...",
            ["Finished"] = "Dokončené",
            ["GitHub Sponsor Pro account, private update channel and WinPerf update package status."] = "GitHub Sponsor Pro účet, súkromný aktualizačný kanál a stav balíka WinPerf.",
            ["Install update"] = "Inštalovať aktualizáciu",
            ["Install WinPerf update?"] = "Nainštalovať aktualizáciu WinPerf?",
            ["Installer launcher/startup wiring is the next updater slice."] = "Spúšťanie inštalátora bude doplnené v ďalšom kroku updatera.",
            ["Installed"] = "Nainštalované",
            ["Invalid server configuration:"] = "Neplatné nastavenie servera:",
            ["Last Summary"] = "Posledný súhrn",
            ["Last sample {0}s"] = "Posledná vzorka {0}s",
            ["Last {0}"] = "Posledné {0}",
            ["Latest"] = "Najnovšie",
            ["Live Total Throughput"] = "Živá celková priepustnosť",
            ["Live total average"] = "Živý celkový priemer",
            ["No completed test yet."] = "Zatiaľ žiadny dokončený test.",
            ["One-off is iperf3 only. iperf2 runs until stopped."] = "Jednorazový režim má iba iperf3. iperf2 beží, kým ho nezastavíš.",
            ["Output"] = "Výstup",
            ["Per-stream: {0} streams · scale 0-{1}"] = "Na stream: {0} streamov · mierka 0-{1}",
            ["Per-stream: {0} streams · avg {1} · min {2} · max {3} · scale 0-{4}"] = "Na stream: {0} streamov · avg {1} · min {2} · max {3} · mierka 0-{4}",
            ["Portable single-EXE runtime"] = "Prenosná single-EXE aplikácia",
            ["Private Sponsor Pro updater"] = "Súkromný Sponsor Pro updater",
            ["Preparing Sponsor Pro update download..."] = "Pripravujem stiahnutie Sponsor Pro aktualizácie...",
            ["Product"] = "Produkt",
            ["Protocol"] = "Protokol",
            ["Receiving samples..."] = "Prijímam vzorky...",
            ["Run mode"] = "Režim spustenia",
            ["Enter target server address."] = "Zadaj cieľovú adresu servera.",
            ["Open app settings, updates and information"] = "Otvoriť nastavenia aplikácie, aktualizácie a informácie",
            ["Speed Test page will be added later."] = "Stránka Speed Test bude doplnená neskôr.",
            ["Enter server address or select one from recent servers."] = "Zadaj adresu servera alebo vyber jednu z posledných adries.",
            ["Warm-up seconds to ignore. Use 10–15 for routed, VLAN, VPN, or public server download tests."] = "Sekundy warm-upu, ktoré sa ignorujú. Pri routed, VLAN, VPN alebo verejnom download teste použi 10–15.",
            ["Target UDP bandwidth per stream. With 10 streams, 10M is about 100M total."] = "Cieľová UDP priepustnosť na stream. Pri 10 streamoch je 10M približne 100M spolu.",
            ["Application version from the running executable."] = "Verzia aplikácie zo spusteného súboru.",
            ["Selected engine integration status"] = "Stav vybranej integrácie enginu",
            ["Server command preview will appear here."] = "Náhľad serverového príkazu sa zobrazí tu.",
            ["Server received total"] = "Server prijal spolu",
            ["Server received total {0} · chart shows sent rate"] = "Server prijal spolu {0} · graf ukazuje odosielanú rýchlosť",
            ["Server result missing"] = "Chýba výsledok servera",
            ["Starting update helper. WinPerf will restart after installation."] = "Spúšťam pomocníka aktualizácie. WinPerf sa po inštalácii reštartuje.",
            ["Incomplete server report: {0}/{1} streams"] = "Neúplný report servera: {0}/{1} streamov",
            ["Sponsor Pro planned · Free edition will be reduced"] = "Sponsor Pro je plánované · Free edícia bude obmedzená",
            ["Start uses these arguments instead of dashboard fields."] = "Štart použije tieto argumenty namiesto polí v prehľade.",
            ["Update channel"] = "Aktualizačný kanál",
            ["Version"] = "Verzia",
            ["Waiting for samples..."] = "Čakám na vzorky...",
            ["iperf output will appear here."] = "Výstup iperf sa zobrazí tu.",
            ["iperf result"] = "Výsledok iperf",
            ["pending"] = "čakám",
            ["unavailable"] = "nedostupné",
            ["Engine  ●  {0}  ●  {1}  ●  {2}"] = "Engine  ●  {0}  ●  {1}  ●  {2}",
            ["Engine  ●  {0}  ●  {1}"] = "Engine  ●  {0}  ●  {1}",
            ["total {0}   min {1} · avg {2} · max {3}"] = "spolu {0}   min {1} · avg {2} · max {3}",
            ["↑ {0} · ↓ {1}   ↑ avg {2} · ↓ avg {3}"] = "↑ {0} · ↓ {1}   ↑ avg {2} · ↓ avg {3}",
            ["data folder"] = "priečinok dát",
            ["iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe"] = "iperf2 spustiteľný súbor · záloha tools\\iperf2\\iperf.exe alebo iperf2.exe",
            ["iperf3 executable · fallback tools\\iperf3\\iperf3.exe"] = "iperf3 spustiteľný súbor · záloha tools\\iperf3\\iperf3.exe",
            ["portable iperf2 engine folder"] = "priečinok prenosného iperf2 enginu",
            ["portable iperf3 engine folder"] = "priečinok prenosného iperf3 enginu",
        };
    }
}
