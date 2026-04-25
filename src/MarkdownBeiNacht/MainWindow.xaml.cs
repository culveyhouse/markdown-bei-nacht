using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private const string ReadyStateTitle = "Ready to Preview Documents";
    private const string ReadyStateMessage = "Open a Markdown or .txt file from File > Open, drag one into the window, or launch Markdown files from Explorer using Open with. If this window already has a file open, another document opens in a new window.";
    private const string WebView2DownloadUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/";
    private const double CascadedWindowOffset = 28d;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationPaths _paths;
    private readonly AppSettingsStore _settingsStore;
    private readonly RecentFilesStore _recentFilesStore;
    private readonly MarkdownRenderer _renderer;
    private readonly FileTextLoader _fileTextLoader;
    private readonly ThemePaletteBuilder _themePaletteBuilder;
    private readonly StartupOptions _startupOptions;
    private readonly WindowPlacementCoordinator _windowPlacementCoordinator;
    private readonly AsyncDebouncer _watcherDebouncer = new(TimeSpan.FromMilliseconds(280));
    private readonly AsyncDebouncer _windowPlacementDebouncer = new(TimeSpan.FromMilliseconds(140));
    private readonly SemaphoreSlim _renderLock = new(1, 1);
    private readonly TaskCompletionSource _webViewReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private AppSettings _settings = AppSettings.Default;
    private RecentFilesState _recentFiles = RecentFilesState.Empty;
    private FileSystemWatcher? _fileWatcher;
    private string? _currentFilePath;
    private string? _pendingAnchor;
    private bool _isInitialized;
    private bool _hasLoadedDocument;

    public MainWindow(
        ApplicationPaths paths,
        AppSettingsStore settingsStore,
        RecentFilesStore recentFilesStore,
        MarkdownRenderer renderer,
        FileTextLoader fileTextLoader,
        ThemePaletteBuilder themePaletteBuilder,
        StartupOptions startupOptions)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _recentFilesStore = recentFilesStore;
        _renderer = renderer;
        _fileTextLoader = fileTextLoader;
        _themePaletteBuilder = themePaletteBuilder;
        _startupOptions = startupOptions;
        _windowPlacementCoordinator = new WindowPlacementCoordinator(paths.WindowPlacementStateFilePath);

        InitializeComponent();
        HookWindowPlacementTracking();
        ApplyStartupWindowPlacement();
        ApplyShellTheme();
        RefreshRecentFilesMenu();
        ShowReadyState();
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        _settings = await _settingsStore.LoadAsync(_paths.SettingsFilePath);
        _recentFiles = await _recentFilesStore.LoadAsync(_paths.RecentFilesStateFilePath);
        ApplyShellTheme();
        RefreshRecentFilesMenu();

        if (await InitializeWebViewAsync() is false)
        {
            Close();
            return;
        }

        if (_startupOptions.HasFile && string.IsNullOrWhiteSpace(_startupOptions.FilePath) is false)
        {
            await OpenFileAsync(_startupOptions.FilePath, _startupOptions.Anchor);
            RememberCurrentWindowPlacement();
            return;
        }

        UpdateWindowTitle(null);
        RememberCurrentWindowPlacement();
    }

    private void Window_OnClosed(object? sender, EventArgs e)
    {
        RememberCurrentWindowPlacement();
        _windowPlacementDebouncer.Dispose();
        _watcherDebouncer.Dispose();
        DisposeFileWatcher();
        _renderLock.Dispose();
    }

    private async Task<bool> InitializeWebViewAsync()
    {
        try
        {
            Directory.CreateDirectory(_paths.WebViewUserDataDirectory);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _paths.WebViewUserDataDirectory);
            await PreviewWebView.EnsureCoreWebView2Async(environment);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            System.Windows.MessageBox.Show(
                this,
                "Markdown bei Nacht could not initialize its browser data folder in LocalAppData. Check folder permissions and try again.",
                AppDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        PreviewWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
#if DEBUG
        PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
        PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif
        PreviewWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PreviewWebView.CoreWebView2.WebMessageReceived += CoreWebView2_OnWebMessageReceived;
        PreviewWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_OnDOMContentLoaded;
        PreviewWebView.CoreWebView2.NavigationCompleted += CoreWebView2_OnNavigationCompleted;
        PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "appassets",
            _paths.AssetsDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        PreviewWebView.Source = new Uri("https://appassets/preview-shell.html");

        var readyTask = _webViewReady.Task;
        var completedTask = await Task.WhenAny(readyTask, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completedTask != readyTask)
        {
            System.Windows.MessageBox.Show(
                this,
                "Markdown bei Nacht could not finish loading its preview shell. Please restart the app and try again.",
                AppDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        await readyTask;
        return true;
    }

    private void CoreWebView2_OnDOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        _webViewReady.TrySetResult();
    }

    private void CoreWebView2_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _webViewReady.TrySetResult();
        }
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

            case LinkTargetKind.LocalDocument when !string.IsNullOrWhiteSpace(target.LocalPath):
                LaunchNewDocumentWindow(target);
                break;

            case LinkTargetKind.LocalFile when !string.IsNullOrWhiteSpace(target.LocalPath):
                OpenWithShell(target.LocalPath);
                break;
        }
    }

    private void LaunchNewDocumentWindow(ResolvedLinkTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.LocalPath) is false)
        {
            LaunchNewDocumentWindow(target.LocalPath, target.Anchor);
            return;
        }

        if (target.Uri is not null)
        {
            LaunchNewDocumentWindowWithArgument(target.Uri.AbsoluteUri, null);
        }
    }

    private void LaunchNewDocumentWindow(string filePath, string? anchor = null)
    {
        var normalizedPath = MarkdownPathUtilities.NormalizePath(filePath);
        var argument = string.IsNullOrWhiteSpace(anchor)
            ? normalizedPath
            : new UriBuilder(new Uri(normalizedPath))
            {
                Fragment = anchor,
            }.Uri.AbsoluteUri;

        LaunchNewDocumentWindowWithArgument(argument, normalizedPath);
    }

    private void LaunchNewDocumentWindowWithArgument(string argument, string? localPath)
    {
        var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        var workingDirectory = string.IsNullOrWhiteSpace(localPath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(localPath) ?? AppContext.BaseDirectory;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            Arguments = BuildLaunchArguments(argument),
            UseShellExecute = true,
            WorkingDirectory = workingDirectory,
        };

        Process.Start(startInfo);
    }

    private string BuildLaunchArguments(string fileArgument)
    {
        var placement = GetCascadedWindowPlacement();
        return string.Join(
            " ",
            [
                QuoteArgument(fileArgument),
                FormattableString.Invariant($"--window-left={placement.Left:0.###}"),
                FormattableString.Invariant($"--window-top={placement.Top:0.###}"),
            ]);
    }

    private void HookWindowPlacementTracking()
    {
        LocationChanged += (_, _) => ScheduleWindowPlacementSave();
        SizeChanged += (_, _) => ScheduleWindowPlacementSave();
        StateChanged += (_, _) => ScheduleWindowPlacementSave();
    }

    private WindowPlacement GetCascadedWindowPlacement()
    {
        return WindowPlacementPlanner.Cascade(
            CreateCurrentWindowPlacement(),
            CascadedWindowOffset,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    private void ApplyStartupWindowPlacement()
    {
        var windowWidth = GetEffectiveWindowDimension(Width, Width, MinWidth);
        var windowHeight = GetEffectiveWindowDimension(Height, Height, MinHeight);
        var placement = _windowPlacementCoordinator.ResolveStartupPlacement(
            _startupOptions,
            windowWidth,
            windowHeight,
            CascadedWindowOffset,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        if (placement is null)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
    }

    private void ScheduleWindowPlacementSave()
    {
        if (IsLoaded is false)
        {
            return;
        }

        _windowPlacementDebouncer.Schedule(async cancellationToken =>
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (cancellationToken.IsCancellationRequested is false)
                    {
                        RememberCurrentWindowPlacement();
                    }
                },
                DispatcherPriority.Background,
                cancellationToken);
        });
    }

    private void RememberCurrentWindowPlacement()
    {
        if (IsLoaded is false)
        {
            return;
        }

        _windowPlacementCoordinator.RememberWindowPlacement(CreateCurrentWindowPlacement());
    }

    private WindowPlacement CreateCurrentWindowPlacement()
    {
        var bounds = GetCurrentWindowBounds();
        var placement = new WindowPlacement(
            bounds.Left,
            bounds.Top,
            GetEffectiveWindowDimension(bounds.Width, Width, MinWidth),
            GetEffectiveWindowDimension(bounds.Height, Height, MinHeight));

        return WindowPlacementPlanner.Clamp(
            placement,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    private Rect GetCurrentWindowBounds()
    {
        if (RestoreBounds.IsEmpty is false)
        {
            return RestoreBounds;
        }

        return new Rect(
            Left,
            Top,
            ActualWidth > 0 ? ActualWidth : Width,
            ActualHeight > 0 ? ActualHeight : Height);
    }

    private static double GetEffectiveWindowDimension(double primaryValue, double fallbackValue, double minimumValue)
    {
        var candidate = double.IsNaN(primaryValue) || primaryValue <= 0 ? fallbackValue : primaryValue;
        return Math.Max(candidate, minimumValue);
    }

    private static string QuoteArgument(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private async void OpenMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenFilePickerAsync();
    }

    private async void OpenFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenFilePickerAsync();
    }

    private void RecentFilesMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        RefreshRecentFilesMenu();
    }

    private async void RecentFileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string filePath })
        {
            return;
        }

        if (File.Exists(filePath) is false)
        {
            await RemoveMissingRecentFileAsync(filePath);
            return;
        }

        await OpenSelectedDocumentFileAsync(filePath);
    }

    private async void ClearRecentFilesMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await ClearRecentFilesAsync();
    }

    private async void ReloadMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await ReloadCurrentFileAsync();
    }

    private async Task ReloadCurrentFileAsync()
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

        if (window.ShowDialog() is not true || window.SettingsResult is null)
        {
            return;
        }

        var updatedSettings = window.SettingsResult.Normalize();
        try
        {
            await _settingsStore.SaveAsync(_paths.SettingsFilePath, updatedSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            System.Windows.MessageBox.Show(
                this,
                "Markdown bei Nacht could not save your theme setting. Check that LocalAppData is writable and try again.",
                AppDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _settings = updatedSettings;
        ApplyShellTheme();

        if (string.IsNullOrWhiteSpace(_currentFilePath) is false)
        {
            await RenderCurrentFileAsync(true);
        }
    }

    private async void UserGuideMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenUserGuideAsync();
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task OpenUserGuideAsync()
    {
        if (File.Exists(_paths.UserGuideFilePath) is false)
        {
            System.Windows.MessageBox.Show(
                this,
                "Markdown bei Nacht could not find its installed user guide. Reinstall the app or check that README.md is present in the app folder.",
                AppDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await OpenSelectedDocumentFileAsync(_paths.UserGuideFilePath);
    }

    private async Task OpenFilePickerAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Supported documents (*.md;*.markdown;*.mdown;*.txt)|*.md;*.markdown;*.mdown;*.txt|All files (*.*)|*.*",
            Title = AppDisplayName,
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) is not true)
        {
            return;
        }

        await OpenSelectedDocumentFileAsync(dialog.FileName);
    }

    private Task OpenSelectedDocumentFileAsync(string filePath, string? anchor = null)
    {
        if (WindowOpenPolicy.ShouldReuseCurrentWindow(_hasLoadedDocument))
        {
            return OpenFileAsync(filePath, anchor);
        }

        LaunchNewDocumentWindow(filePath, anchor);
        return Task.CompletedTask;
    }

    private async Task OpenFileAsync(string filePath, string? anchor = null)
    {
        _currentFilePath = MarkdownPathUtilities.NormalizePath(filePath);
        _pendingAnchor = anchor;
        ConfigureFileWatcher(_currentFilePath);
        UpdateWindowTitle(_currentFilePath);

        if (await RenderCurrentFileAsync(false))
        {
            await RememberRecentFileAsync(_currentFilePath);
        }
    }

    private async Task<bool> RenderCurrentFileAsync(bool preserveScroll)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            _hasLoadedDocument = false;
            UpdateWindowTitle(null);
            ShowReadyState();
            return false;
        }

        await _renderLock.WaitAsync();
        try
        {
            var scrollRatio = preserveScroll ? await CaptureScrollRatioAsync() : null;
            var fileResult = await _fileTextLoader.ReadAsync(_currentFilePath);
            if (!fileResult.Success)
            {
                ShowFileError(fileResult);
                return false;
            }

            var rendered = _renderer.RenderDocument(fileResult.Content ?? string.Empty, _currentFilePath, Path.GetFileNameWithoutExtension(_currentFilePath));
            await ApplyRenderAsync(rendered, scrollRatio, _pendingAnchor);
            _pendingAnchor = null;
            _hasLoadedDocument = true;
            ShowPreview();
            return true;
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

    private void ApplyShellTheme()
    {
        var baseColor = ColorUtilities.NormalizeHexColor(_settings.BaseColor);
        var palette = _themePaletteBuilder.BuildCssVariables(baseColor);

        Background = CreateSolidBrush(palette["--color-bg"]);
        RootGrid.Background = CreateSolidBrush(palette["--color-bg"]);
        MainMenu.Background = CreateSolidBrush(ColorUtilities.Mix(baseColor, "#0E1829", 0.44));
        PreviewWebView.DefaultBackgroundColor = System.Drawing.ColorTranslator.FromHtml(palette["--color-bg"]);

        MainMenu.Foreground = CreateSolidBrush(palette["--color-text"]);
        SetMenuResourceBrush("MenuBarForegroundBrush", palette["--color-text"]);
        SetMenuResourceBrush("MenuBarHighlightBrush", ColorUtilities.Mix(baseColor, "#18314A", 0.62));
        SetMenuResourceBrush("MenuPopupBackgroundBrush", ColorUtilities.Mix(baseColor, "#0E1829", 0.48));
        SetMenuResourceBrush("MenuPopupForegroundBrush", palette["--color-text"]);
        SetMenuResourceBrush("MenuPopupBorderBrush", ColorUtilities.Mix(baseColor, "#5F7FA3", 0.44));
        SetMenuResourceBrush("MenuPopupHighlightBrush", ColorUtilities.Mix(baseColor, "#22435F", 0.62));
        SetMenuResourceBrush("MenuPopupDisabledBrush", ColorUtilities.Mix(baseColor, "#8EA4BC", 0.56));

        StatePanel.Background = CreateSolidBrush(palette["--color-panel"]);
        StatePanel.BorderBrush = CreateSolidBrush(ColorUtilities.Mix(baseColor, "#7BA7D3", 0.38));
        StateEyebrowText.Foreground = CreateSolidBrush(palette["--color-link"]);
        StateTitleText.Foreground = CreateSolidBrush(palette["--color-text"]);
        StateMessageText.Foreground = CreateSolidBrush(palette["--color-muted"]);
        StateHintText.Foreground = CreateSolidBrush(ColorUtilities.Mix(baseColor, "#AFC4DA", 0.72));

        DropOverlay.Background = CreateAlphaBrush(ColorUtilities.Mix(baseColor, "#091320", 0.16), 0.64);
        DropOverlay.BorderBrush = CreateAlphaBrush(ColorUtilities.Mix(baseColor, "#7BA7D3", 0.52), 0.56);
        DropOverlayInnerBorder.Background = CreateAlphaBrush(ColorUtilities.Mix(baseColor, "#091320", 0.1), 0.22);
        DropOverlayInnerBorder.BorderBrush = CreateAlphaBrush(ColorUtilities.Mix(baseColor, "#71D1FF", 0.72), 0.44);
        DropOverlayTitleText.Foreground = CreateSolidBrush(palette["--color-text"]);
        DropOverlayMessageText.Foreground = CreateSolidBrush(ColorUtilities.Mix(baseColor, "#C4D6E7", 0.8));
    }

    private async Task RememberRecentFileAsync(string filePath)
    {
        _recentFiles = await _recentFilesStore.RememberAsync(_paths.RecentFilesStateFilePath, filePath);
        RefreshRecentFilesMenu();
    }

    private async Task RemoveMissingRecentFileAsync(string filePath)
    {
        _recentFiles = new RecentFilesState(
            _recentFiles.Files
                .Where(path => string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase) is false)
                .ToArray())
            .Normalize();
        await _recentFilesStore.SaveAsync(_paths.RecentFilesStateFilePath, _recentFiles);
        RefreshRecentFilesMenu();
    }

    private async Task ClearRecentFilesAsync()
    {
        await _recentFilesStore.ClearAsync(_paths.RecentFilesStateFilePath);
        _recentFiles = RecentFilesState.Empty;
        RefreshRecentFilesMenu();
    }

    private void RefreshRecentFilesMenu()
    {
        if (RecentFilesMenuItem is null)
        {
            return;
        }

        RecentFilesMenuItem.Items.Clear();
        var availableFiles = _recentFiles.Files.Where(File.Exists).ToArray();

        if (availableFiles.Length == 0)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem
            {
                Header = "No recent files",
                IsEnabled = false,
                Style = (Style)MainMenu.Resources["SubmenuMenuItemStyle"],
            });
            return;
        }

        for (var index = 0; index < availableFiles.Length; index++)
        {
            var filePath = availableFiles[index];
            var menuItem = new MenuItem
            {
                Header = $"{index + 1}. {Path.GetFileName(filePath)}",
                ToolTip = filePath,
                Tag = filePath,
                Style = (Style)MainMenu.Resources["SubmenuMenuItemStyle"],
            };
            menuItem.Click += RecentFileMenuItem_OnClick;
            RecentFilesMenuItem.Items.Add(menuItem);
        }

        RecentFilesMenuItem.Items.Add(new Separator());
        var clearItem = new MenuItem
        {
            Header = "Clear Recent Files",
            Style = (Style)MainMenu.Resources["SubmenuMenuItemStyle"],
        };
        clearItem.Click += ClearRecentFilesMenuItem_OnClick;
        RecentFilesMenuItem.Items.Add(clearItem);
    }

    private void SetMenuResourceBrush(string key, string hexColor)
    {
        MainMenu.Resources[key] = CreateSolidBrush(hexColor);
    }

    private static SolidColorBrush CreateSolidBrush(string hexColor)
    {
        var brush = new SolidColorBrush(ParseColor(hexColor));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateAlphaBrush(string hexColor, double opacity)
    {
        var color = ParseColor(hexColor);
        color.A = (byte)Math.Round(Math.Clamp(opacity, 0d, 1d) * 255d);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color ParseColor(string hexColor) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ColorUtilities.NormalizeHexColor(hexColor))!;

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

    private void ShowReadyState()
    {
        ShowState(ReadyStateTitle, ReadyStateMessage);
    }

    private void ShowState(string title, string message, string buttonText = "Open File")
    {
        StateTitleText.Text = title;
        StateMessageText.Text = message;
        StateOpenButton.Content = buttonText;
        StatePanel.Visibility = Visibility.Visible;
        PreviewWebView.Visibility = Visibility.Hidden;
        PreviewWebView.IsHitTestVisible = false;
    }

    private void ShowPreview()
    {
        StatePanel.Visibility = Visibility.Collapsed;
        PreviewWebView.Visibility = Visibility.Visible;
        PreviewWebView.IsHitTestVisible = true;
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

        if (IsReloadShortcut(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
            _ = ReloadCurrentFileAsync();
            return;
        }

        if (e.Key == Key.F1)
        {
            e.Handled = true;
            _ = OpenUserGuideAsync();
            return;
        }
    }

    internal static bool IsReloadShortcut(Key key, ModifierKeys modifiers) =>
        key == Key.F5 || modifiers == ModifierKeys.Control && key == Key.R;

    private void Window_OnPreviewDragOver(object sender, WpfDragEventArgs e)
    {
        var canOpen = TryGetDraggedDocumentPath(e.Data, out _);
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

        if (!TryGetDraggedDocumentPath(e.Data, out var path) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await OpenSelectedDocumentFileAsync(path);
    }

    private static bool TryGetDraggedDocumentPath(WpfDataObject dataObject, out string? path)
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
        if (!MarkdownPathUtilities.IsSupportedDocumentPath(candidate))
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

