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
                """;

            var result = renderer.Render(markdown, markdownPath);

            Assert.Contains("<h1 id=\"demo-heading\">Demo Heading</h1>", result.Html);
            Assert.Contains("type=\"checkbox\"", result.Html);
            Assert.Contains("<table>", result.Html);
            Assert.Contains("language-csharp", result.Html);
            Assert.Contains(new Uri(imagePath).AbsoluteUri, result.Html);
            Assert.Contains("Remote image blocked", result.Html);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Render_ResolvesLocalMarkdownLinksAndPreservesAnchors()
    {
        var renderer = new MarkdownRenderer();
        var tempDirectory = CreateTempDirectory();
        try
        {
            var markdownPath = Path.Combine(tempDirectory, "notes.md");
            var markdown = """
                [Sibling](docs/guide.md#intro)

                [Jump](#section)

                ## Section
                """;

            var result = renderer.Render(markdown, markdownPath);

            Assert.Contains(new Uri(Path.Combine(tempDirectory, "docs", "guide.md")).AbsoluteUri + "#intro", result.Html);
            Assert.Contains("href=\"#section\"", result.Html);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

