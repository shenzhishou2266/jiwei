using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using 积微.Models;
using 积微.Services.Audio;
namespace 积微.Services
{
    /// <summary>通用计时器服务，支持秒表和倒计时模式</summary>
    public class TimerService : INotifyPropertyChanged
    {
        private static TimerService? _instance;
        /// <summary>单例实例（从 DI 容器解析）</summary>
        public static TimerService Instance => _instance ??= AppServices.Provider.GetRequiredService<TimerService>();

        private int _days;
        private int _hours;
        private int _minutes;
        private int _seconds;
        private bool _isActive;
        private bool _isStopwatchMode = true;
        private int _countdownDays;
        private int _countdownHours;
        private int _countdownMinutes;
        private int _countdownSeconds;
        private Goal? _currentGoal;
        private DispatcherTimer _timer;
        private readonly SessionRecorder _sessionRecorder;
        private readonly GoalTimerManager _goalTimerManager;

        private int _startDays;
        private int _startHours;
        private int _startMinutes;
        private int _startSeconds;
        private int _sessionStartDays;
        private int _sessionStartHours;
        private int _sessionStartMinutes;
        private int _sessionStartSeconds;
        private bool _sessionStarted;

        /// <summary>是否有进行中的会话（区分暂停 vs 未开始/已结束）</summary>
        public bool IsSessionStarted
        {
            get => _sessionStarted;
            private set
            {
                _sessionStarted = value;
                OnPropertyChanged();
            }
        }

