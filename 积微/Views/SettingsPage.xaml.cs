using System;
using System.Collections.Generic;
using System.Linq;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWShapes = System.Windows.Shapes;
using 积微.Models;
using 积微.Models.Audio;
using 积微.Services.Audio;
using 积微.ViewModels;

namespace 积微.Views
{
    /// <summary>设置页面，管理番茄钟时长、提示音、白噪音、主题和数据存储等配置。</summary>
    public partial class SettingsPage : SWC.UserControl
    {
        private bool isWhiteNoisePlaying = false;

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            SetupEventHandlers();
            Loaded += SettingsPage_Loaded;
            
            // 监听设置变化
            var settings = SettingsManager.Current;
            if (settings is System.ComponentModel.INotifyPropertyChanged appSettings)
            {
                appSettings.PropertyChanged += AppSettings_PropertyChanged;
            }
        }

        private void SetupEventHandlers()
        {
            WorkDurationInput.LostFocus += (s, e) => AutoSaveSettings();
            BreakDurationInput.LostFocus += (s, e) => AutoSaveSettings();
            LongBreakDurationInput.LostFocus += (s, e) => AutoSaveSettings();
            SessionsBeforeLongBreakInput.LostFocus += (s, e) => AutoSaveSettings();
            CountdownDefaultDaysInput.LostFocus += (s, e) => AutoSaveSettings();
            CountdownDefaultHoursInput.LostFocus += (s, e) => AutoSaveSettings();
            CountdownDefaultMinutesInput.LostFocus += (s, e) => AutoSaveSettings();
            CountdownDefaultSecondsInput.LostFocus += (s, e) => AutoSaveSettings();

            NotificationSoundEnabledCheckBox.Checked += (s, e) => AutoSaveSettings();
            NotificationSoundEnabledCheckBox.Unchecked += (s, e) => AutoSaveSettings();
            NotificationSoundVolumeSlider.ValueChanged += (s, e) =>
            {
                NotificationSoundVolumeValue.Text = $"{(int)NotificationSoundVolumeSlider.Value}%";
                var settings = SettingsManager.Current;
                settings.NotificationSoundManager.SetVolume(NotificationSoundVolumeSlider.Value);
                AutoSaveSettings();
            };

            WhiteNoiseEnabledCheckBox.Checked += (s, e) => AutoSaveSettings();
            WhiteNoiseEnabledCheckBox.Unchecked += (s, e) => AutoSaveSettings();

            WhiteNoisePlayButton.Click += (s, e) =>
            {
                isWhiteNoisePlaying = !isWhiteNoisePlaying;
                var settings = SettingsManager.Current;

                UpdatePlayPauseIcon(isWhiteNoisePlaying);

                try
                {
                    if (isWhiteNoisePlaying)
                        settings.WhiteNoiseManager.Play();
                    else
                        settings.WhiteNoiseManager.Stop();
                }
                catch
                {
                }
            };

            WorkDurationInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            BreakDurationInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            LongBreakDurationInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            SessionsBeforeLongBreakInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            CountdownDefaultDaysInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            CountdownDefaultHoursInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            CountdownDefaultMinutesInput.PreviewTextInput += NumericOnly_PreviewTextInput;
            CountdownDefaultSecondsInput.PreviewTextInput += NumericOnly_PreviewTextInput;

            WidgetWindowEnabledCheckBox.Checked += (s, e) => AutoSaveSettings();
            WidgetWindowEnabledCheckBox.Unchecked += (s, e) => AutoSaveSettings();
            AutoShowWidgetOnMinimizeCheckBox.Checked += (s, e) => AutoSaveSettings();
            AutoShowWidgetOnMinimizeCheckBox.Unchecked += (s, e) => AutoSaveSettings();
            WidgetWindowTopmostCheckBox.Checked += (s, e) => AutoSaveSettings();
            WidgetWindowTopmostCheckBox.Unchecked += (s, e) => AutoSaveSettings();
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            var path = WhiteNoisePlayButton.FindName("PlayPauseIcon") as SWShapes.Path;
            if (path != null)
            {
                path.Data = SWM.Geometry.Parse(isPlaying
                    ? "M6,6H18V18H6V6Z"
                    : "M8,5.14V19.14L19,12.14L8,5.14Z");
            }
        }

