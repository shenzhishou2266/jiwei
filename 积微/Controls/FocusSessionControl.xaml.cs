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
    /// <summary>番茄钟计时器控件，提供工作/休息计时、目标选择和记录时间线功能。</summary>
    public partial class FocusSessionControl : SWC.UserControl
    {
        private readonly FocusSessionService _timerService;
        
        private readonly SWM.Brush RoseBrush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 244, 63, 94));
        private readonly SWM.Brush EmeraldBrush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 16, 185, 129));
        private readonly SWM.Brush Gray600Brush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 75, 85, 99));
        private readonly SWM.Brush WhiteBrush = SWM.Brushes.White;

        public FocusSessionControl()
        {
            InitializeComponent();
            _timerService = FocusSessionService.Instance;
            _timerService.PropertyChanged += TimerService_PropertyChanged;

            // 监听白噪音状态变化
            var settings = SettingsManager.Current;
            if (AudioServices.WhiteNoise is System.ComponentModel.INotifyPropertyChanged whiteNoiseManager)
            {
                whiteNoiseManager.PropertyChanged += WhiteNoiseManager_PropertyChanged;
            }

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

            LoadSettings();
            UpdateDisplay();
            UpdateWhiteNoiseIcon();
        }

        private void TimerService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateDisplay();
        }

        private void UserControl_Loaded(object sender, SW.RoutedEventArgs e)
        {
            this.Focus();
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Current;
            WhiteNoiseButton.Visibility = settings.WhiteNoiseEnabled ? SW.Visibility.Visible : SW.Visibility.Collapsed;
        }

        /// <summary>从设置管理器同步更新控件状态。</summary>
        public void UpdateSettings()
        {
            var settings = SettingsManager.Current;
            _timerService.UpdateSettings();
            WhiteNoiseButton.Visibility = settings.WhiteNoiseEnabled ? SW.Visibility.Visible : SW.Visibility.Collapsed;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            TimerText.Text = _timerService.TimeDisplay;
            SessionText.Text = _timerService.SessionDisplay;
            CurrentGoalText.Text = _timerService.CurrentGoalDisplay;
            CurrentGoalText.Visibility = string.IsNullOrEmpty(_timerService.CurrentGoalDisplay) ? SW.Visibility.Hidden : SW.Visibility.Visible;

            if (_timerService.IsActive)
            {
                PlayPauseIcon.Data = SWM.Geometry.Parse("M8,4V20H10V4H8M14,4V20H16V4H14Z");
            }
            else
            {
                PlayPauseIcon.Data = SWM.Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
            }

            if (_timerService.IsWorkSession)
            {
                WorkButton.Background = RoseBrush;
                WorkButton.Foreground = WhiteBrush;
                BreakButton.Background = WhiteBrush;
                BreakButton.Foreground = Gray600Brush;
                ProgressPath.Stroke = RoseBrush;
            }
            else
            {
                WorkButton.Background = WhiteBrush;
                WorkButton.Foreground = Gray600Brush;
                BreakButton.Background = EmeraldBrush;
                BreakButton.Foreground = WhiteBrush;
                ProgressPath.Stroke = EmeraldBrush;
            }

            UpdateProgressRing();
        }

        private void UpdateProgressRing()
        {
            var settings = SettingsManager.Current;
            double progress = _timerService.Progress;

            double angle = progress * 360;
            double radius = 86;
            double centerX = 100;
            double centerY = 100;

            if (angle == 0)
            {
                ProgressPath.Data = null;
                return;
            }

            if (angle == 360)
            {
                ProgressPath.Data = new SWM.EllipseGeometry(new SW.Point(centerX, centerY), radius, radius);
                return;
            }

            double radians = angle * Math.PI / 180;
            double x2 = centerX + radius * Math.Cos(radians - Math.PI / 2);
            double y2 = centerY + radius * Math.Sin(radians - Math.PI / 2);

            bool largeArc = angle > 180;

            var pathGeometry = new SWM.PathGeometry();
            var pathFigure = new SWM.PathFigure();
            pathFigure.StartPoint = new SW.Point(centerX, centerY - radius);
            pathFigure.Segments.Add(new SWM.ArcSegment(
                new SW.Point(x2, y2),
                new SW.Size(radius, radius),
                0,
                largeArc,
                SWM.SweepDirection.Clockwise,
                true));
            pathGeometry.Figures.Add(pathFigure);

            ProgressPath.Data = pathGeometry;
        }

        private void WorkButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _timerService.SwitchToWork();
        }

        private void BreakButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _timerService.SwitchToBreak();
        }

        private void ResetButton_Click(object sender, SW.RoutedEventArgs e)
        {
            _timerService.ResetTimer();
        }

        private void PlayPauseButton_Click(object sender, SW.RoutedEventArgs e)
        {
            if (!_timerService.IsActive && _timerService.CurrentGoal != null && _timerService.CurrentGoal.Id != "global-goal")
            {
                string? conflictingTimer = GoalTimerManager.Instance.GetActiveAndRunningTimerForGoal(_timerService.CurrentGoal.Id);
                if (conflictingTimer != null && conflictingTimer != GoalTimerManager.TimerTypePomodoro)
                {
                    var messageBox = new Views.MessageBoxWindow("提示", "计时器正在为同一目标计时，请先停止计时器后再开始番茄钟。");
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
                    var selectedGoal = (Goal)(sender as SWC.MenuItem).Tag;
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
            _timerService.CurrentGoal = selectedGoal;
            _timerService.ResetTimer();
        }

        private void ClearGoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            // 清除目标时设置为全局目标，但不显示
            _timerService.CurrentGoal = GoalsPage.GlobalGoal;
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
                    if (_timerService.CurrentGoal != null && _timerService.CurrentGoal.Id != "global-goal")
                    {
                        _timerService.CurrentGoal.AddChild(newGoal);
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
    }
}
