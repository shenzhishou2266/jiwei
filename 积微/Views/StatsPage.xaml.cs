using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWShapes = System.Windows.Shapes;
using 积微.Models;
using 积微.Services;

namespace 积微.Views
{
    /// <summary>统计页面，展示今日、月度、累计番茄钟数据及活动热力图。</summary>
    public partial class StatsPage : SWC.UserControl
    {
        private SWC.Border[,] calendarCells;
        private SWC.StackPanel weeklyChart;

        private static readonly SWM.SolidColorBrush ChangePositiveBrush
            = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#059669"));
        private static readonly SWM.SolidColorBrush ChangeNegativeBrush
            = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#EF4444"));
        private static readonly SWM.SolidColorBrush ChangeZeroBrush
            = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#059669"));

        /// <summary>全局目标列表，供其他页面引用。</summary>
        public static List<Goal> Goals { get; set; } = new List<Goal>();

        public StatsPage()
        {
            InitializeComponent();
            InitializeStatElements();
            Loaded += StatsPage_Loaded;
        }

        private void InitializeStatElements()
        {
            calendarCells = new SWC.Border[8, 14];
            weeklyChart = FindName("WeeklyChart") as SWC.StackPanel;
        }

        // 格式化时长显示：0时显示"0"，否则保留一位小数
        private string FormatHours(double hours)
        {
            if (hours == 0)
                return "0";
            return hours.ToString("F1");
        }

        private async void StatsPage_Loaded(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                await StatisticsService.CleanupOrphanedSessionsAsync();
                await LoadStatisticsAsync();
                await LoadCalendarDataAsync();
                await LoadWeeklyChartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in StatsPage_Loaded: {ex.Message}");
            }
        }

        private void ReportButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var reportWindow = new ReportWindow();
            reportWindow.Show();
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var todayStats = await StatisticsService.GetDailyStatsAsync(DateTime.Today);
                var yesterdayStats = await StatisticsService.GetDailyStatsAsync(DateTime.Today.AddDays(-1));
                var monthlyStats = await StatisticsService.GetMonthlyStatsAsync();
                var totalStats = await StatisticsService.GetTotalStatsAsync();
                var activeDays = await StatisticsService.GetActiveDaysAsync();
                var streak = await StatisticsService.GetStreakDaysAsync();
                var goals = await DataStorageService.LoadGoalsAsync();
                var todayGoalPomodoros = await StatisticsService.GetTodayGoalPomodorosAsync();
                var todayGoalActivity = await StatisticsService.GetTodayGoalActivityAsync();
                var todayFragments = await StatisticsService.GetTodayFragmentStatsAsync();
                Goals = goals;

