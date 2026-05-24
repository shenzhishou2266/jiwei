using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using 积微.Models;

namespace 积微.Views
{
    /// <summary>添加/编辑目标窗口。</summary>
    public partial class AddGoalWindow : SW.Window
    {
        /// <summary>获取或设置目标标题。</summary>
        public string GoalTitle { get; set; }
        /// <summary>获取或设置目标描述。</summary>
        public string GoalDescription { get; set; }
        /// <summary>获取或设置目标类型。</summary>
        public GoalType SelectedGoalType { get; set; } = GoalType.ShortTerm;
        
        /// <summary>目标添加成功事件。</summary>
        public event EventHandler GoalAdded;

        public AddGoalWindow()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Closed += OnClosed;
            UpdateGoalTypeUI(SelectedGoalType);
        }

        private void OnLoaded(object sender, SW.RoutedEventArgs e)
        {
            App.RegisterThemeWindow(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
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

        private void GoalTypeTag_Click(object sender, SW.Input.MouseButtonEventArgs e)
        {
            SWC.Border clickedTag = sender as SWC.Border;
            if (clickedTag == null) return;

            if (clickedTag.Name == "LongTermTag")
            {
                SelectedGoalType = GoalType.LongTerm;
            }
            else if (clickedTag.Name == "ShortTermTag")
            {
                SelectedGoalType = GoalType.ShortTerm;
            }
            else if (clickedTag.Name == "RecurringTag")
            {
                SelectedGoalType = GoalType.Recurring;
            }

            UpdateGoalTypeUI(SelectedGoalType);
        }

        private void AddButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var title = TitleTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                GoalTitle = title;
                GoalDescription = DescriptionTextBox.Text.Trim();
                GoalAdded?.Invoke(this, EventArgs.Empty);
                Close();
            }
            else
            {
                var messageBox = new MessageBoxWindow("提示", "请输入目标标题");
                messageBox.Owner = this;
                messageBox.ShowDialog();
            }
        }

        private void CancelButton_Click(object sender, SW.RoutedEventArgs e)
        {
            Close();
        }
    }
}
