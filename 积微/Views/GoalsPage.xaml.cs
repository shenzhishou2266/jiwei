using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWShapes = System.Windows.Shapes;
using 积微.Controls;
using 积微.Models;
using 积微.Services;
using 积微.Helpers;
using 积微.ViewModels;

namespace 积微.Views
{
    /// <summary>排序依据枚举。</summary>
    public enum SortBy
    {
        /// <summary>按更新时间排序</summary>
        UpdatedAt,
        /// <summary>按创建时间排序</summary>
        CreatedAt
    }

    /// <summary>排序方式枚举。</summary>
    public enum SortOrder
    {
        /// <summary>升序</summary>
        Ascending,
        /// <summary>降序</summary>
        Descending
    }

    /// <summary>目标管理页面，支持目标的增删改查、状态切换和排序。</summary>
    public partial class GoalsPage : SWC.UserControl
    {
        /// <summary>全局目标列表。</summary>
        public static List<Goal> Goals => ViewModel?.Goals ?? new List<Goal>();
        /// <summary>全局目标对象，用于记录与特定目标无关的内容。</summary>
        public static Goal? GlobalGoal => ViewModel?.GlobalGoal;
        private const string GlobalGoalId = "global-goal";
        /// <summary>目标 ID → GoalItem 映射，用于精确 Widget 移动。</summary>
        private Dictionary<string, Controls.GoalItem> _goalItemMap = new Dictionary<string, Controls.GoalItem>();
        /// <summary>当前刷新周期内缓存的排序列表，避免 FindInsertIndex 重复排序。</summary>
        private List<Goal>? _cachedSortedGoals;

        private static GoalsViewModel? _viewModel;
        /// <summary>共享的 ViewModel 实例。</summary>
        public static GoalsViewModel? ViewModel => _viewModel;

        public GoalsPage()
        {
            InitializeComponent();
            Loaded += GoalsPage_Loaded;
            _viewModel = AppServices.Provider.GetRequiredService<GoalsViewModel>();
            if (!_viewModel.IsLoaded)
                LoadGoalsAsync();
            UpdateSortIcons();
        }

        private void GoalsPage_Loaded(object sender, SW.RoutedEventArgs e)
        {
            RefreshStatusButtonColors();
            RefreshSortButtonColors();
        }

        private void GlobalGoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            if (_viewModel?.GlobalGoal != null)
            {
                var timelineWindow = new TimelineWindow(_viewModel.GlobalGoal);
                timelineWindow.WindowStartupLocation = SW.WindowStartupLocation.CenterScreen;
                timelineWindow.Show();
            }
        }

        /// <summary>设置页面可见性，可见时刷新目标列表并重新应用主题颜色。</summary>
        public void SetPageVisible(bool visible)
        {
            isPageVisible = visible;
            if (visible)
            {
                RefreshStatusButtonColors();
                RefreshSortButtonColors();
                UpdateGoalsDisplay();
            }
        }

        private async void LoadGoalsAsync()
        {
            try
            {
                await _viewModel!.LoadGoalsAsync();
                UpdateGoalsDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadGoalsAsync: {ex.Message}");
            }
        }

