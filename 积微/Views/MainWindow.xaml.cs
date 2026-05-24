using System.Windows;
using System.Windows.Controls;
using SWM = System.Windows.Media;
using 积微.Controls;
using 积微.Models;
using 积微.Services;

namespace 积微.Views
{
    /// <summary>应用程序主窗口，管理页面导航和计时器模式切换。</summary>
    public partial class MainWindow : Window
    {
        private string activePage = "home";
        private FocusSessionControl pomodoroControl;
        private TimerControl timerControl;
        private StatsPage statsPage;
        private GoalsPage goalsPage;
        private SettingsPage settingsPage;
        /// <summary>当前是否为番茄钟模式（false 为通用计时器模式）。</summary>
        public bool isPomodoroMode = true;

        private readonly SWM.Brush BlueBrush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 59, 130, 246));
        private readonly SWM.Brush Gray600Brush = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 75, 85, 99));
        private readonly SWM.Brush TransparentBrush = SWM.Brushes.Transparent;
        private readonly SWM.Brush WhiteBrush = SWM.Brushes.White;

        public MainWindow()
        {
            InitializeComponent();
            InitializePages();
        }

        private void InitializePages()
        {
            pomodoroControl = new FocusSessionControl();
            timerControl = new TimerControl();
            statsPage = new StatsPage();
            goalsPage = new GoalsPage();
            settingsPage = new SettingsPage();
            // 默认显示 home 页面，所以 GoalsPage 初始不可见
            goalsPage.SetPageVisible(false);
            ShowPage("home");
        }

        /// <summary>切换番茄钟和通用计时器模式。</summary>
        public void SwitchTimerMode()
        {
            isPomodoroMode = !isPomodoroMode;
            if (activePage == "home")
            {
                if (isPomodoroMode)
                {
                    ContentArea.Content = pomodoroControl;
                }
                else
                {
                    ContentArea.Content = timerControl;
                }
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("home");
        }

        private void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("stats");
        }

        private void GoalsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("goals");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("settings");
        }

        private void ShowPage(string pageName)
        {
            // 先通知之前的 GoalsPage 它不可见了
            if (activePage == "goals")
            {
                goalsPage.SetPageVisible(false);
            }
            
            activePage = pageName;
            UpdateNavigationButtons();

            switch (pageName)
            {
                case "home":
                    if (isPomodoroMode)
                    {
                        ContentArea.Content = pomodoroControl;
                    }
                    else
                    {
                        ContentArea.Content = timerControl;
                    }
                    break;
                case "stats":
                    ContentArea.Content = statsPage;
                    break;
                case "goals":
                    ContentArea.Content = goalsPage;
                    goalsPage.SetPageVisible(true);
                    break;
                case "settings":
                    ContentArea.Content = settingsPage;
                    break;
            }
        }

        private void UpdateNavigationButtons()
        {
            HomeButton.Background = TransparentBrush;
            HomeButton.Foreground = Gray600Brush;
            StatsButton.Background = TransparentBrush;
            StatsButton.Foreground = Gray600Brush;
            GoalsButton.Background = TransparentBrush;
            GoalsButton.Foreground = Gray600Brush;
            SettingsButton.Background = TransparentBrush;
            SettingsButton.Foreground = Gray600Brush;

            switch (activePage)
            {
                case "home":
                    HomeButton.Background = BlueBrush;
                    HomeButton.Foreground = WhiteBrush;
                    break;
                case "stats":
                    StatsButton.Background = BlueBrush;
                    StatsButton.Foreground = WhiteBrush;
                    break;
                case "goals":
                    GoalsButton.Background = BlueBrush;
                    GoalsButton.Foreground = WhiteBrush;
                    break;
                case "settings":
                    SettingsButton.Background = BlueBrush;
                    SettingsButton.Foreground = WhiteBrush;
                    break;
            }
        }

        /// <summary>刷新目标页面的显示内容。</summary>
        public void UpdateGoalsPage()
        {
            goalsPage.RefreshGoals();
        }

        /// <summary>更新番茄钟和计时器控件的设置。</summary>
        public void UpdatePomodoroSettings()
        {
            pomodoroControl?.UpdateSettings();
            timerControl?.UpdateSettings();
        }
    }
}