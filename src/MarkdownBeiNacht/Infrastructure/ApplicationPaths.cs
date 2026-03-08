using System.IO;

namespace MarkdownBeiNacht.Infrastructure;

public sealed class ApplicationPaths
{
    private const string AppFolderName = "Markdown bei Nacht";

    public ApplicationPaths()
    {
        AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);
        SettingsFilePath = Path.Combine(AppDataDirectory, "settings.json");
        WindowPlacementStateFilePath = Path.Combine(AppDataDirectory, "window-placement.json");
        WebViewUserDataDirectory = Path.Combine(AppDataDirectory, "WebView2");
        AssetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        UserGuideFilePath = Path.Combine(AppContext.BaseDirectory, "README.md");
    }

    public string AppDataDirectory { get; }

    public string SettingsFilePath { get; }

    public string WindowPlacementStateFilePath { get; }

    public string WebViewUserDataDirectory { get; }

    public string AssetsDirectory { get; }

    public string UserGuideFilePath { get; }
}
