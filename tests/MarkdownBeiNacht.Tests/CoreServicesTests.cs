using System.Text;
using MarkdownBeiNacht.Core.Models;
using MarkdownBeiNacht.Core.Services;

namespace MarkdownBeiNacht.Tests;

public sealed class CoreServicesTests
{
    [Fact]
    public void CommandLineParser_ParsesFileUriAndAnchor()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "viewer.md");
        var builder = new UriBuilder(new Uri(tempPath))
        {
            Fragment = "intro",
        };

        var result = CommandLineParser.Parse([builder.Uri.AbsoluteUri]);

        Assert.Equal(Path.GetFullPath(tempPath), result.FilePath);
        Assert.Equal("intro", result.Anchor);
        Assert.False(result.HasWindowPlacement);
    }

    [Fact]
    public void CommandLineParser_ParsesWindowPlacementArguments()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "viewer.md");

        var result = CommandLineParser.Parse([
            tempPath,
            "--window-left=412.5",
            "--window-top=188.25",
        ]);

        Assert.Equal(Path.GetFullPath(tempPath), result.FilePath);
        Assert.Equal(412.5, result.WindowLeft);
        Assert.Equal(188.25, result.WindowTop);
        Assert.True(result.HasWindowPlacement);
    }

    [Fact]
    public void MarkdownLinkResolver_ResolvesRelativeMarkdownTarget()
    {
        var currentDirectory = CreateTempDirectory();
        try
        {
            var currentFile = Path.Combine(currentDirectory, "current.md");

            var result = MarkdownLinkResolver.Resolve("guides/next.md#deep-link", currentFile);

            Assert.Equal(LinkTargetKind.LocalMarkdown, result.Kind);
            Assert.Equal(Path.Combine(currentDirectory, "guides", "next.md"), result.LocalPath);
            Assert.Equal("deep-link", result.Anchor);
        }
        finally
        {
            Directory.Delete(currentDirectory, true);
        }
    }

    [Fact]
    public async Task AppSettingsStore_SavesAndLoadsNormalizedColor()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(tempDirectory, "settings.json");
            var store = new AppSettingsStore();

            await store.SaveAsync(settingsPath, new AppSettings("abc"));
            var loaded = await store.LoadAsync(settingsPath);

            Assert.Equal("#AABBCC", loaded.BaseColor);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task AppSettingsStore_SaveAsync_OverwritesExistingSettingsAndCleansTempFile()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(tempDirectory, "settings.json");
            var store = new AppSettingsStore();

            await store.SaveAsync(settingsPath, new AppSettings("#112233"));
            await store.SaveAsync(settingsPath, new AppSettings("#445566"));
            var loaded = await store.LoadAsync(settingsPath);

            Assert.Equal("#445566", loaded.BaseColor);
            Assert.False(File.Exists(settingsPath + ".tmp"));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task FileTextLoader_ReturnsInvalidEncodingForMalformedUtf8()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, "broken.md");
            await File.WriteAllBytesAsync(filePath, [0xC3, 0x28]);

            var result = await new FileTextLoader().ReadAsync(filePath);

            Assert.False(result.Success);
            Assert.Equal(FileLoadFailureKind.InvalidEncoding, result.FailureKind);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task AsyncDebouncer_OnlyRunsTheLastScheduledAction()
    {
        using var debouncer = new AsyncDebouncer(TimeSpan.FromMilliseconds(40));
        var runCount = 0;
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        debouncer.Schedule(_ =>
        {
            Interlocked.Increment(ref runCount);
            return Task.CompletedTask;
        });

        debouncer.Schedule(_ =>
        {
            Interlocked.Increment(ref runCount);
            signal.TrySetResult();
            return Task.CompletedTask;
        });

        await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, runCount);
    }

    [Fact]
    public void ThemePaletteBuilder_ReturnsExpectedThemeKeys()
    {
        var palette = new ThemePaletteBuilder().BuildCssVariables("#112233");

        Assert.Equal("#112233", palette["--color-bg"]);
        Assert.Contains("--color-link", palette.Keys);
        Assert.Contains("rgba", palette["--color-selection"]);
    }

    [Fact]
    public void FileTextLoader_Decode_HandlesUtf16WithBom()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("hello")).ToArray();

        var text = FileTextLoader.Decode(bytes);

        Assert.Equal("hello", text);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void WindowOpenPolicy_ReusesCurrentWindowOnlyUntilDocumentLoads(bool hasLoadedDocument, bool expected)
    {
        Assert.Equal(expected, WindowOpenPolicy.ShouldReuseCurrentWindow(hasLoadedDocument));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
