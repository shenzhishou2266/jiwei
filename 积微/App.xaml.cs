using System;
using System.Windows;
using System.Windows.Forms;
using System.ComponentModel;
using 积微.Views;
using 积微.Models;
using 积微.Services;
using 积微.Services.Audio;
using 积微.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ThemeManager _themeManager = new ThemeManager();

        /// <summary>设置窗口的暗色模式样式。</summary>
        public static void SetWindowDarkMode(Window window, bool isDarkMode)
        {
            ThemeManager.SetWindowDarkMode(window, isDarkMode);
        }

        /// <summary>注册需要跟随主题变化的窗口。</summary>
        public static void RegisterThemeWindow(Window window)
        {
            if (System.Windows.Application.Current is App app)
            {
                app._themeManager.RegisterThemeWindow(window);
                var settings = SettingsManager.Current;
                ThemeManager.SetWindowDarkMode(window, settings.Theme != "Light");
            }
        }

        /// <summary>取消注册主题跟随窗口。</summary>
        public static void UnregisterThemeWindow(Window window)
        {
            if (System.Windows.Application.Current is App app)
            {
                app._themeManager.UnregisterThemeWindow(window);
            }
        }

        private void UpdateAllWindowTheme(bool isDarkMode)
        {
            if (_mainWindow != null)
            {
                ThemeManager.SetWindowDarkMode(_mainWindow, isDarkMode);
            }

            _themeManager.ApplyWindowDarkMode(isDarkMode);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            AppServices.Configure();
            AudioServices.Initialize();

            // 应用保存的白噪音状态（从 SettingsManager 中移出，避免 Models→Services 耦合）
            AudioServices.ApplyWhiteNoiseStates(SettingsManager.Current.WhiteNoiseStates);

            // 预加载目标数据，避免 SessionRecorder 在数据加载前被调用
            var goalVm = AppServices.Provider.GetRequiredService<GoalsViewModel>();
            goalVm.LoadGoalsAsync();

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
            _themeManager.ApplyTheme(theme, this);
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
