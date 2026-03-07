using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WpfDataObject = System.Windows.IDataObject;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using MarkdownBeiNacht.Core.Models;
using MarkdownBeiNacht.Core.Services;
using MarkdownBeiNacht.Infrastructure;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkdownBeiNacht;

public partial class MainWindow : Window
{
    private const string AppDisplayName = "Markdown bei Nacht";
    private const string WebView2DownloadUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationPaths _paths;
    private readonly AppSettingsStore _settingsStore;
    private readonly MarkdownRenderer _renderer;
    private readonly FileTextLoader _fileTextLoader;
    private readonly ThemePaletteBuilder _themePaletteBuilder;
    private readonly StartupOptions _startupOptions;
    private readonly AsyncDebouncer _watcherDebouncer = new(TimeSpan.FromMilliseconds(280));
    private readonly SemaphoreSlim _renderLock = new(1, 1);
    private readonly TaskCompletionSource _webViewReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private AppSettings _settings = AppSettings.Default;
    private FileSystemWatcher? _fileWatcher;
    private string? _currentFilePath;
    private string? _pendingAnchor;
    private bool _isInitialized;

    public MainWindow(
        ApplicationPaths paths,
        AppSettingsStore settingsStore,
        MarkdownRenderer renderer,
        FileTextLoader fileTextLoader,
        ThemePaletteBuilder themePaletteBuilder,
        StartupOptions startupOptions)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _renderer = renderer;
        _fileTextLoader = fileTextLoader;
        _themePaletteBuilder = themePaletteBuilder;
        _startupOptions = startupOptions;

        InitializeComponent();
        ShowState(
            "Ready to Preview Markdown",
            "Open a Markdown file from File > Open, drag one into the window, or launch Markdown bei Nacht from Explorer using Open with.");
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        _settings = await _settingsStore.LoadAsync(_paths.SettingsFilePath);

        if (!await InitializeWebViewAsync())
        {
            Close();
            return;
        }

        if (_startupOptions.HasFile && !string.IsNullOrWhiteSpace(_startupOptions.FilePath))
        {
            await OpenFileAsync(_startupOptions.FilePath, _startupOptions.Anchor);
            return;
        }

