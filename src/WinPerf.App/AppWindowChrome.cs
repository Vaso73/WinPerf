using System.Windows;
using System.Windows.Shell;

namespace WinPerf.App;

public sealed class AppWindowChrome : WindowChrome
{
    public AppWindowChrome()
    {
        CaptionHeight = 38;
        ResizeBorderThickness = new Thickness(6);
        GlassFrameThickness = new Thickness(0);
        CornerRadius = new CornerRadius(12);
    }
}
