namespace MarkdownBeiNacht.Core.Models;

public sealed record StartupOptions(string? FilePath, string? Anchor, double? WindowLeft, double? WindowTop)
{
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
    public bool HasWindowPlacement => WindowLeft is not null && WindowTop is not null;
}