        UpdateWindowTitle(null);
    }

    private void Window_OnClosed(object? sender, EventArgs e)
    {
        _watcherDebouncer.Dispose();
        DisposeFileWatcher();
        _renderLock.Dispose();
    }

    private async Task<bool> InitializeWebViewAsync()
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            var choice = System.Windows.MessageBox.Show(
                this,
                "Markdown bei Nacht needs the Microsoft Edge WebView2 Runtime. Open the official download page now?",
                AppDisplayName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (choice == MessageBoxResult.Yes)
            {
                OpenWithShell(WebView2DownloadUrl);
            }

            return false;
        }

        PreviewWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
        PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        PreviewWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PreviewWebView.CoreWebView2.WebMessageReceived += CoreWebView2_OnWebMessageReceived;
        PreviewWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_OnDOMContentLoaded;
        PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "appassets",
            _paths.AssetsDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        PreviewWebView.Source = new Uri("https://appassets/preview-shell.html");
        await _webViewReady.Task;
        return true;
    }

    private void CoreWebView2_OnDOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        _webViewReady.TrySetResult();
    }

    private async void CoreWebView2_OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
                        var message = JsonSerializer.Deserialize<LinkMessage>(e.TryGetWebMessageAsString(), JsonOptions);
            if (message is not { Type: "linkClick", Href: { Length: > 0 } hrefValue })
            {
                return;
            }

            await HandleLinkAsync(hrefValue);
        }
        catch (JsonException)
        {
        }
    }

    private async Task HandleLinkAsync(string href)
    {
        var target = MarkdownLinkResolver.Resolve(href, _currentFilePath);
        switch (target.Kind)
        {
            case LinkTargetKind.Anchor when !string.IsNullOrWhiteSpace(target.Anchor):
                await ScrollToAnchorAsync(target.Anchor);
                break;

            case LinkTargetKind.External when target.Uri is not null:
                OpenWithShell(target.Uri.ToString());
                break;

            case LinkTargetKind.LocalMarkdown when !string.IsNullOrWhiteSpace(target.LocalPath):
                LaunchNewMarkdownWindow(target);
                break;

            case LinkTargetKind.LocalFile when !string.IsNullOrWhiteSpace(target.LocalPath):
                OpenWithShell(target.LocalPath);
                break;
        }
    }

    private void LaunchNewMarkdownWindow(ResolvedLinkTarget target)
    {
        var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        var argument = target.Uri is not null ? target.Uri.AbsoluteUri : target.LocalPath!;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            Arguments = QuoteArgument(argument),
            UseShellExecute = true,
            WorkingDirectory = !string.IsNullOrWhiteSpace(target.LocalPath)
                ? Path.GetDirectoryName(target.LocalPath)
                : AppContext.BaseDirectory,
        };

        Process.Start(startInfo);
    }

    private static string QuoteArgument(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private async void OpenMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenFilePickerAsync();
    }

    private async void OpenFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenFilePickerAsync();
    }

    private async void ReloadMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await RenderCurrentFileAsync(true);
        }
    }

    private async void SettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings)
        {
            Owner = this,
        };

        if (window.ShowDialog() != true || window.SettingsResult is null)
        {
            return;
        }

        _settings = window.SettingsResult.Normalize();
        await _settingsStore.SaveAsync(_paths.SettingsFilePath, _settings);

        if (!string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await RenderCurrentFileAsync(true);
        }
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task OpenFilePickerAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Markdown files (*.md;*.markdown;*.mdown)|*.md;*.markdown;*.mdown|All files (*.*)|*.*",
            Title = AppDisplayName,
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await OpenFileAsync(dialog.FileName);
    }

    private async Task OpenFileAsync(string filePath, string? anchor = null)
    {
        _currentFilePath = MarkdownPathUtilities.NormalizePath(filePath);
        _pendingAnchor = anchor;
        ConfigureFileWatcher(_currentFilePath);
        UpdateWindowTitle(_currentFilePath);
        await RenderCurrentFileAsync(false);
    }

    private async Task RenderCurrentFileAsync(bool preserveScroll)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            UpdateWindowTitle(null);
            ShowState(
                "Ready to Preview Markdown",
                "Open a Markdown file from File > Open, drag one into the window, or launch Markdown bei Nacht from Explorer using Open with.");
            return;
        }

        await _renderLock.WaitAsync();
        try
        {
            var scrollRatio = preserveScroll ? await CaptureScrollRatioAsync() : null;
            var fileResult = await _fileTextLoader.ReadAsync(_currentFilePath);
            if (!fileResult.Success)
            {
                ShowFileError(fileResult);
                return;
            }

            var rendered = _renderer.Render(fileResult.Content ?? string.Empty, _currentFilePath, Path.GetFileNameWithoutExtension(_currentFilePath));
            await ApplyRenderAsync(rendered, scrollRatio, _pendingAnchor);
            _pendingAnchor = null;
            ShowPreview();
        }
        finally
        {
            _renderLock.Release();
        }
    }

    private async Task ApplyRenderAsync(MarkdownRenderResult result, double? scrollRatio, string? anchor)
    {
        await _webViewReady.Task;

        var payload = new PreviewPayload(
            result.Title,
            result.Html,
            _themePaletteBuilder.BuildCssVariables(_settings.BaseColor));
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        await PreviewWebView.ExecuteScriptAsync($"window.markdownViewer.applyContent({payloadJson});");

        if (scrollRatio is not null)
        {
            await RestoreScrollRatioAsync(scrollRatio.Value);
        }

        if (!string.IsNullOrWhiteSpace(anchor))
        {
            await ScrollToAnchorAsync(anchor);
        }
    }

    private async Task<double?> CaptureScrollRatioAsync()
    {
        if (PreviewWebView.Visibility != Visibility.Visible)
        {
            return null;
        }

        try
        {
            var scriptResult = await PreviewWebView.ExecuteScriptAsync("window.markdownViewer.captureScrollRatio();");
            return ParseDouble(scriptResult);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task RestoreScrollRatioAsync(double scrollRatio)
    {
        var script = FormattableString.Invariant(
            $"window.markdownViewer.restoreScrollRatio({Math.Clamp(scrollRatio, 0d, 1d):0.######});");
        await PreviewWebView.ExecuteScriptAsync(script);
    }

    private async Task ScrollToAnchorAsync(string anchor)
    {
        var anchorJson = JsonSerializer.Serialize(anchor, JsonOptions);
        await PreviewWebView.ExecuteScriptAsync($"window.markdownViewer.scrollToAnchor({anchorJson});");
    }

    private static double? ParseDouble(string scriptResult)
    {
        try
        {
            using var document = JsonDocument.Parse(scriptResult);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Number => document.RootElement.GetDouble(),
                JsonValueKind.String when double.TryParse(
                    document.RootElement.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ShowFileError(FileLoadResult result)
    {
        var title = result.FailureKind switch
        {
            FileLoadFailureKind.NotFound => "File Not Found",
            FileLoadFailureKind.InvalidEncoding => "Unsupported Text Encoding",
            _ => "Could Not Read File",
        };

        var message = result.FailureKind switch
        {
            FileLoadFailureKind.NotFound => $"Markdown bei Nacht could not find this file:\n{_currentFilePath}",
            FileLoadFailureKind.InvalidEncoding => result.Message ?? "The file is not valid UTF-8 or UTF-16 text.",
            _ => result.Message ?? "An unexpected file read error occurred.",
        };

        ShowState(title, message, "Open Different File");
    }

    private void ShowState(string title, string message, string buttonText = "Open File")
    {
        StateTitleText.Text = title;
        StateMessageText.Text = message;
        StateOpenButton.Content = buttonText;
        StatePanel.Visibility = Visibility.Visible;
        PreviewWebView.Visibility = Visibility.Collapsed;
    }

    private void ShowPreview()
    {
        StatePanel.Visibility = Visibility.Collapsed;
        PreviewWebView.Visibility = Visibility.Visible;
    }

    private void UpdateWindowTitle(string? filePath)
    {
        Title = string.IsNullOrWhiteSpace(filePath)
            ? AppDisplayName
            : $"{Path.GetFileName(filePath)} - {AppDisplayName}";
    }

    private void ConfigureFileWatcher(string path)
    {
        DisposeFileWatcher();

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        _fileWatcher = new FileSystemWatcher(directory)
        {
            Filter = Path.GetFileName(path),
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _fileWatcher.Changed += FileWatcher_OnChanged;
        _fileWatcher.Created += FileWatcher_OnChanged;
        _fileWatcher.Deleted += FileWatcher_OnChanged;
        _fileWatcher.Renamed += FileWatcher_OnRenamed;
    }

    private void DisposeFileWatcher()
    {
        if (_fileWatcher is null)
        {
            return;
        }

        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Changed -= FileWatcher_OnChanged;
        _fileWatcher.Created -= FileWatcher_OnChanged;
        _fileWatcher.Deleted -= FileWatcher_OnChanged;
        _fileWatcher.Renamed -= FileWatcher_OnRenamed;
        _fileWatcher.Dispose();
        _fileWatcher = null;
    }

    private void FileWatcher_OnChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleWatchedFileRefresh(null);
    }

    private void FileWatcher_OnRenamed(object sender, RenamedEventArgs e)
    {
        if (PathsEqual(e.OldFullPath, _currentFilePath))
        {
            ScheduleWatchedFileRefresh(e.FullPath);
            return;
        }

        if (PathsEqual(e.FullPath, _currentFilePath))
        {
            ScheduleWatchedFileRefresh(null);
        }
    }

    private void ScheduleWatchedFileRefresh(string? renamedPath)
    {
        _watcherDebouncer.Schedule(async cancellationToken =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (!string.IsNullOrWhiteSpace(renamedPath))
                {
                    _currentFilePath = MarkdownPathUtilities.NormalizePath(renamedPath);
                    ConfigureFileWatcher(_currentFilePath);
                    UpdateWindowTitle(_currentFilePath);
                }

                if (!string.IsNullOrWhiteSpace(_currentFilePath))
                {
                    await RenderCurrentFileAsync(true);
                }
            }, DispatcherPriority.Background, cancellationToken).Task.Unwrap();
        });
    }

    private static bool PathsEqual(string? left, string? right) =>
        string.Equals(
            NormalizeForComparison(left),
            NormalizeForComparison(right),
            StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeForComparison(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : MarkdownPathUtilities.NormalizePath(path);

    private void Window_OnPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            e.Handled = true;
            _ = OpenFilePickerAsync();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
        {
            e.Handled = true;
            SettingsMenuItem_OnClick(sender, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.F5)
        {
            e.Handled = true;
            ReloadMenuItem_OnClick(sender, new RoutedEventArgs());
        }
    }

    private void Window_OnPreviewDragOver(object sender, WpfDragEventArgs e)
    {
        var canOpen = TryGetDraggedMarkdownPath(e.Data, out _);
        e.Effects = canOpen ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
        DropOverlay.Visibility = canOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_OnPreviewDragLeave(object sender, WpfDragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Window_OnDrop(object sender, WpfDragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;

        if (!TryGetDraggedMarkdownPath(e.Data, out var path) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await OpenFileAsync(path);
    }

    private static bool TryGetDraggedMarkdownPath(WpfDataObject dataObject, out string? path)
    {
        path = null;
        if (!dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return false;
        }

        if (dataObject.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length != 1)
        {
            return false;
        }

        var candidate = files[0];
        if (!MarkdownPathUtilities.IsMarkdownPath(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static void OpenWithShell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private sealed record PreviewPayload(string Title, string Html, IReadOnlyDictionary<string, string> Theme);

    private sealed record LinkMessage(string Type, string Href);
}