                SW.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateTodayStats(todayStats, yesterdayStats, streak, todayGoalPomodoros, todayGoalActivity, todayFragments);
                    UpdateMonthlyStats(monthlyStats);
                    UpdateTotalStats(totalStats, activeDays);
                    UpdateDateLabel();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading statistics: {ex.Message}");
            }
        }

        private void UpdateTodayStats(DailyStats today, DailyStats yesterday, int streak,
            Dictionary<string, int> todayGoalPomodoros, List<GoalActivitySummary> todayGoalActivity,
            (int count, int totalSeconds) todayFragments)
        {
            var todayPomodoros = FindName("TodayPomodoros") as SWC.TextBlock;
            var todayPomodorosChange = FindName("TodayPomodorosChange") as SWC.TextBlock;
            var todayHours = FindName("TodayHours") as SWC.TextBlock;
            var todayHoursChange = FindName("TodayHoursChange") as SWC.TextBlock;
            var todayFragmentsTb = FindName("TodayFragments") as SWC.TextBlock;
            var todayFragmentsChange = FindName("TodayFragmentsChange") as SWC.TextBlock;
            var todayFragmentHours = FindName("TodayFragmentHours") as SWC.TextBlock;
            var todayFragmentHoursChange = FindName("TodayFragmentHoursChange") as SWC.TextBlock;
            var streakDays = FindName("StreakDays") as SWC.TextBlock;
            var streakDaysSub = FindName("StreakDaysSub") as SWC.TextBlock;
            var completedGoals = FindName("CompletedGoals") as SWC.TextBlock;
            var completedGoalsSub = FindName("CompletedGoalsSub") as SWC.TextBlock;

            if (todayPomodoros != null)
                todayPomodoros.Text = today.FocusSessions.ToString();

            if (todayPomodorosChange != null)
            {
                int diff = today.FocusSessions - yesterday.FocusSessions;
                if (diff > 0)
                {
                    todayPomodorosChange.Text = $"比昨天多 {diff} 个";
                    todayPomodorosChange.Foreground = ChangePositiveBrush;
                }
                else if (diff < 0)
                {
                    todayPomodorosChange.Text = $"比昨天少 {Math.Abs(diff)} 个";
                    todayPomodorosChange.Foreground = ChangeNegativeBrush;
                }
                else
                {
                    todayPomodorosChange.Text = "与昨天持平";
                    todayPomodorosChange.Foreground = ChangeZeroBrush;
                }
            }

            if (todayHours != null)
            {
                double hours = Math.Round(today.TotalFocusSeconds / 3600.0, 1);
                todayHours.Text = FormatHours(hours);
            }

            if (todayHoursChange != null)
            {
                double todayHoursVal = today.TotalFocusSeconds / 3600.0;
                double yesterdayHoursVal = yesterday.TotalFocusSeconds / 3600.0;
                double diff = todayHoursVal - yesterdayHoursVal;
                if (diff > 0)
                {
                    todayHoursChange.Text = $"较昨天 +{diff:F1}h";
                    todayHoursChange.Foreground = ChangePositiveBrush;
                }
                else if (diff < 0)
                {
                    todayHoursChange.Text = $"较昨天 -{Math.Abs(diff):F1}h";
                    todayHoursChange.Foreground = ChangeNegativeBrush;
                }
                else
                {
                    todayHoursChange.Text = "与昨天持平";
                    todayHoursChange.Foreground = ChangeZeroBrush;
                }
            }

            if (todayFragmentsTb != null)
                todayFragmentsTb.Text = todayFragments.count.ToString();

            if (todayFragmentsChange != null)
            {
                int diff = today.FragmentSessions - yesterday.FragmentSessions;
                if (diff > 0)
                {
                    todayFragmentsChange.Text = $"比昨天多 {diff} 个";
                    todayFragmentsChange.Foreground = ChangePositiveBrush;
                }
                else if (diff < 0)
                {
                    todayFragmentsChange.Text = $"比昨天少 {Math.Abs(diff)} 个";
                    todayFragmentsChange.Foreground = ChangeNegativeBrush;
                }
                else
                {
                    todayFragmentsChange.Text = "与昨天持平";
                    todayFragmentsChange.Foreground = ChangeZeroBrush;
                }
            }

            if (todayFragmentHours != null)
            {
                double hours = Math.Round(today.TotalFragmentSeconds / 3600.0, 1);
                todayFragmentHours.Text = FormatHours(hours);
            }

            if (todayFragmentHoursChange != null)
            {
                double todayFragHours = today.TotalFragmentSeconds / 3600.0;
                double yesterdayFragHours = yesterday.TotalFragmentSeconds / 3600.0;
                double diff = todayFragHours - yesterdayFragHours;
                if (diff > 0)
                {
                    todayFragmentHoursChange.Text = $"较昨天 +{diff:F1}h";
                    todayFragmentHoursChange.Foreground = ChangePositiveBrush;
                }
                else if (diff < 0)
                {
                    todayFragmentHoursChange.Text = $"较昨天 -{Math.Abs(diff):F1}h";
                    todayFragmentHoursChange.Foreground = ChangeNegativeBrush;
                }
                else
                {
                    todayFragmentHoursChange.Text = "与昨天持平";
                    todayFragmentHoursChange.Foreground = ChangeZeroBrush;
                }
            }

            if (streakDays != null)
                streakDays.Text = streak.ToString();

            if (completedGoals != null)
                completedGoals.Text = todayGoalActivity.Count.ToString();

            if (completedGoalsSub != null)
            {
                if (todayGoalActivity.Count > 0)
                {
                    var goalNames = string.Join("、", todayGoalActivity.Select(g => g.GoalTitle));
                    if (goalNames.Length > 20)
                    {
                        completedGoalsSub.Text = goalNames.Substring(0, 20) + "…";
                    }
                    else
                    {
                        completedGoalsSub.Text = goalNames;
                    }

                    var detailLines = todayGoalActivity.Select(g =>
                    {
                        var items = new List<string>();
                        if (g.PomodoroCount > 0)
                            items.Add($"🍅{g.PomodoroCount}");
                        if (g.FragmentCount > 0)
                            items.Add($"🧩{g.FragmentCount}");
                        return $"  {g.GoalTitle}   {string.Join("  ", items)}";
                    });
                    completedGoalsSub.ToolTip = new SWC.ToolTip
                    {
                        Content = string.Join("\n", detailLines),
                        FontSize = 12
                    };
                }
                else
                {
                    completedGoalsSub.Text = "今日无进行目标";
                    completedGoalsSub.ToolTip = null;
                }
            }
        }

        private void UpdateMonthlyStats(DailyStats monthly)
        {
            var monthlyHours = FindName("MonthlyHours") as SWC.TextBlock;
            var monthlyHoursSub = FindName("MonthlyHoursSub") as SWC.TextBlock;

            if (monthlyHours != null)
            {
                double totalHours = (monthly.TotalFocusSeconds + monthly.TotalFragmentSeconds) / 3600.0;
                monthlyHours.Text = FormatHours(Math.Round(totalHours, 1));
            }

            if (monthlyHoursSub != null)
            {
                double pomHours = Math.Round(monthly.TotalFocusSeconds / 3600.0, 1);
                double fragHours = Math.Round(monthly.TotalFragmentSeconds / 3600.0, 1);
                monthlyHoursSub.Text = $"番茄 {FormatHours(pomHours)}h · 碎片 {FormatHours(fragHours)}h";
            }
        }

        private void UpdateTotalStats(DailyStats total, int activeDays)
        {
            var totalHours = FindName("TotalHours") as SWC.TextBlock;
            var totalHoursSub = FindName("TotalHoursSub") as SWC.TextBlock;
            var totalSessions = FindName("TotalSessions") as SWC.TextBlock;
            var totalSessionsSub = FindName("TotalSessionsSub") as SWC.TextBlock;
            var avgDailyHours = FindName("AvgDailyHours") as SWC.TextBlock;
            var avgDailyHoursSub = FindName("AvgDailyHoursSub") as SWC.TextBlock;

            // 累计时长
            if (totalHours != null)
            {
                double allHours = (total.TotalFocusSeconds + total.TotalFragmentSeconds) / 3600.0;
                totalHours.Text = FormatHours(Math.Round(allHours, 1));
            }

            if (totalHoursSub != null)
            {
                totalHoursSub.Text = "自使用以来";
            }

            // 累计个数
            int totalCount = total.FocusSessions + total.FragmentSessions;
            if (totalSessions != null)
                totalSessions.Text = totalCount.ToString();

            if (totalSessionsSub != null)
            {
                totalSessionsSub.Text = $"番茄 {total.FocusSessions}个 · 碎片 {total.FragmentSessions}个";
            }

            // 日均时长
            if (avgDailyHours != null)
            {
                double totalAllHours = (total.TotalFocusSeconds + total.TotalFragmentSeconds) / 3600.0;
                double avgHours = activeDays > 0 ? totalAllHours / activeDays : 0;
                avgDailyHours.Text = FormatHours(Math.Round(avgHours, 1));
            }

            if (avgDailyHoursSub != null)
            {
                avgDailyHoursSub.Text = activeDays > 0 ? $"基于 {activeDays} 个活跃天" : "暂无数据";
            }
        }

        private void UpdateDateLabel()
        {
            var dateLabel = FindName("DateLabel") as SWC.TextBlock;
            if (dateLabel != null)
            {
                dateLabel.Text = $"{DateTime.Today.Year}年{DateTime.Today.Month}月{DateTime.Today.Day}日";
            }
        }

        private async Task LoadCalendarDataAsync()
        {
            await LoadCalendarCellsAsync();
        }

        private async Task LoadCalendarCellsAsync()
        {
            for (int row = 1; row <= 7; row++)
            {
                for (int col = 1; col <= 12; col++)
                {
                    string cellName = $"Cell_{row}_{col}";
                    var cell = FindName(cellName) as SWC.Border;
                    if (cell != null)
                    {
                        calendarCells[row, col] = cell;
                    }
                }
            }

            DateTime today = DateTime.Now;
            int daysToSunday = (int)today.DayOfWeek;
            DateTime currentWeekSunday = today.AddDays(-daysToSunday);

            for (int row = 1; row <= 7; row++)
            {
                for (int col = 1; col <= 12; col++)
                {
                    int weeksOffset = col - 12;
                    int dayOfWeek = row - 1;
                    DateTime cellDate = currentWeekSunday.AddDays(weeksOffset * 7 + dayOfWeek);

                    if (calendarCells[row, col] != null)
                    {
                        var (pomMin, fragMin) = await StatisticsService.GetDayActivityAsync(cellDate);
                        var blendedColor = BlendActivityColor(pomMin, fragMin);
                        calendarCells[row, col].Background = new SWM.SolidColorBrush(blendedColor);

                        var dayStats = await StatisticsService.GetDayDetailStatsAsync(cellDate);
                        string tooltipContent = cellDate.ToString("yyyy-MM-dd");
                        if (dayStats.FocusSessions > 0)
                        {
                            tooltipContent += $"\n番茄钟: {dayStats.FocusSessions} 个 ({dayStats.TotalFocusSeconds / 60}分钟)";
                        }
                        if (dayStats.FragmentSessions > 0)
                        {
                            tooltipContent += $"\n时间碎片: {dayStats.FragmentSessions} 个 ({dayStats.TotalFragmentSeconds / 60}分钟)";
                        }
                        if (dayStats.CompletedGoals > 0)
                        {
                            tooltipContent += $"\n完成目标: {dayStats.CompletedGoals} 个";
                        }
                        SWC.ToolTip tooltip = new SWC.ToolTip
                        {
                            Content = tooltipContent
                        };
                        SWC.ToolTipService.SetToolTip(calendarCells[row, col], tooltip);

                        if (cellDate.Date == today.Date)
                        {
                            calendarCells[row, col].BorderBrush = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#F97316"));
                            calendarCells[row, col].BorderThickness = new SW.Thickness(2);
                        }
                        else
                        {
                            calendarCells[row, col].BorderBrush = SWM.Brushes.Transparent;
                            calendarCells[row, col].BorderThickness = new SW.Thickness(0);
                        }
                    }
                }
            }

            UpdateMonthLabels(currentWeekSunday);
            BuildCalendarLegend();
        }

        /// <summary>
        /// 混合番茄钟(橙色)和时间碎片(青绿)的颜色：各自强度决定混合比例，总强度决定深浅
        /// </summary>
        private static SWM.Color BlendActivityColor(int pomodoroMinutes, int fragmentMinutes)
        {
            const int pR = 249, pG = 115, pB = 22;   // #F97316 番茄钟色
            const int fR = 20, fG = 184, fB = 166;   // #14B8A6 时间碎片色
            const int eR = 243, eG = 244, eB = 246;   // #F3F4F6 无活动色

            // 强度映射：120分钟(2小时)达到满强度
            double p = Math.Min(1.0, pomodoroMinutes / 120.0);
            double f = Math.Min(1.0, fragmentMinutes / 120.0);
            double total = p + f;

            if (total <= 0)
                return SWM.Color.FromRgb((byte)eR, (byte)eG, (byte)eB);

            total = Math.Min(1.0, total);

            // 按各自强度比例混合目标色
            double pRatio = p / (p + f);
            double fRatio = f / (p + f);

            double tR = pR * pRatio + fR * fRatio;
            double tG = pG * pRatio + fG * fRatio;
            double tB = pB * pRatio + fB * fRatio;

            // 从灰色向目标色过渡，使用 sqrt 让低活动天也有可见颜色
            double eased = Math.Sqrt(total);

            byte r = (byte)(eR + (tR - eR) * eased);
            byte g = (byte)(eG + (tG - eG) * eased);
            byte b = (byte)(eB + (tB - eB) * eased);

            return SWM.Color.FromRgb(r, g, b);
        }

        private void BuildCalendarLegend()
        {
            if (CalendarLegend == null) return;
            CalendarLegend.Children.Clear();

            // 碎片标签
            var fragLabel = new SWC.TextBlock
            {
                Text = "碎片",
                FontSize = 11,
                Foreground = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#14B8A6")),
                VerticalAlignment = SW.VerticalAlignment.Center,
                Margin = new SW.Thickness(0, 0, 4, 0)
            };
            CalendarLegend.Children.Add(fragLabel);

            // 7个渐变色块：纯碎片 → 混合 → 纯番茄钟
            for (int i = 0; i < 7; i++)
            {
                // fragmentMinutes 从 120→0，pomodoroMinutes 从 0→120
                int fMin = (6 - i) * 20; // 120, 100, 80, 60, 40, 20, 0
                int pMin = i * 20;        // 0, 20, 40, 60, 80, 100, 120

                var color = BlendActivityColor(pMin, fMin);
                var block = new SWC.Border
                {
                    Background = new SWM.SolidColorBrush(color),
                    CornerRadius = new SW.CornerRadius(3),
                    Width = 14,
                    Height = 14,
                    Margin = new SW.Thickness(2, 0, 2, 0)
                };
                CalendarLegend.Children.Add(block);
            }

            // 番茄钟标签
            var pomoLabel = new SWC.TextBlock
            {
                Text = "番茄",
                FontSize = 11,
                Foreground = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#F97316")),
                VerticalAlignment = SW.VerticalAlignment.Center,
                Margin = new SW.Thickness(4, 0, 0, 0)
            };
            CalendarLegend.Children.Add(pomoLabel);
        }

        private void UpdateMonthLabels(DateTime currentWeekSunday)
        {
            Dictionary<int, int> monthColumns = new Dictionary<int, int>();

            for (int col = 1; col <= 12; col++)
            {
                int weeksOffset = col - 12;
                DateTime columnSunday = currentWeekSunday.AddDays(weeksOffset * 7);

                for (int day = 0; day < 7; day++)
                {
                    DateTime checkDate = columnSunday.AddDays(day);
                    if (checkDate.Day == 1)
                    {
                        if (!monthColumns.ContainsKey(checkDate.Month))
                        {
                            monthColumns[checkDate.Month] = col;
                        }
                        break;
                    }
                }
            }

            List<SWC.TextBlock> labels = new List<SWC.TextBlock>
            {
                FindName("MonthLabel1") as SWC.TextBlock,
                FindName("MonthLabel2") as SWC.TextBlock,
                FindName("MonthLabel3") as SWC.TextBlock
            };

            int labelIndex = 0;
            foreach (var monthColumn in monthColumns.OrderBy(m => m.Key))
            {
                if (labelIndex < labels.Count && labels[labelIndex] != null)
                {
                    SWC.TextBlock label = labels[labelIndex];
                    label.Text = $"{monthColumn.Key}";
                    SWC.Grid.SetColumn(label, monthColumn.Value);
                    label.Visibility = SW.Visibility.Visible;
                    labelIndex++;
                }
            }

            for (int i = labelIndex; i < labels.Count; i++)
            {
                if (labels[i] != null)
                {
                    labels[i].Visibility = SW.Visibility.Collapsed;
                }
            }
        }

        private async Task LoadWeeklyChartAsync()
        {
            if (weeklyChart == null) return;

            try
            {
                var weeklyStats = await StatisticsService.GetWeeklyStatsAsync();

                SW.Application.Current.Dispatcher.Invoke(() =>
                {
                    weeklyChart.Children.Clear();

                    // 以每日总时长(番茄钟+碎片)的最大值为基准
                    int maxTotalSeconds = 0;
                    foreach (var s in weeklyStats)
                    {
                        if (s == null) continue;
                        int total = s.TotalFocusSeconds + s.TotalFragmentSeconds;
                        if (total > maxTotalSeconds) maxTotalSeconds = total;
                    }
                    if (maxTotalSeconds == 0) maxTotalSeconds = 3600;

                    string[] dayNames = { "日", "一", "二", "三", "四", "五", "六" };

                    for (int i = 0; i < 7; i++)
                    {
                        var dayStats = weeklyStats.Count > i ? weeklyStats[i] : null;
                        int pomSeconds = dayStats?.TotalFocusSeconds ?? 0;
                        int fragSeconds = dayStats?.TotalFragmentSeconds ?? 0;
                        int totalSeconds = pomSeconds + fragSeconds;

                        double totalRatio = (double)totalSeconds / maxTotalSeconds;
                        int totalBarHeight = totalSeconds > 0 ? Math.Max(8, (int)(65 * totalRatio)) : 0;

                        // 按比例分配高度
                        int pomHeight = totalSeconds > 0
                            ? Math.Max(0, (int)(totalBarHeight * (double)pomSeconds / totalSeconds))
                            : 0;
                        int fragHeight = totalBarHeight - pomHeight;

                        string tooltipText = $"{dayNames[i]}: 番茄钟 {pomSeconds / 60}分钟, 碎片 {fragSeconds / 60}分钟, 合计 {totalSeconds / 60}分钟";

                        var barContainer = new SWC.StackPanel
                        {
                            Width = 68,
                            Margin = new SW.Thickness(0),
                            VerticalAlignment = SW.VerticalAlignment.Bottom
                        };

                        // 碎片段在上(青绿)
                        if (fragHeight > 0)
                        {
                            bool hasPom = pomHeight > 0;
                            var fragBar = new SWC.Border
                            {
                                Width = 52,
                                Height = fragHeight,
                                Background = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#14B8A6")),
                                CornerRadius = hasPom
                                    ? new SW.CornerRadius(4, 4, 0, 0)
                                    : new SW.CornerRadius(4),
                                Margin = new SW.Thickness(8, 0, 8, 0),
                                ToolTip = new SWC.ToolTip { Content = tooltipText }
                            };
                            barContainer.Children.Add(fragBar);
                        }

                        // 番茄段在下(橙色)
                        if (pomHeight > 0)
                        {
                            bool hasFrag = fragHeight > 0;
                            var pomBar = new SWC.Border
                            {
                                Width = 52,
                                Height = pomHeight,
                                Background = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString("#F97316")),
                                CornerRadius = hasFrag
                                    ? new SW.CornerRadius(0, 0, 4, 4)
                                    : new SW.CornerRadius(4),
                                Margin = new SW.Thickness(8, 0, 8, 0),
                                ToolTip = new SWC.ToolTip { Content = tooltipText }
                            };
                            barContainer.Children.Add(pomBar);
                        }

                        weeklyChart.Children.Add(barContainer);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading weekly chart: {ex.Message}");
            }
        }


    }
}