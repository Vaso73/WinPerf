namespace WinPerf.Tests;

public sealed class WindowPlacementStoreTests
{
    private static readonly string Source = File.ReadAllText(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "WinPerf.App",
            "Settings",
            "WindowPlacementStore.cs"));

    [Fact]
    public void WindowPlacementStore_VersionsSavedLayoutDensity()
    {
        Assert.Contains("CurrentLayoutDensityVersion = 2", Source);
        Assert.Contains("LayoutDensityVersion = CurrentLayoutDensityVersion", Source);
        Assert.Contains("public int LayoutDensityVersion { get; set; }", Source);
    }

    [Fact]
    public void WindowPlacementStore_ClampsLegacyLayoutsToCompactDefaults()
    {
        Assert.Contains("var isCurrentDensityLayout = bounds.LayoutDensityVersion >= CurrentLayoutDensityVersion;", Source);
        Assert.Contains("ClampRestoredDimension(", Source);
        Assert.Contains("isCurrentDensityLayout", Source);
        Assert.Contains("? Math.Max(defaultMaximum, workAreaMaximum)", Source);
        Assert.Contains(": defaultMaximum", Source);
    }

    [Fact]
    public void WindowPlacementStore_ClampsSavedLayoutsToWorkArea()
    {
        Assert.Contains("ClampSavedDimension(width, window.MinWidth, SystemParameters.WorkArea.Width)", Source);
        Assert.Contains("ClampSavedDimension(height, window.MinHeight, SystemParameters.WorkArea.Height)", Source);
        Assert.Contains("Math.Clamp(value, lowerBound, upperBound)", Source);
    }
}