        private async void AddGoalButton_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                var addGoalWindow = new AddGoalWindow();
                addGoalWindow.WindowStartupLocation = SW.WindowStartupLocation.CenterScreen;
                addGoalWindow.GoalAdded += async (s, args) =>
                {
                    await _viewModel!.AddGoalAsync(addGoalWindow.GoalTitle, addGoalWindow.GoalDescription, addGoalWindow.SelectedGoalType);
                    var newGoal = _viewModel.Goals.Last();
                    AddGoalToPanel(newGoal);
                };
                addGoalWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddGoalButton_Click: {ex.Message}");
            }
        }

        private void AddGoalToPanel(Goal goal)
        {
            var vm = _viewModel!;
            if (vm.IsAllTab)
            {
                if (goal.Parent != null)
                {
                    if (_goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                    return;
                }
            }
            else
            {
                if (goal.Status != vm.CurrentStatus)
                    return;

                if (goal.Parent != null && goal.Parent.Status == vm.CurrentStatus)
                {
                    if (_goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                    return;
                }
            }

            var titleCount = GoalDisplayHelper.GetTitleCount(vm.Goals);
            var targetPanel = vm.IsAllTab ? AllGoalsPanel : GetTargetPanel(vm.CurrentStatus);

            bool showParentRef = goal.Parent != null && !vm.IsAllTab;
            GoalStatus? childFilter = vm.IsAllTab ? null : (GoalStatus?)vm.CurrentStatus;
            var goalItem = new Controls.GoalItem(goal, titleCount, childFilter, showParentRef, goalItemMap: _goalItemMap, searchText: vm.SearchText);

            int insertIndex = FindInsertIndex(targetPanel, goal);
            targetPanel.Children.Insert(insertIndex, goalItem);
            targetPanel.Visibility = SW.Visibility.Visible;
            _goalItemMap[goal.Id] = goalItem;
        }

        /// <summary>将目标从旧状态面板移动到新状态面板（状态变更由 HandleStatusChange 中的 statusAction 完成）。</summary>
        public void MoveGoalToNewStatus(Goal goal, GoalStatus oldStatus, GoalStatus newStatus)
        {
            var vm = _viewModel!;

            if (vm.IsAllTab)
            {
                if (_goalItemMap.TryGetValue(goal.Id, out var allGoalItem))
                {
                    allGoalItem.UpdateUI();
                    if (goal.Parent != null && _goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                }
                return;
            }

            if (_goalItemMap.TryGetValue(goal.Id, out var goalItem))
            {
                var parentPanel = goalItem.Parent as SWC.Panel;
                parentPanel?.Children.Remove(goalItem);
                _goalItemMap.Remove(goal.Id);

                if (goal.Parent != null && goal.Parent.Status == oldStatus)
                {
                    if (_goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                }

                CheckPanelVisibility(oldStatus);
            }

            AddGoalToPanel(goal);
        }

        private SWC.StackPanel GetTargetPanel(GoalStatus status)
        {
            return status switch
            {
                GoalStatus.Active => ActiveGoalsPanel,
                GoalStatus.Completed => CompletedGoalsPanel,
                GoalStatus.Failed => FailedGoalsPanel,
                GoalStatus.Pending => PendingGoalsPanel,
                _ => ActiveGoalsPanel
            };
        }

        private int FindInsertIndex(SWC.StackPanel panel, Goal newGoal)
        {
            var sortedGoals = _cachedSortedGoals ?? _viewModel!.GetSortedGoals();
            var vm = _viewModel!;
            int index = 0;
            foreach (var goal in sortedGoals)
            {
                if (goal == newGoal)
                    break;

                bool goalInPanel;
                if (vm.IsAllTab)
                {
                    goalInPanel = goal.Parent == null;
                }
                else
                {
                    goalInPanel = goal.Status == vm.CurrentStatus &&
                        (goal.Parent == null || goal.Parent.Status != vm.CurrentStatus);
                }

                if (goalInPanel)
                    index++;
            }
            return Math.Min(index, panel.Children.Count);
        }

        private void UpdateStatusTitle()
        {
            var vm = _viewModel!;
            if (vm.IsAllTab)
            {
                StatusTitle.Text = "全部目标";
            }
            else
            {
                StatusTitle.Text = vm.CurrentStatus switch
                {
                    GoalStatus.Active => "进行中",
                    GoalStatus.Completed => "已完成",
                    GoalStatus.Failed => "已失败",
                    GoalStatus.Pending => "已搁置",
                    _ => "进行中"
                };
            }
        }

        private void CheckPanelVisibility(GoalStatus status)
        {
            var panel = GetTargetPanel(status);
            if (panel.Children.Count == 0)
            {
                panel.Visibility = SW.Visibility.Collapsed;
            }
        }

        private void UpdateGoalsDisplay()
        {
            var vm = _viewModel!;
            ActiveGoalsPanel.Children.Clear();
            CompletedGoalsPanel.Children.Clear();
            FailedGoalsPanel.Children.Clear();
            PendingGoalsPanel.Children.Clear();
            AllGoalsPanel.Children.Clear();

            ActiveGoalsPanel.Visibility = SW.Visibility.Collapsed;
            CompletedGoalsPanel.Visibility = SW.Visibility.Collapsed;
            FailedGoalsPanel.Visibility = SW.Visibility.Collapsed;
            PendingGoalsPanel.Visibility = SW.Visibility.Collapsed;
            AllGoalsPanel.Visibility = SW.Visibility.Collapsed;

            _goalItemMap.Clear();
            UpdateStatusTitle();

            _cachedSortedGoals = vm.GetSortedGoals();
            var visibleGoals = vm.GetVisibleGoals();
            var titleCount = GoalDisplayHelper.GetTitleCount(vm.Goals);

            if (vm.IsAllTab)
            {
                AllGoalsPanel.Visibility = SW.Visibility.Visible;
                foreach (var goal in visibleGoals)
                {
                    var goalItem = new Controls.GoalItem(goal, titleCount, childFilterStatus: null, goalItemMap: _goalItemMap, searchText: vm.SearchText);
                    AllGoalsPanel.Children.Add(goalItem);
                    _goalItemMap[goal.Id] = goalItem;
                }
            }
            else
            {
                var targetPanel = GetTargetPanel(vm.CurrentStatus);
                targetPanel.Visibility = SW.Visibility.Visible;

                foreach (var goal in visibleGoals)
                {
                    if (goal.Parent == null)
                    {
                        var goalItem = new Controls.GoalItem(goal, titleCount, childFilterStatus: vm.CurrentStatus, goalItemMap: _goalItemMap, searchText: vm.SearchText);
                        targetPanel.Children.Add(goalItem);
                        _goalItemMap[goal.Id] = goalItem;
                    }
                    else
                    {
                        var goalItem = new Controls.GoalItem(goal, titleCount, childFilterStatus: vm.CurrentStatus, showParentRef: true, goalItemMap: _goalItemMap, searchText: vm.SearchText);
                        targetPanel.Children.Add(goalItem);
                        _goalItemMap[goal.Id] = goalItem;
                    }
                }
            }
            _cachedSortedGoals = null;
        }

        /// <summary>根据当前主题资源重新应用状态按钮颜色（用于主题切换后刷新）。</summary>
        private void RefreshStatusButtonColors()
        {
            var inactiveBg = (SWM.SolidColorBrush)FindResource("HoverColor");
            var inactiveFg = (SWM.SolidColorBrush)FindResource("TextSecondary");
            var activeBg = (SWM.SolidColorBrush)FindResource("AccentColor");
            var activeFg = SWM.Brushes.White;
            var vm = _viewModel!;

            ActiveButton.Background = inactiveBg;
            ActiveButton.Foreground = inactiveFg;
            CompletedButton.Background = inactiveBg;
            CompletedButton.Foreground = inactiveFg;
            FailedButton.Background = inactiveBg;
            FailedButton.Foreground = inactiveFg;
            PendingButton.Background = inactiveBg;
            PendingButton.Foreground = inactiveFg;
            AllButton.Background = inactiveBg;
            AllButton.Foreground = inactiveFg;

            if (vm.IsAllTab)
            {
                AllButton.Background = activeBg;
                AllButton.Foreground = activeFg;
                return;
            }

            switch (vm.CurrentStatus)
            {
                case GoalStatus.Active:
                    ActiveButton.Background = activeBg;
                    ActiveButton.Foreground = activeFg;
                    break;
                case GoalStatus.Completed:
                    CompletedButton.Background = activeBg;
                    CompletedButton.Foreground = activeFg;
                    break;
                case GoalStatus.Failed:
                    FailedButton.Background = activeBg;
                    FailedButton.Foreground = activeFg;
                    break;
                case GoalStatus.Pending:
                    PendingButton.Background = activeBg;
                    PendingButton.Foreground = activeFg;
                    break;
            }
        }

        private void StatusButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var vm = _viewModel!;
            var inactiveBg = (SWM.SolidColorBrush)FindResource("HoverColor");
            var inactiveFg = (SWM.SolidColorBrush)FindResource("TextSecondary");
            var activeBg = (SWM.SolidColorBrush)FindResource("AccentColor");
            var activeFg = SWM.Brushes.White;

            ActiveButton.Background = inactiveBg;
            ActiveButton.Foreground = inactiveFg;
            CompletedButton.Background = inactiveBg;
            CompletedButton.Foreground = inactiveFg;
            FailedButton.Background = inactiveBg;
            FailedButton.Foreground = inactiveFg;
            PendingButton.Background = inactiveBg;
            PendingButton.Foreground = inactiveFg;
            AllButton.Background = inactiveBg;
            AllButton.Foreground = inactiveFg;

            var button = sender as SWC.Button;
            if (button != null)
            {
                button.Background = activeBg;
                button.Foreground = activeFg;

                if (button.Name == "AllButton")
                {
                    vm.IsAllTab = true;
                }
                else
                {
                    vm.IsAllTab = false;
                    switch (button.Name)
                    {
                        case "ActiveButton":
                            vm.CurrentStatus = GoalStatus.Active;
                            break;
                        case "CompletedButton":
                            vm.CurrentStatus = GoalStatus.Completed;
                            break;
                        case "FailedButton":
                            vm.CurrentStatus = GoalStatus.Failed;
                            break;
                        case "PendingButton":
                            vm.CurrentStatus = GoalStatus.Pending;
                            break;
                    }
                }

                UpdateGoalsDisplay();
            }
        }

        private void UpdateSortIcons()
        {
            var vm = _viewModel!;
            var sortByUpdatedAtIcon = SortByUpdatedAtButton.Template.FindName("SortByUpdatedAtIcon", SortByUpdatedAtButton) as SWShapes.Path;
            var sortByCreatedAtIcon = SortByCreatedAtButton.Template.FindName("SortByCreatedAtIcon", SortByCreatedAtButton) as SWShapes.Path;

            if (sortByUpdatedAtIcon != null)
            {
                sortByUpdatedAtIcon.Data = SWM.Geometry.Parse(vm.UpdatedAtSortOrder == SortOrder.Descending ? "M6,12L12,6L18,12" : "M6,6L12,12L18,6");
            }

            if (sortByCreatedAtIcon != null)
            {
                sortByCreatedAtIcon.Data = SWM.Geometry.Parse(vm.CreatedAtSortOrder == SortOrder.Descending ? "M6,12L12,6L18,12" : "M6,6L12,12L18,6");
            }
        }

        /// <summary>根据当前主题资源重新应用排序按钮颜色（用于主题切换后刷新）。</summary>
        private void RefreshSortButtonColors()
        {
            var vm = _viewModel!;
            var inactiveBg = (SWM.SolidColorBrush)FindResource("HoverColor");
            var inactiveStroke = (SWM.SolidColorBrush)FindResource("TextSecondary");
            var activeBg = (SWM.SolidColorBrush)FindResource("AccentColor");

            var sortByUpdatedAtIcon = SortByUpdatedAtButton.Template.FindName("SortByUpdatedAtIcon", SortByUpdatedAtButton) as SWShapes.Path;
            var sortByCreatedAtIcon = SortByCreatedAtButton.Template.FindName("SortByCreatedAtIcon", SortByCreatedAtButton) as SWShapes.Path;

            if (vm.CurrentSortBy == SortBy.UpdatedAt)
            {
                SortByUpdatedAtButton.Background = activeBg;
                SortByCreatedAtButton.Background = inactiveBg;
                if (sortByUpdatedAtIcon != null) sortByUpdatedAtIcon.Stroke = SWM.Brushes.White;
                if (sortByCreatedAtIcon != null) sortByCreatedAtIcon.Stroke = inactiveStroke;
            }
            else
            {
                SortByCreatedAtButton.Background = activeBg;
                SortByUpdatedAtButton.Background = inactiveBg;
                if (sortByCreatedAtIcon != null) sortByCreatedAtIcon.Stroke = SWM.Brushes.White;
                if (sortByUpdatedAtIcon != null) sortByUpdatedAtIcon.Stroke = inactiveStroke;
            }
        }

        private void SortButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var vm = _viewModel!;
            var button = sender as SWC.Button;
            if (button != null)
            {
                var inactiveBg = (SWM.SolidColorBrush)FindResource("HoverColor");
                var inactiveIconStroke = (SWM.SolidColorBrush)FindResource("TextSecondary");
                var activeBg = (SWM.SolidColorBrush)FindResource("AccentColor");

                SortByUpdatedAtButton.Background = inactiveBg;
                SortByCreatedAtButton.Background = inactiveBg;

                var sortByUpdatedAtIcon = SortByUpdatedAtButton.Template.FindName("SortByUpdatedAtIcon", SortByUpdatedAtButton) as SWShapes.Path;
                var sortByCreatedAtIcon = SortByCreatedAtButton.Template.FindName("SortByCreatedAtIcon", SortByCreatedAtButton) as SWShapes.Path;
                if (sortByUpdatedAtIcon != null) sortByUpdatedAtIcon.Stroke = inactiveIconStroke;
                if (sortByCreatedAtIcon != null) sortByCreatedAtIcon.Stroke = inactiveIconStroke;

                switch (button.Name)
                {
                    case "SortByUpdatedAtButton":
                        if (vm.CurrentSortBy == SortBy.UpdatedAt)
                        {
                            vm.UpdatedAtSortOrder = vm.UpdatedAtSortOrder == SortOrder.Descending ? SortOrder.Ascending : SortOrder.Descending;
                        }
                        vm.CurrentSortBy = SortBy.UpdatedAt;
                        button.Background = activeBg;
                        if (sortByUpdatedAtIcon != null) sortByUpdatedAtIcon.Stroke = SWM.Brushes.White;
                        break;
                    case "SortByCreatedAtButton":
                        if (vm.CurrentSortBy == SortBy.CreatedAt)
                        {
                            vm.CreatedAtSortOrder = vm.CreatedAtSortOrder == SortOrder.Descending ? SortOrder.Ascending : SortOrder.Descending;
                        }
                        vm.CurrentSortBy = SortBy.CreatedAt;
                        button.Background = activeBg;
                        if (sortByCreatedAtIcon != null) sortByCreatedAtIcon.Stroke = SWM.Brushes.White;
                        break;
                }

                UpdateSortIcons();
                UpdateGoalsDisplay();
            }
        }

        /// <summary>刷新整个目标列表的显示。</summary>
        public void RefreshGoals()
        {
            UpdateGoalsDisplay();
        }

        private bool isPageVisible = false;
        private void SearchTextBox_TextChanged(object sender, SWC.TextChangedEventArgs e)
        {
            _viewModel!.SearchText = SearchTextBox.Text?.Trim() ?? string.Empty;
            ClearSearchButton.Visibility = string.IsNullOrEmpty(_viewModel.SearchText)
                ? SW.Visibility.Collapsed
                : SW.Visibility.Visible;
            UpdateGoalsDisplay();
        }

        private void ClearSearchButton_Click(object sender, SW.RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
        }

        private void SearchTextBox_GotFocus(object sender, SW.RoutedEventArgs e)
        {
            SearchBorder.BorderBrush = (SWM.SolidColorBrush)FindResource("AccentColor");
            SearchBorder.BorderThickness = new SW.Thickness(1.5);
        }

        private void SearchTextBox_LostFocus(object sender, SW.RoutedEventArgs e)
        {
            SearchBorder.BorderBrush = (SWM.SolidColorBrush)FindResource("BorderColor");
            SearchBorder.BorderThickness = new SW.Thickness(1);
        }
    }
}