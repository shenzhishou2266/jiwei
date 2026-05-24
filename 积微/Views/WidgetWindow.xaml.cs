using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using 积微.Models;
using 积微.Services;
using 积微.Services.Audio;
using 积微.Controls;
using 积微.Helpers;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWCP = System.Windows.Controls.Primitives;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace 积微.Views
{
    /// <summary>桌面小组件窗口，提供迷你计时器和目标快速访问。</summary>
    public partial class WidgetWindow : Window
    {
        private readonly FocusSessionService _pomodoroService;
        private readonly TimerService _timerService;
        private WidgetMode _currentMode = WidgetMode.Pomodoro;
        private TimerMode _timerMode = TimerMode.Stopwatch;
        private bool _isExpanded = true;
        private bool _isUpdating = false;

        private enum WidgetMode { Pomodoro, Timer }
        private enum TimerMode { Stopwatch, Countdown }

        private readonly SWM.Brush _grayBrushForMode = new SWM.SolidColorBrush(SWM.Color.FromRgb(156, 163, 175));

        public WidgetWindow()
        {
            InitializeComponent();

            _pomodoroService = FocusSessionService.Instance;
            _timerService = TimerService.Instance;

            _pomodoroService.PropertyChanged += PomodoroService_PropertyChanged;
            _timerService.PropertyChanged += TimerService_PropertyChanged;

            var settings = SettingsManager.Current;
            
            // 读取保存的位置设置
            this.Left = settings.WidgetWindowLeft;
            this.Top = settings.WidgetWindowTop;
            
            // 初始化置顶属性
            this.Topmost = settings.WidgetWindowTopmost;
            
            if (settings.WhiteNoiseManager is INotifyPropertyChanged whiteNoiseManager)
            {
                whiteNoiseManager.PropertyChanged += WhiteNoiseManager_PropertyChanged;
            }

            if (settings is INotifyPropertyChanged appSettings)
            {
                appSettings.PropertyChanged += AppSettings_PropertyChanged;
            }

            UpdateDisplay();
            UpdateGoalButton();
            UpdateGoalButtonStyle();
            UpdateWhiteNoiseButton();
            UpdateModeToggleButton();
        }

        private void PomodoroService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FocusSessionService.TimeDisplay) ||
                e.PropertyName == nameof(FocusSessionService.IsActive) ||
                e.PropertyName == nameof(FocusSessionService.CurrentGoal) ||
                e.PropertyName == nameof(FocusSessionService.IsWorkSession))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_currentMode == WidgetMode.Pomodoro)
                    {
                        UpdateDisplay();
                        UpdateGoalButton();
                    }
                });
            }
        }

        private void TimerService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimerService.TimeDisplay) ||
                e.PropertyName == nameof(TimerService.IsActive) ||
                e.PropertyName == nameof(TimerService.CurrentGoal))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_currentMode == WidgetMode.Timer)
                    {
                        UpdateDisplay();
                        UpdateGoalButton();
                    }
                });
            }
        }

        private void WhiteNoiseManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WhiteNoiseManager.IsPlaying))
            {
                Dispatcher.Invoke(UpdateWhiteNoiseButton);
            }
        }

        private void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "WhiteNoiseEnabled")
            {
                Dispatcher.Invoke(UpdateWhiteNoiseButton);
            }
            else if (e.PropertyName == "Theme")
            {
                Dispatcher.Invoke(UpdateGoalButtonStyle);
            }
            else if (e.PropertyName == nameof(AppSettings.WidgetWindowTopmost))
            {
                Dispatcher.Invoke(() => 
                {
                    var settings = SettingsManager.Current;
                    this.Topmost = settings.WidgetWindowTopmost;
                });
            }
        }

        private void UpdateDisplay()
        {
            if (_currentMode == WidgetMode.Pomodoro)
            {
                SessionText.Text = _pomodoroService.IsWorkSession ? "专注" : "休息";
                TimerText.Text = _pomodoroService.TimeDisplay;

                UpdatePlayPauseButton(_pomodoroService.IsActive);
                UpdateResetButton();
            }
            else
            {
                SessionText.Text = _timerMode == TimerMode.Stopwatch ? "秒表" : "倒计时";
                TimerText.Text = _timerService.ShortTimeDisplay;

                UpdatePlayPauseButton(_timerService.IsActive);
                UpdateResetButton();
            }
        }

        private void UpdatePlayPauseButton(bool isRunning)
        {
            if (isRunning)
            {
                PlayPauseIcon.Data = SWM.Geometry.Parse("M6,19H10V5H6M14,5V19H18V5");
            }
            else
            {
                PlayPauseIcon.Data = SWM.Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
            }
        }

        private void UpdateResetButton()
        {
            ResetButton.IsEnabled = true;
        }

        private void UpdateGoalButton()
        {
            UpdateGoalButtonStyle();

            if (_currentMode == WidgetMode.Pomodoro)
            {
                if (_pomodoroService.CurrentGoal != null && _pomodoroService.CurrentGoal.Id != "global-goal")
                {
                    string goalName = _pomodoroService.CurrentGoal.Title;
                    if (goalName.Length > 10)
                    {
                        goalName = goalName.Substring(0, 10) + "...";
                    }
                    GoalButton.Content = goalName;
                }
                else
                {
                    GoalButton.Content = "";
                }
            }
            else
            {
                if (_timerService.CurrentGoal != null && _timerService.CurrentGoal.Id != "global-goal")
                {
                    string goalName = _timerService.CurrentGoal.Title;
                    if (goalName.Length > 10)
                    {
                        goalName = goalName.Substring(0, 10) + "...";
                    }
                    GoalButton.Content = goalName;
                }
                else
                {
                    GoalButton.Content = "";
                }
            }
        }

        private void UpdateWhiteNoiseButton()
        {
            var settings = SettingsManager.Current;
            WhiteNoiseButton.Visibility = settings.WhiteNoiseEnabled ? SW.Visibility.Visible : SW.Visibility.Collapsed;
            if (settings.WhiteNoiseEnabled && settings.WhiteNoiseManager.IsPlaying)
            {
                WhiteNoiseIcon.Fill = new SWM.SolidColorBrush(SWM.Color.FromRgb(16, 185, 129));
            }
            else
            {
                WhiteNoiseIcon.Fill = new SWM.SolidColorBrush(SWM.Color.FromRgb(107, 114, 128));
            }
        }

        private void UpdateGoalButtonStyle()
        {
            var settings = SettingsManager.Current;
            bool isDarkMode = settings.Theme == "Dark";

            if (isDarkMode)
            {
                GoalButton.Background = new SWM.SolidColorBrush(SWM.Color.FromRgb(58, 58, 60));
                GoalButton.Foreground = new SWM.SolidColorBrush(SWM.Color.FromRgb(255, 255, 255));
            }
            else
            {
                GoalButton.Background = new SWM.SolidColorBrush(SWM.Color.FromRgb(243, 244, 246));
                GoalButton.Foreground = new SWM.SolidColorBrush(SWM.Color.FromRgb(75, 70, 70));
            }
        }

        private void UpdateModeToggleButton()
        {
            if (_currentMode == WidgetMode.Pomodoro)
            {
                ModeToggleIcon.Data = SWM.Geometry.Parse("M12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z");
            }
            else
            {
                ModeToggleIcon.Data = SWM.Geometry.Parse("M12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z");
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == WidgetMode.Pomodoro)
            {
                if (!_pomodoroService.IsActive && _pomodoroService.CurrentGoal != null && _pomodoroService.CurrentGoal.Id != "global-goal")
                {
                    string? conflictingTimer = GoalTimerManager.Instance.GetActiveAndRunningTimerForGoal(_pomodoroService.CurrentGoal.Id);
                    if (conflictingTimer != null && conflictingTimer != GoalTimerManager.TimerTypePomodoro)
                    {
                        var messageBox = new MessageBoxWindow("提示", "计时器正在为同一目标计时，请先停止计时器后再开始番茄钟。");
                        messageBox.Owner = this;
                        messageBox.ShowDialog();
                        return;
                    }
                }
                _pomodoroService.TogglePlayPause();
            }
            else
            {
                if (!_timerService.IsActive && _timerService.CurrentGoal != null && _timerService.CurrentGoal.Id != "global-goal")
                {
                    string? conflictingTimer = GoalTimerManager.Instance.GetActiveAndRunningTimerForGoal(_timerService.CurrentGoal.Id);
                    if (conflictingTimer != null && conflictingTimer != GoalTimerManager.TimerTypeNormal)
                    {
                        var messageBox = new MessageBoxWindow("提示", "番茄钟正在为同一目标计时，请先停止番茄钟后再开始计时器。");
                        messageBox.Owner = this;
                        messageBox.ShowDialog();
                        return;
                    }
                }
                _timerService.TogglePlayPause();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == WidgetMode.Pomodoro)
            {
                _pomodoroService.StopTimerWithRecord();
            }
            else
            {
                _timerService.StopTimerWithRecord();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == WidgetMode.Timer)
            {
                _timerService.ResetTimer();
            }
            else
            {
                _pomodoroService.ResetTimer();
            }
        }

        private void ModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == WidgetMode.Pomodoro)
            {
                if (_pomodoroService.IsWorkSession)
                {
                    _pomodoroService.SwitchToBreak();
                }
                else
                {
                    _pomodoroService.SwitchToWork();
                }
            }
            else
            {
                SwitchTimerMode();
            }
        }

        private void SwitchTimerMode()
        {
            if (_timerMode == TimerMode.Stopwatch)
            {
                _timerMode = TimerMode.Countdown;
                _timerService.SwitchToCountdown();
            }
            else
            {
                _timerMode = TimerMode.Stopwatch;
                _timerService.SwitchToStopwatch();
            }
            UpdateDisplay();
        }

        private void PomodoroModeButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchMode(WidgetMode.Pomodoro);
        }

        private void TimerModeButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchMode(WidgetMode.Timer);
        }

        private void SwitchMode(WidgetMode mode)
        {
            _currentMode = mode;

            if (mode == WidgetMode.Pomodoro)
            {
                PomodoroModeButton.Background = _grayBrushForMode;
                PomodoroModeButton.Foreground = SWM.Brushes.White;
                TimerModeButton.Background = SWM.Brushes.Transparent;
                TimerModeButton.Foreground = SWM.Brushes.Gray;
            }
            else
            {
                TimerModeButton.Background = _grayBrushForMode;
                TimerModeButton.Foreground = SWM.Brushes.White;
                PomodoroModeButton.Background = SWM.Brushes.Transparent;
                PomodoroModeButton.Foreground = SWM.Brushes.Gray;
            }

            UpdateDisplay();
            UpdateGoalButton();
        }

        private void GoalButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new SWC.ContextMenu();

            var noGoalItem = new SWC.MenuItem { Header = "无目标" };
            noGoalItem.Click += (s, args) => {
                if (_currentMode == WidgetMode.Pomodoro)
                {
                    _pomodoroService.CurrentGoal = GoalsPage.GlobalGoal;
                }
                else
                {
                    _timerService.CurrentGoal = GoalsPage.GlobalGoal;
                }
                UpdateGoalButton();
            };
            menu.Items.Add(noGoalItem);

            menu.Items.Add(new SWC.Separator());

            var rootGoals = GoalsPage.Goals.Where(g => g.Parent == null &&
                                                      g.Status != GoalStatus.Completed &&
                                                      g.Status != GoalStatus.Failed &&
                                                      g.Status != GoalStatus.Pending &&
                                                      g.Id != "global-goal").ToList();
            
            if (rootGoals.Count == 0)
            {
                var noGoalsItem = new SWC.MenuItem();
                noGoalsItem.Header = "没有正在进行的目标";
                noGoalsItem.IsEnabled = false;
                menu.Items.Add(noGoalsItem);
            }
            else
            {
                var titleCount = GoalDisplayHelper.GetTitleCount(GoalsPage.Goals);
                foreach (var rootGoal in rootGoals)
                {
                    AddGoalMenuItem(menu, rootGoal, 0, titleCount);
                }
            }

            menu.IsOpen = true;
        }

        private void AddGoalMenuItem(SWC.ItemsControl parentContainer, Goal goal, int level, System.Collections.Generic.Dictionary<string, int> titleCount)
        {
            if (goal.Id == "global-goal")
            {
                return;
            }

            var menuItem = new SWC.MenuItem { Header = new string(' ', level * 2) + GoalDisplayHelper.GetGoalDisplayName(goal, titleCount) };
            menuItem.Tag = goal;

            menuItem.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                var clickedMenuItem = sender as SWC.MenuItem;
                if (clickedMenuItem != null)
                {
                    var selectedGoal = clickedMenuItem.Tag as Goal;
                    if (selectedGoal != null)
                    {
                        if (_currentMode == WidgetMode.Pomodoro)
                        {
                            _pomodoroService.CurrentGoal = selectedGoal;
                        }
                        else
                        {
                            _timerService.CurrentGoal = selectedGoal;
                        }
                        UpdateGoalButton();
                    }
                }
            };

            parentContainer.Items.Add(menuItem);

            var childGoals = goal.Children.Where(g => g.Status != GoalStatus.Completed &&
                                                      g.Status != GoalStatus.Failed &&
                                                      g.Status != GoalStatus.Pending).ToList();
            if (childGoals.Count > 0)
            {
                foreach (var subGoal in childGoals)
                {
                    AddGoalMenuItem(menuItem, subGoal, level + 1, titleCount);
                }
            }
        }

        private void WhiteNoiseButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;
            if (settings.WhiteNoiseEnabled)
            {
                if (settings.WhiteNoiseManager.IsPlaying)
                {
                    settings.WhiteNoiseManager.Stop();
                }
                else
                {
                    settings.WhiteNoiseManager.Play();
                }
                UpdateWhiteNoiseButton();
            }
        }

        private void ToggleExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;
            _isExpanded = !_isExpanded;
            UpdateWidgetSize();
        }

        private void UpdateWidgetSize()
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                var duration = TimeSpan.FromMilliseconds(200);
                int animationsCompleted = 0;
                int totalAnimations = 1;

                EventHandler completionHandler = null;
                completionHandler = (s, evt) =>
                {
                    animationsCompleted++;
                    if (animationsCompleted >= totalAnimations)
                    {
                        _isUpdating = false;
                    }
                };

                if (_isExpanded)
                {
                    var widthAnim = new System.Windows.Media.Animation.DoubleAnimation(430, duration);

                    RightPanel.Visibility = SW.Visibility.Visible;
                    RightColumn.Width = new SW.GridLength(200);
                    LeftPanel.HorizontalAlignment = SW.HorizontalAlignment.Stretch;
                    ToggleExpandButton.HorizontalAlignment = SW.HorizontalAlignment.Center;

                    widthAnim.Completed += completionHandler;

                    this.BeginAnimation(SW.Window.WidthProperty, widthAnim);
                }
                else
                {
                    var widthAnim = new System.Windows.Media.Animation.DoubleAnimation(220, duration);

                    RightColumn.Width = new SW.GridLength(0);
                    RightPanel.Visibility = SW.Visibility.Collapsed;
                    LeftPanel.HorizontalAlignment = SW.HorizontalAlignment.Center;
                    ToggleExpandButton.HorizontalAlignment = SW.HorizontalAlignment.Right;

                    widthAnim.Completed += completionHandler;

                    this.BeginAnimation(SW.Window.WidthProperty, widthAnim);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            var element = e.OriginalSource as SW.DependencyObject;
            while (element != null && !(element is SW.Window))
            {
                if (element is SWC.Button || element is SWC.Image || element is SW.Shapes.Path)
                {
                    return;
                }
                element = SW.Media.VisualTreeHelper.GetParent(element);
            }

            ShowMainWindow();
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            ShowContextMenu(e.GetPosition(this));
        }

        private void ShowContextMenu(SW.Point position)
        {
            var menu = new SWC.ContextMenu();

            var closeItem = new SWC.MenuItem { Header = "关闭小组件" };
            closeItem.Click += (s, args) => {
                this.Hide();
            };
            menu.Items.Add(closeItem);

            menu.PlacementTarget = this;
            menu.Placement = SWCP.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void ShowMainWindow()
        {
            MainWindow mainWindow = null;
            foreach (var window in SW.Application.Current.Windows)
            {
                if (window is MainWindow)
                {
                    mainWindow = window as MainWindow;
                    break;
                }
            }

            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.WindowState = SW.WindowState.Normal;
                mainWindow.Activate();

                if (_currentMode == WidgetMode.Pomodoro)
                {
                    if (!mainWindow.isPomodoroMode)
                    {
                        mainWindow.SwitchTimerMode();
                    }
                }
                else
                {
                    if (mainWindow.isPomodoroMode)
                    {
                        mainWindow.SwitchTimerMode();
                    }
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, SW.Input.KeyEventArgs e)
        {
            if (e.Key == SW.Input.Key.Enter)
            {
                e.Handled = true;
                Goal targetGoal;

                if (_currentMode == WidgetMode.Pomodoro)
                {
                    targetGoal = _pomodoroService.CurrentGoal ?? GoalsPage.GlobalGoal;
                }
                else
                {
                    targetGoal = _timerService.CurrentGoal ?? GoalsPage.GlobalGoal;
                }

                var quickInputWindow = new QuickTimelineInputWindow(targetGoal);
                quickInputWindow.Show();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _pomodoroService.PropertyChanged -= PomodoroService_PropertyChanged;
            _timerService.PropertyChanged -= TimerService_PropertyChanged;

            var settings = SettingsManager.Current;
            settings.WidgetWindowLeft = this.Left;
            settings.WidgetWindowTop = this.Top;
            SettingsManager.SaveSettings();

            if (settings.WhiteNoiseManager is INotifyPropertyChanged whiteNoiseManager)
            {
                whiteNoiseManager.PropertyChanged -= WhiteNoiseManager_PropertyChanged;
            }

            if (settings is INotifyPropertyChanged appSettings)
            {
                appSettings.PropertyChanged -= AppSettings_PropertyChanged;
            }
        }
    }
}