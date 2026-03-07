namespace MarkdownBeiNacht.Core.Models;

public sealed record FileLoadResult(string? Content, FileLoadFailureKind? FailureKind, string? Message)
{
    public bool Success => FailureKind is null;

    public static FileLoadResult FromContent(string content) => new(content, null, null);

    public static FileLoadResult Failure(FileLoadFailureKind kind, string message) => new(null, kind, message);
}

