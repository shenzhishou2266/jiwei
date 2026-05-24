using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using 积微.Models;
namespace 积微.Services
{
    /// <summary>番茄钟计时器服务，支持工作/休息会话切换和自动轮换</summary>
    public class FocusSessionService : INotifyPropertyChanged
    {
        private static FocusSessionService? _instance;
        /// <summary>单例实例</summary>
        public static FocusSessionService Instance => _instance ??= new FocusSessionService();

        private int _minutes;
        private int _seconds;
        private bool _isActive;
        private bool _isWorkSession;
        private int _completedSessions;
        private Goal? _currentGoal;
        private DispatcherTimer _timer;

        private int _sessionStartSeconds;
        private bool _sessionStarted;

        /// <summary>属性变更事件</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>计时器完成事件</summary>
        public event EventHandler? TimerCompleted;
        /// <summary>计时器停止事件</summary>
        public event EventHandler? TimerStopped;

        /// <summary>分钟</summary>
        public int Minutes
        {
            get => _minutes;
            private set
            {
                _minutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeDisplay));
            }
        }

        /// <summary>秒</summary>
        public int Seconds
        {
            get => _seconds;
            private set
            {
                _seconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeDisplay));
            }
        }

        /// <summary>时间显示</summary>
        public string TimeDisplay => $"{Minutes:D2}:{Seconds:D2}";

        /// <summary>是否正在运行</summary>
        public bool IsActive
        {
            get => _isActive;
            private set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }

        /// <summary>是否为工作会话</summary>
        public bool IsWorkSession
        {
            get => _isWorkSession;
            private set
            {
                _isWorkSession = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SessionDisplay));
            }
        }

        /// <summary>会话类型显示文本</summary>
        public string SessionDisplay => IsWorkSession ? "Focus Time" : "Break Time";

        /// <summary>已完成会话次数</summary>
        public int CompletedSessions
        {
            get => _completedSessions;
            private set
            {
                _completedSessions = value;
                OnPropertyChanged();
            }
        }

        /// <summary>当前关联目标</summary>
        public Goal? CurrentGoal
        {
            get => _currentGoal;
            set
            {
                _currentGoal = value;
                GoalTimerManager.Instance.SetGoalForTimer(value, GoalTimerManager.TimerTypePomodoro);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentGoalDisplay));
            }
        }

        /// <summary>当前目标显示文本（全局目标时为空）</summary>
        public string CurrentGoalDisplay
        {
            get
            {
                // 如果是全局目标或没有目标，则不显示名称
                if (_currentGoal != null && _currentGoal.Id == "global-goal")
                    return "";
                return _currentGoal?.Title ?? "";
            }
        }

        /// <summary>进度（0-1）</summary>
        public double Progress
        {
            get
            {
                var settings = SettingsManager.Current;
                double totalSeconds = IsWorkSession ? settings.WorkDuration * 60 : settings.BreakDuration * 60;
                double remainingSeconds = Minutes * 60 + Seconds;
                return (totalSeconds - remainingSeconds) / totalSeconds;
            }
        }

        private FocusSessionService()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Current;
            _minutes = settings.WorkDuration;
            _seconds = 0;
            _isWorkSession = true;
            _completedSessions = 0;
        }

        /// <summary>更新设置（在非运行状态下生效）</summary>
        public void UpdateSettings()
        {
            var settings = SettingsManager.Current;
            if (!IsActive)
            {
                _minutes = IsWorkSession ? settings.WorkDuration : settings.BreakDuration;
                _seconds = 0;
                OnPropertyChanged(nameof(TimeDisplay));
                OnPropertyChanged(nameof(Progress));
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (_seconds == 0)
            {
                if (_minutes == 0)
                {
                    StopTimer();
                    try { await HandleTimerCompletion(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Timer completion error: {ex.Message}"); }
                }
                else
                {
                    _minutes--;
                    _seconds = 59;
                    OnPropertyChanged(nameof(TimeDisplay));
                    OnPropertyChanged(nameof(Progress));
                }
            }
            else
            {
                _seconds--;
                OnPropertyChanged(nameof(TimeDisplay));
                OnPropertyChanged(nameof(Progress));
            }
        }

        private async System.Threading.Tasks.Task HandleTimerCompletion()
        {
            var settings = SettingsManager.Current;
            int totalDuration = IsWorkSession ? settings.WorkDuration : settings.BreakDuration;

            if (settings.NotificationSoundEnabled)
            {
                PlayCompletionSound();
            }

            if (CurrentGoal != null)
            {
                string sessionType = IsWorkSession ? "工作" : "休息";
                CurrentGoal.AddTimelineEntry($"完成了一个{sessionType}番茄钟，时长：{totalDuration}分钟");
                if (IsWorkSession)
                {
                    CurrentGoal.AddElapsedSeconds(totalDuration * 60);
                }
                await DataStorageService.SaveGoalsAsync(DataStorageService.GoalsProvider?.Invoke() ?? new List<Goal>());
            }

            if (IsWorkSession)
            {
                var sessionRecord = new SessionRecord(
                    DateTime.Now.AddSeconds(-totalDuration * 60),
                    DateTime.Now,
                    SessionType.Focus,
                    TimerSessionType.PomodoroFocus,
                    CurrentGoal?.Id,
                    CurrentGoal?.Title);
                await StatisticsService.AddSessionAsync(sessionRecord);
            }

            _sessionStarted = false;
            _sessionStartSeconds = 0;

            if (IsWorkSession)
            {
                _completedSessions++;
                if (_completedSessions % settings.SessionsBeforeLongBreak == 0)
                {
                    _minutes = settings.LongBreakDuration;
                }
                else
                {
                    _minutes = settings.BreakDuration;
                }
                IsWorkSession = false;
            }
            else
            {
                _minutes = settings.WorkDuration;
                IsWorkSession = true;
            }
            _seconds = 0;
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(Progress));

            TimerCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void PlayCompletionSound()
        {
            try
            {
                var settings = SettingsManager.Current;
                if (settings.NotificationSoundManager != null)
                {
                    var notificationSound = settings.NotificationSoundManager.GetNotificationSound(settings.NotificationSoundName);
                    if (notificationSound != null)
                    {
                        settings.NotificationSoundManager.PlayWithNewPlayer(notificationSound);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>切换播放/暂停状态</summary>
        public void TogglePlayPause()
        {
            if (IsActive)
            {
                StopTimer();
            }
            else
            {
                StartTimer();
            }
        }

        /// <summary>开始计时</summary>
        public void StartTimer()
        {
            if (!IsActive)
            {
                if (!_sessionStarted)
                {
                    var settings = SettingsManager.Current;
                    int totalDurationSeconds = IsWorkSession ? settings.WorkDuration * 60 : settings.BreakDuration * 60;
                    int currentRemainingSeconds = _minutes * 60 + _seconds;
                    _sessionStartSeconds = totalDurationSeconds - currentRemainingSeconds;
                    _sessionStarted = true;
                }
                IsActive = true;
                GoalTimerManager.Instance.SetTimerActive(GoalTimerManager.TimerTypePomodoro, true);
                _timer.Start();
            }
        }

        /// <summary>停止计时</summary>
        public void StopTimer()
        {
            if (IsActive)
            {
                IsActive = false;
                GoalTimerManager.Instance.SetTimerActive(GoalTimerManager.TimerTypePomodoro, false);
                _timer.Stop();
            }
        }

        /// <summary>停止计时并记录会话</summary>
        public async void StopTimerWithRecord()
        {
            try
            {
                if (!_sessionStarted && !IsActive)
                {
                    return;
                }

                var settings = SettingsManager.Current;
                int totalDuration = IsWorkSession ? settings.WorkDuration : settings.BreakDuration;
                int totalDurationSeconds = totalDuration * 60;
                int totalRemainingSeconds = _minutes * 60 + _seconds;
                int actualCompletedSeconds = totalDurationSeconds - totalRemainingSeconds;

                if (_sessionStarted)
                {
                    actualCompletedSeconds = totalDurationSeconds - totalRemainingSeconds - _sessionStartSeconds;
                }

                int actualCompletedMinutes = actualCompletedSeconds / 60;
                int actualCompletedSecondsRemainder = actualCompletedSeconds % 60;

                StopTimer();

                if (CurrentGoal != null && actualCompletedSeconds > 0)
                {
                    string sessionType = IsWorkSession ? "工作" : "休息";
                    string durationText = $"{actualCompletedMinutes}分{actualCompletedSecondsRemainder}秒";
                    CurrentGoal.AddTimelineEntry($"停止了一个{sessionType}番茄钟，实际完成时长：{durationText}");
                    if (IsWorkSession)
                    {
                        CurrentGoal.AddElapsedSeconds(actualCompletedSeconds);
                    }
                    await DataStorageService.SaveGoalsAsync(DataStorageService.GoalsProvider?.Invoke() ?? new List<Goal>());

                    if (IsWorkSession)
                    {
                        var sessionRecord = new SessionRecord(
                            DateTime.Now.AddSeconds(-actualCompletedSeconds),
                            DateTime.Now,
                            SessionType.Focus,
                            TimerSessionType.Countdown,
                            CurrentGoal?.Id,
                            CurrentGoal?.Title);
                        await StatisticsService.AddSessionAsync(sessionRecord);
                    }
                }

                _sessionStarted = false;
                _sessionStartSeconds = 0;
                TimerStopped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StopTimerWithRecord error: {ex.Message}");
            }
        }

        /// <summary>重置计时器</summary>
        public void ResetTimer()
        {
            StopTimer();
            _sessionStarted = false;
            _sessionStartSeconds = 0;
            var settings = SettingsManager.Current;
            _minutes = IsWorkSession ? settings.WorkDuration : settings.BreakDuration;
            _seconds = 0;
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(Progress));
        }

        /// <summary>切换到工作会话</summary>
        public void SwitchToWork()
        {
            StopTimer();
            _sessionStarted = false;
            _sessionStartSeconds = 0;
            var settings = SettingsManager.Current;
            IsWorkSession = true;
            _minutes = settings.WorkDuration;
            _seconds = 0;
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(Progress));
        }

        /// <summary>切换到休息会话</summary>
        public void SwitchToBreak()
        {
            StopTimer();
            _sessionStarted = false;
            _sessionStartSeconds = 0;
            var settings = SettingsManager.Current;
            IsWorkSession = false;
            _minutes = settings.BreakDuration;
            _seconds = 0;
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(Progress));
        }

        /// <summary>触发属性变更事件</summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}