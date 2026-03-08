using System.Text.Json;
using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public sealed class RecentFilesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<RecentFilesState> LoadAsync(string statePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(statePath))
            {
                return RecentFilesState.Empty;
            }

            await using var stream = File.OpenRead(statePath);
            var state = await JsonSerializer.DeserializeAsync<RecentFilesState>(stream, SerializerOptions, cancellationToken);
            return state?.Normalize() ?? RecentFilesState.Empty;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return RecentFilesState.Empty;
        }
    }

    public async Task<RecentFilesState> RememberAsync(string statePath, string filePath, CancellationToken cancellationToken = default)
    {
        if (MarkdownPathUtilities.IsMarkdownPath(filePath) is false)
        {
            return await LoadAsync(statePath, cancellationToken);
        }

        var normalizedPath = MarkdownPathUtilities.NormalizePath(filePath);
        var currentState = await LoadAsync(statePath, cancellationToken);
        var updatedFiles = currentState.Files
            .Where(path => string.Equals(path, normalizedPath, StringComparison.OrdinalIgnoreCase) is false)
            .Prepend(normalizedPath)
            .Take(RecentFilesState.MaxEntries)
            .ToArray();
        var updatedState = new RecentFilesState(updatedFiles).Normalize();

        await SaveAsync(statePath, updatedState, cancellationToken);
        return updatedState;
    }

    public Task ClearAsync(string statePath, CancellationToken cancellationToken = default)
    {
        return SaveAsync(statePath, RecentFilesState.Empty, cancellationToken);
    }

    public async Task SaveAsync(string statePath, RecentFilesState state, CancellationToken cancellationToken = default)
    {
        var normalized = state.Normalize();
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = statePath + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, statePath, true);
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
