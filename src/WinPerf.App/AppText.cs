using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WinPerf.Core.Localization;

namespace WinPerf.App;

public static class AppText
{
    private static readonly DependencyProperty OriginalTextKeyProperty =
        DependencyProperty.RegisterAttached(
            "OriginalTextKey",
            typeof(string),
            typeof(AppText),
            new PropertyMetadata(null));

    private static readonly LanguagePackService Service = new();
    private static string _baseDirectory = AppContext.BaseDirectory;

    public static LanguagePackInfo CurrentLanguage => Service.CurrentLanguage;

    public static IReadOnlyList<LanguagePackInfo> AvailableLanguages =>
        Service.GetAvailableLanguages(_baseDirectory);

    public static void Initialize(string baseDirectory, string? languageCode)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;

        Service.EnsureSeedLanguagePacks(_baseDirectory);
        Service.UseLanguage(_baseDirectory, languageCode);
    }

    public static void UseLanguage(string? languageCode)
    {
        Service.EnsureSeedLanguagePacks(_baseDirectory);
        Service.UseLanguage(_baseDirectory, languageCode);
    }

    public static string T(string key)
    {
        return Service.Text(key);
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Service.Text(key), args);
    }

    public static void ApplyTo(Window window)
    {
        var visited = new HashSet<DependencyObject>();
        foreach (var child in EnumerateChildren(window, visited))
        {
            ApplyToObject(child);
        }
    }

    public static string GetLanguageDisplayName(LanguagePackInfo language)
    {
        return string.Equals(language.LanguageCode, LanguagePackService.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? T("WinPerfLanguage.EnglishDisplay")
            : language.NativeName;
    }

    private static void ApplyToObject(DependencyObject item)
    {
        switch (item)
        {
            case Window window:
                ApplyStringProperty(window, Window.TitleProperty);
                break;
            case TextBlock textBlock:
                if (BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty) is null)
                {
                    ApplyStringProperty(textBlock, TextBlock.TextProperty);
                }

                break;
            case Run run:
                ApplyStringProperty(run, Run.TextProperty);
                break;
            case ComboBoxItem:
                break;
            case HeaderedItemsControl headered:
                if (headered.Header is string header)
                {
                    headered.Header = TranslateWithOriginalKey(headered, header);
                }

                break;
            case ContentControl contentControl:
                if (contentControl.Content is string content)
                {
                    contentControl.Content = TranslateWithOriginalKey(contentControl, content);
                }

                break;
        }

        if (item is FrameworkElement element &&
            element.ToolTip is string toolTip)
        {
            element.ToolTip = TranslateWithOriginalKey(element, toolTip);
        }

    }

    private static void ApplyStringProperty(DependencyObject target, DependencyProperty property)
    {
        var value = target.GetValue(property) as string;
        if (value is null)
        {
            return;
        }

        target.SetValue(property, TranslateWithOriginalKey(target, value));
    }

    private static string TranslateWithOriginalKey(DependencyObject target, string currentValue)
    {
        var originalKey = target.GetValue(OriginalTextKeyProperty) as string;
        if (string.IsNullOrEmpty(originalKey))
        {
            originalKey = currentValue;
            target.SetValue(OriginalTextKeyProperty, originalKey);
        }

        return T(originalKey);
    }

    private static IEnumerable<DependencyObject> EnumerateChildren(
        DependencyObject root,
        ISet<DependencyObject> visited)
    {
        if (!visited.Add(root))
        {
            yield break;
        }

        yield return root;

        if (root is Visual or Visual3D)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                foreach (var nested in EnumerateChildren(child, visited))
                {
                    yield return nested;
                }
            }
        }

        foreach (var logicalChild in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in EnumerateChildren(logicalChild, visited))
            {
                yield return nested;
            }
        }

        if (root is FrameworkElement { ContextMenu: not null } elementWithMenu)
        {
            foreach (var nested in EnumerateChildren(elementWithMenu.ContextMenu, visited))
            {
                yield return nested;
            }
        }
    }
}
