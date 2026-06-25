using 积微.Models;

namespace 积微.Services
{
    /// <summary>会话记录器，封装计时完成后的记录和持久化流程。</summary>
    public class SessionRecorder
    {
        private readonly IGoalRepository _goalRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly Func<List<Goal>> _goalsProvider;

        public SessionRecorder(IGoalRepository goalRepo, ISessionRepository sessionRepo, Func<List<Goal>> goalsProvider)
        {
            _goalRepo = goalRepo;
            _sessionRepo = sessionRepo;
            _goalsProvider = goalsProvider;
        }

        /// <summary>记录一次计时完成。</summary>
        /// <param name="isStopRecord">true 表示手动停止记录，false 表示自动完成记录。</param>
        public async Task RecordAsync(Goal goal, int elapsedSeconds, TimerSessionType timerType, bool isStopwatchMode, bool isStopRecord = false)
        {
            if (elapsedSeconds <= 0)
                return;

            string durationText = FormatDuration(elapsedSeconds);
            string entryText = GetEntryText(timerType, isStopwatchMode, durationText, isStopRecord);

            goal.AddTimelineEntry(entryText);
            goal.AddElapsedSeconds(elapsedSeconds);

            await _goalRepo.SaveGoalsAsync(_goalsProvider());

            var sessionType = timerType == TimerSessionType.PomodoroBreak
                ? SessionType.Break
                : SessionType.Focus;

            var sessionRecord = new SessionRecord(
                DateTime.Now.AddSeconds(-elapsedSeconds),
                DateTime.Now,
                sessionType,
                timerType,
                goal.Id,
                goal.Title);
            await _sessionRepo.AddSessionAsync(sessionRecord);
        }

        private static string FormatDuration(int totalSeconds)
        {
            int days = totalSeconds / 86400;
            int hours = (totalSeconds % 86400) / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            string result = "";
            if (days > 0) result += $"{days}天";
            if (hours > 0) result += $"{hours}小时";
            if (minutes > 0) result += $"{minutes}分钟";
            if (seconds > 0 || string.IsNullOrEmpty(result)) result += $"{seconds}秒";
            return result;
        }

        private static string GetEntryText(TimerSessionType timerType, bool isStopwatchMode, string durationText, bool isStopRecord)
        {
            if (isStopRecord)
            {
                return timerType switch
                {
                    TimerSessionType.PomodoroFocus => $"停止了一个工作番茄钟，实际完成时长：{durationText}",
                    TimerSessionType.PomodoroBreak => $"停止了一个休息番茄钟，实际完成时长：{durationText}",
                    TimerSessionType.Countdown => $"倒计时记录，时长：{durationText}",
                    TimerSessionType.Stopwatch => $"计时器记录，时长：{durationText}",
                    _ => isStopwatchMode
                        ? $"计时器记录，时长：{durationText}"
                        : $"倒计时记录，时长：{durationText}"
                };
            }

            return timerType switch
            {
                TimerSessionType.PomodoroFocus => $"完成了一个工作番茄钟，时长：{durationText}",
                TimerSessionType.PomodoroBreak => $"完成了一个休息番茄钟，时长：{durationText}",
                TimerSessionType.Countdown => $"完成了一次倒计时，时长：{durationText}",
                TimerSessionType.Stopwatch => $"计时器记录，时长：{durationText}",
                _ => isStopwatchMode
                    ? $"计时器记录，时长：{durationText}"
                    : $"完成了一次倒计时，时长：{durationText}"
            };
        }
    }
}