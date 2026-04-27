namespace WinPerf.Tests;

public sealed class UiDensityLayoutPersistenceTests
{
    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void Constructor_AppliesDensityWithoutOverridingRestoredWindowSize()
    {
        Assert.Contains("ApplyUiDensity(resizeWindow: false);", MainWindowSource);
        Assert.DoesNotContain("ApplyUiDensity(resizeWindow: true);", MainWindowSource);
    }

    [Fact]
    public void UiDensityToggle_DoesNotForceWindowBackToDensityDefaults()
    {
        Assert.Contains("Width = Math.Max(Width, MinWidth);", MainWindowSource);
        Assert.Contains("Height = Math.Max(Height, MinHeight);", MainWindowSource);
        Assert.DoesNotContain("Width = Math.Clamp(Width, MinWidth, isCompact ? 1080 : 1220);", MainWindowSource);
        Assert.DoesNotContain("Height = Math.Clamp(Height, MinHeight, isCompact ? 720 : 800);", MainWindowSource);
    }

    [Fact]
    public void UiDensity_PreservesSavedDashboardLayout()
    {
        Assert.Contains("GetSavedDashboardLeftRailWidth() is not double savedLeftRailWidth", MainWindowSource);
        Assert.Contains("Math.Clamp(savedLeftRailWidth, LeftRailColumn.MinWidth, LeftRailColumn.MaxWidth)", MainWindowSource);
        Assert.Contains("GetSavedDashboardEngineOutputHeight() is not double savedEngineOutputHeight", MainWindowSource);
        Assert.Contains("Math.Max(EngineOutputRow.MinHeight, savedEngineOutputHeight)", MainWindowSource);
    }
}
