using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using 积微.Services.Audio;

namespace 积微.Models
{
    /// <summary>应用程序设置，包含计时器、通知、白噪音等配置</summary>
    public class AppSettings : INotifyPropertyChanged
    {
        private int _workDuration = 25;
        private int _breakDuration = 5;
        private int _longBreakDuration = 15;
        private int _sessionsBeforeLongBreak = 4;
        private int _countdownDefaultDays = 0;
        private int _countdownDefaultHours = 0;
        private int _countdownDefaultMinutes = 5;
        private int _countdownDefaultSeconds = 0;
        private bool _notificationSoundEnabled = true;
        private string _notificationSoundName = "音效一";
        private int _notificationSoundVolume = 80;
        private bool _whiteNoiseEnabled = false;
        private string[] _whiteNoiseStates = new string[10];
        private string _dataStoragePath = "";
        private bool _widgetWindowEnabled = true;
        private double _widgetWindowLeft = -1;
        private double _widgetWindowTop = -1;
        private bool _widgetWindowTopmost = false;
        private bool _autoShowWidgetOnMinimize = true;
        private string _theme = "Light";

        /// <summary>主题</summary>
        public string Theme
        {
            get => _theme;
            set { if (_theme != value) { _theme = value; OnPropertyChanged(nameof(Theme)); } }
        }

        /// <summary>工作时间（分钟）</summary>
        public int WorkDuration
        {
            get => _workDuration;
            set { if (_workDuration != value) { _workDuration = value; OnPropertyChanged(nameof(WorkDuration)); } }
        }
        /// <summary>休息时间（分钟）</summary>
        public int BreakDuration
        {
            get => _breakDuration;
            set { if (_breakDuration != value) { _breakDuration = value; OnPropertyChanged(nameof(BreakDuration)); } }
        }
        /// <summary>长休息时间（分钟）</summary>
        public int LongBreakDuration
        {
            get => _longBreakDuration;
            set { if (_longBreakDuration != value) { _longBreakDuration = value; OnPropertyChanged(nameof(LongBreakDuration)); } }
        }
        /// <summary>触发长休息前的会话次数</summary>
        public int SessionsBeforeLongBreak
        {
            get => _sessionsBeforeLongBreak;
            set { if (_sessionsBeforeLongBreak != value) { _sessionsBeforeLongBreak = value; OnPropertyChanged(nameof(SessionsBeforeLongBreak)); } }
        }
        /// <summary>倒计时默认天数</summary>
        public int CountdownDefaultDays
        {
            get => _countdownDefaultDays;
            set { if (_countdownDefaultDays != value) { _countdownDefaultDays = value; OnPropertyChanged(nameof(CountdownDefaultDays)); } }
        }
        /// <summary>倒计时默认小时</summary>
        public int CountdownDefaultHours
        {
            get => _countdownDefaultHours;
            set { if (_countdownDefaultHours != value) { _countdownDefaultHours = value; OnPropertyChanged(nameof(CountdownDefaultHours)); } }
        }
        /// <summary>倒计时默认分钟</summary>
        public int CountdownDefaultMinutes
        {
            get => _countdownDefaultMinutes;
            set { if (_countdownDefaultMinutes != value) { _countdownDefaultMinutes = value; OnPropertyChanged(nameof(CountdownDefaultMinutes)); } }
        }
        /// <summary>倒计时默认秒数</summary>
        public int CountdownDefaultSeconds
        {
            get => _countdownDefaultSeconds;
            set { if (_countdownDefaultSeconds != value) { _countdownDefaultSeconds = value; OnPropertyChanged(nameof(CountdownDefaultSeconds)); } }
        }

        /// <summary>是否启用提示音</summary>
        public bool NotificationSoundEnabled
        {
            get => _notificationSoundEnabled;
            set { if (_notificationSoundEnabled != value) { _notificationSoundEnabled = value; OnPropertyChanged(nameof(NotificationSoundEnabled)); } }
        }
        /// <summary>提示音名称</summary>
        public string NotificationSoundName
        {
            get => _notificationSoundName;
            set { if (_notificationSoundName != value) { _notificationSoundName = value; OnPropertyChanged(nameof(NotificationSoundName)); } }
        }
        /// <summary>提示音音量（0-100）</summary>
        public int NotificationSoundVolume
        {
            get => _notificationSoundVolume;
            set { if (_notificationSoundVolume != value) { _notificationSoundVolume = value; OnPropertyChanged(nameof(NotificationSoundVolume)); } }
        }

        /// <summary>是否启用白噪音</summary>
        public bool WhiteNoiseEnabled
        {
            get => _whiteNoiseEnabled;
            set { if (_whiteNoiseEnabled != value) { _whiteNoiseEnabled = value; OnPropertyChanged(nameof(WhiteNoiseEnabled)); } }
        }

        /// <summary>白噪音状态数组，格式为"Name,IsEnabled,Volume"</summary>
        public string[] WhiteNoiseStates
        {
            get => _whiteNoiseStates;
            set { if (_whiteNoiseStates != value) { _whiteNoiseStates = value; OnPropertyChanged(nameof(WhiteNoiseStates)); } }
        }

