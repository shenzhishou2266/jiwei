using System;
using System.Linq;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWShapes = System.Windows.Shapes;
using System.Windows.Threading;
using 积微.Models;
using 积微.Services;
using 积微.Views;
using 积微.Helpers;

namespace 积微.Controls
{
    /// <summary>目标列表项控件，显示单个目标及其子目标、操作按钮。</summary>
    public partial class GoalItem : SWC.UserControl
    {
        /// <summary>获取或设置当前目标对象。</summary>
        public Goal Goal { get; set; }
        /// <summary>获取或设置子目标列表是否展开。</summary>
        public bool IsExpanded { get; set; } = false;
        private Dictionary<string, int>? _titleCount;
        /// <summary>子目标状态过滤器，null 表示加载所有子目标。</summary>
        private GoalStatus? _childFilterStatus;
        /// <summary>是否显示父目标引用标签（子目标独立显示时）。</summary>
        private bool _showParentRef;
        /// <summary>GoalItem 映射表引用，用于注册子项。</summary>
        private Dictionary<string, GoalItem>? _goalItemMap;
        /// <summary>搜索文本，用于过滤 LoadChildren 中的子目标。</summary>
        private string? _searchText;

        private DispatcherTimer _longPressTimer;
        private const int LongPressDurationMs = 1200;

        public GoalItem()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>使用指定目标和标题计数构造 GoalItem。</summary>
        public GoalItem(Goal goal, Dictionary<string, int>? titleCount = null,
                        GoalStatus? childFilterStatus = null, bool showParentRef = false,
                        Dictionary<string, GoalItem>? goalItemMap = null, string? searchText = null)
            : this()
        {
            Goal = goal;
            _titleCount = titleCount;
            _childFilterStatus = childFilterStatus;
            _showParentRef = showParentRef;
            _goalItemMap = goalItemMap;
            _searchText = searchText;
            UpdateUI();
            LoadChildren();
            SetupLongPress();
        }

        private void SetupLongPress()
        {
            SetupButtonLongPress(CompleteOnceButton, CompleteOnceButton_Click);
            SetupButtonLongPress(CompleteButton, CompleteButton_Click);
            SetupButtonLongPress(FailButton, FailButton_Click);
            SetupButtonLongPress(StartButton, StartButton_Click);
            SetupButtonLongPress(PendingButton, PendingButton_Click);
        }

        private void OnLoaded(object sender, SW.RoutedEventArgs e)
        {
            if (Goal != null)
            {
                Goal.DurationChanged += OnGoalDurationChanged;
                UpdateDurationDisplay();
            }
        }

        private void OnUnloaded(object sender, SW.RoutedEventArgs e)
        {
            if (Goal != null)
            {
                Goal.DurationChanged -= OnGoalDurationChanged;
            }
        }

