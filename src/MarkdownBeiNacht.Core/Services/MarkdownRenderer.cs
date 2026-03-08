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
        foreach (var tag in new[] { "input", "figure", "figcaption" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        foreach (var attribute in new[] { "class", "id", "disabled", "checked", "type", "href", "src", "alt", "title", "rel" })
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
            var source = image.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            if (!TryResolveUri(source, baseUri, out var resolvedUri))
            {
                ReplaceNodeWithPlaceholder(document, image, "Image unavailable", source);
                continue;
            }

            if (string.Equals(resolvedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resolvedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                ReplaceNodeWithPlaceholder(document, image, "Remote image blocked", resolvedUri.AbsoluteUri);
                continue;
            }

            if (resolvedUri.IsFile)
            {
                var localPath = MarkdownPathUtilities.NormalizePath(resolvedUri.LocalPath.Split('#', '?')[0]);
                if (!File.Exists(localPath))
                {
                    ReplaceNodeWithPlaceholder(document, image, "Missing local image", localPath);
                    continue;
                }

                image.SetAttribute("src", resolvedUri.AbsoluteUri);
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

