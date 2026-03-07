using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht;

public partial class SettingsWindow : Window
{
    private string _currentHexColor;

    public SettingsWindow(AppSettings currentSettings)
    {
        InitializeComponent();
        _currentHexColor = ColorUtilities.NormalizeHexColor(currentSettings.BaseColor);
        ApplyPreview();
    }

    public AppSettings? SettingsResult { get; private set; }

    private void ChooseColorButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = System.Drawing.ColorTranslator.FromHtml(_currentHexColor),
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        _currentHexColor = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        ApplyPreview();
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _currentHexColor = AppSettings.DefaultBaseColor;
        ApplyPreview();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsResult = new AppSettings(_currentHexColor).Normalize();
        DialogResult = true;
    }

    private void ApplyPreview()
    {
        ColorSwatch.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(_currentHexColor)!;
        ColorValueText.Text = _currentHexColor;
    }
}


