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
        public static List<Goal> Goals { get; private set; } = new List<Goal>();
        /// <summary>全局目标对象，用于记录与特定目标无关的内容。</summary>
        public static Goal? GlobalGoal { get; private set; }
        private const string GlobalGoalId = "global-goal";
        private GoalStatus currentStatus = GoalStatus.Active;
        private SortBy currentSortBy = SortBy.UpdatedAt;
        private SortOrder updatedAtSortOrder = SortOrder.Descending;
        private SortOrder createdAtSortOrder = SortOrder.Descending;
        private bool isPageVisible = false;
        private string _searchText = string.Empty;
        private bool isAllTab = false;
        /// <summary>目标 ID → GoalItem 映射，用于精确 Widget 移动。</summary>
        private Dictionary<string, Controls.GoalItem> _goalItemMap = new Dictionary<string, Controls.GoalItem>();
        /// <summary>当前刷新周期内缓存的排序列表，避免 FindInsertIndex 重复排序。</summary>
        private List<Goal>? _cachedSortedGoals;

        public GoalsPage()
        {
            InitializeComponent();
            Loaded += GoalsPage_Loaded;
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
            if (GlobalGoal != null)
            {
                var timelineWindow = new TimelineWindow(GlobalGoal);
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
                Goals = await DataStorageService.LoadGoalsAsync();
                DataStorageService.GoalsProvider = () => Goals;

                // 查找或创建全局目标
                GlobalGoal = Goals.FirstOrDefault(g => g.Id == GlobalGoalId);
                if (GlobalGoal == null)
                {
                    GlobalGoal = new Goal("全局目标", "记录与任何目标无关的想法和内容")
                    {
                        Id = GlobalGoalId
                    };
                    // 移除创建时自动添加的第一条时间线
                    GlobalGoal.Timeline.Clear();
                    Goals.Add(GlobalGoal);
                    await DataStorageService.SaveGoalsAsync(Goals);
                }

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
                    var newGoal = new Goal(addGoalWindow.GoalTitle, addGoalWindow.GoalDescription);
                    newGoal.Type = addGoalWindow.SelectedGoalType;
                    Goals.Add(newGoal);
                    await DataStorageService.SaveGoalsAsync(Goals);

                    // 只添加新目标到对应面板，不清空所有控件
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
            if (isAllTab)
            {
                // "全部" Tab：子目标始终嵌套在父目标下
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
                // 状态 Tab：只添加匹配当前状态的
                if (goal.Status != currentStatus)
                    return;

                // 如果父目标也在当前 Tab，嵌套到父目标下
                if (goal.Parent != null && goal.Parent.Status == currentStatus)
                {
                    if (_goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                    return;
                }
            }

            // 独立渲染
            var titleCount = GoalDisplayHelper.GetTitleCount(Goals);
            var targetPanel = isAllTab ? AllGoalsPanel : GetTargetPanel(currentStatus);

            bool showParentRef = goal.Parent != null && !isAllTab;
            GoalStatus? childFilter = isAllTab ? null : (GoalStatus?)currentStatus;
            var goalItem = new Controls.GoalItem(goal, titleCount, childFilter, showParentRef, goalItemMap: _goalItemMap, searchText: _searchText);

            int insertIndex = FindInsertIndex(targetPanel, goal);
            targetPanel.Children.Insert(insertIndex, goalItem);
            targetPanel.Visibility = SW.Visibility.Visible;
            _goalItemMap[goal.Id] = goalItem;
        }

        /// <summary>将目标从旧状态面板移动到新状态面板。</summary>
        public void MoveGoalToNewStatus(Goal goal, GoalStatus oldStatus, GoalStatus newStatus)
        {
            // "全部" Tab：不需要移动 Widget，只需刷新以更新颜色
            if (isAllTab)
            {
                if (_goalItemMap.TryGetValue(goal.Id, out var allGoalItem))
                {
                    allGoalItem.UpdateUI();
                    // 子目标状态变化可能影响父目标的子目标列表
                    if (goal.Parent != null && _goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                }
                return;
            }

            // 从旧位置移除
            if (_goalItemMap.TryGetValue(goal.Id, out var goalItem))
            {
                var parentPanel = goalItem.Parent as SWC.Panel;
                parentPanel?.Children.Remove(goalItem);
                _goalItemMap.Remove(goal.Id);

                // 如果是从父目标的 ChildrenPanel 中移除，刷新父目标的子目标列表
                if (goal.Parent != null && goal.Parent.Status == oldStatus)
                {
                    if (_goalItemMap.TryGetValue(goal.Parent.Id, out var parentItem))
                    {
                        parentItem.LoadChildren();
                    }
                }

                // 检查旧面板是否为空
                CheckPanelVisibility(oldStatus);
            }

            // 添加到新位置
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
            var sortedGoals = _cachedSortedGoals ?? GetSortedGoals();
            int index = 0;
            foreach (var goal in sortedGoals)
            {
                if (goal == newGoal)
                    break;

                bool goalInPanel;
                if (isAllTab)
                {
                    // "全部" Tab：统计所有顶层目标
                    goalInPanel = goal.Parent == null;
                }
                else
                {
                    // 状态 Tab：统计匹配当前状态的目标（含独立子目标）
                    goalInPanel = goal.Status == currentStatus &&
                        (goal.Parent == null || goal.Parent.Status != currentStatus);
                }

                if (goalInPanel)
                    index++;
            }
            return Math.Min(index, panel.Children.Count);
        }

        private void UpdateStatusTitle()
        {
            if (isAllTab)
            {
                StatusTitle.Text = "全部目标";
            }
            else
            {
                StatusTitle.Text = currentStatus switch
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

        private List<Goal> GetSortedGoals()
        {
            var allGoalsSet = new HashSet<Goal>();
            CollectAllGoals(Goals, allGoalsSet);

            if (currentSortBy == SortBy.UpdatedAt)
            {
                return updatedAtSortOrder == SortOrder.Descending
                    ? allGoalsSet.OrderByDescending(g => g.UpdatedAt).ToList()
                    : allGoalsSet.OrderBy(g => g.UpdatedAt).ToList();
            }
            else
            {
                return createdAtSortOrder == SortOrder.Descending
                    ? allGoalsSet.OrderByDescending(g => g.CreatedAt).ToList()
                    : allGoalsSet.OrderBy(g => g.CreatedAt).ToList();
            }
        }

        private void CollectAllGoals(List<Goal> source, HashSet<Goal> result)
        {
            foreach (var goal in source)
            {
                if (result.Add(goal))
                    CollectAllGoals(goal.Children, result);
            }
        }

        /// <summary>将目标及其所有匹配指定状态的子孙目标加入可见 ID 集合，用于搜索时保持层级结构。</summary>
        private void AddDescendantsInStatus(Goal goal, GoalStatus status, HashSet<string> visibleIds)
        {
            if (goal.Status != status)
                return;
            visibleIds.Add(goal.Id);
            foreach (var child in goal.Children)
                AddDescendantsInStatus(child, status, visibleIds);
        }

        private async Task SaveGoalsAsync()
        {
            await DataStorageService.SaveGoalsAsync(Goals);
        }

        private void UpdateGoalsDisplay()
        {
            // 清空所有面板并重置
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

            // 缓存排序列表，避免 FindInsertIndex 重复排序
            _cachedSortedGoals = GetSortedGoals();
            List<Goal> sortedGoals = _cachedSortedGoals;
            var titleCount = GoalDisplayHelper.GetTitleCount(Goals);

            if (isAllTab)
            {
                // "全部" Tab：显示所有顶层目标，完整层级，保留各自状态颜色
                AllGoalsPanel.Visibility = SW.Visibility.Visible;

                // 收集搜索匹配的顶层祖先 ID（含子目标匹配时追溯到顶层）
                HashSet<string>? visibleTopLevelIds = null;
                if (!string.IsNullOrEmpty(_searchText))
                {
                    visibleTopLevelIds = new HashSet<string>();
                    foreach (var goal in sortedGoals)
                    {
                        if (goal.Id == GlobalGoalId)
                            continue;
                        var titleMatch = (goal.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        var descMatch = (goal.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (titleMatch || descMatch)
                        {
                            // 追溯到顶层祖先
                            var ancestor = goal;
                            while (ancestor.Parent != null)
                                ancestor = ancestor.Parent;
                            visibleTopLevelIds.Add(ancestor.Id);
                        }
                    }
                }

                foreach (var goal in sortedGoals)
                {
                    if (goal.Id == GlobalGoalId)
                        continue;
                    if (goal.Parent != null)
                        continue; // 只渲染顶层目标，子目标通过层级渲染

                    if (visibleTopLevelIds != null && !visibleTopLevelIds.Contains(goal.Id))
                        continue;

                    var goalItem = new Controls.GoalItem(goal, titleCount, childFilterStatus: null, goalItemMap: _goalItemMap, searchText: _searchText);
                    AllGoalsPanel.Children.Add(goalItem);
                    _goalItemMap[goal.Id] = goalItem;
                }
            }
            else
            {
                // 状态 Tab：显示匹配 currentStatus 的目标，按层级组织
                var statusGoals = sortedGoals
                    .Where(g => g.Status == currentStatus && g.Id != GlobalGoalId)
                    .ToList();

                // 搜索过滤：收集匹配搜索的目标，并追溯到顶层祖先，保持层级
                HashSet<string>? visibleIds = null;
                if (!string.IsNullOrEmpty(_searchText))
                {
                    visibleIds = new HashSet<string>();
                    foreach (var goal in statusGoals)
                    {
                        var titleMatch = (goal.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        var descMatch = (goal.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (titleMatch || descMatch)
                        {
                            // 追溯到当前 Tab 中的顶层祖先（父目标可能不在当前状态 Tab，此时子目标独立显示）
                            var ancestor = goal;
                            while (ancestor.Parent != null && ancestor.Parent.Status == currentStatus)
                                ancestor = ancestor.Parent;
                            // 同时把该祖先的所有子孙（匹配当前状态的）也加入可见集，以保持层级渲染
                            AddDescendantsInStatus(ancestor, currentStatus, visibleIds);
                        }
                    }
                }

                var matchingGoals = visibleIds != null
                    ? statusGoals.Where(g => visibleIds.Contains(g.Id)).ToList()
                    : statusGoals;

                var targetPanel = GetTargetPanel(currentStatus);
                targetPanel.Visibility = SW.Visibility.Visible;

                var matchingIds = new HashSet<string>(matchingGoals.Select(g => g.Id));

                foreach (var goal in matchingGoals)
                {
                    if (goal.Parent == null)
                    {
                        // 顶层目标
                        var goalItem = new Controls.GoalItem(goal, titleCount, childFilterStatus: currentStatus, goalItemMap: _goalItemMap, searchText: _searchText);
                        targetPanel.Children.Add(goalItem);
                        _goalItemMap[goal.Id] = goalItem;
                    }
                    else if (!matchingIds.Contains(goal.Parent.Id))
                    {
                        // 子目标独立显示（父目标不在当前 Tab）
                        var goalItem = new Controls.GoalItem(goal, titleCount, childFilterStatus: currentStatus, showParentRef: true, goalItemMap: _goalItemMap, searchText: _searchText);
                        targetPanel.Children.Add(goalItem);
                        _goalItemMap[goal.Id] = goalItem;
                    }
                    // 否则：子目标由父目标层级渲染，跳过
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

            if (isAllTab)
            {
                AllButton.Background = activeBg;
                AllButton.Foreground = activeFg;
                return;
            }

            switch (currentStatus)
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
                    isAllTab = true;
                }
                else
                {
                    isAllTab = false;
                    switch (button.Name)
                    {
                        case "ActiveButton":
                            currentStatus = GoalStatus.Active;
                            break;
                        case "CompletedButton":
                            currentStatus = GoalStatus.Completed;
                            break;
                        case "FailedButton":
                            currentStatus = GoalStatus.Failed;
                            break;
                        case "PendingButton":
                            currentStatus = GoalStatus.Pending;
                            break;
                    }
                }

                UpdateGoalsDisplay();
            }
        }

        private void UpdateSortIcons()
        {
            var sortByUpdatedAtIcon = SortByUpdatedAtButton.Template.FindName("SortByUpdatedAtIcon", SortByUpdatedAtButton) as SWShapes.Path;
            var sortByCreatedAtIcon = SortByCreatedAtButton.Template.FindName("SortByCreatedAtIcon", SortByCreatedAtButton) as SWShapes.Path;

            if (sortByUpdatedAtIcon != null)
            {
                sortByUpdatedAtIcon.Data = SWM.Geometry.Parse(updatedAtSortOrder == SortOrder.Descending ? "M6,12L12,6L18,12" : "M6,6L12,12L18,6");
            }

            if (sortByCreatedAtIcon != null)
            {
                sortByCreatedAtIcon.Data = SWM.Geometry.Parse(createdAtSortOrder == SortOrder.Descending ? "M6,12L12,6L18,12" : "M6,6L12,12L18,6");
            }
        }

        /// <summary>根据当前主题资源重新应用排序按钮颜色（用于主题切换后刷新）。</summary>
        private void RefreshSortButtonColors()
        {
            var inactiveBg = (SWM.SolidColorBrush)FindResource("HoverColor");
            var inactiveStroke = (SWM.SolidColorBrush)FindResource("TextSecondary");
            var activeBg = (SWM.SolidColorBrush)FindResource("AccentColor");

            var sortByUpdatedAtIcon = SortByUpdatedAtButton.Template.FindName("SortByUpdatedAtIcon", SortByUpdatedAtButton) as SWShapes.Path;
            var sortByCreatedAtIcon = SortByCreatedAtButton.Template.FindName("SortByCreatedAtIcon", SortByCreatedAtButton) as SWShapes.Path;

            if (currentSortBy == SortBy.UpdatedAt)
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
                        if (currentSortBy == SortBy.UpdatedAt)
                        {
                            updatedAtSortOrder = updatedAtSortOrder == SortOrder.Descending ? SortOrder.Ascending : SortOrder.Descending;
                        }
                        currentSortBy = SortBy.UpdatedAt;
                        button.Background = activeBg;
                        if (sortByUpdatedAtIcon != null) sortByUpdatedAtIcon.Stroke = SWM.Brushes.White;
                        break;
                    case "SortByCreatedAtButton":
                        if (currentSortBy == SortBy.CreatedAt)
                        {
                            createdAtSortOrder = createdAtSortOrder == SortOrder.Descending ? SortOrder.Ascending : SortOrder.Descending;
                        }
                        currentSortBy = SortBy.CreatedAt;
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

        private void SearchTextBox_TextChanged(object sender, SWC.TextChangedEventArgs e)
        {
            _searchText = SearchTextBox.Text?.Trim() ?? string.Empty;
            ClearSearchButton.Visibility = string.IsNullOrEmpty(_searchText)
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