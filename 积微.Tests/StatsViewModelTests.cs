using 积微.ViewModels;

namespace 积微.Tests;

public class StatsViewModelTests
{
    [Fact]
    public void FormatHours_Zero_ReturnsZero()
    {
        var result = StatsViewModel.FormatHours(0);
        Assert.Equal("0", result);
    }

    [Fact]
    public void FormatHours_NonZero_ReturnsOneDecimal()
    {
        var result = StatsViewModel.FormatHours(2.5);
        Assert.Equal("2.5", result);
    }

    [Fact]
    public void FormatHours_LongDecimal_ReturnsOneDecimal()
    {
        var result = StatsViewModel.FormatHours(1.234);
        Assert.Equal("1.2", result);
    }

    [Fact]
    public void CompareInt_TodayGreater_ReturnsPositive()
    {
        var (text, changeType) = StatsViewModel.CompareInt(10, 5, "个");
        Assert.Equal("比昨天多 5 个", text);
        Assert.Equal("Positive", changeType);
    }

    [Fact]
    public void CompareInt_TodayLess_ReturnsNegative()
    {
        var (text, changeType) = StatsViewModel.CompareInt(3, 8, "个");
        Assert.Equal("比昨天少 5 个", text);
        Assert.Equal("Negative", changeType);
    }

    [Fact]
    public void CompareInt_Equal_ReturnsZero()
    {
        var (text, changeType) = StatsViewModel.CompareInt(5, 5, "个");
        Assert.Equal("与昨天持平", text);
        Assert.Equal("Zero", changeType);
    }

    [Fact]
    public void CompareDouble_TodayGreater_ReturnsPositive()
    {
        var (text, changeType) = StatsViewModel.CompareDouble(2.5, 1.0, "h");
        Assert.Equal("较昨天 +1.5h", text);
        Assert.Equal("Positive", changeType);
    }

    [Fact]
    public void CompareDouble_TodayLess_ReturnsNegative()
    {
        var (text, changeType) = StatsViewModel.CompareDouble(1.0, 3.5, "h");
        Assert.Equal("较昨天 -2.5h", text);
        Assert.Equal("Negative", changeType);
    }

    [Fact]
    public void CompareDouble_Equal_ReturnsZero()
    {
        var (text, changeType) = StatsViewModel.CompareDouble(2.0, 2.0, "h");
        Assert.Equal("与昨天持平", text);
        Assert.Equal("Zero", changeType);
    }
}