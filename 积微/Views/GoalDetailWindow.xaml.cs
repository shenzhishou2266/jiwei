using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using 积微.Models;
using 积微.Services;

namespace 积微.Views
{
    /// <summary>目标详情窗口，可编辑目标的标题、描述、分析和反馈。</summary>
    public partial class GoalDetailWindow : SW.Window
    {
        /// <summary>获取或设置当前目标。</summary>
        public Goal Goal { get; set; }
        /// <summary>目标更新事件，保存后触发以通知父页面刷新。</summary>
        public event EventHandler GoalUpdated;
        
        private GoalType _pendingGoalType;

        public GoalDetailWindow(Goal goal)
        {
            InitializeComponent();
            Goal = goal;
            LoadGoalData();

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, SW.RoutedEventArgs e)
        {
            App.RegisterThemeWindow(this);
            if (Goal != null)
            {
                Goal.DurationChanged += OnGoalDurationChanged;
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
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
            if (total > 0)
            {
                int self = Goal.TotalElapsedSeconds;
                int childrenTime = total - self;
                if (childrenTime > 0)
                {
                    DurationTextBlock.Text = $"累计时长 {FormatDuration(total)}（本目标 {FormatDuration(self)} + 子目标 {FormatDuration(childrenTime)}）";
                }
                else
                {
                    DurationTextBlock.Text = $"累计时长 {FormatDuration(total)}";
                }
            }
            else
            {
                DurationTextBlock.Text = "";
            }
        }

        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0)
                return "0s";

            int years = totalSeconds / 31536000;
            int remainingAfterYears = totalSeconds % 31536000;
            int days = remainingAfterYears / 86400;
            int hours = (remainingAfterYears % 86400) / 3600;
            int minutes = (remainingAfterYears % 3600) / 60;
            int seconds = remainingAfterYears % 60;

            string result = "";
            if (years > 0) result += $"{years}y ";
            if (days > 0) result += $"{days}d ";
            if (hours > 0) result += $"{hours}h ";
            if (minutes > 0) result += $"{minutes}m ";
            if (seconds > 0 || string.IsNullOrEmpty(result)) result += $"{seconds}s";
            return result.Trim();
        }

        private void LoadGoalData()
        {
            if (Goal != null)
            {
                TitleTextBox.Text = Goal.Title;
                DescriptionTextBox.Text = Goal.Description;
                ProcessAnalysisTextBox.Text = Goal.ProcessAnalysis;
                ResultFeedbackTextBox.Text = Goal.ResultFeedback;
                TimeTextBlock.Text = Goal.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                _pendingGoalType = Goal.Type;
                UpdateGoalTypeUI(Goal.Type);
                UpdateDurationDisplay();
            }
        }

        private void UpdateGoalTypeUI(GoalType type)
        {
            ResetTagStyle(LongTermTag, "UnselectedTagLongTermStyle", "#10B981");
            ResetTagStyle(ShortTermTag, "UnselectedTagShortTermStyle", "#3B82F6");
            ResetTagStyle(RecurringTag, "UnselectedTagRecurringStyle", "#8B5CF6");

            switch (type)
            {
                case GoalType.LongTerm:
                    ApplySelectedTagStyle(LongTermTag, "SelectedTagLongTermStyle", "TagLongTermFg");
                    break;
                case GoalType.ShortTerm:
                    ApplySelectedTagStyle(ShortTermTag, "SelectedTagShortTermStyle", "TagShortTermFg");
                    break;
                case GoalType.Recurring:
                    ApplySelectedTagStyle(RecurringTag, "SelectedTagRecurringStyle", "TagRecurringFg");
                    break;
            }
        }

        private void ApplySelectedTagStyle(SWC.Border tag, string styleKey, string foregroundKey)
        {
            tag.Style = (SW.Style)FindResource(styleKey);
            var textBlock = tag.Child as SWC.TextBlock;
            if (textBlock != null)
            {
                textBlock.SetResourceReference(SWC.TextBlock.ForegroundProperty, foregroundKey);
                textBlock.FontWeight = SW.FontWeights.SemiBold;
            }
        }

        private void ResetTagStyle(SWC.Border tag, string styleKey, string foreground)
        {
            tag.Style = (SW.Style)FindResource(styleKey);
            var textBlock = tag.Child as SWC.TextBlock;
            if (textBlock != null)
            {
                textBlock.Foreground = new SWM.SolidColorBrush((SWM.Color)SWM.ColorConverter.ConvertFromString(foreground));
                textBlock.FontWeight = SW.FontWeights.SemiBold;
                textBlock.FontSize = 12;
            }
        }

        private async void GoalTypeTag_Click(object sender, SW.Input.MouseButtonEventArgs e)
        {
            SWC.Border clickedTag = sender as SWC.Border;
            if (clickedTag == null) return;

            if (clickedTag.Name == "LongTermTag")
            {
                _pendingGoalType = GoalType.LongTerm;
            }
            else if (clickedTag.Name == "ShortTermTag")
            {
                _pendingGoalType = GoalType.ShortTerm;
            }
            else if (clickedTag.Name == "RecurringTag")
            {
                _pendingGoalType = GoalType.Recurring;
            }

            UpdateGoalTypeUI(_pendingGoalType);
        }

        private async void SaveButton_Click(object sender, SW.RoutedEventArgs e)
        {
            try
            {
                if (Goal != null)
                {
                    string oldTitle = Goal.Title;
                    string newTitle = TitleTextBox.Text.Trim();
                    Goal.Title = newTitle;
                    Goal.Description = DescriptionTextBox.Text.Trim();
                    Goal.ProcessAnalysis = ProcessAnalysisTextBox.Text.Trim();
                    Goal.ResultFeedback = ResultFeedbackTextBox.Text.Trim();
                    Goal.Type = _pendingGoalType;
                    await GoalsPage.ViewModel!.SaveAsync();

                    // 如果目标名称发生变更，同步更新所有历史会话记录中的目标名称
                    if (oldTitle != newTitle)
                    {
                        await StatisticsService.UpdateSessionGoalTitleAsync(Goal.Id, newTitle);
                    }

                    GoalUpdated?.Invoke(this, EventArgs.Empty);
                }
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveButton_Click: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, SW.RoutedEventArgs e)
        {
            Close();
        }
    }
}
