using System.Windows;
using MarkdownBeiNacht.Core.Services;
using MarkdownBeiNacht.Infrastructure;

namespace MarkdownBeiNacht;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var paths = new ApplicationPaths();
        var mainWindow = new MainWindow(
            paths,
            new AppSettingsStore(),
            new RecentFilesStore(),
            new MarkdownRenderer(),
            new FileTextLoader(),
            new ThemePaletteBuilder(),
            CommandLineParser.Parse(e.Args));

        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
