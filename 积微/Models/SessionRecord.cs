using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace 积微.Models
{
    /// <summary>会话类型枚举</summary>
    public enum SessionType
    {
        /// <summary>专注</summary>
        Focus,
        /// <summary>休息</summary>
        Break
    }

    /// <summary>会话记录</summary>
    public class SessionRecord
    {
        /// <summary>记录ID</summary>
        public string Id { get; set; }
        /// <summary>开始时间</summary>
        public DateTime StartTime { get; set; }
        /// <summary>结束时间</summary>
        public DateTime EndTime { get; set; }
        /// <summary>持续时间（秒）</summary>
        public int DurationSeconds { get; set; }
        /// <summary>会话类型</summary>
        public SessionType Type { get; set; }
        /// <summary>计时器类型</summary>
        public TimerSessionType TimerType { get; set; }
        /// <summary>关联目标ID</summary>
        public string? GoalId { get; set; }
        /// <summary>关联目标标题</summary>
        public string? GoalTitle { get; set; }

        /// <summary>构造会话记录</summary>
        public SessionRecord()
        {
            Id = Guid.NewGuid().ToString();
            StartTime = DateTime.Now;
            EndTime = DateTime.Now;
            DurationSeconds = 0;
            Type = SessionType.Focus;
            TimerType = TimerSessionType.PomodoroFocus;
        }

        /// <summary>构造会话记录（向后兼容）</summary>
        public SessionRecord(DateTime startTime, DateTime endTime, SessionType type, string? goalId = null, string? goalTitle = null)
            : this()
        {
            StartTime = startTime;
            EndTime = endTime;
            DurationSeconds = (int)(endTime - startTime).TotalSeconds;
            Type = type;
            TimerType = type == SessionType.Focus ? TimerSessionType.PomodoroFocus : TimerSessionType.PomodoroBreak;
            GoalId = goalId;
            GoalTitle = goalTitle;
        }

        /// <summary>构造会话记录（指定计时器类型）</summary>
        public SessionRecord(DateTime startTime, DateTime endTime, SessionType type, TimerSessionType timerType, string? goalId = null, string? goalTitle = null)
            : this()
        {
            StartTime = startTime;
            EndTime = endTime;
            DurationSeconds = (int)(endTime - startTime).TotalSeconds;
            Type = type;
            TimerType = timerType;
            GoalId = goalId;
            GoalTitle = goalTitle;
        }

        /// <summary>持续时间</summary>
        [JsonIgnore]
        public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    }

    /// <summary>每日统计</summary>
    public class DailyStats
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
        /// <summary>休息会话次数</summary>
        public int BreakSessions { get; set; }
        /// <summary>休息总时间（秒）</summary>
        public int TotalBreakSeconds { get; set; }

        /// <summary>构造每日统计</summary>
        public DailyStats()
        {
            Date = DateTime.Today;
            FocusSessions = 0;
            TotalFocusSeconds = 0;
            FragmentSessions = 0;
            TotalFragmentSeconds = 0;
            BreakSessions = 0;
            TotalBreakSeconds = 0;
        }

        /// <summary>专注总时间</summary>
        [JsonIgnore]
        public TimeSpan TotalFocusTime => TimeSpan.FromSeconds(TotalFocusSeconds);

        /// <summary>碎片总时间</summary>
        [JsonIgnore]
        public TimeSpan TotalFragmentTime => TimeSpan.FromSeconds(TotalFragmentSeconds);

        /// <summary>休息总时间</summary>
        [JsonIgnore]
        public TimeSpan TotalBreakTime => TimeSpan.FromSeconds(TotalBreakSeconds);
    }
}
