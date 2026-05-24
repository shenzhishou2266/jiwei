using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.InteropServices;
using 积微.Views;
using 积微.Models;
using 积微.Services;
using Application = System.Windows.Application;

namespace 积微
{
    /// <summary>应用入口类，管理窗口生命周期、主题切换和系统托盘图标。</summary>
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private WidgetWindow? _widgetWindow;
        private MainWindow? _mainWindow;
        private bool _isShuttingDown = false;
        private bool _keepWidgetOnRestore = false;
        private readonly List<Window> _themeAwareWindows = new List<Window>();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        /// <summary>设置窗口的暗色模式样式。</summary>
        public static void SetWindowDarkMode(Window window, bool isDarkMode)
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int value = isDarkMode ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
                }
            }
            catch
            {
            }
        }

        /// <summary>注册需要跟随主题变化的窗口。</summary>
        public static void RegisterThemeWindow(Window window)
        {
            if (System.Windows.Application.Current is App app)
            {
                if (!app._themeAwareWindows.Contains(window))
                {
                    app._themeAwareWindows.Add(window);
                    var settings = SettingsManager.Current;
                    SetWindowDarkMode(window, settings.Theme != "Light");
                }
            }
        }

        /// <summary>取消注册主题跟随窗口。</summary>
        public static void UnregisterThemeWindow(Window window)
        {
            if (System.Windows.Application.Current is App app)
            {
                app._themeAwareWindows.Remove(window);
            }
        }

        private void UpdateAllWindowTheme(bool isDarkMode)
        {
            if (_mainWindow != null)
            {
                SetWindowDarkMode(_mainWindow, isDarkMode);
            }

            foreach (var window in _themeAwareWindows)
            {
                if (window.IsLoaded)
                {
                    SetWindowDarkMode(window, isDarkMode);
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            ApplyTheme(SettingsManager.Current.Theme);
            
            InitializeNotifyIcon();
            InitializeWindows();
            
            var settings = SettingsManager.Current;
            if (settings is INotifyPropertyChanged appSettings)
            {
                appSettings.PropertyChanged += AppSettings_PropertyChanged;
            }
        }

        private void ApplyTheme(string theme)
        {
            Resources.MergedDictionaries.Clear();
            
            var themeDictionary = new ResourceDictionary();
            bool isDarkMode = theme != "Light";
            
            if (theme == "Light")
            {
                themeDictionary.Add("Background", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)));
                themeDictionary.Add("CardBackground", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)));
                themeDictionary.Add("TextPrimary", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 24, 39)));
                themeDictionary.Add("TextSecondary", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 65, 81)));
                themeDictionary.Add("TextTertiary", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)));
                themeDictionary.Add("BorderColor", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)));
                themeDictionary.Add("AccentColor", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)));
                themeDictionary.Add("AccentColorHover", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)));
                themeDictionary.Add("HoverColor", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 244, 246)));
                themeDictionary.Add("SecondaryBackground", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 244, 246)));
                themeDictionary.Add("SecondaryText", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 85, 99)));
                themeDictionary.Add("TagSelectedFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)));
                themeDictionary.Add("TagSelectedBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)));
                themeDictionary.Add("TagSelectedBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)));
                themeDictionary.Add("TagLongTermFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105)));
                themeDictionary.Add("TagLongTermBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 250, 229)));
                themeDictionary.Add("TagLongTermBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)));
                themeDictionary.Add("TagShortTermFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)));
                themeDictionary.Add("TagShortTermBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)));
                themeDictionary.Add("TagShortTermBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)));
                themeDictionary.Add("TagRecurringFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(124, 58, 237)));
                themeDictionary.Add("TagRecurringBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 233, 254)));
                themeDictionary.Add("TagRecurringBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246)));
            }
            else
            {
                themeDictionary.Add("Background", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)));
                themeDictionary.Add("CardBackground", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 30)));
                themeDictionary.Add("TextPrimary", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)));
                themeDictionary.Add("TextSecondary", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 142, 147)));
                themeDictionary.Add("TextTertiary", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 99, 102)));
                themeDictionary.Add("BorderColor", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 56, 58)));
                themeDictionary.Add("AccentColor", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 132, 255)));
                themeDictionary.Add("AccentColorHover", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 111, 255)));
                themeDictionary.Add("HoverColor", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 60)));
                themeDictionary.Add("SecondaryBackground", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)));
                themeDictionary.Add("SecondaryText", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 185)));
                themeDictionary.Add("TagSelectedFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(147, 197, 253)));
                themeDictionary.Add("TagSelectedBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 64, 175)));
                themeDictionary.Add("TagSelectedBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)));
                themeDictionary.Add("TagLongTermFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153)));
                themeDictionary.Add("TagLongTermBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 78, 59)));
                themeDictionary.Add("TagLongTermBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105)));
                themeDictionary.Add("TagShortTermFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 165, 250)));
                themeDictionary.Add("TagShortTermBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 58, 95)));
                themeDictionary.Add("TagShortTermBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)));
                themeDictionary.Add("TagRecurringFg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(167, 139, 250)));
                themeDictionary.Add("TagRecurringBg", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 27, 105)));
                themeDictionary.Add("TagRecurringBorder", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(124, 58, 237)));
            }
            
            Resources.MergedDictionaries.Add(themeDictionary);

            UpdateAllWindowTheme(isDarkMode);
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _notifyIcon.Text = "积微";
            _notifyIcon.Visible = true;

            var contextMenu = new ContextMenuStrip();

            var showWidgetItem = new ToolStripMenuItem("显示小组件");
            showWidgetItem.Click += ShowWidget_Click;
            contextMenu.Items.Add(showWidgetItem);

            var showMainItem = new ToolStripMenuItem("显示主窗口");
            showMainItem.Click += ShowMain_Click;
            contextMenu.Items.Add(showMainItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += Exit_Click;
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void InitializeWindows()
        {
            _mainWindow = new MainWindow();
            _mainWindow.StateChanged += MainWindow_StateChanged;
            _mainWindow.Closing += MainWindow_Closing;
            MainWindow = _mainWindow;
            
            // 等待窗口加载完成后应用主题
            _mainWindow.Loaded += (s, e) =>
            {
                var settings = SettingsManager.Current;
                bool isDarkMode = settings.Theme != "Light";
                SetWindowDarkMode(_mainWindow, isDarkMode);
            };
            
            _mainWindow.Show();
            // 应用启动时，不显示小组件，只在最小化时才显示
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_mainWindow != null)
            {
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.Hide();
                    var settings = SettingsManager.Current;
                    if (settings.WidgetWindowEnabled && settings.AutoShowWidgetOnMinimize)
                    {
                        if (_widgetWindow == null)
                        {
                            _widgetWindow = new WidgetWindow();
                            _widgetWindow.Closing += WidgetWindow_Closing;
                            _widgetWindow.Closed += WidgetWindow_Closed;
                        }
                        _widgetWindow.Show();
                    }
                }
                else
                {
                    if (_keepWidgetOnRestore)
                    {
                        _keepWidgetOnRestore = false;
                    }
                    else if (_widgetWindow != null && _widgetWindow.Visibility == Visibility.Visible)
                    {
                        _widgetWindow.Hide();
                    }
                }
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _isShuttingDown = true;
            _widgetWindow?.Close();
            _notifyIcon?.Dispose();
        }

        private void WidgetWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isShuttingDown)
            {
                e.Cancel = false;
                return;
            }

            var settings = SettingsManager.Current;
            if (settings.WidgetWindowEnabled)
            {
                e.Cancel = true;
                _widgetWindow?.Hide();
            }
            else
            {
                e.Cancel = false;
            }
        }

        private void NotifyIcon_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ToggleMainWindow();
            }
        }

        private void ShowWidget_Click(object? sender, EventArgs e)
        {
            ToggleWidgetWindow();
        }

        private void ShowMain_Click(object? sender, EventArgs e)
        {
            if (_mainWindow != null && _mainWindow.Visibility != Visibility.Visible)
            {
                _keepWidgetOnRestore = true;
            }
            ToggleMainWindow();
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            _isShuttingDown = true;
            _widgetWindow?.Close();
            _notifyIcon?.Dispose();
            Shutdown();
        }

        private void ToggleMainWindow()
        {
            if (_mainWindow == null) return;

            if (_mainWindow.Visibility == Visibility.Visible)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.WidgetWindowEnabled))
            {
                var settings = SettingsManager.Current;
                if (!settings.WidgetWindowEnabled)
                {
                    if (_widgetWindow != null && _widgetWindow.Visibility == Visibility.Visible)
                    {
                        _widgetWindow.Hide();
                    }
                }
            }
            else if (e.PropertyName == nameof(AppSettings.Theme))
            {
                var settings = SettingsManager.Current;
                ApplyTheme(settings.Theme);
            }
            else if (e.PropertyName == nameof(AppSettings.WidgetWindowTopmost))
            {
                if (_widgetWindow != null)
                {
                    var settings = SettingsManager.Current;
                    _widgetWindow.Topmost = settings.WidgetWindowTopmost;
                }
            }
        }

        private void WidgetWindow_Closed(object? sender, EventArgs e)
        {
            // 当窗口关闭时，将 _widgetWindow 引用设置为 null
            _widgetWindow = null;
        }

        private void ToggleWidgetWindow()
        {
            var settings = SettingsManager.Current;
            if (!settings.WidgetWindowEnabled)
                return;

            if (_widgetWindow == null)
            {
                _widgetWindow = new WidgetWindow();
                _widgetWindow.Closing += WidgetWindow_Closing;
                _widgetWindow.Closed += WidgetWindow_Closed;
                _widgetWindow.Show();
                return;
            }

            if (_widgetWindow.Visibility == Visibility.Visible)
            {
                _widgetWindow.Hide();
            }
            else
            {
                _widgetWindow.Show();
                _widgetWindow.Activate();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _isShuttingDown = true;
            _notifyIcon?.Dispose();
            _widgetWindow?.Close();
            _mainWindow?.Close();
            base.OnExit(e);
        }
    }
}
