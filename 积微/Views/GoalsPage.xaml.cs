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
            // 只有进行中状态且是顶层目标才显示在列表中
            if (goal.Status != currentStatus || goal.Parent != null)
                return;
            
            var titleCount = GoalDisplayHelper.GetTitleCount(Goals);
            var goalItem = new GoalItem(goal, titleCount);
            var targetPanel = GetTargetPanel(goal.Status);
            
            // 根据排序规则找到正确的插入位置
            int insertIndex = FindInsertIndex(targetPanel, goal);
            targetPanel.Children.Insert(insertIndex, goalItem);
            
            // 确保面板可见
            targetPanel.Visibility = SW.Visibility.Visible;
        }
        
        /// <summary>将目标从旧状态面板移动到新状态面板。</summary>
        public void MoveGoalToNewStatus(Goal goal, GoalStatus oldStatus, GoalStatus newStatus)
        {
            // 只处理顶层目标
            if (goal.Parent != null)
                return;
            
            // 从旧的面板中移除
            var oldPanel = GetTargetPanel(oldStatus);
            // 查找并移除对应的 GoalItem
            for (int i = oldPanel.Children.Count - 1; i >= 0; i--)
            {
                if (oldPanel.Children[i] is GoalItem goalItem && goalItem.Goal == goal)
                {
                    oldPanel.Children.RemoveAt(i);
                    break;
                }
            }
            
            // 如果旧面板现在为空，隐藏它
            if (oldPanel.Children.Count == 0)
            {
                oldPanel.Visibility = SW.Visibility.Collapsed;
            }
            
            // 添加到新的面板
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
            // 获取当前排序方式
            var sortedGoals = GetSortedGoals();
            int index = 0;
            foreach (var goal in sortedGoals)
            {
                if (goal == newGoal)
                    break;
                // 检查这个目标是否在目标面板中
                if (goal.Status == currentStatus && goal.Parent == null)
                    index++;
            }
            return Math.Min(index, panel.Children.Count);
        }

        private List<Goal> GetSortedGoals()
        {
            if (currentSortBy == SortBy.UpdatedAt)
            {
                return updatedAtSortOrder == SortOrder.Descending
                    ? Goals.OrderByDescending(g => g.UpdatedAt).ToList()
                    : Goals.OrderBy(g => g.UpdatedAt).ToList();
            }
            else
            {
                return createdAtSortOrder == SortOrder.Descending
                    ? Goals.OrderByDescending(g => g.CreatedAt).ToList()
                    : Goals.OrderBy(g => g.CreatedAt).ToList();
            }
        }

        private async Task SaveGoalsAsync()
        {
            await DataStorageService.SaveGoalsAsync(Goals);
        }

        private void UpdateGoalsDisplay()
        {
            ActiveGoalsPanel.Children.Clear();
            CompletedGoalsPanel.Children.Clear();
            FailedGoalsPanel.Children.Clear();
            PendingGoalsPanel.Children.Clear();

            ActiveGoalsPanel.Visibility = SW.Visibility.Collapsed;
            CompletedGoalsPanel.Visibility = SW.Visibility.Collapsed;
            FailedGoalsPanel.Visibility = SW.Visibility.Collapsed;
            PendingGoalsPanel.Visibility = SW.Visibility.Collapsed;

            switch (currentStatus)
            {
                case GoalStatus.Active:
                    StatusTitle.Text = "进行中";
                    break;
                case GoalStatus.Completed:
                    StatusTitle.Text = "已完成";
                    break;
                case GoalStatus.Failed:
                    StatusTitle.Text = "已失败";
                    break;
                case GoalStatus.Pending:
                    StatusTitle.Text = "已搁置";
                    break;
            }

            List<Goal> sortedGoals;
            if (currentSortBy == SortBy.UpdatedAt)
            {
                if (updatedAtSortOrder == SortOrder.Descending)
                {
                    sortedGoals = Goals.OrderByDescending(g => g.UpdatedAt).ToList();
                }
                else
                {
                    sortedGoals = Goals.OrderBy(g => g.UpdatedAt).ToList();
                }
            }
            else
            {
                if (createdAtSortOrder == SortOrder.Descending)
                {
                    sortedGoals = Goals.OrderByDescending(g => g.CreatedAt).ToList();
                }
                else
                {
                    sortedGoals = Goals.OrderBy(g => g.CreatedAt).ToList();
                }
            }

            var titleCount = GoalDisplayHelper.GetTitleCount(Goals);
            foreach (var goal in sortedGoals)
            {
                // 跳过全局目标，不显示在列表中
                if (goal.Id == GlobalGoalId)
                    continue;
                
                // 搜索过滤：标题或描述匹配搜索文本（不区分大小写）
                if (!string.IsNullOrEmpty(_searchText))
                {
                    var titleMatch = (goal.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    var descMatch = (goal.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!titleMatch && !descMatch)
                        continue;
                }
                
                if (goal.Status == currentStatus && goal.Parent == null)
                {
                    var goalItem = new GoalItem(goal, titleCount);
                    switch (currentStatus)
                    {
                        case GoalStatus.Active:
                            ActiveGoalsPanel.Visibility = SW.Visibility.Visible;
                            ActiveGoalsPanel.Children.Add(goalItem);
                            break;
                        case GoalStatus.Completed:
                            CompletedGoalsPanel.Visibility = SW.Visibility.Visible;
                            CompletedGoalsPanel.Children.Add(goalItem);
                            break;
                        case GoalStatus.Failed:
                            FailedGoalsPanel.Visibility = SW.Visibility.Visible;
                            FailedGoalsPanel.Children.Add(goalItem);
                            break;
                        case GoalStatus.Pending:
                            PendingGoalsPanel.Visibility = SW.Visibility.Visible;
                            PendingGoalsPanel.Children.Add(goalItem);
                            break;
                    }
                }
            }
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

            var button = sender as SWC.Button;
            if (button != null)
            {
                button.Background = activeBg;
                button.Foreground = activeFg;

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