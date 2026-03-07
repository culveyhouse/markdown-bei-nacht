namespace MarkdownBeiNacht.Core.Models;

public enum LinkTargetKind
{
    Anchor,
    External,
    LocalMarkdown,
    LocalFile,
    Unsupported,
}

public sealed record ResolvedLinkTarget(
    LinkTargetKind Kind,
    string OriginalHref,
    string? LocalPath = null,
    Uri? Uri = null,
    string? Anchor = null);

