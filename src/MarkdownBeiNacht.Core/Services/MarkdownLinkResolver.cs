using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public static class MarkdownLinkResolver
{
    public static ResolvedLinkTarget Resolve(string href, string? currentFilePath)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return new ResolvedLinkTarget(LinkTargetKind.Unsupported, href ?? string.Empty);
        }

        var trimmed = href.Trim();
        if (trimmed.StartsWith('#'))
        {
            return new ResolvedLinkTarget(
                LinkTargetKind.Anchor,
                trimmed,
                Anchor: Uri.UnescapeDataString(trimmed.TrimStart('#')));
        }

        if (TryResolveUri(trimmed, currentFilePath, out var resolvedUri))
        {
            if (!resolvedUri.IsFile)
            {
                return new ResolvedLinkTarget(LinkTargetKind.External, trimmed, Uri: resolvedUri);
            }

            var localPath = MarkdownPathUtilities.NormalizePath(resolvedUri.LocalPath.Split('#', '?')[0]);
            var anchor = ExtractAnchor(resolvedUri.Fragment)
                ?? ExtractAnchor(trimmed)
                ?? ExtractAnchor(resolvedUri.LocalPath);
            var isSupportedDocumentTarget = MarkdownPathUtilities.IsSupportedDocumentPath(localPath)
                || MarkdownPathUtilities.IsSupportedDocumentPath(trimmed)
                || MarkdownPathUtilities.IsSupportedDocumentPath(resolvedUri.AbsolutePath);

            return new ResolvedLinkTarget(
                isSupportedDocumentTarget ? LinkTargetKind.LocalDocument : LinkTargetKind.LocalFile,
                trimmed,
                localPath,
                resolvedUri,
                anchor);
        }

        return new ResolvedLinkTarget(LinkTargetKind.Unsupported, trimmed);
    }

    private static string? ExtractAnchor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var fragmentIndex = value.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? value[(fragmentIndex + 1)..] : value.TrimStart('#');
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        var queryIndex = fragment.IndexOf('?');
        var cleanFragment = queryIndex >= 0 ? fragment[..queryIndex] : fragment;
        return string.IsNullOrWhiteSpace(cleanFragment) ? null : Uri.UnescapeDataString(cleanFragment);
    }

    private static bool TryResolveUri(string href, string? currentFilePath, out Uri resolvedUri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri) && absoluteUri is not null)
        {
            resolvedUri = absoluteUri;
            return true;
        }

        var baseUri = MarkdownPathUtilities.CreateBaseUri(currentFilePath);
        if (baseUri is not null && Uri.TryCreate(baseUri, href, out var relativeUri) && relativeUri is not null)
        {
            resolvedUri = relativeUri;
            return true;
        }

        resolvedUri = new Uri("about:blank");
        return false;
    }
}
