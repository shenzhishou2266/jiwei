using System;
using System.Linq;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using 积微.Models;
using 积微.Services;
using 积微.Controls;
using 积微.ViewModels;

namespace 积微.Views
{
    /// <summary>统计报表窗口，支持日报、周报、月报、年报和自定义时间范围五种类型。</summary>
    public partial class ReportWindow : SW.Window
    {
        /// <summary>报表类型枚举。</summary>
        public enum ReportType
        {
            /// <summary>日报</summary>
            Daily,
            /// <summary>周报</summary>
            Weekly,
            /// <summary>月报</summary>
            Monthly,
            /// <summary>年报</summary>
            Yearly,
            /// <summary>自定义</summary>
            Custom
        }

        private ReportType CurrentReportType = ReportType.Daily;
        private DateTime CurrentDate = DateTime.Today;
        private DateTime CurrentEndDate = DateTime.Today;

        /// <summary>目标统计排序字段</summary>
        private enum GoalSortField
        {
            番茄钟数量,
            专注时间,
            时间碎片数量,
            碎片时长,
            总计时间,
            专注会话数
        }

        private GoalSortField CurrentSortField = GoalSortField.专注会话数;
        private string GoalSearchText = string.Empty;

        public ReportWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += OnClosed;
            LoadReport();
        }

        private void OnLoaded(object sender, SW.RoutedEventArgs e)
        {
            App.RegisterThemeWindow(this);
            UpdateCalendarVisibility();
            DailyCalendarPicker.DateDisplayText.FontSize = 16;
            DailyCalendarPicker.DateDisplayText.FontWeight = SW.FontWeights.Bold;
            CustomStartCalendarPicker.DateDisplayText.FontSize = 16;
            CustomStartCalendarPicker.DateDisplayText.FontWeight = SW.FontWeights.Bold;
            CustomEndCalendarPicker.DateDisplayText.FontSize = 16;
            CustomEndCalendarPicker.DateDisplayText.FontWeight = SW.FontWeights.Bold;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
        }

        private async void LoadReport()
        {
            try
            {
                ReportStats? stats = null;
                string dateRangeText = "";

                switch (CurrentReportType)
                {
                    case ReportType.Daily:
                        stats = await StatisticsService.GetDailyReportAsync(CurrentDate);
                        dateRangeText = stats.StartDate.ToString("yyyy年MM月dd日");
                        break;
                    case ReportType.Weekly:
                        stats = await StatisticsService.GetWeeklyReportAsync(CurrentDate);
                        dateRangeText = $"{stats.StartDate:yyyy年MM月dd日} - {stats.EndDate:yyyy年MM月dd日}";
                        break;
                    case ReportType.Monthly:
                        stats = await StatisticsService.GetMonthlyReportAsync(CurrentDate.Year, CurrentDate.Month);
                        dateRangeText = $"{stats.StartDate:yyyy年MM月}";
                        break;
                    case ReportType.Yearly:
                        stats = await StatisticsService.GetYearlyReportAsync(CurrentDate.Year);
                        dateRangeText = $"{stats.StartDate:yyyy年}";
                        break;
                    case ReportType.Custom:
                        stats = await StatisticsService.GetReportStatsAsync(CurrentDate.Date, CurrentEndDate.Date.AddDays(1).AddSeconds(-1));
                        await StatisticsService.EnrichReportWithGoalInfoAsync(stats);
                        dateRangeText = $"{stats.StartDate:yyyy年MM月dd日} - {stats.EndDate:yyyy年MM月dd日}";
                        break;
                }

                DateRangeText.Text = dateRangeText;
                RenderReport(stats);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadReport: {ex.Message}");
            }
        }

