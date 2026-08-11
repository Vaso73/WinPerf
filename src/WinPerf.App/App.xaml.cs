using System.Windows;
using WinPerf.App.Updates;
using WinPerf.Core.Updates;

namespace WinPerf.App;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        if (WinPerfUpdateHelper.IsApplyRequest(e.Args))
        {
            Shutdown(WinPerfUpdateHelper.RunApply(e.Args));
            return;
        }

        WinPerfUpdateHelper.ScheduleCleanup(e.Args);
        new MainWindow().Show();
    }
}
