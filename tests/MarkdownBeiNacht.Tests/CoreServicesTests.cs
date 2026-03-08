using System.Text;
using System.Text.Json;
using MarkdownBeiNacht.Core.Models;
using MarkdownBeiNacht.Core.Services;
using MarkdownBeiNacht.Infrastructure;

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
    public void WindowPlacementPlanner_CascadesWithinBounds()
    {
        var result = WindowPlacementPlanner.Cascade(
            new WindowPlacement(100, 120, 1280, 900),
            28,
            0,
            0,
            1920,
            1080);

        Assert.Equal(128, result.Left);
        Assert.Equal(148, result.Top);
    }

    [Fact]
    public void WindowPlacementPlanner_ClampsCascadeNearScreenEdge()
    {
        var result = WindowPlacementPlanner.Cascade(
            new WindowPlacement(900, 400, 1280, 900),
            28,
            0,
            0,
            1920,
            1080);

        Assert.Equal(640, result.Left);
        Assert.Equal(180, result.Top);
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

    [Fact]
    public void ApplicationPaths_UsesInstalledGuideAndLocalAppDataLocations()
    {
        var paths = new ApplicationPaths();

        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), paths.AppDataDirectory);
        Assert.Equal(Path.Combine(paths.AppDataDirectory, "settings.json"), paths.SettingsFilePath);
        Assert.Equal(Path.Combine(paths.AppDataDirectory, "recent-files.json"), paths.RecentFilesStateFilePath);
        Assert.Equal(Path.Combine(paths.AppDataDirectory, "window-placement.json"), paths.WindowPlacementStateFilePath);
        Assert.Equal(Path.Combine(paths.AppDataDirectory, "WebView2"), paths.WebViewUserDataDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Assets"), paths.AssetsDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "README.md"), paths.UserGuideFilePath);
    }

    [Fact]
    public void WindowPlacementCoordinator_ReturnsNullWithoutExplicitPlacementOrPriorInstance()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var statePath = Path.Combine(tempDirectory, "window-placement.json");
            var coordinator = new WindowPlacementCoordinator(statePath);

            var result = coordinator.ResolveStartupPlacement(
                new StartupOptions(null, null, null, null),
                1280,
                900,
                28,
                0,
                0,
                1920,
                1080);

            Assert.Null(result);
            Assert.False(File.Exists(statePath));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void WindowPlacementCoordinator_ClampsAndPersistsExplicitPlacement()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var statePath = Path.Combine(tempDirectory, "window-placement.json");
            var coordinator = new WindowPlacementCoordinator(statePath);

            var result = coordinator.ResolveStartupPlacement(
                new StartupOptions(null, null, 5000, -100),
                1280,
                900,
                28,
                0,
                0,
                1920,
                1080);

            Assert.NotNull(result);
            Assert.Equal(640, result!.Left);
            Assert.Equal(0, result.Top);
            Assert.False(File.Exists(statePath + ".tmp"));

            var persisted = JsonSerializer.Deserialize<WindowPlacement>(
                File.ReadAllText(statePath),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Equal(result, persisted);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task RecentFilesStore_RememberAsync_DeduplicatesMovesToTopAndTrims()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var statePath = Path.Combine(tempDirectory, "recent-files.json");
            var store = new RecentFilesStore();

            var expectedPaths = Enumerable.Range(1, RecentFilesState.MaxEntries + 1)
                .Select(index => Path.Combine(tempDirectory, $"note-{index}.md"))
                .ToArray();

            foreach (var path in expectedPaths)
            {
                await File.WriteAllTextAsync(path, "# Note");
                await store.RememberAsync(statePath, path);
            }

            var updated = await store.RememberAsync(statePath, expectedPaths[2]);

            Assert.Equal(RecentFilesState.MaxEntries, updated.Files.Length);
            Assert.Equal(expectedPaths[2], updated.Files[0]);
            Assert.Equal(1, updated.Files.Count(path => string.Equals(path, expectedPaths[2], StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain(expectedPaths[0], updated.Files);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task RecentFilesStore_IgnoresNonMarkdownAndClearsState()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var statePath = Path.Combine(tempDirectory, "recent-files.json");
            var store = new RecentFilesStore();
            var markdownPath = Path.Combine(tempDirectory, "guide.md");
            var textPath = Path.Combine(tempDirectory, "notes.txt");

            await File.WriteAllTextAsync(markdownPath, "# Guide");
            await File.WriteAllTextAsync(textPath, "plain text");

            await store.RememberAsync(statePath, markdownPath);
            var ignored = await store.RememberAsync(statePath, textPath);

            Assert.Single(ignored.Files);
            Assert.Equal(markdownPath, ignored.Files[0]);

            await store.ClearAsync(statePath);
            var cleared = await store.LoadAsync(statePath);

            Assert.Empty(cleared.Files);
            Assert.False(File.Exists(statePath + ".tmp"));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task RecentFilesStore_SaveAndLoad_RoundTripsNormalizedState()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var statePath = Path.Combine(tempDirectory, "recent-files.json");
            var markdownPath = Path.Combine(tempDirectory, ".", "docs", "guide.md");
            Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
            await File.WriteAllTextAsync(markdownPath, "# Guide");

            var store = new RecentFilesStore();
            await store.SaveAsync(statePath, new RecentFilesState([markdownPath, markdownPath, Path.Combine(tempDirectory, "skip.txt")]));
            var loaded = await store.LoadAsync(statePath);

            Assert.Single(loaded.Files);
            Assert.Equal(Path.GetFullPath(markdownPath), loaded.Files[0]);
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
