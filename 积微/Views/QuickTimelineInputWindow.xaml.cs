using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using 积微.Models;
using 积微.Services;
using 积微.Helpers;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace 积微.Views
{
    /// <summary>快速时间线输入窗口，支持文本和粘贴图片。</summary>
    public partial class QuickTimelineInputWindow : Window
    {
        /// <summary>获取或设置要添加时间线条目的目标。</summary>
        public Goal Goal { get; set; }
        private List<BitmapImage> _pendingImages = new List<BitmapImage>();

        public QuickTimelineInputWindow(Goal goal)
        {
            InitializeComponent();
            Goal = goal;
            TimelineInputTextBox.Focus();

            CommandManager.AddPreviewExecutedHandler(TimelineInputTextBox, TimelineInputTextBox_PreviewExecuted);
            CommandManager.AddPreviewCanExecuteHandler(TimelineInputTextBox, TimelineInputTextBox_PreviewCanExecute);

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.RegisterThemeWindow(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
        }

        private void TimelineInputTextBox_PreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Paste && TimelineInputTextBox.IsFocused)
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void TimelineInputTextBox_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Paste && TimelineInputTextBox.IsFocused)
            {
                try
                {
                    var dataObject = System.Windows.Clipboard.GetDataObject();
                    if (dataObject != null)
                    {
                        bool handled = false;
                        
                        if (dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap))
                        {
                            var bitmapSource = dataObject.GetData(System.Windows.DataFormats.Bitmap) as BitmapSource;
                            if (bitmapSource != null)
                            {
                                var bitmapImage = BitmapSourceToBitmapImage(bitmapSource);
                                if (bitmapImage != null)
                                {
                                    _pendingImages.Add(bitmapImage);
                                    UpdateImagePreview();
                                    handled = true;
                                }
                            }
                        }
                        else if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
                        {
                            var files = dataObject.GetData(System.Windows.DataFormats.FileDrop) as string[];
                            if (files != null)
                            {
                                bool hasImage = false;
                                foreach (var file in files)
                                {
                                    if (IsImageFile(file))
                                    {
                                        try
                                        {
                                            var bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.UriSource = new Uri(file, UriKind.Absolute);
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.EndInit();
                                            bitmap.Freeze();
                                            _pendingImages.Add(bitmap);
                                            hasImage = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Error loading image: {ex.Message}");
                                        }
                                    }
                                }
                                if (hasImage)
                                {
                                    UpdateImagePreview();
                                }
                                handled = hasImage;
                            }
                        }
                        
                        if (handled)
                        {
                            e.Handled = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Paste error: {ex.Message}");
                }
            }
        }

        private BitmapImage BitmapSourceToBitmapImage(BitmapSource bitmapSource)
        {
            var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
            stream.Position = 0;
            
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        private bool IsImageFile(string filePath)
        {
            string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return extensions.Contains(ext);
        }

        private string GetImageStoragePath()
        {
            string dataStoragePath = SettingsManager.Current.DataStoragePath;
            if (string.IsNullOrEmpty(dataStoragePath))
            {
                dataStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "积微"
                );
            }
            
            string imagePath = Path.Combine(dataStoragePath, "images");
            if (!Directory.Exists(imagePath))
            {
                Directory.CreateDirectory(imagePath);
            }
            
            return imagePath;
        }

        private string SaveImageToFile(BitmapSource bitmapSource)
        {
            string imagePath = GetImageStoragePath();
            string fileName = $"{Guid.NewGuid()}.png";
            string fullPath = Path.Combine(imagePath, fileName);
            
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(stream);
            }
            
            return Path.Combine("images", fileName);
        }

        private void TimelineInputTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                SaveTimelineEntry();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SaveTimelineEntry();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveButton_Click: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task SaveTimelineEntry()
        {
            string content = TimelineInputTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(content) && _pendingImages.Count == 0)
            {
                Close();
                return;
            }

            if (Goal != null)
            {
                List<string> imagePathList = null;
                if (_pendingImages.Count > 0)
                {
                    imagePathList = new List<string>();
                    foreach (var img in _pendingImages)
                    {
                        imagePathList.Add(SaveImageToFile(img));
                    }
                }

                TimelineEntryType entryType = TimelineEntryType.Thought;
                if (TypeComboBox.SelectedIndex == 1)
                {
                    entryType = TimelineEntryType.Question;
                }

                if (!string.IsNullOrEmpty(content) || imagePathList != null)
                {
                    Goal.AddTimelineEntry(
                        content ?? "",
                        entryType,
                        DateTime.Now,
                        imagePathList
                    );
                    await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                }
            }

            Close();
        }

        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp|所有文件|*.*",
                Multiselect = true,
                Title = "选择图片"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var fileName in dialog.FileNames)
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fileName);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        _pendingImages.Add(bitmap);
                    }
                    catch
                    {
                    }
                }

                UpdateImagePreview();
            }
        }

        private void UpdateImagePreview()
        {
            if (_pendingImages.Count > 0)
            {
                ImagePreviewPanel.ItemsSource = null;
                ImagePreviewPanel.ItemsSource = _pendingImages;
                ImagePreviewPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ImagePreviewPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is BitmapImage bitmapImage)
            {
                _pendingImages.Remove(bitmapImage);
                UpdateImagePreview();
            }
        }

        private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var image = sender as System.Windows.Controls.Image;
            if (image?.Tag is BitmapImage bitmapImage)
            {
                var viewer = new ImageViewerWindow(bitmapImage);
                viewer.Owner = this;
                viewer.Topmost = true;
                viewer.Show();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }
    }
}
