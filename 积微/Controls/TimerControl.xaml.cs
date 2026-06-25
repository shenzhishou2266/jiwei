using System;
using System.Collections.Generic;
using System.Linq;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWInput = System.Windows.Input;
using SWM = System.Windows.Media;
using 积微.Models;
using 积微.Services;
using 积微.Views;
using 积微.Helpers;
using 积微.Services.Audio;

namespace 积微.Controls
{
    /// <summary>通用计时器控件，支持秒表和倒计时两种模式。</summary>
    public partial class TimerControl : SWC.UserControl
    {
        private readonly TimerService _timerService;
        /// <summary>获取或设置当前关联的目标。</summary>
        public Goal CurrentGoal { get; set; }

        private readonly SWM.Brush BlueBrush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 59, 130, 246));
        private readonly SWM.Brush Gray600Brush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 75, 85, 99));
        private readonly SWM.Brush WhiteBrush = SWM.Brushes.White;

        private bool _suppressTimerDisplayUpdate = false;
        private bool _suppressScrollerValueChanged = false;

        public TimerControl()
        {
            InitializeComponent();
            _timerService = TimerService.Instance;
            _timerService.PropertyChanged += TimerService_PropertyChanged;

            // 监听白噪音状态变化
            var settings = SettingsManager.Current;
            if (AudioServices.WhiteNoise is System.ComponentModel.INotifyPropertyChanged whiteNoiseManager)
            {
                whiteNoiseManager.PropertyChanged += WhiteNoiseManager_PropertyChanged;
            }

            Hours1.MaxValue = 2;
            Hours2.MaxValue = 9;
            Minutes1.MaxValue = 5;
            Seconds1.MaxValue = 5;

            Days1.SetValueWithoutAnimation(0);
            Days2.SetValueWithoutAnimation(0);
            Days3.SetValueWithoutAnimation(0);
            Hours1.SetValueWithoutAnimation(0);
            Hours2.SetValueWithoutAnimation(0);
            Minutes1.SetValueWithoutAnimation(0);
            Minutes2.SetValueWithoutAnimation(0);
            Seconds1.SetValueWithoutAnimation(0);
            Seconds2.SetValueWithoutAnimation(0);

            Loaded += (s, e) =>
            {
                // 延迟设置默认目标为全局目标，确保GoalsPage已经加载
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_timerService.CurrentGoal == null && GoalsPage.GlobalGoal != null)
                    {
                        _timerService.CurrentGoal = GoalsPage.GlobalGoal;
                    }
                }));
            };

            InitializeNumberScrollers();
            LoadSettings();
            UpdateTimerDisplay();
            UpdatePlayPauseIcon();
            UpdateWhiteNoiseIcon();
        }

        private void TimerService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(TimerService.TimeDisplay) ||
                e.PropertyName == nameof(TimerService.Days) ||
                e.PropertyName == nameof(TimerService.Hours) ||
                e.PropertyName == nameof(TimerService.Minutes) ||
                e.PropertyName == nameof(TimerService.Seconds))
                && !_suppressTimerDisplayUpdate)
            {
                UpdateTimerDisplay();
            }
            else if (e.PropertyName == nameof(TimerService.IsActive))
            {
                UpdatePlayPauseIcon();
                UpdateScrollersEditable();
            }
            else if (e.PropertyName == nameof(TimerService.IsSessionStarted))
            {
                UpdateScrollersEditable();
            }
            else if (e.PropertyName == nameof(TimerService.IsStopwatchMode))
            {
                UpdateModeButtons();
            }
            else if (e.PropertyName == nameof(TimerService.CurrentGoal))
            {
                CurrentGoal = _timerService.CurrentGoal;
                UpdateGoalDisplay();
            }
        }

        private void UserControl_Loaded(object sender, SW.RoutedEventArgs e)
        {
            this.Focus();
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var targetGoal = _timerService.CurrentGoal ?? GoalsPage.GlobalGoal;
                if (targetGoal != null)
                {
                    e.Handled = true;
                    var quickInputWindow = new QuickTimelineInputWindow(targetGoal);
                    quickInputWindow.Show();
                }
            }
        }


        private void LoadSettings()
        {
            var settings = SettingsManager.Current;
            WhiteNoiseButton.Visibility = settings.WhiteNoiseEnabled ? SW.Visibility.Visible : SW.Visibility.Collapsed;

            // Initialize countdown default time
            if (!_timerService.IsStopwatchMode && !_timerService.IsActive)
            {
                int defaultDays = settings.CountdownDefaultDays;
                int defaultHours = settings.CountdownDefaultHours;
                int defaultMinutes = settings.CountdownDefaultMinutes;
                int defaultSeconds = settings.CountdownDefaultSeconds;

                Days1.SetValueWithoutAnimation(defaultDays / 100);
                Days2.SetValueWithoutAnimation((defaultDays / 10) % 10);
                Days3.SetValueWithoutAnimation(defaultDays % 10);
                Hours1.SetValueWithoutAnimation(defaultHours / 10);
                Hours2.SetValueWithoutAnimation(defaultHours % 10);
                Minutes1.SetValueWithoutAnimation(defaultMinutes / 10);
                Minutes2.SetValueWithoutAnimation(defaultMinutes % 10);
                Seconds1.SetValueWithoutAnimation(defaultSeconds / 10);
                Seconds2.SetValueWithoutAnimation(defaultSeconds % 10);
                UpdateFromScrollers();
            }
        }

        /// <summary>从设置管理器同步更新控件状态。</summary>
        public void UpdateSettings()
        {
            var settings = SettingsManager.Current;
            WhiteNoiseButton.Visibility = settings.WhiteNoiseEnabled ? SW.Visibility.Visible : SW.Visibility.Collapsed;

            // Update countdown default time if in countdown mode
            if (!_timerService.IsStopwatchMode && !_timerService.IsActive)
            {
                int defaultDays = settings.CountdownDefaultDays;
                int defaultHours = settings.CountdownDefaultHours;
                int defaultMinutes = settings.CountdownDefaultMinutes;
                int defaultSeconds = settings.CountdownDefaultSeconds;

                Days1.SetValueWithoutAnimation(defaultDays / 100);
                Days2.SetValueWithoutAnimation((defaultDays / 10) % 10);
                Days3.SetValueWithoutAnimation(defaultDays % 10);
                Hours1.SetValueWithoutAnimation(defaultHours / 10);
                Hours2.SetValueWithoutAnimation(defaultHours % 10);
                Minutes1.SetValueWithoutAnimation(defaultMinutes / 10);
                Minutes2.SetValueWithoutAnimation(defaultMinutes % 10);
                Seconds1.SetValueWithoutAnimation(defaultSeconds / 10);
                Seconds2.SetValueWithoutAnimation(defaultSeconds % 10);
                UpdateFromScrollers();
            }
        }

        private void InitializeNumberScrollers()
        {
            SetScrollersMaxValues();

            Days1.ValueChanged += OnDay1ValueChanged;
            Days2.ValueChanged += OnDay2ValueChanged;
            Days3.ValueChanged += OnDay3ValueChanged;
            Hours1.ValueChanged += OnHour1ValueChanged;
            Hours2.ValueChanged += OnHour2ValueChanged;
            Minutes1.ValueChanged += OnMinute1ValueChanged;
            Minutes2.ValueChanged += OnMinute2ValueChanged;
            Seconds1.ValueChanged += OnSecond1ValueChanged;
            Seconds2.ValueChanged += OnSecond2ValueChanged;
        }

        private void SetScrollersMaxValues()
        {
            Hours1.MaxValue = 2;
            Hours2.MaxValue = Hours1.CurrentValue == 2 ? 3 : 9;
            Minutes1.MaxValue = 5;
            Seconds1.MaxValue = 5;
        }

        private void OnDay1ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnDay2ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnDay3ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnHour1ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
            {
                SetScrollersMaxValues();
                UpdateFromScrollers();
            }
        }

        private void OnHour2ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnMinute1ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnMinute2ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnSecond1ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void OnSecond2ValueChanged(object sender, int value)
        {
            if (!_timerService.IsActive && !_suppressScrollerValueChanged)
                UpdateFromScrollers();
        }

        private void UpdateFromScrollers()
        {
            int newDays = Days1.CurrentValue * 100 + Days2.CurrentValue * 10 + Days3.CurrentValue;
            int newHours = Hours1.CurrentValue * 10 + Hours2.CurrentValue;
            int newMinutes = Minutes1.CurrentValue * 10 + Minutes2.CurrentValue;
            int newSeconds = Seconds1.CurrentValue * 10 + Seconds2.CurrentValue;

            newHours = Math.Min(newHours, 23);
            newMinutes = Math.Min(newMinutes, 59);
            newSeconds = Math.Min(newSeconds, 59);

            _timerService.SetCurrentTime(newDays, newHours, newMinutes, newSeconds);

            if (!_timerService.IsStopwatchMode)
            {
                _timerService.SetCountdownTime(newDays, newHours, newMinutes, newSeconds);
            }

            int h1 = newHours / 10;
            int h2 = newHours % 10;
            int m1 = newMinutes / 10;
            int m2 = newMinutes % 10;
            int s1 = newSeconds / 10;
            int s2 = newSeconds % 10;

            if (Hours1.CurrentValue != h1) Hours1.SetValueWithoutAnimation(h1);
            if (Hours2.CurrentValue != h2) Hours2.SetValueWithoutAnimation(h2);
            if (Minutes1.CurrentValue != m1) Minutes1.SetValueWithoutAnimation(m1);
            if (Minutes2.CurrentValue != m2) Minutes2.SetValueWithoutAnimation(m2);
            if (Seconds1.CurrentValue != s1) Seconds1.SetValueWithoutAnimation(s1);
            if (Seconds2.CurrentValue != s2) Seconds2.SetValueWithoutAnimation(s2);
        }

        private void UpdateTimerDisplay()
        {
            int days = _timerService.Days;
            int hours = _timerService.Hours;
            int minutes = _timerService.Minutes;
            int seconds = _timerService.Seconds;

            int d1 = days / 100;
            int d2 = (days / 10) % 10;
            int d3 = days % 10;
            int h1 = hours / 10;
            int h2 = hours % 10;
            int m1 = minutes / 10;
            int m2 = minutes % 10;
            int s1 = seconds / 10;
            int s2 = seconds % 10;

            if (Days1.CurrentValue != d1) Days1.CurrentValue = d1;
            if (Days2.CurrentValue != d2) Days2.CurrentValue = d2;
            if (Days3.CurrentValue != d3) Days3.CurrentValue = d3;
            if (Hours1.CurrentValue != h1) Hours1.CurrentValue = h1;
            if (Hours2.CurrentValue != h2) Hours2.CurrentValue = h2;
            if (Minutes1.CurrentValue != m1) Minutes1.CurrentValue = m1;
            if (Minutes2.CurrentValue != m2) Minutes2.CurrentValue = m2;
            if (Seconds1.CurrentValue != s1) Seconds1.CurrentValue = s1;
            if (Seconds2.CurrentValue != s2) Seconds2.CurrentValue = s2;
        }

        /// <summary>按指定方向更新所有滚数字控件。内部屏蔽中间态 ValueChanged，避免逐位设值时的数据污染和动画竞争。</summary>
        private int GetTotalSeconds() =>
            _timerService.Days * 86400 + _timerService.Hours * 3600 + _timerService.Minutes * 60 + _timerService.Seconds;

        private void SetScrollersFromService(int oldTotalSeconds)
        {
            int newTotalSeconds = GetTotalSeconds();
            ScrollDirection direction = newTotalSeconds >= oldTotalSeconds ? ScrollDirection.Up : ScrollDirection.Down;

            _suppressScrollerValueChanged = true;

            int days = _timerService.Days;
            int hours = _timerService.Hours;
            int minutes = _timerService.Minutes;
            int seconds = _timerService.Seconds;

            // 天数位 MaxValue 始终为 9
            Days1.MaxValue = 9;
            Days2.MaxValue = 9;
            Days3.MaxValue = 9;
            Days1.SetValueWithScroll(days / 100, direction);
            Days2.SetValueWithScroll((days / 10) % 10, direction);
            Days3.SetValueWithScroll(days % 10, direction);

            // 先设小时十位，再根据新值设个位 MaxValue（避免被旧值截断）
            int hoursTens = hours / 10;
            int hoursOnes = hours % 10;
            Hours1.MaxValue = 2;
            Hours1.SetValueWithScroll(hoursTens, direction);
            Hours2.MaxValue = hoursTens == 2 ? 3 : 9;
            Hours2.SetValueWithScroll(hoursOnes, direction);

            // 分钟
            int minTens = minutes / 10;
            int minOnes = minutes % 10;
            Minutes1.MaxValue = 5;
            Minutes1.SetValueWithScroll(minTens, direction);
            Minutes2.SetValueWithScroll(minOnes, direction);

            // 秒
            int secTens = seconds / 10;
            int secOnes = seconds % 10;
            Seconds1.MaxValue = 5;
            Seconds1.SetValueWithScroll(secTens, direction);
            Seconds2.SetValueWithScroll(secOnes, direction);

            _suppressScrollerValueChanged = false;

            // 所有滚轮设完后，统一同步一次 TimerService（用最终的正确值）
            UpdateFromScrollers();
        }

        private void UpdatePlayPauseIcon()
        {
            if (_timerService.IsActive)
            {
                PlayPauseIcon.Data = SWM.Geometry.Parse("M8,4V20H10V4H8M14,4V20H16V4H14Z");
            }
            else
            {
                PlayPauseIcon.Data = SWM.Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
            }
        }

        private void UpdateModeButtons()
        {
            if (_timerService.IsStopwatchMode)
            {
                StopwatchButton.Background = BlueBrush;
                StopwatchButton.Foreground = WhiteBrush;
                CountdownButton.Background = WhiteBrush;
                CountdownButton.Foreground = Gray600Brush;
                SessionText.Text = "Stopwatch";
            }
            else
            {
                CountdownButton.Background = BlueBrush;
                CountdownButton.Foreground = WhiteBrush;
                StopwatchButton.Background = WhiteBrush;
                StopwatchButton.Foreground = Gray600Brush;
                SessionText.Text = "Countdown";
            }
        }

        private void UpdateGoalDisplay()
        {
            if (_timerService.CurrentGoal != null && _timerService.CurrentGoal.Id != "global-goal")
            {
                CurrentGoalText.Text = _timerService.CurrentGoal.Title;
                CurrentGoalText.Visibility = SW.Visibility.Visible;
            }
            else
            {
                CurrentGoalText.Text = "";
                CurrentGoalText.Visibility = SW.Visibility.Hidden;
            }
        }

        private void UpdateScrollersEditable()
        {
            bool editable = !_timerService.IsActive && !_timerService.IsSessionStarted;
            Days1.IsEditable = editable;
            Days2.IsEditable = editable;
            Days3.IsEditable = editable;
            Hours1.IsEditable = editable;
            Hours2.IsEditable = editable;
            Minutes1.IsEditable = editable;
            Minutes2.IsEditable = editable;
            Seconds1.IsEditable = editable;
            Seconds2.IsEditable = editable;
        }

        private void StopwatchButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _suppressTimerDisplayUpdate = true;
            int oldSeconds = GetTotalSeconds();
            _timerService.SwitchToStopwatch();
            SetScrollersFromService(oldSeconds);
            _suppressTimerDisplayUpdate = false;
            UpdateScrollersEditable();
            UpdateModeButtons();
        }

        private void CountdownButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _suppressTimerDisplayUpdate = true;
            int oldSeconds = GetTotalSeconds();
            _timerService.SwitchToCountdown();
            SetScrollersFromService(oldSeconds);
            _suppressTimerDisplayUpdate = false;
            UpdateScrollersEditable();
            UpdateModeButtons();
        }

        private void ResetButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _suppressTimerDisplayUpdate = true;
            int oldSeconds = GetTotalSeconds();
            _timerService.ResetTimer();
            SetScrollersFromService(oldSeconds);

            _suppressTimerDisplayUpdate = false;
            UpdateScrollersEditable();
        }

        private void PlayPauseButton_Click(object sender, SW.RoutedEventArgs e)
        {
            if (!_timerService.IsActive && _timerService.CurrentGoal != null && _timerService.CurrentGoal.Id != "global-goal")
            {
                string? conflictingTimer = GoalTimerManager.Instance.GetActiveAndRunningTimerForGoal(_timerService.CurrentGoal.Id);
                if (conflictingTimer != null && conflictingTimer != GoalTimerManager.TimerTypeNormal)
                {
                    var messageBox = new Views.MessageBoxWindow("提示", "番茄钟正在为同一目标计时，请先停止番茄钟后再开始计时器。");
                    messageBox.Owner = SW.Window.GetWindow(this);
                    messageBox.ShowDialog();
                    return;
                }
            }
            _timerService.TogglePlayPause();
        }

        private void StopButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _timerService.StopTimerWithRecord();
            UpdateScrollersEditable();
        }

        private void ModeSwitchButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var mainWindow = SW.Window.GetWindow(this) as MainWindow;
            mainWindow?.SwitchTimerMode();
        }

        private void SelectGoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var menu = new SWC.ContextMenu();
            AddGoalMenuItems(menu, null);
            menu.IsOpen = true;
        }

        private void AddGoalMenuItems(SWC.ItemsControl parentContainer, Goal parentGoal)
        {
            if (GoalsPage.Goals.Count == 0)
            {
                var goal1 = new Goal("学习 C# 编程");
                goal1.AddChild(new Goal("学习基础语法"));
                goal1.AddChild(new Goal("学习面向对象编程"));
                goal1.AddChild(new Goal("学习WPF框架"));

                var goal2 = new Goal("阅读技术书籍");
                goal2.AddChild(new Goal("阅读《C# 本质论》"));
                goal2.AddChild(new Goal("阅读《WPF 编程指南》"));

                var goal3 = new Goal("完成 WPF 项目");
                goal3.Complete();

                var goal4 = new Goal("减肥计划");
                goal4.Fail();

                GoalsPage.Goals.Add(goal1);
                GoalsPage.Goals.Add(goal2);
                GoalsPage.Goals.Add(goal3);
                GoalsPage.Goals.Add(goal4);
            }

            var goals = parentGoal == null ?
                GoalsPage.Goals.Where(g => g.Parent == null &&
                                          g.Status != GoalStatus.Completed &&
                                          g.Status != GoalStatus.Failed &&
                                          g.Status != GoalStatus.Pending &&
                                          g.Id != "global-goal").ToList() :
                parentGoal.Children.Where(g => g.Status != GoalStatus.Completed &&
                                              g.Status != GoalStatus.Failed &&
                                              g.Status != GoalStatus.Pending &&
                                              g.Id != "global-goal").ToList();

            if (goals.Count == 0)
            {
                var noGoalsItem = new SWC.MenuItem();
                noGoalsItem.Header = "没有正在进行的目标";
                noGoalsItem.IsEnabled = false;
                parentContainer.Items.Add(noGoalsItem);
                return;
            }

            var titleCount = GoalDisplayHelper.GetTitleCount(GoalsPage.Goals);

            foreach (var goal in goals)
            {
                var menuItem = new SWC.MenuItem();
                menuItem.Header = GoalDisplayHelper.GetGoalDisplayName(goal, titleCount);
                menuItem.Tag = goal;

                menuItem.PreviewMouseLeftButtonDown += (sender, e) =>
                {
                    var item = (SWC.MenuItem)sender;
                    // 若点击来自子菜单项则跳过，由子项自己的handler处理
                    var source = e.OriginalSource as SW.DependencyObject;
                    while (source != null)
                    {
                        if (source is SWC.MenuItem child && child != item)
                            return;
                        source = SWM.VisualTreeHelper.GetParent(source);
                    }

                    var selectedGoal = (Goal)item.Tag;
                    SelectGoal(selectedGoal);
                };

                parentContainer.Items.Add(menuItem);

                var childGoals = goal.Children.Where(g => g.Status != GoalStatus.Completed &&
                                                         g.Status != GoalStatus.Failed &&
                                                         g.Status != GoalStatus.Pending).ToList();
                if (childGoals.Count > 0)
                {
                    AddGoalMenuItems(menuItem, goal);
                }
            }
        }

        private void SelectGoal(Goal selectedGoal)
        {
            // 同一个目标且计时器未运行，无需重新初始化，避免不必要的滚动
            if (CurrentGoal?.Id == selectedGoal.Id && !_timerService.IsActive)
                return;

            CurrentGoal = selectedGoal;

            // 先停止计时器，确保 IsActive=false，使 CurrentGoal setter 能正确更新时间
            _timerService.StopTimer();

            _suppressTimerDisplayUpdate = true;
            int oldSeconds = GetTotalSeconds();
            _timerService.CurrentGoal = selectedGoal;

            if (!_timerService.IsStopwatchMode)
            {
                // 倒计时模式切换目标时重置计时
                _timerService.ResetTimer();
            }

            SetScrollersFromService(oldSeconds);
            _suppressTimerDisplayUpdate = false;

            UpdateScrollersEditable();

            UpdateGoalDisplay();
        }

        private void ClearGoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            // 清除目标时设置为全局目标，但不显示
            CurrentGoal = GoalsPage.GlobalGoal;

            _timerService.StopTimer();

            _suppressTimerDisplayUpdate = true;
            int oldSeconds = GetTotalSeconds();
            _timerService.CurrentGoal = GoalsPage.GlobalGoal;
            SetScrollersFromService(oldSeconds);
            _suppressTimerDisplayUpdate = false;

            UpdateScrollersEditable();

            UpdateGoalDisplay();
        }

        private void UpdateWhiteNoiseIcon()
        {
            var settings = SettingsManager.Current;
            if (AudioServices.WhiteNoise.IsPlaying)
            {
                WhiteNoiseIcon.Fill = new SWM.SolidColorBrush(SWM.Color.FromRgb(16, 185, 129));
                WhiteNoiseButton.ToolTip = "暂停白噪音";
            }
            else
            {
                WhiteNoiseIcon.Fill = (SWM.Brush)FindResource("TextSecondary");
                WhiteNoiseButton.ToolTip = "播放白噪音";
            }
        }

        private void WhiteNoiseButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;
            if (settings.WhiteNoiseEnabled)
            {
                if (AudioServices.WhiteNoise.IsPlaying)
                {
                    AudioServices.WhiteNoise.Stop();
                }
                else
                {
                    AudioServices.WhiteNoise.Play();
                }
                UpdateWhiteNoiseIcon();
            }
        }

        private void WhiteNoiseManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Services.Audio.WhiteNoiseManager.IsPlaying))
            {
                UpdateWhiteNoiseIcon();
            }
        }

        private async void AddGoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                var addGoalWindow = new Views.AddGoalWindow();
                addGoalWindow.Owner = SW.Window.GetWindow(this);
                addGoalWindow.GoalAdded += async (s, args) =>
                {
                    var newGoal = new Goal(addGoalWindow.GoalTitle, addGoalWindow.GoalDescription);
                    if (CurrentGoal != null && CurrentGoal.Id != "global-goal")
                    {
                        CurrentGoal.AddChild(newGoal);
                    }
                    else
                    {
                        GoalsPage.Goals.Add(newGoal);
                    }
                    SelectGoal(newGoal);
                    await GoalsPage.ViewModel!.SaveAsync();
                };
                addGoalWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddGoalButton_Click: {ex.Message}");
            }
        }
    }
}