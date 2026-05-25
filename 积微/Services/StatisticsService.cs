using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.Threading;
using System.Threading.Tasks;
using 积微.Models;

namespace 积微.Services
{
    /// <summary>统计服务，提供会话记录的分析和报告功能</summary>
    public class StatisticsService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve
        };

        private static List<SessionRecord>? _cachedSessions;
        private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        private static string GetSessionFilePath()
        {
            string dataStoragePath = SettingsManager.Current.DataStoragePath;

            if (string.IsNullOrEmpty(dataStoragePath))
            {
                dataStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "积微"
                );
            }

            if (!Directory.Exists(dataStoragePath))
            {
                Directory.CreateDirectory(dataStoragePath);
            }

            return Path.Combine(dataStoragePath, "sessions.json");
        }

        /// <summary>加载所有会话记录</summary>
        public static async Task<List<SessionRecord>> LoadSessionsAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedSessions != null)
                {
                    return _cachedSessions;
                }

                try
                {
                    string filePath = GetSessionFilePath();
                    if (File.Exists(filePath))
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        _cachedSessions = JsonSerializer.Deserialize<List<SessionRecord>>(json, SerializerOptions) ?? new List<SessionRecord>();
                        return _cachedSessions;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading sessions: {ex.Message}");
                }

                _cachedSessions = new List<SessionRecord>();
                return _cachedSessions;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>保存会话记录列表</summary>
        public static async Task SaveSessionsAsync(List<SessionRecord> sessions)
        {
            await _cacheLock.WaitAsync();
            try
            {
                _cachedSessions = sessions;
                try
                {
                    string filePath = GetSessionFilePath();
                    var json = JsonSerializer.Serialize(sessions, SerializerOptions);
                    await File.WriteAllTextAsync(filePath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving sessions: {ex.Message}");
                }
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>删除指定目标及其子目标关联的所有会话记录</summary>
        public static async Task DeleteSessionsByGoalAsync(Goal goal)
        {
            await _cacheLock.WaitAsync();
            try
            {
                var sessions = _cachedSessions ?? await LoadSessionsInternalAsync();
                var ids = new HashSet<string>();
                var titles = new HashSet<string>();
                CollectGoalIdentifiers(goal, ids, titles);

                var filtered = sessions.Where(s =>
                {
                    bool matchId = s.GoalId != null && ids.Contains(s.GoalId);
                    bool matchTitle = s.GoalTitle != null && titles.Contains(s.GoalTitle);
                    return !matchId && !matchTitle;
                }).ToList();

                if (filtered.Count != sessions.Count)
                    await SaveSessionsInternalAsync(filtered);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private static void CollectGoalIdentifiers(Goal goal, HashSet<string> ids, HashSet<string> titles)
        {
            ids.Add(goal.Id);
            titles.Add(goal.Title);
            foreach (var child in goal.Children)
                CollectGoalIdentifiers(child, ids, titles);
        }

        private static void CollectAllGoalIdentifiers(List<Goal> goals, HashSet<string> ids, HashSet<string> titles)
        {
            foreach (var goal in goals)
                CollectGoalIdentifiers(goal, ids, titles);
        }

        /// <summary>清理已删除目标的孤立会话记录</summary>
        public static async Task CleanupOrphanedSessionsAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                var goals = await DataStorageService.LoadGoalsAsync();
                // 始终从磁盘读取，避免使用可能已过期的内存缓存覆盖磁盘数据
                var sessions = await LoadSessionsInternalAsync();

                var ids = new HashSet<string>();
                var titles = new HashSet<string>();
                CollectAllGoalIdentifiers(goals, ids, titles);

                var filtered = sessions.Where(s =>
                {
                    bool hasGoalRef = !string.IsNullOrEmpty(s.GoalId) || !string.IsNullOrEmpty(s.GoalTitle);
                    if (!hasGoalRef) return true;

                    bool idExists = s.GoalId != null && ids.Contains(s.GoalId);
                    bool titleExists = s.GoalTitle != null && titles.Contains(s.GoalTitle);
                    return idExists || titleExists;
                }).ToList();

                if (filtered.Count != sessions.Count)
                    await SaveSessionsInternalAsync(filtered);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>添加一条会话记录</summary>
        public static async Task AddSessionAsync(SessionRecord session)
        {
            await _cacheLock.WaitAsync();
            try
            {
                var sessions = _cachedSessions ?? await LoadSessionsInternalAsync();
                sessions.Add(session);
                await SaveSessionsInternalAsync(sessions);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private static async Task<List<SessionRecord>> LoadSessionsInternalAsync()
        {
            try
            {
                string filePath = GetSessionFilePath();
                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    _cachedSessions = JsonSerializer.Deserialize<List<SessionRecord>>(json, SerializerOptions) ?? new List<SessionRecord>();
                    return _cachedSessions;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sessions: {ex.Message}");
            }

            _cachedSessions = new List<SessionRecord>();
            return _cachedSessions;
        }

        private static async Task SaveSessionsInternalAsync(List<SessionRecord> sessions)
        {
            _cachedSessions = sessions;
            try
            {
                string filePath = GetSessionFilePath();
                var json = JsonSerializer.Serialize(sessions, SerializerOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving sessions: {ex.Message}");
            }
        }

        /// <summary>获取指定日期的每日统计</summary>
        public static async Task<DailyStats> GetDailyStatsAsync(DateTime date)
        {
            var sessions = await LoadSessionsAsync();
            var stats = new DailyStats { Date = date.Date };

            var daySessions = sessions.Where(s => s.StartTime.Date == date.Date && s.GoalId != "global-goal").ToList();

            foreach (var session in daySessions)
            {
                if (session.Type == SessionType.Focus)
                {
                    if (session.TimerType == TimerSessionType.PomodoroFocus)
                    {
                        stats.FocusSessions++;
                        stats.TotalFocusSeconds += session.DurationSeconds;
                    }
                    else if (session.TimerType == TimerSessionType.Stopwatch
                          || session.TimerType == TimerSessionType.Countdown)
                    {
                        stats.FragmentSessions++;
                        stats.TotalFragmentSeconds += session.DurationSeconds;
                    }
                }
                else
                {
                    stats.BreakSessions++;
                    stats.TotalBreakSeconds += session.DurationSeconds;
                }
            }

            return stats;
        }

        /// <summary>获取连续专注天数</summary>
        public static async Task<int> GetStreakDaysAsync()
        {
            var sessions = await LoadSessionsAsync();
            if (!sessions.Any())
                return 0;

            var focusDates = sessions
                .Where(s => s.Type == SessionType.Focus && s.GoalId != "global-goal")
                .Select(s => s.StartTime.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            if (!focusDates.Any())
                return 0;

            int streak = 0;
            DateTime currentDate = DateTime.Today;

            foreach (var date in focusDates)
            {
                if (date == currentDate.AddDays(-streak))
                {
                    streak++;
                }
                else if (date < currentDate.AddDays(-streak))
                {
                    break;
                }
            }

            return streak;
        }

        /// <summary>获取总活跃天数</summary>
        public static async Task<int> GetActiveDaysAsync()
        {
            var sessions = await LoadSessionsAsync();
            return sessions
                .Where(s => s.Type == SessionType.Focus && s.GoalId != "global-goal")
                .Select(s => s.StartTime.Date)
                .Distinct()
                .Count();
        }

        /// <summary>获取本周每日统计</summary>
        public static async Task<List<DailyStats>> GetWeeklyStatsAsync()
        {
            var stats = new List<DailyStats>();
            DateTime today = DateTime.Today;
            DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            for (int i = 0; i < 7; i++)
            {
                var date = startOfWeek.AddDays(i);
                var dailyStats = await GetDailyStatsAsync(date);
                stats.Add(dailyStats);
            }

            return stats;
        }

        /// <summary>获取本月统计汇总</summary>
        public static async Task<DailyStats> GetMonthlyStatsAsync()
        {
            var sessions = await LoadSessionsAsync();
            var stats = new DailyStats();

            var monthSessions = sessions.Where(s => s.StartTime.Year == DateTime.Today.Year && s.StartTime.Month == DateTime.Today.Month && s.GoalId != "global-goal").ToList();

            foreach (var session in monthSessions)
            {
                if (session.Type == SessionType.Focus)
                {
                    if (session.TimerType == TimerSessionType.PomodoroFocus)
                    {
                        stats.FocusSessions++;
                        stats.TotalFocusSeconds += session.DurationSeconds;
                    }
                    else if (session.TimerType == TimerSessionType.Stopwatch
                          || session.TimerType == TimerSessionType.Countdown)
                    {
                        stats.FragmentSessions++;
                        stats.TotalFragmentSeconds += session.DurationSeconds;
                    }
                }
                else
                {
                    stats.BreakSessions++;
                    stats.TotalBreakSeconds += session.DurationSeconds;
                }
            }

            return stats;
        }

        /// <summary>获取总计统计</summary>
        public static async Task<DailyStats> GetTotalStatsAsync()
        {
            var sessions = await LoadSessionsAsync();
            var stats = new DailyStats();

            foreach (var session in sessions)
            {
                if (session.GoalId == "global-goal")
                    continue;

                if (session.Type == SessionType.Focus)
                {
                    if (session.TimerType == TimerSessionType.PomodoroFocus)
                    {
                        stats.FocusSessions++;
                        stats.TotalFocusSeconds += session.DurationSeconds;
                    }
                    else if (session.TimerType == TimerSessionType.Stopwatch
                          || session.TimerType == TimerSessionType.Countdown)
                    {
                        stats.FragmentSessions++;
                        stats.TotalFragmentSeconds += session.DurationSeconds;
                    }
                }
                else
                {
                    stats.BreakSessions++;
                    stats.TotalBreakSeconds += session.DurationSeconds;
                }
            }

            return stats;
        }

        /// <summary>获取指定日期的专注分钟数和碎片分钟数</summary>
        public static async Task<(int pomodoroMinutes, int fragmentMinutes)> GetDayActivityAsync(DateTime date)
        {
            var dailyStats = await GetDailyStatsAsync(date);
            int pomodoroMin = dailyStats.TotalFocusSeconds / 60;
            int fragmentMin = dailyStats.TotalFragmentSeconds / 60;
            return (pomodoroMin, fragmentMin);
        }

        /// <summary>获取指定日期的详细统计（含完成目标数）</summary>
        public static async Task<DayDetailStats> GetDayDetailStatsAsync(DateTime date)
        {
            var dailyStats = await GetDailyStatsAsync(date);
            var goals = await DataStorageService.LoadGoalsAsync();
            int completedGoalsCount = goals.Count(g =>
                g.Id != "global-goal" &&
                g.CompletedAt.HasValue &&
                g.CompletedAt.Value.Date == date.Date);

            return new DayDetailStats
            {
                Date = date,
                FocusSessions = dailyStats.FocusSessions,
                TotalFocusSeconds = dailyStats.TotalFocusSeconds,
                FragmentSessions = dailyStats.FragmentSessions,
                TotalFragmentSeconds = dailyStats.TotalFragmentSeconds,
                CompletedGoals = completedGoalsCount
            };
        }

        /// <summary>获取今日各目标的记录次数统计（番茄钟+碎片）</summary>
        public static async Task<Dictionary<string, int>> GetTodayGoalPomodorosAsync()
        {
            var sessions = await LoadSessionsAsync();
            var todaySessions = sessions.Where(s =>
                (s.TimerType == TimerSessionType.PomodoroFocus
                 || s.TimerType == TimerSessionType.Stopwatch
                 || s.TimerType == TimerSessionType.Countdown) &&
                s.StartTime.Date == DateTime.Today &&
                s.GoalId != "global-goal" &&
                !string.IsNullOrEmpty(s.GoalTitle)).ToList();

            return todaySessions
                .GroupBy(s => s.GoalTitle)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>获取今日各目标的活动摘要（区分番茄钟和碎片）</summary>
        public static async Task<List<GoalActivitySummary>> GetTodayGoalActivityAsync()
        {
            var sessions = await LoadSessionsAsync();
            var todaySessions = sessions.Where(s =>
                (s.TimerType == TimerSessionType.PomodoroFocus
                 || s.TimerType == TimerSessionType.Stopwatch
                 || s.TimerType == TimerSessionType.Countdown) &&
                s.StartTime.Date == DateTime.Today &&
                s.GoalId != "global-goal" &&
                !string.IsNullOrEmpty(s.GoalTitle)).ToList();

            return todaySessions
                .GroupBy(s => s.GoalTitle)
                .Select(g => new GoalActivitySummary
                {
                    GoalTitle = g.Key,
                    PomodoroCount = g.Count(s => s.TimerType == TimerSessionType.PomodoroFocus),
                    FragmentCount = g.Count(s => s.TimerType == TimerSessionType.Stopwatch
                                             || s.TimerType == TimerSessionType.Countdown)
                })
                .OrderByDescending(g => g.PomodoroCount + g.FragmentCount)
                .ToList();
        }

        /// <summary>获取今日碎片计时器统计</summary>
        public static async Task<(int count, int totalSeconds)> GetTodayFragmentStatsAsync()
        {
            var sessions = await LoadSessionsAsync();
            var fragments = sessions.Where(s =>
                s.StartTime.Date == DateTime.Today &&
                s.Type == SessionType.Focus &&
                s.GoalId != "global-goal" &&
                (s.TimerType == TimerSessionType.Stopwatch || s.TimerType == TimerSessionType.Countdown)).ToList();

            return (fragments.Count, fragments.Sum(s => s.DurationSeconds));
        }

        /// <summary>获取所有目标的统计</summary>
        public static async Task<List<GoalStats>> GetAllGoalsStatsAsync()
        {
            var sessions = await LoadSessionsAsync();
            var goalStatsDict = new Dictionary<string, GoalStats>();

            foreach (var session in sessions)
            {
                if (string.IsNullOrEmpty(session.GoalId) || session.GoalId == "global-goal")
                    continue;

                if (!goalStatsDict.TryGetValue(session.GoalId, out var stats))
                {
                    stats = new GoalStats
                    {
                        GoalId = session.GoalId,
                        GoalTitle = session.GoalTitle,
                        TotalSeconds = 0,
                        PomodoroFocusSeconds = 0,
                        PomodoroBreakSeconds = 0,
                        StopwatchSeconds = 0,
                        CountdownSeconds = 0,
                        PomodoroCount = 0
                    };
                    goalStatsDict[session.GoalId] = stats;
                }

                stats.TotalSeconds += session.DurationSeconds;

                switch (session.TimerType)
                {
                    case TimerSessionType.PomodoroFocus:
                        stats.PomodoroFocusSeconds += session.DurationSeconds;
                        stats.PomodoroCount++;
                        break;
                    case TimerSessionType.PomodoroBreak:
                        stats.PomodoroBreakSeconds += session.DurationSeconds;
                        break;
                    case TimerSessionType.Stopwatch:
                        stats.StopwatchSeconds += session.DurationSeconds;
                        break;
                    case TimerSessionType.Countdown:
                        stats.CountdownSeconds += session.DurationSeconds;
                        break;
                }
            }

            return goalStatsDict.Values.OrderByDescending(g => g.TotalSeconds).ToList();
        }

        /// <summary>获取指定日期范围的报告统计</summary>
        public static async Task<ReportStats> GetReportStatsAsync(DateTime startDate, DateTime endDate)
        {
            var sessions = await LoadSessionsAsync();
            var stats = new ReportStats
            {
                StartDate = startDate,
                EndDate = endDate
            };

            var reportSessions = sessions.Where(s => s.StartTime >= startDate && s.StartTime <= endDate && s.GoalId != "global-goal").ToList();

            foreach (var session in reportSessions)
            {
                if (session.Type == SessionType.Focus)
                {
                    bool isPomodoro = session.TimerType == TimerSessionType.PomodoroFocus;
                    bool isFragment = session.TimerType == TimerSessionType.Stopwatch
                                   || session.TimerType == TimerSessionType.Countdown;

                    if (isPomodoro)
                    {
                        stats.PomodoroSessions++;
                        stats.TotalPomodoroSeconds += session.DurationSeconds;
                    }
                    else if (isFragment)
                    {
                        stats.FragmentSessions++;
                        stats.TotalFragmentSeconds += session.DurationSeconds;
                    }

                    if (!string.IsNullOrEmpty(session.GoalTitle))
                    {
                        if (!stats.GoalStats.TryGetValue(session.GoalTitle, out var goalStat))
                        {
                            goalStat = new GoalReportStat { GoalTitle = session.GoalTitle };
                            stats.GoalStats[session.GoalTitle] = goalStat;
                        }
                        goalStat.FocusSessions++;
                        goalStat.TotalSeconds += session.DurationSeconds;

                        switch (session.TimerType)
                        {
                            case TimerSessionType.PomodoroFocus:
                                goalStat.PomodoroFocusSeconds += session.DurationSeconds;
                                goalStat.PomodoroCount++;
                                break;
                            case TimerSessionType.PomodoroBreak:
                                goalStat.PomodoroBreakSeconds += session.DurationSeconds;
                                break;
                            case TimerSessionType.Stopwatch:
                                goalStat.StopwatchSeconds += session.DurationSeconds;
                                goalStat.FragmentCount++;
                                break;
                            case TimerSessionType.Countdown:
                                goalStat.CountdownSeconds += session.DurationSeconds;
                                goalStat.FragmentCount++;
                                break;
                        }
                    }
                }
                else
                {
                    stats.BreakSessions++;
                    stats.TotalBreakSeconds += session.DurationSeconds;

                    if (!string.IsNullOrEmpty(session.GoalTitle))
                    {
                        if (!stats.GoalStats.TryGetValue(session.GoalTitle, out var goalStat))
                        {
                            goalStat = new GoalReportStat { GoalTitle = session.GoalTitle };
                            stats.GoalStats[session.GoalTitle] = goalStat;
                        }
                        goalStat.TotalSeconds += session.DurationSeconds;

                        if (session.TimerType == TimerSessionType.PomodoroBreak)
                        {
                            goalStat.PomodoroBreakSeconds += session.DurationSeconds;
                        }
                    }
                }
            }

            return stats;
        }

        /// <summary>获取日报</summary>
        public static async Task<ReportStats> GetDailyReportAsync(DateTime date)
        {
            var startDate = date.Date;
            var endDate = date.Date.AddDays(1).AddSeconds(-1);
            var stats = await GetReportStatsAsync(startDate, endDate);
            await EnrichReportWithGoalInfoAsync(stats);
            return stats;
        }

        /// <summary>获取周报</summary>
        public static async Task<ReportStats> GetWeeklyReportAsync(DateTime date)
        {
            var dayOfWeek = (int)date.DayOfWeek;
            var startDate = date.Date.AddDays(-dayOfWeek);
            var endDate = startDate.AddDays(7).AddSeconds(-1);
            var stats = await GetReportStatsAsync(startDate, endDate);
            await EnrichReportWithGoalInfoAsync(stats);
            return stats;
        }

        /// <summary>获取月报</summary>
        public static async Task<ReportStats> GetMonthlyReportAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddSeconds(-1);
            var stats = await GetReportStatsAsync(startDate, endDate);
            await EnrichReportWithGoalInfoAsync(stats);
            return stats;
        }

        /// <summary>获取年报</summary>
        public static async Task<ReportStats> GetYearlyReportAsync(int year)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = startDate.AddYears(1).AddSeconds(-1);
            var stats = await GetReportStatsAsync(startDate, endDate);
            await EnrichReportWithGoalInfoAsync(stats);
            return stats;
        }

        public static async Task EnrichReportWithGoalInfoAsync(ReportStats stats)
        {
            var goals = await DataStorageService.LoadGoalsAsync();

            foreach (var kvp in stats.GoalStats)
            {
                var goalStat = kvp.Value;
                var goal = FindGoalByTitle(goals, goalStat.GoalTitle);

                if (goal != null)
                {
                    goalStat.Status = goal.Status;
                    goalStat.Type = goal.Type;
                    ApplyLatestStatusInRange(goalStat, goal, stats.StartDate, stats.EndDate);
                    goalStat.RecurringCompletions = CountCompletionsInRange(goal, stats.StartDate, stats.EndDate);
                }
            }

            // 补充报告期内有状态变更但无 session 记录的目标
            var goalsWithStatusInRange = FindGoalsWithStatusInRange(goals, stats.StartDate, stats.EndDate);
            foreach (var (title, (goal, statusType, timestamp)) in goalsWithStatusInRange)
            {
                if (!stats.GoalStats.ContainsKey(title))
                {
                    var goalStat = new GoalReportStat
                    {
                        GoalTitle = goal.Title,
                        Status = goal.Status,
                        Type = goal.Type,
                    };
                    goalStat.IsCompleted = statusType == GoalStatus.Completed;
                    goalStat.IsFailed = statusType == GoalStatus.Failed;
                    goalStat.IsPending = statusType == GoalStatus.Pending;
                    goalStat.RecurringCompletions = CountCompletionsInRange(goal, stats.StartDate, stats.EndDate);
                    stats.GoalStats[title] = goalStat;
                }
            }

            int activeGoalCount = stats.GoalStats.Values.Count(g => g.FocusSessions > 0);
            int completedGoalCount = stats.GoalStats.Values.Count(g => g.IsCompleted);

            foreach (var kvp in stats.GoalStats)
            {
                kvp.Value.ActiveGoalCount = activeGoalCount;
                kvp.Value.CompletedGoalCount = completedGoalCount;
            }
        }

        // 从 StatusHistory 中找到报告期内最新的状态变更，设置到 goalStat 上
        private static void ApplyLatestStatusInRange(GoalReportStat goalStat, Goal goal, DateTime start, DateTime end)
        {
            var latest = GetLatestStatusChangeInRange(goal, start, end);

            goalStat.IsCompleted = latest?.Status == GoalStatus.Completed;
            goalStat.IsFailed = latest?.Status == GoalStatus.Failed;
            goalStat.IsPending = latest?.Status == GoalStatus.Pending;
        }

        // 从 StatusHistory 中获取报告期内最新的非 Active 状态变更
        private static StatusChange? GetLatestStatusChangeInRange(Goal goal, DateTime start, DateTime end)
        {
            StatusChange? latest = null;
            foreach (var change in goal.StatusHistory)
            {
                if (change.Status == GoalStatus.Active)
                    continue;
                if (change.Timestamp >= start && change.Timestamp <= end)
                {
                    if (latest == null || change.Timestamp > latest.Timestamp)
                        latest = change;
                }
            }
            return latest;
        }

        // 统计报告期内"完成一次"的次数（仅统计 IsFinalCompletion == false 的记录）
        private static int CountCompletionsInRange(Goal goal, DateTime start, DateTime end)
        {
            return goal.StatusHistory.Count(c =>
                c.Status == GoalStatus.Completed
                && !c.IsFinalCompletion
                && c.Timestamp >= start
                && c.Timestamp <= end);
        }

        // 查找报告期内有状态变更的所有目标（含子目标），返回每个目标的最新状态
        private static Dictionary<string, (Goal goal, GoalStatus statusType, DateTime timestamp)> FindGoalsWithStatusInRange(List<Goal> goals, DateTime start, DateTime end)
        {
            var result = new Dictionary<string, (Goal, GoalStatus, DateTime)>();
            CollectGoalsWithStatusInRange(goals, start, end, result);
            return result;
        }

        private static void CollectGoalsWithStatusInRange(List<Goal> goals, DateTime start, DateTime end, Dictionary<string, (Goal goal, GoalStatus statusType, DateTime timestamp)> result)
        {
            foreach (var goal in goals)
            {
                if (goal.Id == "global-goal")
                    continue;

                var latest = GetLatestStatusChangeInRange(goal, start, end);
                if (latest != null)
                {
                    if (!result.TryGetValue(goal.Title, out var existing) || latest.Timestamp > existing.timestamp)
                        result[goal.Title] = (goal, latest.Status, latest.Timestamp);
                }

                CollectGoalsWithStatusInRange(goal.Children, start, end, result);
            }
        }

        private static Goal? FindGoalByTitle(List<Goal> goals, string title)
        {
            foreach (var goal in goals)
            {
                if (goal.Title == title) return goal;
                var found = FindGoalByTitle(goal.Children, title);
                if (found != null) return found;
            }
            return null;
        }

    }

    /// <summary>报告统计结果</summary>
    public class ReportStats
    {
        /// <summary>开始日期</summary>
        public DateTime StartDate { get; set; }
        /// <summary>结束日期</summary>
        public DateTime EndDate { get; set; }
        /// <summary>番茄钟会话次数</summary>
        public int PomodoroSessions { get; set; }
        /// <summary>番茄钟总时间（秒）</summary>
        public int TotalPomodoroSeconds { get; set; }
        /// <summary>碎片会话次数</summary>
        public int FragmentSessions { get; set; }
        /// <summary>碎片总时间（秒）</summary>
        public int TotalFragmentSeconds { get; set; }
        /// <summary>休息会话次数</summary>
        public int BreakSessions { get; set; }
        /// <summary>休息总时间（秒）</summary>
        public int TotalBreakSeconds { get; set; }
        /// <summary>各目标统计</summary>
        public Dictionary<string, GoalReportStat> GoalStats { get; set; } = new Dictionary<string, GoalReportStat>();
    }

    /// <summary>报告中的目标统计</summary>
    public class GoalReportStat
    {
        /// <summary>目标标题</summary>
        public string GoalTitle { get; set; } = string.Empty;
        /// <summary>专注会话次数</summary>
        public int FocusSessions { get; set; }
        /// <summary>总时间（秒）</summary>
        public int TotalSeconds { get; set; }
        /// <summary>番茄专注时间（秒）</summary>
        public int PomodoroFocusSeconds { get; set; }
        /// <summary>番茄休息时间（秒）</summary>
        public int PomodoroBreakSeconds { get; set; }
        /// <summary>秒表时间（秒）</summary>
        public int StopwatchSeconds { get; set; }
        /// <summary>倒计时时间（秒）</summary>
        public int CountdownSeconds { get; set; }
        /// <summary>番茄钟次数</summary>
        public int PomodoroCount { get; set; }
        /// <summary>碎片次数</summary>
        public int FragmentCount { get; set; }
        /// <summary>是否已完成</summary>
        public bool IsCompleted { get; set; }
        /// <summary>是否已失败</summary>
        public bool IsFailed { get; set; }
        /// <summary>是否已搁置</summary>
        public bool IsPending { get; set; }
        /// <summary>目标状态</summary>
        public GoalStatus? Status { get; set; }
        /// <summary>目标类型</summary>
        public GoalType? Type { get; set; }
        /// <summary>完成时间</summary>
        public DateTime? CompletedAt { get; set; }
        /// <summary>失败时间</summary>
        public DateTime? FailedAt { get; set; }
        /// <summary>搁置时间</summary>
        public DateTime? PendingAt { get; set; }
        /// <summary>活跃目标数</summary>
        public int ActiveGoalCount { get; set; }
        /// <summary>完成目标数</summary>
        public int CompletedGoalCount { get; set; }
        /// <summary>重复完成次数</summary>
        public int RecurringCompletions { get; set; }
    }

    /// <summary>每日详细统计</summary>
    public class DayDetailStats
    {
        /// <summary>日期</summary>
        public DateTime Date { get; set; }
        /// <summary>专注会话次数</summary>
        public int FocusSessions { get; set; }
        /// <summary>专注总时间（秒）</summary>
        public int TotalFocusSeconds { get; set; }
        /// <summary>碎片会话次数</summary>
        public int FragmentSessions { get; set; }
        /// <summary>碎片总时间（秒）</summary>
        public int TotalFragmentSeconds { get; set; }
        /// <summary>完成目标数</summary>
        public int CompletedGoals { get; set; }
    }

    /// <summary>目标统计信息</summary>
    public class GoalStats
    {
        /// <summary>目标ID</summary>
        public string? GoalId { get; set; }
        /// <summary>目标标题</summary>
        public string? GoalTitle { get; set; }
        /// <summary>总时间（秒）</summary>
        public int TotalSeconds { get; set; }
        /// <summary>番茄专注时间（秒）</summary>
        public int PomodoroFocusSeconds { get; set; }
        /// <summary>番茄休息时间（秒）</summary>
        public int PomodoroBreakSeconds { get; set; }
        /// <summary>秒表时间（秒）</summary>
        public int StopwatchSeconds { get; set; }
        /// <summary>倒计时时间（秒）</summary>
        public int CountdownSeconds { get; set; }
        /// <summary>番茄钟次数</summary>
        public int PomodoroCount { get; set; }
    }

    /// <summary>目标活动摘要（区分番茄钟和碎片次数）</summary>
    public class GoalActivitySummary
    {
        public string? GoalTitle { get; set; }
        public int PomodoroCount { get; set; }
        public int FragmentCount { get; set; }
    }
}