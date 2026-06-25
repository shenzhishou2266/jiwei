using System.Windows;
using System.Windows.Media;
using 积微.Services;
using Color = System.Windows.Media.Color;
using WpfApp = System.Windows.Application;

namespace 积微.Tests;

public class ThemeManagerTests
{
    /// <summary>Light 主题应包含所有 20 个颜色键。</summary>
    [Fact]
    public void CreateTheme_Light_ContainsAllColors()
    {
        var dictionary = ThemeManager.CreateTheme("Light");

        Assert.Equal(23, dictionary.Count);
        Assert.Equal(Color.FromRgb(249, 250, 251), ((SolidColorBrush)dictionary["Background"]).Color);
        Assert.Equal(Color.FromRgb(255, 255, 255), ((SolidColorBrush)dictionary["CardBackground"]).Color);
        Assert.Equal(Color.FromRgb(17, 24, 39), ((SolidColorBrush)dictionary["TextPrimary"]).Color);
        Assert.Equal(Color.FromRgb(59, 130, 246), ((SolidColorBrush)dictionary["AccentColor"]).Color);
        Assert.Equal(Color.FromRgb(229, 231, 235), ((SolidColorBrush)dictionary["BorderColor"]).Color);
    }

    /// <summary>Dark 主题应包含所有 20 个颜色键。</summary>
    [Fact]
    public void CreateTheme_Dark_ContainsAllColors()
    {
        var dictionary = ThemeManager.CreateTheme("Dark");

        Assert.Equal(23, dictionary.Count);
        Assert.Equal(Color.FromRgb(0, 0, 0), ((SolidColorBrush)dictionary["Background"]).Color);
        Assert.Equal(Color.FromRgb(28, 28, 30), ((SolidColorBrush)dictionary["CardBackground"]).Color);
        Assert.Equal(Color.FromRgb(255, 255, 255), ((SolidColorBrush)dictionary["TextPrimary"]).Color);
        Assert.Equal(Color.FromRgb(10, 132, 255), ((SolidColorBrush)dictionary["AccentColor"]).Color);
        Assert.Equal(Color.FromRgb(56, 56, 58), ((SolidColorBrush)dictionary["BorderColor"]).Color);
    }

    /// <summary>非 Light 主题（如 "Dark"）应返回 Dark 主题。</summary>
    [Fact]
    public void CreateTheme_NonLight_ReturnsDark()
    {
        var dark1 = ThemeManager.CreateTheme("Dark");
        var dark2 = ThemeManager.CreateTheme("System");

        var darkBg = ((SolidColorBrush)dark1["Background"]).Color;
        var sysBg = ((SolidColorBrush)dark2["Background"]).Color;

        Assert.Equal(darkBg, sysBg);
    }

    /// <summary>Light 主题标签颜色应正确。</summary>
    [Fact]
    public void CreateTheme_Light_HasCorrectTagColors()
    {
        var dictionary = ThemeManager.CreateTheme("Light");

        // TagSelected
        Assert.Equal(Color.FromRgb(59, 130, 246), ((SolidColorBrush)dictionary["TagSelectedFg"]).Color);
        Assert.Equal(Color.FromRgb(219, 234, 254), ((SolidColorBrush)dictionary["TagSelectedBg"]).Color);

        // TagLongTerm
        Assert.Equal(Color.FromRgb(5, 150, 105), ((SolidColorBrush)dictionary["TagLongTermFg"]).Color);

        // TagShortTerm
        Assert.Equal(Color.FromRgb(37, 99, 235), ((SolidColorBrush)dictionary["TagShortTermFg"]).Color);

        // TagRecurring
        Assert.Equal(Color.FromRgb(124, 58, 237), ((SolidColorBrush)dictionary["TagRecurringFg"]).Color);
    }

    /// <summary>Dark 主题标签颜色应正确。</summary>
    [Fact]
    public void CreateTheme_Dark_HasCorrectTagColors()
    {
        var dictionary = ThemeManager.CreateTheme("Dark");

        Assert.Equal(Color.FromRgb(147, 197, 253), ((SolidColorBrush)dictionary["TagSelectedFg"]).Color);
        Assert.Equal(Color.FromRgb(52, 211, 153), ((SolidColorBrush)dictionary["TagLongTermFg"]).Color);
        Assert.Equal(Color.FromRgb(96, 165, 250), ((SolidColorBrush)dictionary["TagShortTermFg"]).Color);
        Assert.Equal(Color.FromRgb(167, 139, 250), ((SolidColorBrush)dictionary["TagRecurringFg"]).Color);
    }
}