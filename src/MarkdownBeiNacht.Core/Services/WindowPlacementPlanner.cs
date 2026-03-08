using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public static class WindowPlacementPlanner
{
    public static WindowPlacement Cascade(
        WindowPlacement placement,
        double offset,
        double virtualLeft,
        double virtualTop,
        double virtualWidth,
        double virtualHeight)
    {
        return Clamp(
            placement with
            {
                Left = placement.Left + offset,
                Top = placement.Top + offset,
            },
            virtualLeft,
            virtualTop,
            virtualWidth,
            virtualHeight);
    }

    public static WindowPlacement Clamp(
        WindowPlacement placement,
        double virtualLeft,
        double virtualTop,
        double virtualWidth,
        double virtualHeight)
    {
        var width = Math.Max(placement.Width, 0d);
        var height = Math.Max(placement.Height, 0d);
        var maxLeft = Math.Max(virtualLeft, virtualLeft + virtualWidth - width);
        var maxTop = Math.Max(virtualTop, virtualTop + virtualHeight - height);

        return placement with
        {
            Left = Math.Clamp(placement.Left, virtualLeft, maxLeft),
            Top = Math.Clamp(placement.Top, virtualTop, maxTop),
        };
    }
}
