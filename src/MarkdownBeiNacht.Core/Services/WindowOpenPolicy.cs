namespace MarkdownBeiNacht.Core.Services;

public static class WindowOpenPolicy
{
    public static bool ShouldReuseCurrentWindow(bool hasLoadedDocument) =>
        hasLoadedDocument is false;
}
