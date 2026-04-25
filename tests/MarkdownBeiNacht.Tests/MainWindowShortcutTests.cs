using System.Windows.Input;
using MarkdownBeiNacht;

namespace MarkdownBeiNacht.Tests;

public sealed class MainWindowShortcutTests
{
    [Theory]
    [InlineData(Key.F5, ModifierKeys.None)]
    [InlineData(Key.R, ModifierKeys.Control)]
    public void IsReloadShortcut_AcceptsSupportedReloadShortcuts(Key key, ModifierKeys modifiers)
    {
        Assert.True(MainWindow.IsReloadShortcut(key, modifiers));
    }

    [Theory]
    [InlineData(Key.R, ModifierKeys.None)]
    [InlineData(Key.O, ModifierKeys.Control)]
    [InlineData(Key.F1, ModifierKeys.None)]
    public void IsReloadShortcut_RejectsOtherShortcuts(Key key, ModifierKeys modifiers)
    {
        Assert.False(MainWindow.IsReloadShortcut(key, modifiers));
    }
}
