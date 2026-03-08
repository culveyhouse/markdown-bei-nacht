using System.Text.Json;
using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<AppSettings> LoadAsync(string settingsPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return AppSettings.Default;
            }

            await using var stream = File.OpenRead(settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            return settings?.Normalize() ?? AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(string settingsPath, AppSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = settings.Normalize();
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = settingsPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, settingsPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
