using System.Globalization;

namespace MarkdownBeiNacht.Core.Models;

public static class ColorUtilities
{
    public static string NormalizeHexColor(string? hexColor, string fallback = AppSettings.DefaultBaseColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return fallback;
        }

        var trimmed = hexColor.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 3)
        {
            trimmed = string.Concat(trimmed.Select(character => new string(character, 2)));
        }

        if (trimmed.Length != 6 || !int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return fallback;
        }

        return $"#{trimmed.ToUpperInvariant()}";
    }

    public static string Mix(string fromHex, string toHex, double amount)
    {
        var from = ParseRgb(fromHex);
        var to = ParseRgb(toHex);
        var clamped = Math.Clamp(amount, 0d, 1d);

        return ToHex(
            (byte)Math.Round(from.Red + ((to.Red - from.Red) * clamped)),
            (byte)Math.Round(from.Green + ((to.Green - from.Green) * clamped)),
            (byte)Math.Round(from.Blue + ((to.Blue - from.Blue) * clamped)));
    }

    public static string ToRgba(string hex, double alpha)
    {
        var rgb = ParseRgb(hex);
        return FormattableString.Invariant($"rgba({rgb.Red}, {rgb.Green}, {rgb.Blue}, {Math.Clamp(alpha, 0d, 1d):0.###})");
    }

    private static (byte Red, byte Green, byte Blue) ParseRgb(string hexColor)
    {
        var normalized = NormalizeHexColor(hexColor);
        return (
            byte.Parse(normalized[1..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized[3..5], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized[5..7], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static string ToHex(byte red, byte green, byte blue) =>
        FormattableString.Invariant($"#{red:X2}{green:X2}{blue:X2}");
}