        private void NumericOnly_PreviewTextInput(object sender, SW.Input.TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Current;

            WorkDurationInput.Text = settings.WorkDuration.ToString();
            BreakDurationInput.Text = settings.BreakDuration.ToString();
            LongBreakDurationInput.Text = settings.LongBreakDuration.ToString();
            SessionsBeforeLongBreakInput.Text = settings.SessionsBeforeLongBreak.ToString();
            CountdownDefaultDaysInput.Text = settings.CountdownDefaultDays.ToString();
            CountdownDefaultHoursInput.Text = settings.CountdownDefaultHours.ToString();
            CountdownDefaultMinutesInput.Text = settings.CountdownDefaultMinutes.ToString();
            CountdownDefaultSecondsInput.Text = settings.CountdownDefaultSeconds.ToString();
            NotificationSoundEnabledCheckBox.IsChecked = settings.NotificationSoundEnabled;
            NotificationSoundVolumeSlider.Value = settings.NotificationSoundVolume;
            NotificationSoundVolumeValue.Text = $"{settings.NotificationSoundVolume}%";
            settings.NotificationSoundManager.SetVolume(settings.NotificationSoundVolume);
            WhiteNoiseEnabledCheckBox.IsChecked = settings.WhiteNoiseEnabled;

            StoragePathTextBox.Text = settings.DataStoragePath;
            WidgetWindowEnabledCheckBox.IsChecked = settings.WidgetWindowEnabled;
            AutoShowWidgetOnMinimizeCheckBox.IsChecked = settings.AutoShowWidgetOnMinimize;
            WidgetWindowTopmostCheckBox.IsChecked = settings.WidgetWindowTopmost;

            // 加载主题设置
            UpdateThemeButtons(settings.Theme);
        }

