namespace WinPerf.Core.Localization;

public sealed record LanguagePackDocument(
    LanguagePackInfo Info,
    IReadOnlyDictionary<string, string> Texts);