        private void OnGoalDurationChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => UpdateDurationDisplay());
        }

        private void UpdateDurationDisplay()
        {
            if (Goal == null) return;
            int total = Goal.TotalDurationIncludingChildren;
            DurationTextBlock.Text = total > 0 ? FormatDurationShort(total) : "";
        }

        private static string FormatDurationShort(int totalSeconds)
        {
            if (totalSeconds <= 0)
                return "";

            int years = totalSeconds / 31536000;
            int remainingAfterYears = totalSeconds % 31536000;
            int days = remainingAfterYears / 86400;
            int hours = (remainingAfterYears % 86400) / 3600;
            int minutes = (remainingAfterYears % 3600) / 60;

            if (years > 0)
                return $"⏱ {years}y {days}d";
            if (days > 0)
                return $"⏱ {days}d {hours}h";
            if (hours > 0)
                return $"⏱ {hours}h {minutes}m";
            if (minutes > 0)
                return $"⏱ {minutes}m";
            return $"⏱ {totalSeconds}s";
        }

        private void AnimateBadgePulse()
        {
            var storyboard = new SWM.Animation.Storyboard()
            {
                FillBehavior = SWM.Animation.FillBehavior.Stop
            };

            var scaleX = new SWM.Animation.DoubleAnimation(1.0, 1.45, TimeSpan.FromSeconds(0.08))
            {
                AutoReverse = true
            };
            SWM.Animation.Storyboard.SetTarget(scaleX, CompletionBadge);
            SWM.Animation.Storyboard.SetTargetProperty(scaleX,
                new SW.PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(scaleX);

            var scaleY = new SWM.Animation.DoubleAnimation(1.0, 1.45, TimeSpan.FromSeconds(0.08))
            {
                AutoReverse = true
            };
            SWM.Animation.Storyboard.SetTarget(scaleY, CompletionBadge);
            SWM.Animation.Storyboard.SetTargetProperty(scaleY,
                new SW.PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(scaleY);

            var glowO = new SWM.Animation.DoubleAnimation(0.0, 0.5, TimeSpan.FromSeconds(0.08))
            {
                AutoReverse = true
            };
            SWM.Animation.Storyboard.SetTarget(glowO, CompletionBadge);
            SWM.Animation.Storyboard.SetTargetProperty(glowO,
                new SW.PropertyPath("(UIElement.Effect).(DropShadowEffect.Opacity)"));
            storyboard.Children.Add(glowO);

            var glowB = new SWM.Animation.DoubleAnimation(0.0, 12, TimeSpan.FromSeconds(0.08))
            {
                AutoReverse = true
            };
            SWM.Animation.Storyboard.SetTarget(glowB, CompletionBadge);
            SWM.Animation.Storyboard.SetTargetProperty(glowB,
                new SW.PropertyPath("(UIElement.Effect).(DropShadowEffect.BlurRadius)"));
            storyboard.Children.Add(glowB);

            storyboard.Begin(this);
        }

        private void SetupButtonLongPress(SWC.Button button, SW.RoutedEventHandler clickHandler)
        {
            button.PreviewMouseLeftButtonDown += (s, e) => StartLongPress(s, button, clickHandler);
            button.PreviewMouseLeftButtonUp += (s, e) => CancelLongPress(button);
            button.MouseLeave += (s, e) => CancelLongPress(button);
        }

        private void StartLongPress(object sender, SWC.Button button, SW.RoutedEventHandler clickHandler)
        {
            var progressOverlay = FindVisualChild<SWC.Border>(button, "ProgressOverlay");
            var progressFill = FindVisualChild<SWC.Border>(button, "ProgressFill");

            if (progressOverlay == null || progressFill == null) return;

            progressOverlay.Visibility = SW.Visibility.Visible;
            progressFill.Width = 0;

            // 获取按钮实际宽度
            double buttonWidth = button.Width;

            _longPressTimer = new DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(30);
            int elapsed = 0;
            _longPressTimer.Tick += (s, e) =>
            {
                elapsed += 30;
                double progress = Math.Min(1.0, (double)elapsed / LongPressDurationMs);
                progressFill.Width = progress * buttonWidth;
                if (elapsed >= LongPressDurationMs)
                {
                    _longPressTimer.Stop();
                    progressOverlay.Visibility = SW.Visibility.Collapsed;
                    clickHandler(sender, new SW.RoutedEventArgs());
                }
            };
            _longPressTimer.Start();
        }

        private void CancelLongPress(SWC.Button button)
        {
            if (_longPressTimer != null)
            {
                _longPressTimer.Stop();
                _longPressTimer = null;
            }

            var progressOverlay = FindVisualChild<SWC.Border>(button, "ProgressOverlay");
            var progressFill = FindVisualChild<SWC.Border>(button, "ProgressFill");

            if (progressOverlay != null)
                progressOverlay.Visibility = SW.Visibility.Collapsed;
            if (progressFill != null)
                progressFill.Width = 0;
        }

        public void UpdateUI()
        {
            if (Goal != null)
            {
                // 使用构造函数传入的 _titleCount，避免每次刷新都重新遍历全部目标
                GoalTitle.Text = GoalDisplayHelper.GetGoalDisplayName(Goal, _titleCount);

                // 显示父目标引用 ToolTip
                if (_showParentRef && Goal.Parent != null)
                {
                    GoalBorder.ToolTip = $"隶属于: {Goal.Parent.Title}";
                }
                else
                {
                    GoalBorder.ToolTip = null;
                }

                // 展开按钮始终显示，保持视觉一致性；无子目标时点击无效果
                bool hasChildrenToShow = Goal.Children.Count > 0 && 
                    (!_childFilterStatus.HasValue || Goal.Children.Any(c => c.Status == _childFilterStatus.Value));
                ExpandButton.Visibility = SW.Visibility.Visible;

                // 处理重复目标特有的 UI 元素
                bool isRecurring = Goal.Type == GoalType.Recurring;
                CompletionBadge.Visibility = isRecurring ? SW.Visibility.Visible : SW.Visibility.Collapsed;
                CompletionCountText.Text = Goal.RecurringCompletionCount.ToString();

                switch (Goal.Status)
                {
                    case GoalStatus.Completed:
                        GoalBorder.Background = SWM.Brushes.LightGreen;
                        GoalTitle.TextDecorations = SW.TextDecorations.Strikethrough;
                        GoalTitle.Foreground = SWM.Brushes.Black;
                        CompleteOnceButton.Visibility = SW.Visibility.Collapsed;
                        CompleteButton.Visibility = SW.Visibility.Collapsed;
                        FailButton.Visibility = SW.Visibility.Collapsed;
                        PendingButton.Visibility = SW.Visibility.Collapsed;
                        StartButton.Visibility = SW.Visibility.Visible;
                        break;
                    case GoalStatus.Failed:
                        GoalBorder.Background = SWM.Brushes.LightPink;
                        GoalTitle.TextDecorations = SW.TextDecorations.Strikethrough;
                        GoalTitle.Foreground = SWM.Brushes.Black;
                        CompleteOnceButton.Visibility = SW.Visibility.Collapsed;
                        CompleteButton.Visibility = SW.Visibility.Collapsed;
                        FailButton.Visibility = SW.Visibility.Collapsed;
                        PendingButton.Visibility = SW.Visibility.Collapsed;
                        StartButton.Visibility = SW.Visibility.Visible;
                        break;
                    case GoalStatus.Pending:
                        GoalBorder.Background = SWM.Brushes.LightYellow;
                        GoalTitle.TextDecorations = SW.TextDecorations.Strikethrough;
                        GoalTitle.Foreground = SWM.Brushes.Black;
                        CompleteOnceButton.Visibility = isRecurring ? SW.Visibility.Visible : SW.Visibility.Collapsed;
                        CompleteButton.Visibility = SW.Visibility.Collapsed;
                        FailButton.Visibility = SW.Visibility.Visible;
                        PendingButton.Visibility = SW.Visibility.Collapsed;
                        StartButton.Visibility = SW.Visibility.Visible;
                        break;
                    default:
                        GoalBorder.Background = (SWM.Brush)SW.Application.Current.Resources["CardBackground"];
                        GoalTitle.TextDecorations = null;
                        GoalTitle.Foreground = (SWM.Brush)SW.Application.Current.Resources["TextPrimary"];
                        CompleteOnceButton.Visibility = isRecurring ? SW.Visibility.Visible : SW.Visibility.Collapsed;
                        CompleteButton.Visibility = SW.Visibility.Visible;
                        FailButton.Visibility = SW.Visibility.Visible;
                        PendingButton.Visibility = SW.Visibility.Visible;
                        StartButton.Visibility = SW.Visibility.Collapsed;
                        break;
                }

                // 搜索高亮：匹配搜索的目标添加左侧蓝色强调边框
                if (!string.IsNullOrEmpty(_searchText))
                {
                    bool matchesSearch = (Goal.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         (Goal.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (matchesSearch)
                    {
                        GoalBorder.BorderBrush = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#8B5CF6"));
                        GoalBorder.BorderThickness = new SW.Thickness(3, 1, 1, 1);
                    }
                    else
                    {
                        GoalBorder.BorderBrush = (SWM.Brush)SW.Application.Current.Resources["BorderColor"];
                        GoalBorder.BorderThickness = new SW.Thickness(1);
                    }
                }
                else
                {
                    GoalBorder.BorderBrush = (SWM.Brush)SW.Application.Current.Resources["BorderColor"];
                    GoalBorder.BorderThickness = new SW.Thickness(1);
                }
            }
        }

        public void LoadChildren()
        {
            ChildrenPanel.Children.Clear();

            // 判断当前目标自身是否匹配搜索，若是则不筛子目标，让用户看到完整层级
            bool currentGoalMatchesSearch = !string.IsNullOrEmpty(_searchText) &&
                ((Goal.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (Goal.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var child in Goal.Children)
            {
                // 状态过滤
                if (_childFilterStatus.HasValue && child.Status != _childFilterStatus.Value)
                    continue;

                // 搜索过滤：父目标自身匹配时不过滤子目标，否则只显示匹配搜索的子目标
                if (!string.IsNullOrEmpty(_searchText) && !currentGoalMatchesSearch)
                {
                    var titleMatch = (child.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    var descMatch = (child.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!titleMatch && !descMatch)
                        continue;
                }

                var childItem = new GoalItem(child, _titleCount, _childFilterStatus, goalItemMap: _goalItemMap, searchText: _searchText);
                ChildrenPanel.Children.Add(childItem);
                if (_goalItemMap != null && !_goalItemMap.ContainsKey(child.Id))
                    _goalItemMap[child.Id] = childItem;
            }

            // 仅当父目标自身不匹配搜索、因子目标匹配才显示时，自动展开以便用户直接看到结果
            if (!string.IsNullOrEmpty(_searchText) && !currentGoalMatchesSearch && ChildrenPanel.Children.Count > 0)
            {
                IsExpanded = true;
                ChildrenPanel.Visibility = SW.Visibility.Visible;
                UpdateExpandIcon();
            }
        }

        // 我们不需要这个方法了，因为事件监听已经在 GoalsPage 中统一处理
        // 同时在 AddSubgoalButton_Click 中为新子目标单独添加监听

        private void ExpandButton_Click(object sender, SW.RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
            ChildrenPanel.Visibility = IsExpanded ? SW.Visibility.Visible : SW.Visibility.Collapsed;
            UpdateExpandIcon();
        }

        private void UpdateExpandIcon()
        {
            var expandIcon = FindVisualChild<SWC.TextBlock>(ExpandButton, "ExpandIcon");
            if (expandIcon != null)
            {
                expandIcon.Text = IsExpanded ? "▼" : "▲";
            }
        }

        private T FindVisualChild<T>(SW.DependencyObject parent, string name) where T : SW.FrameworkElement
        {
            for (int i = 0; i < SWM.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = SWM.VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private void DetailButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var detailWindow = new GoalDetailWindow(Goal);
            detailWindow.WindowStartupLocation = SW.WindowStartupLocation.CenterScreen;
            detailWindow.GoalUpdated += (s, args) =>
            {
                // 只更新当前GoalItem的UI，不需要刷新整个页面
                UpdateUI();
            };
            detailWindow.Show();
        }

        private async void AddSubgoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                var addSubgoalWindow = new AddGoalWindow();
                addSubgoalWindow.WindowStartupLocation = SW.WindowStartupLocation.CenterScreen;
                addSubgoalWindow.GoalAdded += async (s, args) =>
                {
                    var newSubgoal = new Goal(addSubgoalWindow.GoalTitle, addSubgoalWindow.GoalDescription);
                    newSubgoal.Type = addSubgoalWindow.SelectedGoalType;
                    Goal.AddChild(newSubgoal);
                    await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                    IsExpanded = true;
                    ChildrenPanel.Visibility = SW.Visibility.Visible;
                    UpdateExpandIcon();
                    LoadChildren();
                };
                addSubgoalWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddSubgoalButton_Click: {ex.Message}");
            }
        }

        private 积微.Views.GoalsPage? FindParentGoalsPage(SW.DependencyObject child)
        {
            SW.DependencyObject parent = SW.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is 积微.Views.GoalsPage goalsPage)
                {
                    return goalsPage;
                }
                parent = SW.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private async void CompleteButton_Click(object sender, SW.RoutedEventArgs e)
        {
            await HandleStatusChange(() => Goal.Complete(), playSound: true);
        }

        private async void FailButton_Click(object sender, SW.RoutedEventArgs e)
        {
            await HandleStatusChange(() => Goal.Fail());
        }

        private async void PendingButton_Click(object sender, SW.RoutedEventArgs e)
        {
            await HandleStatusChange(() => Goal.Pending());
        }

        private async void StartButton_Click(object sender, SW.RoutedEventArgs e)
        {
            await HandleStatusChange(() => Goal.Reactivate());
        }

        /// <summary>统一处理目标状态变更：保存 → 可选的播放音效 → 移动 Widget。</summary>
        private async Task HandleStatusChange(Action statusAction, bool playSound = false)
        {
            try
            {
                var oldStatus = Goal.Status;
                statusAction();
                await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);

                if (playSound)
                {
                    var settings = SettingsManager.Current;
                    if (settings.NotificationSoundManager != null)
                    {
                        var notificationSound = settings.NotificationSoundManager.GetNotificationSound("音效四");
                        if (notificationSound != null)
                        {
                            settings.NotificationSoundManager.Play(notificationSound);
                        }
                    }
                }

                // 所有目标（含子目标）都需要移动到新面板
                var goalsPage = FindParentGoalsPage(this);
                goalsPage?.MoveGoalToNewStatus(Goal, oldStatus, Goal.Status);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HandleStatusChange: {ex.Message}");
            }
        }

        private async void CompleteOnceButton_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                Goal.IncrementRecurringCompletion();
                await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);

                // 播放完成提示音
                var settings = SettingsManager.Current;
                if (settings.NotificationSoundManager != null)
                {
                    var notificationSound = settings.NotificationSoundManager.GetNotificationSound("音效四");
                    if (notificationSound != null)
                    {
                        settings.NotificationSoundManager.Play(notificationSound);
                    }
                }

                UpdateUI();
                AnimateBadgePulse();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CompleteOnceButton_Click: {ex.Message}");
            }
        }

        private async void DeleteButton_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                var confirmWindow = new Views.DeleteConfirmationWindow();
                confirmWindow.Owner = SW.Window.GetWindow(this);
                confirmWindow.ShowDialog();
                if (confirmWindow.Confirmed)
                {
                    await StatisticsService.DeleteSessionsByGoalAsync(Goal);

                    if (Goal.Parent != null)
                    {
                        Goal.Parent.RemoveChild(Goal);
                    }
                    else
                    {
                        GoalsPage.Goals.Remove(Goal);
                    }
                    await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                    // 从父容器中移除自身
                    var parent = this.Parent as SWC.Panel;
                    if (parent != null)
                    {
                        parent.Children.Remove(this);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteButton_Click: {ex.Message}");
            }
        }

        private void DetailMenuItem_Click(object sender, SW.RoutedEventArgs e)
        {
            DetailButton_Click(sender, e);
        }

        private void AddSubgoalMenuItem_Click(object sender, SW.RoutedEventArgs e)
        {
            AddSubgoalButton_Click(sender, e);
        }

        private void DeleteMenuItem_Click(object sender, SW.RoutedEventArgs e)
        {
            DeleteButton_Click(sender, e);
        }

        private void TimelineMenuItem_Click(object sender, SW.RoutedEventArgs e)
        {
            var timelineWindow = new TimelineWindow(Goal);
            timelineWindow.WindowStartupLocation = SW.WindowStartupLocation.CenterScreen;
            timelineWindow.Show();
        }

        private async void SetAsTopLevelMenuItem_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                if (Goal.Parent != null)
                {
                    Goal.Parent.RemoveChild(Goal);
                    Goal.Parent = null;
                    GoalsPage.Goals.Add(Goal);
                    await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                    // 刷新整个页面以更新目标列表
                    var goalsPage = FindParentGoalsPage(this);
                    goalsPage?.RefreshGoals();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SetAsTopLevelMenuItem_Click: {ex.Message}");
            }
        }

        private void ParentGoalMenuItem_SubmenuOpened(object sender, SW.RoutedEventArgs e)
        {
            var parentMenuItem = sender as SWC.MenuItem;
            if (parentMenuItem != null && parentMenuItem.Items.Count == 1 && parentMenuItem.Items[0] is SWC.MenuItem && (parentMenuItem.Items[0] as SWC.MenuItem).Header.ToString() == "加载中...")
            {
                parentMenuItem.Items.Clear();
                AddGoalMenuItems(parentMenuItem, null);
            }
        }

        private void AddGoalMenuItems(SWC.ItemsControl parentContainer, Goal parentGoal)
        {
            var goals = parentGoal == null ? GoalsPage.Goals.Where(g => g.Parent == null).ToList() : parentGoal.Children;
            var titleCount = GoalDisplayHelper.GetTitleCount(GoalsPage.Goals);

            foreach (var goal in goals)
            {
                if (goal != this.Goal && goal != this.Goal.Parent && goal.Id != "global-goal")
                {
                    var menuItem = new SWC.MenuItem();
                    menuItem.Header = GoalDisplayHelper.GetGoalDisplayName(goal, titleCount);
                    menuItem.Tag = goal;

                    menuItem.PreviewMouseLeftButtonDown += (sender, e) =>
                    {
                        var selectedGoal = (Goal)(sender as SWC.MenuItem).Tag;
                        SetParentGoal(selectedGoal);
                    };

                    parentContainer.Items.Add(menuItem);

                    if (goal.Children.Count > 0)
                    {
                        AddGoalMenuItems(menuItem, goal);
                    }
                }
            }
        }

        private async void SetParentGoal(Goal newParent)
        {
            try
            {
                if (Goal.Parent != null)
                {
                    Goal.Parent.RemoveChild(Goal);
                }
                else
                {
                    GoalsPage.Goals.Remove(Goal);
                }
                newParent.AddChild(Goal);
                await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                GoalContextMenu.IsOpen = false;
                // 刷新整个页面以更新目标层级
                var goalsPage = FindParentGoalsPage(this);
                goalsPage?.RefreshGoals();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SetParentGoal: {ex.Message}");
            }
        }
    }
}