        private void BrowseButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择数据存储文件夹",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择文件夹"
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    StoragePathTextBox.Text = folderPath;
                    var settings = SettingsManager.Current;
                    settings.DataStoragePath = folderPath;
                    SettingsManager.SaveSettings();
                }
            }
        }

        private void NavButton_Click(object sender, SW.RoutedEventArgs e)
        {
            // Hide all pages and set all buttons to inactive (auto-updates on theme change)
            SetNavButtonInactive(ClockButton);
            SetNavButtonInactive(SoundButton);
            SetNavButtonInactive(OtherButton);
            ClockPage.Visibility = SW.Visibility.Collapsed;
            SoundPage.Visibility = SW.Visibility.Collapsed;
            OtherPage.Visibility = SW.Visibility.Collapsed;

            if (sender == ClockButton)
            {
                SetNavButtonActive(ClockButton);
                ClockPage.Visibility = SW.Visibility.Visible;
            }
            else if (sender == SoundButton)
            {
                SetNavButtonActive(SoundButton);
                SoundPage.Visibility = SW.Visibility.Visible;
            }
            else if (sender == OtherButton)
            {
                SetNavButtonActive(OtherButton);
                OtherPage.Visibility = SW.Visibility.Visible;
            }
        }

        /// <summary>设置为激活状态（通过资源引用，主题切换时自动更新）</summary>
        private static void SetNavButtonActive(SWC.Button button)
        {
            button.SetResourceReference(SWC.Control.BackgroundProperty, "AccentColor");
            button.Foreground = new SWM.SolidColorBrush(SWM.Colors.White);
        }

        /// <summary>设置为非激活状态（通过资源引用，主题切换时自动更新）</summary>
        private static void SetNavButtonInactive(SWC.Button button)
        {
            button.SetResourceReference(SWC.Control.BackgroundProperty, "HoverColor");
            button.SetResourceReference(SWC.Control.ForegroundProperty, "TextPrimary");
        }

        private void NotificationSoundButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;

            foreach (var vm in _notificationSoundViewModels)
            {
                vm.IsSelected = false;
            }

            if (sender is SWC.Button clickedButton &&
                clickedButton.DataContext is NotificationSoundViewModel viewModel)
            {
                var currentViewModel = _notificationSoundViewModels.FirstOrDefault(vm => vm.NotificationSound.Name == viewModel.NotificationSound.Name);
                if (currentViewModel != null)
                {
                    currentViewModel.IsSelected = true;
                    settings.NotificationSoundName = currentViewModel.NotificationSound.Name;
                    AutoSaveSettings();
                }
            }

            try
            {
                var notificationSound = settings.NotificationSoundManager.GetNotificationSound(settings.NotificationSoundName);
                if (notificationSound != null)
                {
                    settings.NotificationSoundManager.Play(notificationSound);
                }
            }
            catch
            {
            }
        }


        private void AutoSaveSettings()
        {
            try
            {
                var settings = SettingsManager.Current;

                if (int.TryParse(WorkDurationInput.Text, out int workDuration) && workDuration > 0)
                    settings.WorkDuration = workDuration;
                else
                    WorkDurationInput.Text = settings.WorkDuration.ToString();

                if (int.TryParse(BreakDurationInput.Text, out int breakDuration) && breakDuration > 0)
                    settings.BreakDuration = breakDuration;
                else
                    BreakDurationInput.Text = settings.BreakDuration.ToString();

                if (int.TryParse(LongBreakDurationInput.Text, out int longBreakDuration) && longBreakDuration > 0)
                    settings.LongBreakDuration = longBreakDuration;
                else
                    LongBreakDurationInput.Text = settings.LongBreakDuration.ToString();

                if (int.TryParse(SessionsBeforeLongBreakInput.Text, out int sessionsBeforeLongBreak) &&
                    sessionsBeforeLongBreak > 0)
                    settings.SessionsBeforeLongBreak = sessionsBeforeLongBreak;
                else
                    SessionsBeforeLongBreakInput.Text = settings.SessionsBeforeLongBreak.ToString();

                if (int.TryParse(CountdownDefaultDaysInput.Text, out int countdownDefaultDays) && countdownDefaultDays >= 0)
                    settings.CountdownDefaultDays = countdownDefaultDays;
                else
                    CountdownDefaultDaysInput.Text = settings.CountdownDefaultDays.ToString();

                if (int.TryParse(CountdownDefaultHoursInput.Text, out int countdownDefaultHours) && countdownDefaultHours >= 0 && countdownDefaultHours < 24)
                    settings.CountdownDefaultHours = countdownDefaultHours;
                else
                    CountdownDefaultHoursInput.Text = settings.CountdownDefaultHours.ToString();

                if (int.TryParse(CountdownDefaultMinutesInput.Text, out int countdownDefaultMinutes) && countdownDefaultMinutes >= 0 && countdownDefaultMinutes < 60)
                    settings.CountdownDefaultMinutes = countdownDefaultMinutes;
                else
                    CountdownDefaultMinutesInput.Text = settings.CountdownDefaultMinutes.ToString();

                if (int.TryParse(CountdownDefaultSecondsInput.Text, out int countdownDefaultSeconds) && countdownDefaultSeconds >= 0 && countdownDefaultSeconds < 60)
                    settings.CountdownDefaultSeconds = countdownDefaultSeconds;
                else
                    CountdownDefaultSecondsInput.Text = settings.CountdownDefaultSeconds.ToString();

                settings.NotificationSoundEnabled = NotificationSoundEnabledCheckBox.IsChecked ?? true;
                settings.NotificationSoundVolume = (int)NotificationSoundVolumeSlider.Value;

                var whiteNoiseStates = new List<string>();
                var whiteNoiseManager = settings.WhiteNoiseManager;
                foreach (var whiteNoise in whiteNoiseManager.WhiteNoises)
                {
                    var player = whiteNoiseManager.GetPlayer(whiteNoise);
                    if (player != null)
                    {
                        string state = $"{whiteNoise.Name},{player.IsEnabled},{player.Volume}";
                        whiteNoiseStates.Add(state);
                    }
                }

                settings.WhiteNoiseStates = whiteNoiseStates.ToArray();

                settings.WhiteNoiseEnabled = WhiteNoiseEnabledCheckBox.IsChecked ?? false;

                if (!string.IsNullOrEmpty(StoragePathTextBox.Text))
                {
                    settings.DataStoragePath = StoragePathTextBox.Text;
                }

                settings.WidgetWindowEnabled = WidgetWindowEnabledCheckBox.IsChecked ?? true;
                settings.AutoShowWidgetOnMinimize = AutoShowWidgetOnMinimizeCheckBox.IsChecked ?? true;
                settings.WidgetWindowTopmost = WidgetWindowTopmostCheckBox.IsChecked ?? false;

                SettingsManager.SaveSettings();

                if (SW.Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.UpdatePomodoroSettings();
                }
            }
            catch (Exception ex)
            {
                var messageBox = new MessageBoxWindow("错误", $"保存设置时出错: {ex.Message}");
                messageBox.Owner = SW.Window.GetWindow(this);
                messageBox.ShowDialog();
            }
        }

        private List<NotificationSoundViewModel> _notificationSoundViewModels;
        private List<WhiteNoiseViewModel> _whiteNoiseViewModels;

        private void SettingsPage_Loaded(object sender, SW.RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;
            _whiteNoiseViewModels = new List<WhiteNoiseViewModel>();
            _notificationSoundViewModels = new List<NotificationSoundViewModel>();

            LoadNotificationSoundStates();

            NotificationSoundItemsControl.ItemsSource = _notificationSoundViewModels;

            LoadWhiteNoiseStates();

            WhiteNoiseItemsControl.ItemsSource = _whiteNoiseViewModels;
        }


        private void LoadNotificationSoundStates()
        {
            var settings = SettingsManager.Current;
            _notificationSoundViewModels.Clear();

            foreach (var notificationSound in settings.NotificationSoundManager.NotificationSounds)
            {
                bool isSelected = notificationSound.Name == settings.NotificationSoundName;
                _notificationSoundViewModels.Add(
                    new NotificationSoundViewModel(notificationSound, isSelected));
            }
        }

        private void LoadWhiteNoiseStates()
        {
            var settings = SettingsManager.Current;
            _whiteNoiseViewModels.Clear();

            foreach (var whiteNoise in settings.WhiteNoiseManager.WhiteNoises)
            {
                var player = settings.WhiteNoiseManager.GetPlayer(whiteNoise);
                bool isEnabled = player?.IsEnabled ?? false;
                int volume = player?.Volume ?? 50;

                _whiteNoiseViewModels.Add(new WhiteNoiseViewModel(whiteNoise, isEnabled, volume));
            }
        }


        private void WhiteNoiseVolumeSlider_ValueChanged(object sender, SW.RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = (SWC.Slider)sender;
            var viewModel = ((SW.FrameworkElement)sender).DataContext as WhiteNoiseViewModel;
            if (viewModel == null) return;

            var settings = SettingsManager.Current;
            int volume = (int)slider.Value;
            viewModel.Volume = volume;
            settings.WhiteNoiseManager.UpdatePlayerState(viewModel.WhiteNoise, viewModel.IsEnabled, volume);
            AutoSaveSettings();
        }

        private void WhiteNoiseContainer_MouseEnter(object sender, SW.Input.MouseEventArgs e)
        {
            UpdateWhiteNoiseMaskOpacity((SWC.Grid)sender, 0);
        }

        private void WhiteNoiseContainer_MouseLeave(object sender, SW.Input.MouseEventArgs e)
        {
            UpdateWhiteNoiseMaskOpacity((SWC.Grid)sender, 0.8);
        }

        private void UpdateWhiteNoiseMaskOpacity(SWC.Grid container, double opacity)
        {
            var viewModel = container.DataContext as WhiteNoiseViewModel;
            var mask = container.FindName("WhiteNoiseMask") as SWC.Border;

            if (mask != null && !viewModel.IsEnabled)
            {
                mask.Opacity = opacity;
            }
        }

        private void WhiteNoiseContainer_Click(object sender, SW.Input.MouseButtonEventArgs e)
        {
            SWC.Grid container = (SWC.Grid)sender;
            var viewModel = container.DataContext as WhiteNoiseViewModel;
            if (viewModel != null)
            {
                var settings = SettingsManager.Current;

                viewModel.IsEnabled = !viewModel.IsEnabled;

                settings.WhiteNoiseManager.UpdatePlayerState(viewModel.WhiteNoise, viewModel.IsEnabled,
                    viewModel.Volume);

                if (settings.WhiteNoiseManager.IsPlaying)
                {
                    if (viewModel.IsEnabled)
                    {
                        settings.WhiteNoiseManager.Play(viewModel.WhiteNoise);
                    }
                    else
                    {
                        settings.WhiteNoiseManager.Stop(viewModel.WhiteNoise);
                    }
                }

                AutoSaveSettings();
            }
        }

        private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.WidgetWindowEnabled))
            {
                var settings = SettingsManager.Current;
                WidgetWindowEnabledCheckBox.IsChecked = settings.WidgetWindowEnabled;
            }
            else if (e.PropertyName == nameof(AppSettings.AutoShowWidgetOnMinimize))
            {
                var settings = SettingsManager.Current;
                AutoShowWidgetOnMinimizeCheckBox.IsChecked = settings.AutoShowWidgetOnMinimize;
            }
            else if (e.PropertyName == nameof(AppSettings.Theme))
            {
                var settings = SettingsManager.Current;
                UpdateThemeButtons(settings.Theme);
            }
        }

        private void UpdateThemeButtons(string theme)
        {
            // 重置所有主题按钮状态
            LightThemeButton.DataContext = new { IsSelected = false };
            DarkThemeButton.DataContext = new { IsSelected = false };

            // 设置当前主题按钮为选中状态
            if (theme == "Light")
            {
                LightThemeButton.DataContext = new { IsSelected = true };
            }
            else if (theme == "Dark")
            {
                DarkThemeButton.DataContext = new { IsSelected = true };
            }
        }

        private void ThemeButton_Click(object sender, SW.RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;

            if (sender == LightThemeButton)
            {
                settings.Theme = "Light";
            }
            else if (sender == DarkThemeButton)
            {
                settings.Theme = "Dark";
            }

            SettingsManager.SaveSettings();
        }
    }
}
