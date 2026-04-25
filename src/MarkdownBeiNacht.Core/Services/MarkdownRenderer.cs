using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using MarkdownBeiNacht.Core.Models;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace MarkdownBeiNacht.Core.Services;

public sealed class MarkdownRenderer
{
    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();
    private readonly MarkdownPipeline _pipeline = CreatePipeline();
    private readonly HtmlParser _parser = new();

    public MarkdownRenderResult Render(string markdown, string? sourceFilePath, string? fallbackTitle = null)
    {
        var content = markdown ?? string.Empty;
        var baseUri = MarkdownPathUtilities.CreateBaseUri(sourceFilePath);
        var rawHtml = Markdown.ToHtml(content, _pipeline);
        var sanitizedHtml = baseUri is null
            ? _sanitizer.Sanitize(rawHtml)
            : _sanitizer.Sanitize(rawHtml, baseUri.AbsoluteUri);

        var processedHtml = PostProcessHtml(sanitizedHtml, baseUri);
        var title = ExtractMarkdownTitle(content, sourceFilePath, fallbackTitle);

        return new MarkdownRenderResult(title, processedHtml);
    }

    public MarkdownRenderResult RenderDocument(string content, string? sourceFilePath, string? fallbackTitle = null)
    {
        if (MarkdownPathUtilities.IsPlainTextPath(sourceFilePath))
        {
            return RenderPlainText(content, sourceFilePath, fallbackTitle);
        }

        return Render(content, sourceFilePath, fallbackTitle);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        foreach (var tag in new[] { "input", "figure", "figcaption", "audio", "video", "source", "track" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        foreach (var attribute in new[] { "class", "id", "disabled", "checked", "type", "href", "src", "alt", "title", "rel", "controls", "poster", "preload", "width", "height", "kind", "srclang", "label" })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        foreach (var scheme in new[] { "file", "http", "https", "mailto" })
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }

    private static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UseAutoLinks()
            .UseFootnotes()
            .UsePipeTables()
            .UseTaskLists()
            .Build();
    }

    private static string ExtractMarkdownTitle(string markdown, string? sourceFilePath, string? fallbackTitle)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("# "))
            {
                continue;
            }

