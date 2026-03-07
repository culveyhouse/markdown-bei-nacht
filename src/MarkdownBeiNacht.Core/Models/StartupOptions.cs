namespace MarkdownBeiNacht.Core.Models;

public sealed record StartupOptions(string? FilePath, string? Anchor)
{
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
}

