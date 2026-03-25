using System.Windows;

namespace ArchiveViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load config to get theme preference, then apply
        var config = Services.ConfigService.Load();
        var themeName = string.IsNullOrEmpty(config.State.Theme) ? Theme.DefaultTheme : config.State.Theme;
        Theme.ApplyTheme(themeName);
    }
}