        /// <summary>属性变更事件</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>天</summary>
        public int Days
        {
            get => _days;
            private set
            {
                _days = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeDisplay));
                OnPropertyChanged(nameof(ShortTimeDisplay));
            }
        }

        /// <summary>小时</summary>
        public int Hours
        {
            get => _hours;
            private set
            {
                _hours = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeDisplay));
                OnPropertyChanged(nameof(ShortTimeDisplay));
            }
        }

        /// <summary>分钟</summary>
        public int Minutes
        {
            get => _minutes;
            private set
            {
                _minutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeDisplay));
                OnPropertyChanged(nameof(ShortTimeDisplay));
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
                OnPropertyChanged(nameof(ShortTimeDisplay));
            }
        }

        /// <summary>时间显示（含天数）</summary>
        public string TimeDisplay => $"{Days:D3}:{Hours:D2}:{Minutes:D2}:{Seconds:D2}";

        /// <summary>时间短显示（不含天数）</summary>
        public string ShortTimeDisplay => Hours > 0 ? $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}" : $"{Minutes:D2}:{Seconds:D2}";

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

        /// <summary>是否为秒表模式</summary>
        public bool IsStopwatchMode
        {
            get => _isStopwatchMode;
            private set
            {
                _isStopwatchMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModeDisplay));
            }
        }

        /// <summary>模式显示文本</summary>
        public string ModeDisplay => IsStopwatchMode ? "秒表" : "倒计时";

        /// <summary>当前关联目标</summary>
        public Goal? CurrentGoal
        {
            get => _currentGoal;
            set
            {
                _currentGoal = value;
                _goalTimerManager.SetGoalForTimer(value, GoalTimerManager.TimerTypeNormal);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentGoalDisplay));

                // 当设置了目标且当前是秒表模式时，自动从目标的 TotalElapsedSeconds 来设置计时器的当前时间
                if (value != null && IsStopwatchMode && !IsActive && value.Id != "global-goal")
                {
                    SetTimeFromTotalSeconds(value.TotalElapsedSeconds);
                    NotifyTimeChanged();
                }
                // 全局目标：秒表从0开始，倒计时从默认时长开始
                else if (value != null && value.Id == "global-goal" && !IsActive)
                {
                    if (IsStopwatchMode)
                    {
                        _days = 0; _hours = 0; _minutes = 0; _seconds = 0;
                    }
                    else
                    {
                        SyncDisplayToCountdown();
                    }
                    NotifyTimeChanged();
                }
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

        public TimerService(SessionRecorder sessionRecorder, GoalTimerManager goalTimerManager)
        {
            _sessionRecorder = sessionRecorder;
            _goalTimerManager = goalTimerManager;
            LoadCountdownFromSettings();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (IsStopwatchMode)
            {
                _seconds++;
                if (_seconds == 60)
                {
                    _seconds = 0;
                    _minutes++;
                    if (_minutes == 60)
                    {
                        _minutes = 0;
                        _hours++;
                        if (_hours == 24)
                        {
                            _hours = 0;
                            _days++;
                        }
                    }
                }
            }
            else
            {
                if (_seconds == 0)
                {
                    if (_minutes == 0)
                    {
                        if (_hours == 0)
                        {
                            if (_days == 0)
                            {
                                try { await HandleCountdownCompletion(); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Countdown completion error: {ex.Message}"); }
                                return;
                            }
                            _days--;
                            _hours = 23;
                            _minutes = 59;
                            _seconds = 59;
                        }
                        else
                        {
                            _hours--;
                            _minutes = 59;
                            _seconds = 59;
                        }
                    }
                    else
                    {
                        _minutes--;
                        _seconds = 59;
                    }
                }
                else
                {
                    _seconds--;
                }
            }
            NotifyTimeChanged();
        }

        private async System.Threading.Tasks.Task HandleCountdownCompletion()
        {
            StopTimer();

            var settings = SettingsManager.Current;
            if (settings.NotificationSoundEnabled)
            {
                PlayCompletionSound();
            }

            if (CurrentGoal != null)
            {
                // 使用会话开始时间来计算总时长
                int totalElapsedSeconds = _sessionStartDays * 86400 + _sessionStartHours * 3600 + _sessionStartMinutes * 60 + _sessionStartSeconds;

                // 如果会话还没开始（直接完成），则使用_start时间
                if (!IsSessionStarted)
                {
                    totalElapsedSeconds = _startDays * 86400 + _startHours * 3600 + _startMinutes * 60 + _startSeconds;
                }

                await _sessionRecorder.RecordAsync(CurrentGoal, totalElapsedSeconds, TimerSessionType.Countdown, isStopwatchMode: false);
            }

            // 重置会话状态
            IsSessionStarted = false;
            _sessionStartDays = 0;
            _sessionStartHours = 0;
            _sessionStartMinutes = 0;
            _sessionStartSeconds = 0;
        }

        private string FormatDuration(int days, int hours, int minutes, int seconds)
        {
            string result = "";
            if (days > 0) result += $"{days}天";
            if (hours > 0) result += $"{hours}小时";
            if (minutes > 0) result += $"{minutes}分钟";
            if (seconds > 0 || string.IsNullOrEmpty(result)) result += $"{seconds}秒";
            return result;
        }

        private void PlayCompletionSound()
        {
            try
            {
                var settings = SettingsManager.Current;
                if (AudioServices.Notification != null)
                {
                    var notificationSound = AudioServices.Notification.GetNotificationSound(settings.NotificationSoundName);
                    if (notificationSound != null)
                    {
                        AudioServices.Notification.PlayWithNewPlayer(notificationSound);
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
                _startDays = _days;
                _startHours = _hours;
                _startMinutes = _minutes;
                _startSeconds = _seconds;

                // 如果是第一次开始整个会话，则设置会话开始时间
                if (!IsSessionStarted)
                {
                    _sessionStartDays = _days;
                    _sessionStartHours = _hours;
                    _sessionStartMinutes = _minutes;
                    _sessionStartSeconds = _seconds;
                    IsSessionStarted = true;
                }

                IsActive = true;
                _goalTimerManager.SetTimerActive(GoalTimerManager.TimerTypeNormal, true);
                _timer.Start();
            }
        }

        /// <summary>停止计时</summary>
        public void StopTimer()
        {
            if (IsActive)
            {
                IsActive = false;
                _goalTimerManager.SetTimerActive(GoalTimerManager.TimerTypeNormal, false);
                _timer.Stop();
            }
        }

        /// <summary>停止计时并记录会话</summary>
        public async void StopTimerWithRecord()
        {
            try
            {
                if (!IsActive && !IsSessionStarted) return;

                int elapsedSeconds;
                if (IsStopwatchMode)
                {
                    elapsedSeconds = _days * 86400 + _hours * 3600 + _minutes * 60 + _seconds -
                                     _sessionStartDays * 86400 - _sessionStartHours * 3600 - _sessionStartMinutes * 60 - _sessionStartSeconds;
                }
                else
                {
                    int totalStartSeconds = _sessionStartDays * 86400 + _sessionStartHours * 3600 + _sessionStartMinutes * 60 + _sessionStartSeconds;
                    int remainingSeconds = _days * 86400 + _hours * 3600 + _minutes * 60 + _seconds;
                    elapsedSeconds = totalStartSeconds - remainingSeconds;
                }

                StopTimer();

                if (CurrentGoal != null && elapsedSeconds > 0)
                {
                    await _sessionRecorder.RecordAsync(CurrentGoal, elapsedSeconds,
                        IsStopwatchMode ? TimerSessionType.Stopwatch : TimerSessionType.Countdown,
                        IsStopwatchMode);
                }

                // 重置会话标志，为下一次会话做准备
                IsSessionStarted = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StopTimerWithRecord error: {ex.Message}");
            }
        }

        /// <summary>将当前显示时间同步为倒计时设定值。</summary>
        private void SyncDisplayToCountdown()
        {
            _days = _countdownDays;
            _hours = _countdownHours;
            _minutes = _countdownMinutes;
            _seconds = _countdownSeconds;
        }

        /// <summary>从设置加载默认倒计时设定值。不负责同步显示，由调用方决定。</summary>
        private void LoadCountdownFromSettings()
        {
            var settings = SettingsManager.Current;
            _countdownDays = settings.CountdownDefaultDays;
            _countdownHours = settings.CountdownDefaultHours;
            _countdownMinutes = settings.CountdownDefaultMinutes;
            _countdownSeconds = settings.CountdownDefaultSeconds;
        }

        /// <summary>
        /// 重置计时器。
        /// 秒表：会话进行中重置为会话开始值（目标累积时长），结束后重置为 0。
        /// 倒计时：会话进行中重置为手动调整值，结束后重置为设置默认值。
        /// </summary>
        public void ResetTimer()
        {
            bool hadActiveSession = IsSessionStarted;
            int savedStartDays = _sessionStartDays;
            int savedStartHours = _sessionStartHours;
            int savedStartMinutes = _sessionStartMinutes;
            int savedStartSeconds = _sessionStartSeconds;

            StopTimer();
            IsSessionStarted = false;
            _sessionStartDays = _sessionStartHours = _sessionStartMinutes = _sessionStartSeconds = 0;

            if (IsStopwatchMode)
            {
                if (hadActiveSession)
                {
                    _days = savedStartDays;
                    _hours = savedStartHours;
                    _minutes = savedStartMinutes;
                    _seconds = savedStartSeconds;
                }
                else
                {
                    _days = _hours = _minutes = _seconds = 0;
                }
            }
            else if (hadActiveSession)
            {
                SyncDisplayToCountdown();
            }
            else
            {
                LoadCountdownFromSettings();
                SyncDisplayToCountdown();
            }

            NotifyTimeChanged();
        }

        /// <summary>切换到秒表模式</summary>
        public void SwitchToStopwatch()
        {
            StopTimer();
            IsSessionStarted = false;
            _sessionStartDays = _sessionStartHours = _sessionStartMinutes = _sessionStartSeconds = 0;
            IsStopwatchMode = true;

            // 如果有目标（非全局目标），从目标的累计时间开始
            if (CurrentGoal != null && CurrentGoal.Id != "global-goal")
            {
                SetTimeFromTotalSeconds(CurrentGoal.TotalElapsedSeconds);
            }
            else
            {
                _days = 0;
                _hours = 0;
                _minutes = 0;
                _seconds = 0;
            }

            NotifyTimeChanged();
        }

        /// <summary>切换到倒计时模式</summary>
        public void SwitchToCountdown()
        {
            StopTimer();
            IsSessionStarted = false;
            _sessionStartDays = _sessionStartHours = _sessionStartMinutes = _sessionStartSeconds = 0;
            IsStopwatchMode = false;
            SyncDisplayToCountdown();
            NotifyTimeChanged();
        }

        /// <summary>设置倒计时时间</summary>
        public void SetCountdownTime(int days, int hours, int minutes, int seconds)
        {
            _countdownDays = days;
            _countdownHours = hours;
            _countdownMinutes = minutes;
            _countdownSeconds = seconds;
            if (!IsStopwatchMode && !IsActive)
            {
                _days = days;
                _hours = hours;
                _minutes = minutes;
                _seconds = seconds;
                NotifyTimeChanged();
            }
        }

        /// <summary>设置当前显示时间</summary>
        public void SetCurrentTime(int days, int hours, int minutes, int seconds)
        {
            if (!IsActive)
            {
                _days = days;
                _hours = hours;
                _minutes = minutes;
                _seconds = seconds;
                NotifyTimeChanged();
            }
        }

        /// <summary>触发属性变更事件</summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>统一通知时间相关属性变更（Days/Hours/Minutes/Seconds/TimeDisplay/ShortTimeDisplay）。</summary>
        private void NotifyTimeChanged()
        {
            OnPropertyChanged(nameof(Days));
            OnPropertyChanged(nameof(Hours));
            OnPropertyChanged(nameof(Minutes));
            OnPropertyChanged(nameof(Seconds));
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(ShortTimeDisplay));
        }

        /// <summary>根据总秒数设置当前显示时间字段。</summary>
        private void SetTimeFromTotalSeconds(int totalSeconds)
        {
            _days = totalSeconds / 86400;
            int r = totalSeconds % 86400;
            _hours = r / 3600;
            r %= 3600;
            _minutes = r / 60;
            _seconds = r % 60;
        }
    }
}