            var title = trimmed[2..].Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        return ResolveDocumentTitle(sourceFilePath, fallbackTitle);
    }

    private static string ResolveDocumentTitle(string? sourceFilePath, string? fallbackTitle)
    {
        if (!string.IsNullOrWhiteSpace(fallbackTitle))
        {
            return fallbackTitle;
        }

        if (!string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return Path.GetFileNameWithoutExtension(sourceFilePath);
        }

        return "Markdown bei Nacht";
    }

    private MarkdownRenderResult RenderPlainText(string content, string? sourceFilePath, string? fallbackTitle)
    {
        var normalizedContent = (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = Regex.Split(normalizedContent.Trim(), "\\n\\s*\\n")
            .Where(paragraph => string.IsNullOrWhiteSpace(paragraph) is false)
            .Select(RenderPlainTextParagraph);
        var html = string.Join(Environment.NewLine, paragraphs);
        var title = ResolveDocumentTitle(sourceFilePath, fallbackTitle);

        return new MarkdownRenderResult(title, html);
    }

    private static string RenderPlainTextParagraph(string paragraph)
    {
        var encoded = WebUtility.HtmlEncode(paragraph.Trim());
        encoded = encoded.Replace("\n", "<br />" + Environment.NewLine);
        return $"<p>{encoded}</p>";
    }

    private string PostProcessHtml(string html, Uri? baseUri)
    {
        var document = _parser.ParseDocument($"<body>{html}</body>");

        foreach (var image in document.Images.ToArray())
        {
            NormalizeResourceAttribute(document, image, "src", baseUri, "Image unavailable", "Missing local image", replaceNodeOnFailure: true);
        }

        foreach (var media in document.QuerySelectorAll("audio, video").ToArray())
        {
            NormalizeResourceAttribute(document, media, "src", baseUri, "Media unavailable", "Missing local media", replaceNodeOnFailure: true);
            NormalizeResourceAttribute(document, media, "poster", baseUri, "Poster unavailable", "Missing local poster", replaceNodeOnFailure: false);
        }

        foreach (var source in document.QuerySelectorAll("source, track").ToArray())
        {
            if (!NormalizeResourceAttribute(document, source, "src", baseUri, "Media source unavailable", "Missing local media source", replaceNodeOnFailure: false))
            {
                RemoveNode(source);
            }
        }

        foreach (var anchor in document.QuerySelectorAll("a[href]").OfType<IHtmlAnchorElement>())
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!TryResolveUri(href, baseUri, out var resolvedUri))
            {
                continue;
            }

            if (resolvedUri.IsFile)
            {
                var currentLocalPath = baseUri is null ? null : MarkdownPathUtilities.NormalizePath(baseUri.LocalPath.Split('#', '?')[0]);
                var targetLocalPath = MarkdownPathUtilities.NormalizePath(resolvedUri.LocalPath.Split('#', '?')[0]);
                if (!string.IsNullOrWhiteSpace(currentLocalPath) &&
                    string.Equals(currentLocalPath, targetLocalPath, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(resolvedUri.Fragment))
                {
                    anchor.SetAttribute("href", resolvedUri.Fragment);
                }
                else
                {
                    anchor.SetAttribute("href", resolvedUri.AbsoluteUri);
                }
            }
            else
            {
                anchor.SetAttribute("href", resolvedUri.ToString());
            }

            anchor.SetAttribute("rel", "noreferrer");
        }

        return document.Body?.InnerHtml ?? string.Empty;
    }

    private static bool NormalizeResourceAttribute(
        IHtmlDocument document,
        IElement element,
        string attributeName,
        Uri? baseUri,
        string unavailableTitle,
        string missingLocalTitle,
        bool replaceNodeOnFailure)
    {
        var source = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        if (!TryResolveUri(source, baseUri, out var resolvedUri) || !IsAllowedPreviewResourceUri(resolvedUri))
        {
            HandleInvalidResource(document, element, attributeName, unavailableTitle, source, replaceNodeOnFailure);
            return false;
        }

        if (resolvedUri.IsFile)
        {
            var localPath = MarkdownPathUtilities.NormalizePath(resolvedUri.LocalPath.Split('#', '?')[0]);
            if (!File.Exists(localPath))
            {
                HandleInvalidResource(document, element, attributeName, missingLocalTitle, localPath, replaceNodeOnFailure);
                return false;
            }
        }

        element.SetAttribute(attributeName, resolvedUri.AbsoluteUri);
        return true;
    }

    private static void HandleInvalidResource(
        IHtmlDocument document,
        IElement element,
        string attributeName,
        string title,
        string detail,
        bool replaceNodeOnFailure)
    {
        if (replaceNodeOnFailure)
        {
            ReplaceNodeWithPlaceholder(document, element, title, detail);
            return;
        }

        element.RemoveAttribute(attributeName);
    }

    private static bool IsAllowedPreviewResourceUri(Uri uri) =>
        uri.IsFile ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);

    private static void ReplaceNodeWithPlaceholder(IHtmlDocument document, IElement node, string title, string detail)
    {
        var figure = document.CreateElement("figure");
        figure.SetAttribute("class", "asset-placeholder");

        var heading = document.CreateElement("strong");
        heading.TextContent = title;
        figure.AppendChild(heading);

        var caption = document.CreateElement("figcaption");
        caption.TextContent = detail;
        figure.AppendChild(caption);

        node.Parent?.ReplaceChild(figure, node);
    }

    private static void RemoveNode(IElement node)
    {
        node.Parent?.RemoveChild(node);
    }

    private static bool TryResolveUri(string href, Uri? baseUri, out Uri resolvedUri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri) && absoluteUri is not null)
        {
            resolvedUri = absoluteUri;
            return true;
        }

        if (baseUri is not null && Uri.TryCreate(baseUri, href, out var relativeUri) && relativeUri is not null)
        {
            resolvedUri = relativeUri;
            return true;
        }

        resolvedUri = new Uri("about:blank");
        return false;
    }
}

