using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using WpfApp = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace 积微.Services
{
    /// <summary>主题管理器，负责主题颜色定义、暗色模式切换和窗口主题注册。</summary>
    public class ThemeManager
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly List<Window> _themeAwareWindows = new();

        /// <summary>创建指定主题的 ResourceDictionary。</summary>
        public static ResourceDictionary CreateTheme(string theme)
        {
            var dictionary = new ResourceDictionary();
            bool isDark = theme != "Light";

            if (isDark)
            {
                dictionary.Add("Background", new SolidColorBrush(Color.FromRgb(0, 0, 0)));
                dictionary.Add("CardBackground", new SolidColorBrush(Color.FromRgb(28, 28, 30)));
                dictionary.Add("TextPrimary", new SolidColorBrush(Color.FromRgb(255, 255, 255)));
                dictionary.Add("TextSecondary", new SolidColorBrush(Color.FromRgb(142, 142, 147)));
                dictionary.Add("TextTertiary", new SolidColorBrush(Color.FromRgb(99, 99, 102)));
                dictionary.Add("BorderColor", new SolidColorBrush(Color.FromRgb(56, 56, 58)));
                dictionary.Add("AccentColor", new SolidColorBrush(Color.FromRgb(10, 132, 255)));
                dictionary.Add("AccentColorHover", new SolidColorBrush(Color.FromRgb(0, 111, 255)));
                dictionary.Add("HoverColor", new SolidColorBrush(Color.FromRgb(58, 58, 60)));
                dictionary.Add("SecondaryBackground", new SolidColorBrush(Color.FromRgb(45, 45, 48)));
                dictionary.Add("SecondaryText", new SolidColorBrush(Color.FromRgb(180, 180, 185)));
                dictionary.Add("TagSelectedFg", new SolidColorBrush(Color.FromRgb(147, 197, 253)));
                dictionary.Add("TagSelectedBg", new SolidColorBrush(Color.FromRgb(30, 64, 175)));
                dictionary.Add("TagSelectedBorder", new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                dictionary.Add("TagLongTermFg", new SolidColorBrush(Color.FromRgb(52, 211, 153)));
                dictionary.Add("TagLongTermBg", new SolidColorBrush(Color.FromRgb(6, 78, 59)));
                dictionary.Add("TagLongTermBorder", new SolidColorBrush(Color.FromRgb(5, 150, 105)));
                dictionary.Add("TagShortTermFg", new SolidColorBrush(Color.FromRgb(96, 165, 250)));
                dictionary.Add("TagShortTermBg", new SolidColorBrush(Color.FromRgb(30, 58, 95)));
                dictionary.Add("TagShortTermBorder", new SolidColorBrush(Color.FromRgb(37, 99, 235)));
                dictionary.Add("TagRecurringFg", new SolidColorBrush(Color.FromRgb(167, 139, 250)));
                dictionary.Add("TagRecurringBg", new SolidColorBrush(Color.FromRgb(45, 27, 105)));
                dictionary.Add("TagRecurringBorder", new SolidColorBrush(Color.FromRgb(124, 58, 237)));
            }
            else
            {
                dictionary.Add("Background", new SolidColorBrush(Color.FromRgb(249, 250, 251)));
                dictionary.Add("CardBackground", new SolidColorBrush(Color.FromRgb(255, 255, 255)));
                dictionary.Add("TextPrimary", new SolidColorBrush(Color.FromRgb(17, 24, 39)));
                dictionary.Add("TextSecondary", new SolidColorBrush(Color.FromRgb(55, 65, 81)));
                dictionary.Add("TextTertiary", new SolidColorBrush(Color.FromRgb(107, 114, 128)));
                dictionary.Add("BorderColor", new SolidColorBrush(Color.FromRgb(229, 231, 235)));
                dictionary.Add("AccentColor", new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                dictionary.Add("AccentColorHover", new SolidColorBrush(Color.FromRgb(37, 99, 235)));
                dictionary.Add("HoverColor", new SolidColorBrush(Color.FromRgb(243, 244, 246)));
                dictionary.Add("SecondaryBackground", new SolidColorBrush(Color.FromRgb(243, 244, 246)));
                dictionary.Add("SecondaryText", new SolidColorBrush(Color.FromRgb(75, 85, 99)));
                dictionary.Add("TagSelectedFg", new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                dictionary.Add("TagSelectedBg", new SolidColorBrush(Color.FromRgb(219, 234, 254)));
                dictionary.Add("TagSelectedBorder", new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                dictionary.Add("TagLongTermFg", new SolidColorBrush(Color.FromRgb(5, 150, 105)));
                dictionary.Add("TagLongTermBg", new SolidColorBrush(Color.FromRgb(209, 250, 229)));
                dictionary.Add("TagLongTermBorder", new SolidColorBrush(Color.FromRgb(16, 185, 129)));
                dictionary.Add("TagShortTermFg", new SolidColorBrush(Color.FromRgb(37, 99, 235)));
                dictionary.Add("TagShortTermBg", new SolidColorBrush(Color.FromRgb(219, 234, 254)));
                dictionary.Add("TagShortTermBorder", new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                dictionary.Add("TagRecurringFg", new SolidColorBrush(Color.FromRgb(124, 58, 237)));
                dictionary.Add("TagRecurringBg", new SolidColorBrush(Color.FromRgb(237, 233, 254)));
                dictionary.Add("TagRecurringBorder", new SolidColorBrush(Color.FromRgb(139, 92, 246)));
            }

            return dictionary;
        }

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
        public void RegisterThemeWindow(Window window)
        {
            if (!_themeAwareWindows.Contains(window))
            {
                _themeAwareWindows.Add(window);
            }
        }

        /// <summary>取消注册主题跟随窗口。</summary>
        public void UnregisterThemeWindow(Window window)
        {
            _themeAwareWindows.Remove(window);
        }

        /// <summary>应用主题到 Application 和所有已注册窗口。</summary>
        public void ApplyTheme(string theme, WpfApp app)
        {
            app.Resources.MergedDictionaries.Clear();
            var themeDictionary = CreateTheme(theme);
            app.Resources.MergedDictionaries.Add(themeDictionary);

            bool isDarkMode = theme != "Light";
            ApplyWindowDarkMode(isDarkMode);
        }

        /// <summary>对所有已注册窗口应用暗色模式。</summary>
        public void ApplyWindowDarkMode(bool isDarkMode)
        {
            foreach (var window in _themeAwareWindows)
            {
                if (window.IsLoaded)
                {
                    SetWindowDarkMode(window, isDarkMode);
                }
            }
        }
    }
}