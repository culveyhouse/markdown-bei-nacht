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
        AssetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
    }

    public string AppDataDirectory { get; }

    public string SettingsFilePath { get; }

    public string AssetsDirectory { get; }
}


