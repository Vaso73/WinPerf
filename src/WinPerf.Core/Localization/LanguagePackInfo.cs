namespace WinPerf.Core.Localization;

public sealed record LanguagePackInfo(
    string LanguageCode,
    string LanguageName,
    string NativeName,
    string Direction,
    bool IsBuiltIn,
    string? FilePath);