        /// <summary>数据存储路径</summary>
        public string DataStoragePath
        {
            get => _dataStoragePath;
            set { if (_dataStoragePath != value) { _dataStoragePath = value; OnPropertyChanged(nameof(DataStoragePath)); } }
        }
        /// <summary>是否启用悬浮窗</summary>
        public bool WidgetWindowEnabled
        {
            get => _widgetWindowEnabled;
            set { if (_widgetWindowEnabled != value) { _widgetWindowEnabled = value; OnPropertyChanged(nameof(WidgetWindowEnabled)); } }
        }
        /// <summary>悬浮窗左边距</summary>
        public double WidgetWindowLeft
        {
            get => _widgetWindowLeft;
            set { if (_widgetWindowLeft != value) { _widgetWindowLeft = value; OnPropertyChanged(nameof(WidgetWindowLeft)); } }
        }
        /// <summary>悬浮窗上边距</summary>
        public double WidgetWindowTop
        {
            get => _widgetWindowTop;
            set { if (_widgetWindowTop != value) { _widgetWindowTop = value; OnPropertyChanged(nameof(WidgetWindowTop)); } }
        }
        /// <summary>悬浮窗是否置顶</summary>
        public bool WidgetWindowTopmost
        {
            get => _widgetWindowTopmost;
            set { if (_widgetWindowTopmost != value) { _widgetWindowTopmost = value; OnPropertyChanged(nameof(WidgetWindowTopmost)); } }
        }
        /// <summary>最小化时是否自动显示小组件</summary>
        public bool AutoShowWidgetOnMinimize
        {
            get => _autoShowWidgetOnMinimize;
            set { if (_autoShowWidgetOnMinimize != value) { _autoShowWidgetOnMinimize = value; OnPropertyChanged(nameof(AutoShowWidgetOnMinimize)); } }
        }

        // 音频相关成员
        /// <summary>提示音管理器（不序列化）</summary>
        [JsonIgnore]
        public NotificationSoundManager NotificationSoundManager { get; set; }
        /// <summary>白噪音管理器（不序列化）</summary>
        [JsonIgnore]
        public WhiteNoiseManager WhiteNoiseManager { get; set; }

        /// <summary>属性变更事件</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>构造应用设置实例</summary>
        public AppSettings()
        {
            if (string.IsNullOrEmpty(DataStoragePath))
            {
                DataStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "积微"
                );
            }

            // 初始化音频相关成员
            NotificationSoundManager = new NotificationSoundManager();
            WhiteNoiseManager = new WhiteNoiseManager();
        }

        /// <summary>触发属性变更事件</summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>设置管理器，提供设置的加载和保存</summary>
    public static class SettingsManager
    {
        private static readonly string SettingsFileName = "settings.json";
        private static AppSettings? _currentSettings;
        private static readonly object _lock = new object();

        /// <summary>当前应用设置实例</summary>
        public static AppSettings Current
        {
            get
            {
                if (_currentSettings == null)
                {
                    lock (_lock)
                    {
                        if (_currentSettings == null)
                        {
                            LoadSettings();
                        }
                    }
                }
                return _currentSettings!;
            }
        }

        /// <summary>settings.json 始终存放在默认目录，不受 DataStoragePath 影响</summary>
        private static string GetSettingsFilePath()
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "积微"
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(path, SettingsFileName);
        }

        /// <summary>加载设置文件</summary>
        public static void LoadSettings()
        {
            string settingsPath = GetSettingsFilePath();

            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    // 重新初始化音频相关成员
                    _currentSettings.NotificationSoundManager = new NotificationSoundManager();
                    _currentSettings.WhiteNoiseManager = new WhiteNoiseManager();

                    // 应用保存的白噪音状态
                    ApplyWhiteNoiseStates(_currentSettings);
                }
                catch
                {
                    _currentSettings = new AppSettings();
                }
            }
            else
            {
                _currentSettings = new AppSettings();
                SaveSettings();
            }
        }

        private static void ApplyWhiteNoiseStates(AppSettings settings)
        {
            if (settings.WhiteNoiseStates != null)
            {
                foreach (var state in settings.WhiteNoiseStates)
                {
                    if (!string.IsNullOrEmpty(state))
                    {
                        var parts = state.Split(',');
                        if (parts.Length >= 3)
                        {
                            string name = parts[0];
                            bool isEnabled = bool.Parse(parts[1]);
                            int volume = int.Parse(parts[2]);

                            // 查找对应的白噪音并更新状态
                            var whiteNoise = settings.WhiteNoiseManager.WhiteNoises.FirstOrDefault(wn => wn.Name == name);
                            if (whiteNoise != null)
                            {
                                settings.WhiteNoiseManager.UpdatePlayerState(whiteNoise, isEnabled, volume);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>保存当前设置到文件</summary>
        public static void SaveSettings()
        {
            if (_currentSettings == null) return;

            try
            {
                string settingsPath = GetSettingsFilePath();
                string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }
    }
}
