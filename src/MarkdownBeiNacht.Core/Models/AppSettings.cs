namespace MarkdownBeiNacht.Core.Models;

public sealed record AppSettings(string BaseColor)
{
    public const string DefaultBaseColor = "#0B1320";

    public static AppSettings Default { get; } = new(DefaultBaseColor);

    public AppSettings Normalize() => this with
    {
        BaseColor = ColorUtilities.NormalizeHexColor(BaseColor, DefaultBaseColor),
    };
}