        private void RenderReport(ReportStats stats)
        {
            ReportContent.Children.Clear();

            // 从目标统计汇总总览数据
            int totalPomodoroCount = stats.GoalStats.Values.Sum(g => g.PomodoroCount);
            int totalPomodoroSeconds = stats.GoalStats.Values.Sum(g => g.PomodoroFocusSeconds);
            int totalFragmentCount = stats.GoalStats.Values.Sum(g => g.FragmentCount);
            int totalFragmentSeconds = stats.GoalStats.Values.Sum(g => g.StopwatchSeconds + g.CountdownSeconds);
            int totalAllSeconds = stats.GoalStats.Values.Sum(g => g.TotalSeconds);

            var pomodoroHours = Math.Round(totalPomodoroSeconds / 3600.0, 1);
            var fragmentHours = Math.Round(totalFragmentSeconds / 3600.0, 1);
            var totalHours = Math.Round(totalAllSeconds / 3600.0, 1);

            int activeGoalCount = stats.GoalStats.Values.Count(g => g.FocusSessions > 0);
            int completedGoalCount = stats.GoalStats.Values.Count(g => g.IsCompleted);

            var firstGoal = stats.GoalStats.Values.FirstOrDefault();
            if (firstGoal != null)
            {
                if (firstGoal.ActiveGoalCount > 0) activeGoalCount = firstGoal.ActiveGoalCount;
                if (firstGoal.CompletedGoalCount > 0) completedGoalCount = firstGoal.CompletedGoalCount;
            }

            double avgPerGoalHours = activeGoalCount > 0 ? totalAllSeconds / 3600.0 / activeGoalCount : 0;

            // 总览卡片
            var totalCard = new SWC.Border
            {
                Style = (SW.Style)FindResource("InteractiveStatCard"),
                Margin = new SW.Thickness(0, 0, 0, 14)
            };

            var totalContent = new SWC.StackPanel();

            var overviewTitleText = new SWC.TextBlock
            {
                Text = "总览",
                FontSize = 15,
                FontWeight = SW.FontWeights.SemiBold,
                Margin = new SW.Thickness(0, 0, 0, 14)
            };
            overviewTitleText.SetResourceReference(SWC.TextBlock.ForegroundProperty, "TextPrimary");
            totalContent.Children.Add(overviewTitleText);

            // 4列网格，容纳8个指标
            var statsGrid = new SWC.Grid();
            for (int i = 0; i < 4; i++)
                statsGrid.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = new SW.GridLength(1, SW.GridUnitType.Star) });
            statsGrid.RowDefinitions.Add(new SWC.RowDefinition { Height = SW.GridLength.Auto });
            statsGrid.RowDefinitions.Add(new SWC.RowDefinition { Height = SW.GridLength.Auto });

            AddStatItem(statsGrid, 0, 0, "番茄钟", totalPomodoroCount.ToString(), "#F97316");
            AddStatItem(statsGrid, 0, 1, "专注时长", $"{StatsViewModel.FormatHours(pomodoroHours)} h", "#3B82F6");
            AddStatItem(statsGrid, 0, 2, "时间碎片", totalFragmentCount.ToString(), "#10B981");
            AddStatItem(statsGrid, 0, 3, "碎片时长", $"{StatsViewModel.FormatHours(fragmentHours)} h", "#14B8A6");

            AddStatItem(statsGrid, 1, 0, "总时长", $"{StatsViewModel.FormatHours(totalHours)} h", "#8B5CF6");
            AddStatItem(statsGrid, 1, 1, "目标均时", $"{StatsViewModel.FormatHours(avgPerGoalHours)} h", "#EC4899");
            AddStatItem(statsGrid, 1, 2, "进行目标", activeGoalCount.ToString(), "#6366F1");
            AddStatItem(statsGrid, 1, 3, "完成目标", completedGoalCount.ToString(), "#F59E0B");

            totalContent.Children.Add(statsGrid);
            totalCard.Child = totalContent;
            ReportContent.Children.Add(totalCard);

            // 目标统计
            if (stats.GoalStats.Count > 0)
            {
                var goalCard = new SWC.Border
                {
                    Style = (SW.Style)FindResource("InteractiveStatCard"),
                    Margin = new SW.Thickness(0, 0, 0, 14)
                };

                var goalContent = new SWC.StackPanel();

                // 标题行：标题 + 搜索框 + 排序下拉
                // Col0:标题 | Col1:填充(星号) | Col2:搜索框 | Col3:排序
                var headerRow = new SWC.Grid();
                headerRow.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = SW.GridLength.Auto });
                headerRow.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = new SW.GridLength(1, SW.GridUnitType.Star) });
                headerRow.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = SW.GridLength.Auto });
                headerRow.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = SW.GridLength.Auto });
                headerRow.Margin = new SW.Thickness(0, 0, 0, 12);

                var goalStatsTitleText = new SWC.TextBlock
                {
                    Text = "目标统计",
                    FontSize = 15,
                    FontWeight = SW.FontWeights.SemiBold,
                    VerticalAlignment = SW.VerticalAlignment.Center
                };
                goalStatsTitleText.SetResourceReference(SWC.TextBlock.ForegroundProperty, "TextPrimary");
                SWC.Grid.SetColumn(goalStatsTitleText, 0);
                headerRow.Children.Add(goalStatsTitleText);

                // 目标列表容器（提前声明，供搜索/排序事件使用）
                var goalItemContainer = new SWC.StackPanel();

                // 搜索框（Border 包裹以支持圆角 + 主题适配）
                var searchBorder = new SWC.Border
                {
                    CornerRadius = new SW.CornerRadius(8),
                    BorderThickness = new SW.Thickness(1),
                    Margin = new SW.Thickness(0, 0, 8, 0),
                    VerticalAlignment = SW.VerticalAlignment.Center,
                    Width = 160,
                    Height = 32
                };
                searchBorder.SetResourceReference(SWC.Border.BackgroundProperty, "CardBackground");
                searchBorder.SetResourceReference(SWC.Border.BorderBrushProperty, "BorderColor");
                SWC.Grid.SetColumn(searchBorder, 2);

                var searchBox = new SWC.TextBox
                {
                    Width = 156,
                    Height = 28,
                    FontSize = 13,
                    BorderThickness = new SW.Thickness(0),
                    Padding = new SW.Thickness(10, 0, 10, 0),
                    VerticalAlignment = SW.VerticalAlignment.Center,
                    VerticalContentAlignment = SW.VerticalAlignment.Center,
                    HorizontalAlignment = SW.HorizontalAlignment.Center
                };
                searchBox.SetResourceReference(SWC.TextBox.CaretBrushProperty, "TextPrimary");

                // 搜索框样式：背景透明
                var searchBoxStyle = new SW.Style(typeof(SWC.TextBox));
                searchBoxStyle.Setters.Add(new SW.Setter(SWC.TextBox.BackgroundProperty, SWM.Brushes.Transparent));
                searchBox.Style = searchBoxStyle;

                // 占位文字模拟（直接用 TextBox.Text，确保与输入文本位置完全一致）
                const string PlaceholderText = "搜索目标...";
                bool isPlaceholder = string.IsNullOrEmpty(GoalSearchText);
                if (isPlaceholder)
                {
                    searchBox.Text = PlaceholderText;
                    searchBox.SetResourceReference(SWC.TextBox.ForegroundProperty, "TextTertiary");
                }
                else
                {
                    searchBox.Text = GoalSearchText;
                    searchBox.SetResourceReference(SWC.TextBox.ForegroundProperty, "TextPrimary");
                }

                searchBox.GotFocus += (s, e) =>
                {
                    if (isPlaceholder)
                    {
                        searchBox.Text = "";
                        searchBox.SetResourceReference(SWC.TextBox.ForegroundProperty, "TextPrimary");
                        isPlaceholder = false;
                    }
                };
                searchBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrEmpty(searchBox.Text))
                    {
                        isPlaceholder = true;
                        searchBox.Text = PlaceholderText;
                        searchBox.SetResourceReference(SWC.TextBox.ForegroundProperty, "TextTertiary");
                    }
                };

                searchBox.TextChanged += (s, e) =>
                {
                    if (!isPlaceholder)
                    {
                        GoalSearchText = searchBox.Text;
                        ReRenderGoals(goalItemContainer, stats);
                    }
                };

                searchBorder.Child = searchBox;

                // 搜索框悬停效果：边框变蓝
                searchBorder.MouseEnter += (s, e) =>
                {
                    searchBorder.BorderBrush = (SWM.Brush)FindResource("AccentColor");
                };
                searchBorder.MouseLeave += (s, e) =>
                {
                    searchBorder.SetResourceReference(SWC.Border.BorderBrushProperty, "BorderColor");
                };

                headerRow.Children.Add(searchBorder);

                // 排序面板：标签 + 下拉
                var sortPanel = new SWC.StackPanel
                {
                    Orientation = SWC.Orientation.Horizontal,
                    VerticalAlignment = SW.VerticalAlignment.Center
                };
                SWC.Grid.SetColumn(sortPanel, 3);

                var sortLabel = new SWC.TextBlock
                {
                    Text = "排序",
                    FontSize = 12,
                    VerticalAlignment = SW.VerticalAlignment.Center,
                    Margin = new SW.Thickness(0, 0, 6, 0)
                };
                sortLabel.SetResourceReference(SWC.TextBlock.ForegroundProperty, "TextTertiary");
                sortPanel.Children.Add(sortLabel);

                var sortComboBox = new SWC.ComboBox
                {
                    Width = 108,
                    Height = 28,
                    FontSize = 12,
                    VerticalContentAlignment = SW.VerticalAlignment.Center,
                    VerticalAlignment = SW.VerticalAlignment.Center,
                    Style = (SW.Style)FindResource("ReportComboBoxStyle")
                };
                sortComboBox.Items.Add("番茄钟");
                sortComboBox.Items.Add("专注时间");
                sortComboBox.Items.Add("时间碎片");
                sortComboBox.Items.Add("碎片时长");
                sortComboBox.Items.Add("总计时间");
                sortComboBox.Items.Add("番茄钟+碎片");
                sortComboBox.SelectedIndex = (int)CurrentSortField;
                sortComboBox.SelectionChanged += (s, e) =>
                {
                    if (sortComboBox.SelectedIndex >= 0)
                    {
                        CurrentSortField = (GoalSortField)sortComboBox.SelectedIndex;
                        ReRenderGoals(goalItemContainer, stats);
                    }
                };

                sortPanel.Children.Add(sortComboBox);
                headerRow.Children.Add(sortPanel);

                goalContent.Children.Add(headerRow);

                // 目标列表容器
                goalContent.Children.Add(goalItemContainer);

                // 空状态提示
                var emptyHint = new SWC.TextBlock
                {
                    Text = "没有匹配的目标",
                    FontSize = 13,
                    Foreground = (SWM.Brush)FindResource("TextTertiary") ?? SWM.Brushes.Gray,
                    HorizontalAlignment = SW.HorizontalAlignment.Center,
                    Margin = new SW.Thickness(0, 12, 0, 0),
                    Visibility = SW.Visibility.Collapsed
                };
                goalContent.Children.Add(emptyHint);

                goalCard.Child = goalContent;
                ReportContent.Children.Add(goalCard);

                // 渲染目标列表
                ReRenderGoals(goalItemContainer, stats);
            }
        }

        /// <summary>获取排序后的目标列表</summary>
        private IEnumerable<GoalReportStat> GetSortedGoals(Dictionary<string, GoalReportStat> goalStats)
        {
            var query = goalStats.Values.AsEnumerable();

            // 先按搜索过滤
            if (!string.IsNullOrWhiteSpace(GoalSearchText))
            {
                var searchLower = GoalSearchText.Trim().ToLower();
                query = query.Where(g => g.GoalTitle?.ToLower().Contains(searchLower) == true);
            }

            // 再按排序字段排序
            switch (CurrentSortField)
            {
                case GoalSortField.番茄钟数量:
                    return query.OrderByDescending(g => g.PomodoroCount);
                case GoalSortField.专注时间:
                    return query.OrderByDescending(g => g.PomodoroFocusSeconds);
                case GoalSortField.时间碎片数量:
                    return query.OrderByDescending(g => g.FragmentCount);
                case GoalSortField.碎片时长:
                    return query.OrderByDescending(g => g.StopwatchSeconds + g.CountdownSeconds);
                case GoalSortField.总计时间:
                    return query.OrderByDescending(g => g.TotalSeconds);
                case GoalSortField.专注会话数:
                default:
                    return query.OrderByDescending(g => g.FocusSessions);
            }
        }

        /// <summary>重新渲染目标列表（排序/搜索变更时调用）</summary>
        private void ReRenderGoals(SWC.StackPanel container, ReportStats stats)
        {
            container.Children.Clear();

            var sortedGoals = GetSortedGoals(stats.GoalStats).ToList();

            // 更新空状态提示
            SWC.TextBlock? emptyHint = null;
            if (container.Parent is SWC.StackPanel parentPanel)
            {
                foreach (var child in parentPanel.Children)
                {
                    if (child is SWC.TextBlock tb && (tb.Text == "没有匹配的目标"))
                    {
                        emptyHint = tb;
                        break;
                    }
                }
            }
            if (emptyHint != null)
            {
                emptyHint.Visibility = sortedGoals.Count == 0 ? SW.Visibility.Visible : SW.Visibility.Collapsed;
            }

            foreach (var goalStat in sortedGoals)
            {
                bool isFinalCompleted = goalStat.IsCompleted;
                bool isFailedGoal = goalStat.IsFailed;
                bool isPendingGoal = goalStat.IsPending;
                // 重复完成：仅对 Recurring 类型目标，报告期内有完成次数
                bool isRecurringGoal = goalStat.Type == GoalType.Recurring;
                bool isRecurringCompletion = isRecurringGoal && goalStat.RecurringCompletions > 0;
                bool hasSpecialStatus = isFinalCompleted || isFailedGoal || isPendingGoal;

                string statusBorderColor = isFinalCompleted ? "#10B981" : isFailedGoal ? "#EF4444" : "#F59E0B";
                string statusTagText = isFinalCompleted ? "已达成" : isFailedGoal ? "已失败" : "已搁置";
                string statusTagColor = isFinalCompleted ? "#10B981" : isFailedGoal ? "#EF4444" : "#F59E0B";

                bool hasBorder = isFinalCompleted || isFailedGoal || isPendingGoal || isRecurringCompletion;
                string borderColor = (isFinalCompleted || isRecurringCompletion) ? "#10B981" : statusBorderColor;

                var goalItem = new SWC.Border
                {
                    Style = (SW.Style)FindResource("GoalItemStyle"),
                    CornerRadius = new SW.CornerRadius(10),
                    BorderThickness = hasBorder ? new SW.Thickness(2) : new SW.Thickness(1),
                    BorderBrush = hasBorder
                        ? new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString(borderColor))
                        : null
                };

                if (!hasBorder)
                {
                    goalItem.SetResourceReference(SWC.Border.BorderBrushProperty, "BorderColor");
                }

                var goalItemContent = new SWC.StackPanel();

                var titlePanel = new SWC.StackPanel { Orientation = SWC.Orientation.Horizontal };
                var goalTitleText = new SWC.TextBlock
                {
                    Text = goalStat.GoalTitle,
                    FontSize = 14,
                    FontWeight = SW.FontWeights.SemiBold,
                    VerticalAlignment = SW.VerticalAlignment.Center
                };
                goalTitleText.SetResourceReference(SWC.TextBlock.ForegroundProperty, "TextPrimary");
                titlePanel.Children.Add(goalTitleText);

                if (isRecurringCompletion)
                {
                    var recurringBadge = new SWC.Border
                    {
                        Background = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#10B981")),
                        CornerRadius = new SW.CornerRadius(4),
                        Padding = new SW.Thickness(6, 2, 6, 2),
                        Margin = new SW.Thickness(8, 0, 0, 0),
                        VerticalAlignment = SW.VerticalAlignment.Center
                    };
                    var recurringText = new SWC.TextBlock
                    {
                        Text = $"完成 {goalStat.RecurringCompletions} 次",
                        FontSize = 11,
                        Foreground = SWM.Brushes.White,
                        FontWeight = SW.FontWeights.Medium
                    };
                    recurringBadge.Child = recurringText;
                    titlePanel.Children.Add(recurringBadge);
                }

                if (hasSpecialStatus)
                {
                    var statusTag = new SWC.Border
                    {
                        Background = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString(statusTagColor)),
                        CornerRadius = new SW.CornerRadius(4),
                        Padding = new SW.Thickness(6, 2, 6, 2),
                        Margin = new SW.Thickness(8, 0, 0, 0),
                        VerticalAlignment = SW.VerticalAlignment.Center
                    };
                    var tagText = new SWC.TextBlock
                    {
                        Text = statusTagText,
                        FontSize = 11,
                        Foreground = SWM.Brushes.White,
                        FontWeight = SW.FontWeights.Medium
                    };
                    statusTag.Child = tagText;
                    titlePanel.Children.Add(statusTag);
                }

                titlePanel.Margin = new SW.Thickness(0, 0, 0, 8);
                goalItemContent.Children.Add(titlePanel);

                // 5列网格，单行
                var goalStatsGrid = new SWC.Grid();
                for (int i = 0; i < 5; i++)
                    goalStatsGrid.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = new SW.GridLength(1, SW.GridUnitType.Star) });
                goalStatsGrid.RowDefinitions.Add(new SWC.RowDefinition { Height = SW.GridLength.Auto });

                var goalFocusHours = Math.Round(goalStat.PomodoroFocusSeconds / 3600.0, 1);
                var goalFragmentHours = Math.Round((goalStat.StopwatchSeconds + goalStat.CountdownSeconds) / 3600.0, 1);
                var goalTotalHours = Math.Round(goalStat.TotalSeconds / 3600.0, 1);

                AddStatItem(goalStatsGrid, 0, 0, "番茄钟", goalStat.PomodoroCount.ToString(), "#F97316");
                AddStatItem(goalStatsGrid, 0, 1, "专注", $"{StatsViewModel.FormatHours(goalFocusHours)} h", "#3B82F6");
                AddStatItem(goalStatsGrid, 0, 2, "时间碎片", goalStat.FragmentCount.ToString(), "#10B981");
                AddStatItem(goalStatsGrid, 0, 3, "碎片时长", $"{StatsViewModel.FormatHours(goalFragmentHours)} h", "#14B8A6");
                AddStatItem(goalStatsGrid, 0, 4, "总计", $"{StatsViewModel.FormatHours(goalTotalHours)} h", "#8B5CF6");

                goalItemContent.Children.Add(goalStatsGrid);
                goalItem.Child = goalItemContent;
                container.Children.Add(goalItem);
            }
        }

        private void AddStatItem(SWC.Grid grid, int row, int col, string label, string value, string colorHex)
        {
            var itemPanel = new SWC.StackPanel
            {
                Margin = new SW.Thickness(0, 0, 4, 10)
            };

            var labelText = new SWC.TextBlock
            {
                Text = label,
                FontSize = 12,
                Margin = new SW.Thickness(0, 0, 0, 3)
            };
            labelText.SetResourceReference(SWC.TextBlock.ForegroundProperty, "TextTertiary");

            var valueText = new SWC.TextBlock
            {
                Text = value,
                FontSize = 20,
                FontWeight = SW.FontWeights.Bold,
                Foreground = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString(colorHex))
            };

            itemPanel.Children.Add(labelText);
            itemPanel.Children.Add(valueText);

            SWC.Grid.SetRow(itemPanel, row);
            SWC.Grid.SetColumn(itemPanel, col);
            grid.Children.Add(itemPanel);
        }

        private void ReportTypeButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var button = sender as SWC.Button;
            if (button == null) return;

            // 重置所有按钮样式
            ResetButtonStyle(DailyButton);
            ResetButtonStyle(WeeklyButton);
            ResetButtonStyle(MonthlyButton);
            ResetButtonStyle(YearlyButton);
            ResetButtonStyle(CustomRangeButton);

            // 设置选中按钮样式
            SetSelectedButtonStyle(button);

            // 更新报表类型
            switch (button.Name)
            {
                case "DailyButton":
                    CurrentReportType = ReportType.Daily;
                    break;
                case "WeeklyButton":
                    CurrentReportType = ReportType.Weekly;
                    break;
                case "MonthlyButton":
                    CurrentReportType = ReportType.Monthly;
                    break;
                case "YearlyButton":
                    CurrentReportType = ReportType.Yearly;
                    break;
                case "CustomRangeButton":
                    CurrentReportType = ReportType.Custom;
                    break;
            }

            // 控制日历选择器的显示/隐藏
            UpdateCalendarVisibility();
            LoadReport();
        }

        private void UpdateCalendarVisibility()
        {
            if (CurrentReportType == ReportType.Daily)
            {
                CalendarPickerContainer.Visibility = SW.Visibility.Visible;
                CustomRangeContainer.Visibility = SW.Visibility.Collapsed;
                DateRangeText.Visibility = SW.Visibility.Collapsed;
                DailyCalendarPicker.SetDate(CurrentDate);
            }
            else if (CurrentReportType == ReportType.Custom)
            {
                CalendarPickerContainer.Visibility = SW.Visibility.Collapsed;
                CustomRangeContainer.Visibility = SW.Visibility.Visible;
                DateRangeText.Visibility = SW.Visibility.Collapsed;
                CustomStartCalendarPicker.SetDate(CurrentDate);
                CustomEndCalendarPicker.SetDate(CurrentEndDate);
            }
            else
            {
                CalendarPickerContainer.Visibility = SW.Visibility.Collapsed;
                CustomRangeContainer.Visibility = SW.Visibility.Collapsed;
                DateRangeText.Visibility = SW.Visibility.Visible;
            }
        }

        private void DailyCalendarPicker_DateChanged(object? sender, DateTime? selectedDate)
        {
            if (selectedDate.HasValue && CurrentReportType == ReportType.Daily)
            {
                CurrentDate = selectedDate.Value;
                LoadReport();
            }
        }

        private void CustomStartCalendarPicker_DateChanged(object? sender, DateTime? selectedDate)
        {
            if (selectedDate.HasValue && CurrentReportType == ReportType.Custom)
            {
                CurrentDate = selectedDate.Value;
                if (CurrentEndDate < CurrentDate)
                {
                    CurrentEndDate = CurrentDate;
                    CustomEndCalendarPicker.SetDate(CurrentEndDate);
                }
                LoadReport();
            }
        }

        private void CustomEndCalendarPicker_DateChanged(object? sender, DateTime? selectedDate)
        {
            if (selectedDate.HasValue && CurrentReportType == ReportType.Custom)
            {
                CurrentEndDate = selectedDate.Value;
                if (CurrentEndDate < CurrentDate)
                {
                    CurrentDate = CurrentEndDate;
                    CustomStartCalendarPicker.SetDate(CurrentDate);
                }
                LoadReport();
            }
        }

        private void ResetButtonStyle(SWC.Button button)
        {
            button.SetResourceReference(SWC.Control.BackgroundProperty, "HoverColor");
            button.SetResourceReference(SWC.Control.ForegroundProperty, "TextSecondary");
        }

        private void SetSelectedButtonStyle(SWC.Button button)
        {
            button.SetResourceReference(SWC.Control.BackgroundProperty, "AccentColor");
            button.Foreground = SWM.Brushes.White;
        }

        private void PrevButton_Click(object sender, SW.RoutedEventArgs e)
        {
            switch (CurrentReportType)
            {
                case ReportType.Daily:
                    CurrentDate = CurrentDate.AddDays(-1);
                    DailyCalendarPicker.SetDate(CurrentDate);
                    break;
                case ReportType.Weekly:
                    CurrentDate = CurrentDate.AddDays(-7);
                    break;
                case ReportType.Monthly:
                    CurrentDate = CurrentDate.AddMonths(-1);
                    break;
                case ReportType.Yearly:
                    CurrentDate = CurrentDate.AddYears(-1);
                    break;
                case ReportType.Custom:
                    int customDays = (CurrentEndDate - CurrentDate).Days;
                    CurrentDate = CurrentDate.AddDays(-customDays - 1);
                    CurrentEndDate = CurrentEndDate.AddDays(-customDays - 1);
                    CustomStartCalendarPicker.SetDate(CurrentDate);
                    CustomEndCalendarPicker.SetDate(CurrentEndDate);
                    break;
            }
            LoadReport();
        }

        private void NextButton_Click(object sender, SW.RoutedEventArgs e)
        {
            switch (CurrentReportType)
            {
                case ReportType.Daily:
                    CurrentDate = CurrentDate.AddDays(1);
                    DailyCalendarPicker.SetDate(CurrentDate);
                    break;
                case ReportType.Weekly:
                    CurrentDate = CurrentDate.AddDays(7);
                    break;
                case ReportType.Monthly:
                    CurrentDate = CurrentDate.AddMonths(1);
                    break;
                case ReportType.Yearly:
                    CurrentDate = CurrentDate.AddYears(1);
                    break;
                case ReportType.Custom:
                    int customDays = (CurrentEndDate - CurrentDate).Days;
                    CurrentDate = CurrentDate.AddDays(customDays + 1);
                    CurrentEndDate = CurrentEndDate.AddDays(customDays + 1);
                    CustomStartCalendarPicker.SetDate(CurrentDate);
                    CustomEndCalendarPicker.SetDate(CurrentEndDate);
                    break;
            }
            LoadReport();
        }
    }
}