using System;
using System.Collections.Generic;
using 积微.Models;

namespace 积微.Services
{
    /// <summary>目标与计时器关联管理器</summary>
    public class GoalTimerManager
    {
        private static GoalTimerManager? _instance;
        /// <summary>单例实例</summary>
        public static GoalTimerManager Instance => _instance ??= new GoalTimerManager();

        /// <summary>番茄钟计时器类型标识</summary>
        public const string TimerTypePomodoro = "Pomodoro";
        /// <summary>普通计时器类型标识</summary>
        public const string TimerTypeNormal = "Normal";

        private readonly Dictionary<string, string?> _timerCurrentGoals = new Dictionary<string, string?>();
        private readonly Dictionary<string, bool> _timerIsActive = new Dictionary<string, bool>();

        /// <summary>目标变更事件</summary>
        public event EventHandler<GoalChangedEventArgs>? GoalChanged;

        private GoalTimerManager()
        {
            _timerCurrentGoals[TimerTypePomodoro] = null;
            _timerCurrentGoals[TimerTypeNormal] = null;
            _timerIsActive[TimerTypePomodoro] = false;
            _timerIsActive[TimerTypeNormal] = false;
        }

        /// <summary>获取指定目标当前活跃且正在运行的计时器类型</summary>
        public string? GetActiveAndRunningTimerForGoal(string goalId)
        {
            if (string.IsNullOrEmpty(goalId))
                return null;

            foreach (var kvp in _timerCurrentGoals)
            {
                if (kvp.Value == goalId && _timerIsActive.TryGetValue(kvp.Key, out bool isActive) && isActive)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        /// <summary>获取指定计时器类型的当前关联目标ID</summary>
        public string? GetCurrentGoalForTimer(string timerType)
        {
            return _timerCurrentGoals.TryGetValue(timerType, out var goalId) ? goalId : null;
        }

        /// <summary>设置计时器关联的目标</summary>
        public void SetGoalForTimer(Goal? goal, string timerType)
        {
            string? oldGoalId = _timerCurrentGoals[timerType];
            string? newGoalId = goal?.Id;

            if (oldGoalId == newGoalId)
                return;

            _timerCurrentGoals[timerType] = newGoalId;
            OnGoalChanged(timerType, goal);
        }

        /// <summary>设置计时器活跃状态</summary>
        public void SetTimerActive(string timerType, bool isActive)
        {
            _timerIsActive[timerType] = isActive;
        }

        /// <summary>获取计时器是否活跃</summary>
        public bool IsTimerActive(string timerType)
        {
            return _timerIsActive.TryGetValue(timerType, out bool isActive) && isActive;
        }

        /// <summary>触发目标变更事件</summary>
        protected virtual void OnGoalChanged(string timerType, Goal? goal)
        {
            GoalChanged?.Invoke(this, new GoalChangedEventArgs(timerType, goal));
        }
    }

    /// <summary>目标变更事件参数</summary>
    public class GoalChangedEventArgs : EventArgs
    {
        /// <summary>计时器类型</summary>
        public string TimerType { get; }
        /// <summary>关联目标</summary>
        public Goal? Goal { get; }

        /// <summary>构造目标变更事件参数</summary>
        public GoalChangedEventArgs(string timerType, Goal? goal)
        {
            TimerType = timerType;
            Goal = goal;
        }
    }
}