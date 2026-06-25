using 积微.Models;
using 积微.Services;

namespace 积微.Tests;

public class SessionRecorderTests
{
    /// <summary>秒表计时完成：应记录时间线、更新累计时长、创建 SessionRecord。</summary>
    [Fact]
    public async Task RecordAsync_StopwatchSession_RecordsTimelineAndSession()
    {
        // Arrange
        var goal = new Goal("测试目标");
        var savedGoals = new List<List<Goal>>();
        var savedSessions = new List<SessionRecord>();

        var goalRepo = new FakeGoalRepository(savedGoals);
        var sessionRepo = new FakeSessionRepository(savedSessions);
        var goalsProvider = new Func<List<Goal>>(() => savedGoals.Count > 0 ? savedGoals.Last() : new List<Goal> { goal });

        var recorder = new SessionRecorder(goalRepo, sessionRepo, goalsProvider);

        // Act: 记录 125 秒的秒表计时
        await recorder.RecordAsync(goal, elapsedSeconds: 125, timerType: TimerSessionType.Stopwatch, isStopwatchMode: true);

        // Assert: 目标累计时长增加
        Assert.Equal(125, goal.TotalElapsedSeconds);

        // Assert: 时间线包含计时记录（Goal 构造函数会自动添加"目标已创建"）
        var timelineEntry = Assert.Single(goal.Timeline, e => e.Content.Contains("计时器记录"));
        Assert.Contains("2分钟5秒", timelineEntry.Content);

        // Assert: 会话记录已保存
        var session = Assert.Single(savedSessions);
        Assert.Equal(SessionType.Focus, session.Type);
        Assert.Equal(TimerSessionType.Stopwatch, session.TimerType);
        Assert.Equal(125, session.DurationSeconds);
        Assert.Equal("测试目标", session.GoalTitle);
    }

    /// <summary>倒计时完成：时间线条目文本不同于秒表。</summary>
    [Fact]
    public async Task RecordAsync_CountdownSession_UsesCountdownText()
    {
        // Arrange
        var goal = new Goal("倒计时目标");
        var savedSessions = new List<SessionRecord>();
        var goalRepo = new FakeGoalRepository(new List<List<Goal>>());
        var sessionRepo = new FakeSessionRepository(savedSessions);
        var goalsProvider = new Func<List<Goal>>(() => new List<Goal> { goal });

        var recorder = new SessionRecorder(goalRepo, sessionRepo, goalsProvider);

        // Act: 记录 60 秒的倒计时
        await recorder.RecordAsync(goal, elapsedSeconds: 60, timerType: TimerSessionType.Countdown, isStopwatchMode: false);

        // Assert: 时间线文本提到"倒计时"
        var timelineEntry = Assert.Single(goal.Timeline, e => e.Content.Contains("倒计时"));
        Assert.Contains("1分钟", timelineEntry.Content);
    }

    /// <summary>番茄钟专注完成：会话类型为 PomodoroFocus。</summary>
    [Fact]
    public async Task RecordAsync_PomodoroFocusSession_CreatesPomodoroRecord()
    {
        // Arrange
        var goal = new Goal("番茄钟目标");
        var savedSessions = new List<SessionRecord>();
        var goalRepo = new FakeGoalRepository(new List<List<Goal>>());
        var sessionRepo = new FakeSessionRepository(savedSessions);
        var goalsProvider = new Func<List<Goal>>(() => new List<Goal> { goal });

        var recorder = new SessionRecorder(goalRepo, sessionRepo, goalsProvider);

        // Act: 记录 25 分钟番茄钟
        await recorder.RecordAsync(goal, elapsedSeconds: 1500, timerType: TimerSessionType.PomodoroFocus, isStopwatchMode: false);

        // Assert
        var session = Assert.Single(savedSessions);
        Assert.Equal(TimerSessionType.PomodoroFocus, session.TimerType);
        Assert.Equal(1500, session.DurationSeconds);
        Assert.Equal(1500, goal.TotalElapsedSeconds);

        // 时间线文本提到"番茄钟"
        Assert.Contains(goal.Timeline, e => e.Content.Contains("番茄钟"));
    }

    /// <summary>elapsedSeconds=0 不应创建 SessionRecord。</summary>
    [Fact]
    public async Task RecordAsync_ZeroElapsedSeconds_SkipsSessionRecord()
    {
        // Arrange
        var goal = new Goal("零时目标");
        var savedSessions = new List<SessionRecord>();
        var goalRepo = new FakeGoalRepository(new List<List<Goal>>());
        var sessionRepo = new FakeSessionRepository(savedSessions);
        var goalsProvider = new Func<List<Goal>>(() => new List<Goal> { goal });

        var recorder = new SessionRecorder(goalRepo, sessionRepo, goalsProvider);

        // Act
        await recorder.RecordAsync(goal, elapsedSeconds: 0, timerType: TimerSessionType.Stopwatch, isStopwatchMode: true);

        // Assert: 不创建 SessionRecord
        Assert.Empty(savedSessions);
        // 但目标本身不应报错
        Assert.Equal(0, goal.TotalElapsedSeconds);
    }
}

/// <summary>用于测试的假目标存储实现。</summary>
public class FakeGoalRepository : IGoalRepository
{
    private readonly List<List<Goal>> _savedSnapshots;
    public FakeGoalRepository(List<List<Goal>> savedSnapshots) => _savedSnapshots = savedSnapshots;

    public Task<List<Goal>> LoadGoalsAsync()
    {
        return Task.FromResult(_savedSnapshots.Count > 0
            ? _savedSnapshots.Last()
            : new List<Goal>());
    }

    public Task SaveGoalsAsync(List<Goal> goals)
    {
        _savedSnapshots.Add(goals);
        return Task.CompletedTask;
    }
}

/// <summary>用于测试的假会话存储实现。</summary>
public class FakeSessionRepository : ISessionRepository
{
    private readonly List<SessionRecord> _sessions;
    public FakeSessionRepository(List<SessionRecord> sessions) => _sessions = sessions;
    public Task AddSessionAsync(SessionRecord session)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }
}