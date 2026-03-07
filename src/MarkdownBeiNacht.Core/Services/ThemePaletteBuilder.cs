using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public sealed class ThemePaletteBuilder
{
    public IReadOnlyDictionary<string, string> BuildCssVariables(string? baseColor)
    {
        var background = ColorUtilities.NormalizeHexColor(baseColor);
        return new Dictionary<string, string>
        {
            ["--color-bg"] = background,
            ["--color-panel"] = ColorUtilities.Mix(background, "#11233B", 0.32),
            ["--color-panel-alt"] = ColorUtilities.Mix(background, "#1A3554", 0.42),
            ["--color-border"] = ColorUtilities.Mix(background, "#5F7FA3", 0.36),
            ["--color-border-soft"] = ColorUtilities.ToRgba(ColorUtilities.Mix(background, "#7BA7D3", 0.44), 0.28),
            ["--color-text"] = ColorUtilities.Mix(background, "#F4F8FF", 0.9),
            ["--color-muted"] = ColorUtilities.Mix(background, "#B7C7DA", 0.74),
            ["--color-link"] = ColorUtilities.Mix(background, "#73C7FF", 0.8),
            ["--color-link-hover"] = ColorUtilities.Mix(background, "#B8E6FF", 0.9),
            ["--color-code-bg"] = ColorUtilities.Mix(background, "#08101D", 0.24),
            ["--color-pre-bg"] = ColorUtilities.Mix(background, "#040B15", 0.12),
            ["--color-accent"] = ColorUtilities.Mix(background, "#71D1FF", 0.68),
            ["--color-selection"] = ColorUtilities.ToRgba(ColorUtilities.Mix(background, "#71D1FF", 0.68), 0.26),
        };
    }
}

