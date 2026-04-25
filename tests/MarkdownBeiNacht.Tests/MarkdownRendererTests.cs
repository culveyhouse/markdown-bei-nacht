using MarkdownBeiNacht.Core.Services;

namespace MarkdownBeiNacht.Tests;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void Render_RendersCoreGithubFlavoredMarkdownFeatures()
    {
        var renderer = new MarkdownRenderer();
        var tempDirectory = CreateTempDirectory();
        try
        {
            var imagePath = Path.Combine(tempDirectory, "planet.png");
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });
            var markdownPath = Path.Combine(tempDirectory, "notes.md");
            var markdown = """
                # Demo Heading

                - [x] Task item

                | Name | Value |
                | ---- | ----- |
                | Pluto | 9 |

                ```csharp
                Console.WriteLine("hello");
                ```

                ![Local](planet.png)

                ![Remote](https://example.com/image.png)

                <video controls src="https://example.com/demo.mp4"></video>
                """;

            var result = renderer.Render(markdown, markdownPath);

            Assert.Contains("<h1 id=\"demo-heading\">Demo Heading</h1>", result.Html);
            Assert.Contains("type=\"checkbox\"", result.Html);
            Assert.Contains("<table>", result.Html);
            Assert.Contains("language-csharp", result.Html);
            Assert.Contains(new Uri(imagePath).AbsoluteUri, result.Html);
            Assert.Contains("https://example.com/image.png", result.Html);
            Assert.Contains("https://example.com/demo.mp4", result.Html);
            Assert.DoesNotContain("Remote image blocked", result.Html);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Render_ResolvesLocalDocumentLinksAndPreservesAnchors()
    {
        var renderer = new MarkdownRenderer();
        var tempDirectory = CreateTempDirectory();
        try
        {
            var markdownPath = Path.Combine(tempDirectory, "notes.md");
            var markdown = """
                [Sibling](docs/guide.txt#intro)

                [Jump](#section)

                ## Section
                """;

            var result = renderer.Render(markdown, markdownPath);

            Assert.Contains(new Uri(Path.Combine(tempDirectory, "docs", "guide.txt")).AbsoluteUri + "#intro", result.Html);
            Assert.Contains("href=\"#section\"", result.Html);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void RenderDocument_RendersPlainTextAsNormalParagraphs()
    {
        var renderer = new MarkdownRenderer();
        var textPath = Path.Combine(Path.GetTempPath(), "notes.txt");
        var text = """
            hello world
            this is a note

            second paragraph
            * not markdown formatting *
            """;

        var result = renderer.RenderDocument(text, textPath, "Notes");

        Assert.Equal("Notes", result.Title);
        Assert.Contains("<p>hello world<br />", result.Html);
        Assert.Contains("this is a note</p>", result.Html);
        Assert.Contains("<p>second paragraph<br />", result.Html);
        Assert.Contains("* not markdown formatting *</p>", result.Html);
        Assert.DoesNotContain("<em>", result.Html);
        Assert.DoesNotContain("<ul>", result.Html);
    }

    [Fact]
    public void Render_AllowsSafeMediaEmbedsButNotIframes()
    {
        var renderer = new MarkdownRenderer();
        var markdown = """
            <audio controls>
                <source src="https://example.com/clip.mp3" type="audio/mpeg">
            </audio>

            <video controls poster="https://example.com/poster.png">
                <source src="https://example.com/movie.mp4" type="video/mp4">
            </video>

            <iframe src="https://example.com/embed"></iframe>
            """;

        var result = renderer.Render(markdown, null);

        Assert.Contains("<audio", result.Html);
        Assert.Contains("https://example.com/clip.mp3", result.Html);
        Assert.Contains("<video", result.Html);
        Assert.Contains("https://example.com/poster.png", result.Html);
        Assert.Contains("https://example.com/movie.mp4", result.Html);
        Assert.DoesNotContain("<iframe", result.Html);
    }

    [Fact]
    public void Render_PreservesMermaidFencedCodeBlocksForPreviewRendering()
    {
        var renderer = new MarkdownRenderer();
        var markdown = """
            ```mermaid
            flowchart TD
                A[Open Markdown] --> B[Render Diagram]
            ```
            """;

        var result = renderer.Render(markdown, null);

        Assert.Contains("language-mermaid", result.Html);
        Assert.Contains("flowchart TD", result.Html);
        Assert.Contains("A[Open Markdown] --&gt; B[Render Diagram]", result.Html);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
