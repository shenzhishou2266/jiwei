using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using 积微.Models;
using 积微.Services;
using WPF = System.Windows;
using WPFControls = System.Windows.Controls;
using WPFMedia = System.Windows.Media;
using WPFShapes = System.Windows.Shapes;
using WPFBitmap = System.Windows.Media.Imaging;
using Win32Dialog = Microsoft.Win32.OpenFileDialog;
using System.Windows;

namespace 积微.Views
{
    /// <summary>时间线窗口，查看和管理目标的时间线记录。</summary>
    public partial class TimelineWindow : WPF.Window
    {
        /// <summary>获取或设置关联的目标。</summary>
        public Goal Goal { get; set; }
        private bool _isSortedDescending = true;
        private TimelineEntryType? _filterType = null;
        private string _searchText = string.Empty;
        private DateTime? _startDate = null;
        private DateTime? _endDate = null;
        private List<WPFBitmap.BitmapImage> _pendingImages = new List<WPFBitmap.BitmapImage>();
        private TimelineEntry? _editingEntry = null;
        private WPFControls.Border? _editingCard = null;
        private string? _originalContent = null;
        private List<string>? _originalImages = null;
        private List<WPFBitmap.BitmapImage> _editingNewImages = new List<WPFBitmap.BitmapImage>();

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

        private string SaveImageToFile(WPFBitmap.BitmapSource bitmapSource)
        {
            string imagePath = GetImageStoragePath();
            string fileName = $"{Guid.NewGuid()}.png";
            string fullPath = Path.Combine(imagePath, fileName);
            
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                var encoder = new WPFBitmap.PngBitmapEncoder();
                encoder.Frames.Add(WPFBitmap.BitmapFrame.Create(bitmapSource));
                encoder.Save(stream);
            }
            
            return Path.Combine("images", fileName);
        }

        public TimelineWindow(Goal goal)
        {
            InitializeComponent();
            Goal = goal;
            if (Goal != null)
            {
                TitleTextBlock.Text = Goal.Title;
            }
            SetPlaceholderText();
            TimelineInputTextBox.GotFocus += TimelineInputTextBox_GotFocus;
            TimelineInputTextBox.LostFocus += TimelineInputTextBox_LostFocus;
            TimelineInputTextBox.PreviewTextInput += TimelineInputTextBox_PreviewTextInput;
            WPF.Input.CommandManager.AddPreviewExecutedHandler(TimelineInputTextBox, TimelineInputTextBox_PreviewExecuted);
            WPF.Input.CommandManager.AddPreviewCanExecuteHandler(TimelineInputTextBox, TimelineInputTextBox_PreviewCanExecute);
            UpdateSortButtonStyle();
            UpdateFilterButtonStyle();
            LoadTimeline();

            Loaded += OnLoaded;
            Closed += OnClosed;
            Goal.TimelineEntryAdded += OnGoalTimelineEntryAdded;

            var settings = SettingsManager.Current;
            if (settings is System.ComponentModel.INotifyPropertyChanged settingsNotify)
            {
                settingsNotify.PropertyChanged += Settings_PropertyChanged;
            }
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.Theme))
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateFilterButtonStyle();
                    LoadTimeline();
                });
            }
        }

        private void OnGoalTimelineEntryAdded(object sender, EventArgs e)
        {
            if (Goal != null && sender is Goal && ReferenceEquals(sender, Goal))
            {
                Dispatcher.Invoke(() => LoadTimeline());
            }
        }

        private void OnLoaded(object sender, WPF.RoutedEventArgs e)
        {
            App.RegisterThemeWindow(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
            if (Goal != null)
            {
                Goal.TimelineEntryAdded -= OnGoalTimelineEntryAdded;
            }

            var settings = SettingsManager.Current;
            if (settings is System.ComponentModel.INotifyPropertyChanged settingsNotify)
            {
                settingsNotify.PropertyChanged -= Settings_PropertyChanged;
            }
        }

        private void TimelineInputTextBox_PreviewTextInput(object sender, WPF.Input.TextCompositionEventArgs e)
        {
        }

        private void TimelineInputTextBox_PreviewCanExecute(object sender, WPF.Input.CanExecuteRoutedEventArgs e)
        {
            if (e.Command == WPF.Input.ApplicationCommands.Paste && TimelineInputTextBox.IsFocused)
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void TimelineInputTextBox_PreviewExecuted(object sender, WPF.Input.ExecutedRoutedEventArgs e)
        {
            if (e.Command == WPF.Input.ApplicationCommands.Paste && TimelineInputTextBox.IsFocused)
            {
                try
                {
                    var dataObject = WPF.Clipboard.GetDataObject();
                    if (dataObject != null)
                    {
                        bool handled = false;
                        
                        if (dataObject.GetDataPresent(WPF.DataFormats.Bitmap))
                        {
                            var bitmapSource = dataObject.GetData(WPF.DataFormats.Bitmap) as WPFBitmap.BitmapSource;
                            if (bitmapSource != null)
                            {
                                var bitmapImage = BitmapSourceToBitmapImage(bitmapSource);
                                if (bitmapImage != null)
                                {
                                    AddImageToPreview(bitmapImage);
                                    handled = true;
                                }
                            }
                        }
                        else if (dataObject.GetDataPresent(WPF.DataFormats.FileDrop))
                        {
                            var files = dataObject.GetData(WPF.DataFormats.FileDrop) as string[];
                            if (files != null)
                            {
                                bool hasImage = false;
                                foreach (var file in files)
                                {
                                    if (IsImageFile(file))
                                    {
                                        LoadImageFromFile(file);
                                        hasImage = true;
                                    }
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

        private WPFBitmap.BitmapImage? BitmapSourceToBitmapImage(WPFBitmap.BitmapSource bitmapSource)
        {
            var stream = new MemoryStream();
            var encoder = new WPFBitmap.PngBitmapEncoder();
            encoder.Frames.Add(WPFBitmap.BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
            stream.Position = 0;
            
            var bitmapImage = new WPFBitmap.BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = WPFBitmap.BitmapCacheOption.OnLoad;
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

        private void LoadImageFromFile(string filePath)
        {
            try
            {
                var bitmap = new WPFBitmap.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = WPFBitmap.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                AddImageToPreview(bitmap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading image: {ex.Message}");
            }
        }

        private void AddImageToPreview(WPFBitmap.BitmapImage bitmap)
        {
            _pendingImages.Add(bitmap);
            UpdateImagePreview();
        }

        private void UpdateImagePreview()
        {
            if (_pendingImages.Count > 0)
            {
                ImagePreviewPanel.ItemsSource = null;
                ImagePreviewPanel.ItemsSource = _pendingImages;
                ImagePreviewPanel.Visibility = WPF.Visibility.Visible;
            }
            else
            {
                ImagePreviewPanel.Visibility = WPF.Visibility.Collapsed;
            }
        }

        private void RemoveImage_Click(object sender, WPF.RoutedEventArgs e)
        {
            var button = sender as WPFControls.Button;
            if (button?.Tag is WPFBitmap.BitmapImage image)
            {
                _pendingImages.Remove(image);
                UpdateImagePreview();
            }
        }

        private void PreviewImage_MouseLeftButtonDown(object sender, WPF.Input.MouseButtonEventArgs e)
        {
            var image = sender as WPFControls.Image;
            if (image?.Tag is WPFBitmap.BitmapImage bitmapImage)
            {
                var viewer = new ImageViewerWindow(bitmapImage);
                viewer.Owner = this;
                viewer.Topmost = false;
                viewer.Show();
            }
        }

        private void TimelineImage_MouseLeftButtonDown(object sender, WPF.Input.MouseButtonEventArgs e)
        {
            var image = sender as WPFControls.Image;
            if (image?.Tag is string imagePath)
            {
                var bitmapImage = PathToBitmapImage(imagePath);
                if (bitmapImage != null)
                {
                    var viewer = new ImageViewerWindow(bitmapImage, imagePath);
                    viewer.Owner = this;
                    viewer.Topmost = false;
                    viewer.Show();
                }
            }
        }

        private void AddImageButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            var dialog = new Win32Dialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
                Multiselect = true,
                Title = "选择图片"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    LoadImageFromFile(file);
                }
            }
        }

        private void SetPlaceholderText()
        {
            if (string.IsNullOrEmpty(TimelineInputTextBox.Text))
            {
                TimelineInputTextBox.Text = "记录一些想法或者感受吧";
                TimelineInputTextBox.Foreground = (WPFMedia.Brush)WPF.Application.Current.Resources["TextTertiary"];
                TimelineInputTextBox.CaretBrush = (WPFMedia.Brush)WPF.Application.Current.Resources["TextTertiary"];
            }
        }

        private void TimelineInputTextBox_GotFocus(object sender, WPF.RoutedEventArgs e)
        {
            if (TimelineInputTextBox.Text == "记录一些想法或者感受吧")
            {
                TimelineInputTextBox.Text = string.Empty;
                TimelineInputTextBox.Foreground = (WPFMedia.Brush)WPF.Application.Current.Resources["TextPrimary"];
                TimelineInputTextBox.CaretBrush = (WPFMedia.Brush)WPF.Application.Current.Resources["TextPrimary"];
            }
        }

        private void TimelineInputTextBox_LostFocus(object sender, WPF.RoutedEventArgs e)
        {
            SetPlaceholderText();
        }

        private void LoadTimeline()
        {
            TimelinePanel.Children.Clear();
            if (Goal != null)
            {
                var filteredEntries = Goal.Timeline.AsEnumerable();

                if (_filterType.HasValue)
                {
                    filteredEntries = filteredEntries.Where(e => e.Type == _filterType.Value);
                }

                if (!string.IsNullOrEmpty(_searchText))
                {
                    string searchLower = _searchText.ToLower();
                    filteredEntries = filteredEntries.Where(e => 
                        (e.Content != null && e.Content.ToLower().Contains(searchLower))
                    );
                }

                if (_startDate.HasValue)
                {
                    var startDate = _startDate.Value.Date;
                    filteredEntries = filteredEntries.Where(e => e.Timestamp.Date >= startDate);
                }

                if (_endDate.HasValue)
                {
                    var endDate = _endDate.Value.Date.AddDays(1).AddTicks(-1);
                    filteredEntries = filteredEntries.Where(e => e.Timestamp <= endDate);
                }

                var sortedEntries = _isSortedDescending
                    ? filteredEntries.OrderByDescending(e => e.Timestamp)
                    : filteredEntries.OrderBy(e => e.Timestamp);

                foreach (var entry in sortedEntries)
                {
                    AddTimelineEntryToUI(entry);
                }
            }
        }

        private void SortButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            _isSortedDescending = !_isSortedDescending;
            UpdateSortButtonStyle();
            LoadTimeline();
        }

        private void UpdateSortButtonStyle()
        {
            if (_isSortedDescending)
            {
                var sortButtonTemplate = SortButton.Template;
                var path = sortButtonTemplate.FindName("SortIcon", SortButton) as WPFShapes.Path;
                if (path != null)
                {
                    path.Data = WPFMedia.Geometry.Parse("M6,12L12,6L18,12");
                }
                SortButton.ToolTip = "倒序";
            }
            else
            {
                var sortButtonTemplate = SortButton.Template;
                var path = sortButtonTemplate.FindName("SortIcon", SortButton) as WPFShapes.Path;
                if (path != null)
                {
                    path.Data = WPFMedia.Geometry.Parse("M6,6L12,12L18,6");
                }
                SortButton.ToolTip = "正序";
            }
        }

        private void FilterAllButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            _filterType = null;
            UpdateFilterButtonStyle();
            LoadTimeline();
        }

        private void FilterThoughtButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            _filterType = TimelineEntryType.Thought;
            UpdateFilterButtonStyle();
            LoadTimeline();
        }

        private void FilterOperationButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            _filterType = TimelineEntryType.Operation;
            UpdateFilterButtonStyle();
            LoadTimeline();
        }

        private void FilterQuestionButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            _filterType = TimelineEntryType.Question;
            UpdateFilterButtonStyle();
            LoadTimeline();
        }

        private void SearchTextBox_TextChanged(object sender, WPFControls.TextChangedEventArgs e)
        {
            _searchText = SearchTextBox.Text;
            LoadTimeline();
        }

        private void StartDatePicker_DateChanged(object sender, DateTime? e)
        {
            _startDate = e;
            LoadTimeline();
        }

        private void EndDatePicker_DateChanged(object sender, DateTime? e)
        {
            _endDate = e;
            LoadTimeline();
        }

        private void ClearDateFilterButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            StartDatePicker.Clear();
            EndDatePicker.Clear();
            _startDate = null;
            _endDate = null;
            LoadTimeline();
        }

        private void UpdateFilterButtonStyle()
        {
            var secondaryBackground = (WPFMedia.Brush)WPF.Application.Current.Resources["SecondaryBackground"];
            var secondaryText = (WPFMedia.Brush)WPF.Application.Current.Resources["SecondaryText"];

            FilterAllButton.Background = secondaryBackground;
            FilterAllButton.Foreground = secondaryText;

            FilterThoughtButton.Background = secondaryBackground;
            FilterThoughtButton.Foreground = secondaryText;

            FilterQuestionButton.Background = secondaryBackground;
            FilterQuestionButton.Foreground = secondaryText;

            FilterOperationButton.Background = secondaryBackground;
            FilterOperationButton.Foreground = secondaryText;

            if (_filterType == null)
            {
                FilterAllButton.Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#3B82F6"));
                FilterAllButton.Foreground = WPFMedia.Brushes.White;
            }
            else if (_filterType == TimelineEntryType.Thought)
            {
                FilterThoughtButton.Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#7C3AED"));
                FilterThoughtButton.Foreground = WPFMedia.Brushes.White;
            }
            else if (_filterType == TimelineEntryType.Question)
            {
                FilterQuestionButton.Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#8B5CF6"));
                FilterQuestionButton.Foreground = WPFMedia.Brushes.White;
            }
            else if (_filterType == TimelineEntryType.Operation)
            {
                FilterOperationButton.Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#3B82F6"));
                FilterOperationButton.Foreground = WPFMedia.Brushes.White;
            }
        }

        private WPFBitmap.BitmapImage PathToBitmapImage(string relativePath)
        {
            try
            {
                string dataStoragePath = SettingsManager.Current.DataStoragePath;
                if (string.IsNullOrEmpty(dataStoragePath))
                {
                    dataStoragePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "积微"
                    );
                }
                
                string fullPath = Path.Combine(dataStoragePath, relativePath);
                if (File.Exists(fullPath))
                {
                    var bitmapImage = new WPFBitmap.BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmapImage.CacheOption = WPFBitmap.BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    return bitmapImage;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void AddTimelineEntryToUI(TimelineEntry entry)
        {
            var border = new WPFControls.Border
            {
                Margin = new WPF.Thickness(0, 0, 0, 16),
                Padding = new WPF.Thickness(0),
                Background = new WPFMedia.SolidColorBrush(WPFMedia.Colors.Transparent),
                Tag = entry
            };

            var grid = new WPFControls.Grid();
            grid.ColumnDefinitions.Add(new WPFControls.ColumnDefinition { Width = new WPF.GridLength(24) });
            grid.ColumnDefinitions.Add(new WPFControls.ColumnDefinition { Width = new WPF.GridLength(1) });
            grid.ColumnDefinitions.Add(new WPFControls.ColumnDefinition { Width = new WPF.GridLength(1, WPF.GridUnitType.Star) });

            var dotBorder = new WPFControls.Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new WPF.CornerRadius(6),
                Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString(
                    entry.Type == TimelineEntryType.Thought ? "#7C3AED" : 
                    entry.Type == TimelineEntryType.Question ? "#8B5CF6" : 
                    "#3B82F6")),
                Margin = new WPF.Thickness(0, 16, 0, 0),
                HorizontalAlignment = WPF.HorizontalAlignment.Center,
                VerticalAlignment = WPF.VerticalAlignment.Top
            };
            WPFControls.Grid.SetColumn(dotBorder, 0);

            var lineBorder = new WPFControls.Border
            {
                Width = 1,
                Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#E5E7EB")),
                Margin = new WPF.Thickness(11.5, 28, 0, 0)
            };
            WPFControls.Grid.SetColumn(lineBorder, 0);

            var contentBorder = new WPFControls.Border
            {
                Style = (WPF.Style)FindResource("TimelineEntryCard"),
                Margin = new WPF.Thickness(8, 0, 0, 0)
            };
            WPFControls.Grid.SetColumn(contentBorder, 2);

            var contentStack = new WPFControls.StackPanel
            {
                Margin = new WPF.Thickness(16)
            };

            var timeText = new WPFControls.TextBlock
            {
                Text = entry.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 12,
                Foreground = (WPFMedia.Brush)WPF.Application.Current.Resources["TextTertiary"],
                Margin = new WPF.Thickness(0, 0, 0, 8)
            };
            contentStack.Children.Add(timeText);

            if (!string.IsNullOrEmpty(entry.Content))
            {
                var contentText = new WPFControls.TextBlock
                {
                    Text = entry.Content,
                    FontSize = 14,
                    Foreground = (WPFMedia.Brush)WPF.Application.Current.Resources["TextPrimary"],
                    TextWrapping = WPF.TextWrapping.Wrap,
                    LineHeight = 20,
                    Margin = new WPF.Thickness(0, 0, 0, 8),
                    Name = "ContentTextBlock",
                    Tag = entry
                };
                contentStack.Children.Add(contentText);
            }

            if (entry.HasImages && entry.ImagePathList != null)
            {
                var imagePanel = new WPFControls.WrapPanel();
                foreach (var imagePath in entry.ImagePathList)
                {
                    try
                    {
                        var bitmapImage = PathToBitmapImage(imagePath);
                        if (bitmapImage != null)
                        {
                            var imageBorder = new WPFControls.Border
                            {
                                Margin = new WPF.Thickness(0, 0, 8, 8),
                                CornerRadius = new WPF.CornerRadius(8),
                                Background = (WPFMedia.Brush)WPF.Application.Current.Resources["HoverColor"],
                                Cursor = WPF.Input.Cursors.Hand
                            };
                            var image = new WPFControls.Image
                            {
                                Source = bitmapImage,
                                MaxHeight = 120,
                                MaxWidth = 180,
                                Stretch = WPFMedia.Stretch.Uniform,
                                StretchDirection = WPFControls.StretchDirection.DownOnly,
                                Tag = imagePath
                            };
                            image.MouseLeftButtonDown += TimelineImage_MouseLeftButtonDown;
                            imageBorder.Child = image;
                            imagePanel.Children.Add(imageBorder);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading image: {ex.Message}");
                    }
                }
                contentStack.Children.Add(imagePanel);
            }

            var contextMenu = new WPFControls.ContextMenu();
            var editMenuItem = new WPFControls.MenuItem
            {
                Header = "编辑",
                Tag = entry
            };
            editMenuItem.Click += EditMenuItem_Click;
            contextMenu.Items.Add(editMenuItem);
            
            var deleteMenuItem = new WPFControls.MenuItem
            {
                Header = "删除",
                Tag = entry
            };
            deleteMenuItem.Click += DeleteMenuItem_Click;
            contextMenu.Items.Add(deleteMenuItem);
            contentBorder.ContextMenu = contextMenu;

            contentBorder.Child = contentStack;

            grid.Children.Add(dotBorder);
            grid.Children.Add(lineBorder);
            grid.Children.Add(contentBorder);

            border.Child = grid;
            TimelinePanel.Children.Add(border);
        }

        private WPFBitmap.BitmapImage Base64ToBitmapImage(string base64String)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64String);
                using (var stream = new MemoryStream(bytes))
                {
                    var bitmapImage = new WPFBitmap.BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = WPFBitmap.BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        private void ShowAddButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            if (InputPanel.Visibility == WPF.Visibility.Collapsed)
            {
                InputPanel.Visibility = WPF.Visibility.Visible;
                var buttonTemplate = ShowAddButton.Template;
                var path = buttonTemplate.FindName("AddIcon", ShowAddButton) as WPFShapes.Path;
                if (path != null)
                {
                    path.Data = WPFMedia.Geometry.Parse("M5,12H19");
                }
                ShowAddButton.ToolTip = "关闭";
            }
            else
            {
                InputPanel.Visibility = WPF.Visibility.Collapsed;
                TimelineInputTextBox.Clear();
                SetPlaceholderText();
                _pendingImages.Clear();
                UpdateImagePreview();
                var buttonTemplate = ShowAddButton.Template;
                var path = buttonTemplate.FindName("AddIcon", ShowAddButton) as WPFShapes.Path;
                if (path != null)
                {
                    path.Data = WPFMedia.Geometry.Parse("M12,5V19M5,12H19");
                }
                ShowAddButton.ToolTip = "添加";
            }
        }

        private void CancelTimelineButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            InputPanel.Visibility = WPF.Visibility.Collapsed;
            TimelineInputTextBox.Clear();
            SetPlaceholderText();
            _pendingImages.Clear();
            UpdateImagePreview();
            var buttonTemplate = ShowAddButton.Template;
            var path = buttonTemplate.FindName("AddIcon", ShowAddButton) as WPFShapes.Path;
            if (path != null)
            {
                path.Data = WPFMedia.Geometry.Parse("M12,5V19M5,12H19");
            }
            ShowAddButton.ToolTip = "添加";
        }

        private async void SaveTimelineButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            try
            {
                var content = TimelineInputTextBox.Text.Trim();
                bool hasContent = !string.IsNullOrEmpty(content) && content != "记录一些想法或者感受吧";
                bool hasImages = _pendingImages.Count > 0;

                if ((hasContent && Goal != null) || hasImages)
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
                    if (TimelineTypeComboBox.SelectedIndex == 1)
                    {
                        entryType = TimelineEntryType.Question;
                    }

                    if (hasContent)
                    {
                        Goal.AddTimelineEntry(content, entryType, null, imagePathList);
                    }
                    else if (imagePathList != null)
                    {
                        Goal.AddTimelineEntry(string.Empty, entryType, null, imagePathList);
                    }

                    await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                    LoadTimeline();
                    TimelineInputTextBox.Clear();
                    SetPlaceholderText();
                    _pendingImages.Clear();
                    UpdateImagePreview();
                    InputPanel.Visibility = WPF.Visibility.Collapsed;
                    var buttonTemplate = ShowAddButton.Template;
                    var path = buttonTemplate.FindName("AddIcon", ShowAddButton) as WPFShapes.Path;
                    if (path != null)
                    {
                        path.Data = WPFMedia.Geometry.Parse("M12,5V19M5,12H19");
                    }
                    ShowAddButton.ToolTip = "添加";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveTimelineButton_Click: {ex.Message}");
            }
        }

        private string BitmapSourceToBase64(WPFBitmap.BitmapSource bitmapSource)
        {
            using (var stream = new MemoryStream())
            {
                var encoder = new WPFBitmap.PngBitmapEncoder();
                encoder.Frames.Add(WPFBitmap.BitmapFrame.Create(bitmapSource));
                encoder.Save(stream);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private async void DeleteMenuItem_Click(object sender, WPF.RoutedEventArgs e)
        {
            try
            {
                var menuItem = sender as WPFControls.MenuItem;
                if (menuItem?.Tag is TimelineEntry entry && Goal != null)
                {
                    var confirmWindow = new DeleteConfirmationWindow("确认删除", "确定要删除这条记录吗？", "删除");
                    confirmWindow.Owner = this;
                    confirmWindow.ShowDialog();

                    if (confirmWindow.Confirmed)
                    {
                        // 删除关联的图片文件
                        if (entry.HasImages && entry.ImagePathList != null)
                        {
                            string dataStoragePath = SettingsManager.Current.DataStoragePath;
                            if (string.IsNullOrEmpty(dataStoragePath))
                            {
                                dataStoragePath = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "积微"
                                );
                            }

                            foreach (var imagePath in entry.ImagePathList)
                            {
                                try
                                {
                                    string fullPath = Path.Combine(dataStoragePath, imagePath);
                                    if (File.Exists(fullPath))
                                    {
                                        File.Delete(fullPath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error deleting image: {ex.Message}");
                                }
                            }
                        }

                        Goal.Timeline.Remove(entry);
                        await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                        LoadTimeline();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteMenuItem_Click: {ex.Message}");
            }
        }

        private void EditMenuItem_Click(object sender, WPF.RoutedEventArgs e)
        {
            if (_editingEntry != null)
            {
                CancelEditing();
            }

            var menuItem = sender as WPFControls.MenuItem;
            if (menuItem?.Tag is TimelineEntry entry)
            {
                _editingEntry = entry;
                _originalContent = entry.Content;
                _originalImages = entry.ImagePathList != null ? new List<string>(entry.ImagePathList) : new List<string>();
                _editingNewImages.Clear();

                foreach (WPFControls.Border border in TimelinePanel.Children)
                {
                    if (border.Tag == entry)
                    {
                        _editingCard = border;
                        ConvertToEditMode(border, entry);
                        break;
                    }
                }
            }
        }

        private void ConvertToEditMode(WPFControls.Border border, TimelineEntry entry)
        {
            var grid = border.Child as WPFControls.Grid;
            if (grid == null) return;

            var contentBorder = grid.Children[2] as WPFControls.Border;
            if (contentBorder == null) return;

            var contentStack = contentBorder.Child as WPFControls.StackPanel;
            if (contentStack == null) return;

            contentStack.Children.Clear();

            var timeText = new WPFControls.TextBlock
            {
                Text = entry.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 12,
                Foreground = (WPFMedia.Brush)WPF.Application.Current.Resources["TextTertiary"],
                Margin = new WPF.Thickness(0, 0, 0, 8)
            };
            contentStack.Children.Add(timeText);

            var textBox = new WPFControls.TextBox
            {
                Text = entry.Content,
                FontSize = 14,
                Foreground = (WPFMedia.Brush)WPF.Application.Current.Resources["TextPrimary"],
                CaretBrush = (WPFMedia.Brush)WPF.Application.Current.Resources["TextPrimary"],
                TextWrapping = WPF.TextWrapping.Wrap,
                AcceptsReturn = true,
                BorderThickness = new WPF.Thickness(0),
                Background = new WPFMedia.SolidColorBrush(WPFMedia.Colors.Transparent),
                Margin = new WPF.Thickness(0, 0, 0, 8),
                Name = "EditTextBox",
                Tag = entry
            };
            textBox.PreviewTextInput += TimelineInputTextBox_PreviewTextInput;
            WPF.Input.CommandManager.AddPreviewExecutedHandler(textBox, EditTextBox_PreviewExecuted);
            WPF.Input.CommandManager.AddPreviewCanExecuteHandler(textBox, EditTextBox_PreviewCanExecute);
            contentStack.Children.Add(textBox);

            var imagePanel = new WPFControls.WrapPanel();

            if (entry.HasImages && entry.ImagePathList != null)
            {
                foreach (var imagePath in entry.ImagePathList)
                {
                    try
                    {
                        var bitmapImage = PathToBitmapImage(imagePath);
                        if (bitmapImage != null)
                        {
                            var imageContainer = new WPFControls.Grid();
                            var imageBorder = new WPFControls.Border
                            {
                                Margin = new WPF.Thickness(0, 0, 8, 8),
                                CornerRadius = new WPF.CornerRadius(8),
                                Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#F3F4F6")),
                                Cursor = WPF.Input.Cursors.Hand
                            };
                            var image = new WPFControls.Image
                            {
                                Source = bitmapImage,
                                MaxHeight = 120,
                                MaxWidth = 180,
                                Stretch = WPFMedia.Stretch.Uniform,
                                StretchDirection = WPFControls.StretchDirection.DownOnly,
                                Tag = imagePath
                            };
                            image.MouseLeftButtonDown += TimelineImage_MouseLeftButtonDown;
                            imageBorder.Child = image;
                            imageContainer.Children.Add(imageBorder);

                            var deleteImageButton = new WPFControls.Button
                            {
                                Content = "×",
                                Width = 24,
                                Height = 24,
                                FontSize = 16,
                                FontWeight = WPF.FontWeights.Bold,
                                Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#EF4444")),
                                Foreground = new WPFMedia.SolidColorBrush(WPFMedia.Colors.White),
                                BorderThickness = new WPF.Thickness(0),
                                Cursor = WPF.Input.Cursors.Hand,
                                Margin = new WPF.Thickness(0, 0, 8, 8),
                                HorizontalAlignment = WPF.HorizontalAlignment.Right,
                                VerticalAlignment = WPF.VerticalAlignment.Top,
                                Tag = imagePath
                            };
                            deleteImageButton.Click += DeleteEditImage_Click;
                            imageContainer.Children.Add(deleteImageButton);

                            imagePanel.Children.Add(imageContainer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading image: {ex.Message}");
                    }
                }
            }

            if (_editingNewImages.Count > 0)
            {
                foreach (var bitmapImage in _editingNewImages)
                {
                    var imageContainer = new WPFControls.Grid();
                    var imageBorder = new WPFControls.Border
                    {
                        Margin = new WPF.Thickness(0, 0, 8, 8),
                        CornerRadius = new WPF.CornerRadius(8),
                        Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#F3F4F6")),
                        Cursor = WPF.Input.Cursors.Hand
                    };
                    var image = new WPFControls.Image
                    {
                        Source = bitmapImage,
                        MaxHeight = 120,
                        MaxWidth = 180,
                        Stretch = WPFMedia.Stretch.Uniform,
                        StretchDirection = WPFControls.StretchDirection.DownOnly,
                        Tag = bitmapImage
                    };
                    image.MouseLeftButtonDown += PreviewImage_MouseLeftButtonDown;
                    imageBorder.Child = image;
                    imageContainer.Children.Add(imageBorder);

                    var deleteImageButton = new WPFControls.Button
                    {
                        Content = "×",
                        Width = 24,
                        Height = 24,
                        FontSize = 16,
                        FontWeight = WPF.FontWeights.Bold,
                        Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#EF4444")),
                        Foreground = new WPFMedia.SolidColorBrush(WPFMedia.Colors.White),
                        BorderThickness = new WPF.Thickness(0),
                        Cursor = WPF.Input.Cursors.Hand,
                        Margin = new WPF.Thickness(0, 0, 8, 8),
                        HorizontalAlignment = WPF.HorizontalAlignment.Right,
                        VerticalAlignment = WPF.VerticalAlignment.Top,
                        Tag = bitmapImage
                    };
                    deleteImageButton.Click += DeleteNewEditImage_Click;
                    imageContainer.Children.Add(deleteImageButton);

                    imagePanel.Children.Add(imageContainer);
                }
            }
            
            contentStack.Children.Add(imagePanel);

            // 创建底部按钮区域：添加图片按钮在左边，取消和保存按钮在右边
            var bottomButtonGrid = new WPF.Controls.Grid
            {
                Margin = new WPF.Thickness(0, 8, 0, 0)
            };
            bottomButtonGrid.ColumnDefinitions.Add(new WPF.Controls.ColumnDefinition { Width = WPF.GridLength.Auto });
            bottomButtonGrid.ColumnDefinitions.Add(new WPF.Controls.ColumnDefinition { Width = WPF.GridLength.Auto, SharedSizeGroup = "ButtonSpacing" });
            bottomButtonGrid.ColumnDefinitions.Add(new WPF.Controls.ColumnDefinition { Width = new WPF.GridLength(1, WPF.GridUnitType.Star) });
            bottomButtonGrid.ColumnDefinitions.Add(new WPF.Controls.ColumnDefinition { Width = WPF.GridLength.Auto });
            bottomButtonGrid.ColumnDefinitions.Add(new WPF.Controls.ColumnDefinition { Width = WPF.GridLength.Auto });

            // 创建添加图片按钮
            var addImageButton = new WPFControls.Button
            {
                Width = 36,
                Height = 36,
                Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#F3F4F6")),
                Foreground = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString("#4B5563")),
                Margin = new WPF.Thickness(0),
                BorderThickness = new WPF.Thickness(0),
                Cursor = WPF.Input.Cursors.Hand,
                Style = (WPF.Style)FindResource("InteractiveButton"),
                ToolTip = "添加图片"
            };
            addImageButton.Click += AddEditImageButton_Click;
            
            // 创建带有图片图标的ControlTemplate
            var addImageTemplate = new WPF.Controls.ControlTemplate(typeof(WPF.Controls.Button));
            
            // 创建Border
            var borderFactory = new WPF.FrameworkElementFactory(typeof(WPF.Controls.Border));
            borderFactory.SetValue(WPF.Controls.Border.CornerRadiusProperty, new WPF.CornerRadius(8));
            borderFactory.SetValue(WPF.Controls.Border.BackgroundProperty, new WPF.TemplateBindingExtension(WPF.Controls.Button.BackgroundProperty));
            borderFactory.SetValue(WPF.Controls.Border.PaddingProperty, new WPF.Thickness(8));
            
            // 创建Path图标
            var pathFactory = new WPF.FrameworkElementFactory(typeof(WPFShapes.Path));
            pathFactory.SetValue(WPFShapes.Path.DataProperty, WPFMedia.Geometry.Parse("M21,19V5C21,3.89 20.1,3 19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19M8.5,13.5L11,16.51L14.5,12L19,18H5L8.5,13.5"));
            pathFactory.SetValue(WPFShapes.Path.FillProperty, new WPF.TemplateBindingExtension(WPF.Controls.Button.ForegroundProperty));
            pathFactory.SetValue(WPFShapes.Path.WidthProperty, 16.0);
            pathFactory.SetValue(WPFShapes.Path.HeightProperty, 16.0);
            pathFactory.SetValue(WPFShapes.Path.StretchProperty, WPFMedia.Stretch.Uniform);
            pathFactory.SetValue(WPF.Controls.Control.HorizontalAlignmentProperty, WPF.HorizontalAlignment.Center);
            pathFactory.SetValue(WPF.Controls.Control.VerticalAlignmentProperty, WPF.VerticalAlignment.Center);
            
            borderFactory.AppendChild(pathFactory);
            addImageTemplate.VisualTree = borderFactory;
            
            // 一次性赋值给按钮
            addImageButton.Template = addImageTemplate;
            
            // 添加图片按钮到第一列
            WPF.Controls.Grid.SetColumn(addImageButton, 0);
            bottomButtonGrid.Children.Add(addImageButton);

            // 创建取消按钮
            var cancelButton = CreateRoundedButton(
                "取消",
                70,
                36,
                "#F3F4F6",
                "#4B5563",
                new WPF.Thickness(0, 0, 10, 0));
            cancelButton.Click += CancelEditButton_Click;
            WPF.Controls.Grid.SetColumn(cancelButton, 3);
            bottomButtonGrid.Children.Add(cancelButton);

            // 创建保存按钮
            var saveButton = CreateRoundedButton(
                "保存",
                70,
                36,
                "#10B981",
                "#FFFFFF",
                new WPF.Thickness(0, 0, 0, 0));
            saveButton.Click += SaveEditButton_Click;
            WPF.Controls.Grid.SetColumn(saveButton, 4);
            bottomButtonGrid.Children.Add(saveButton);

            contentStack.Children.Add(bottomButtonGrid);
        }

        private void EditTextBox_PreviewCanExecute(object sender, WPF.Input.CanExecuteRoutedEventArgs e)
        {
            if (e.Command == WPF.Input.ApplicationCommands.Paste)
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void EditTextBox_PreviewExecuted(object sender, WPF.Input.ExecutedRoutedEventArgs e)
        {
            if (e.Command == WPF.Input.ApplicationCommands.Paste && _editingEntry != null)
            {
                try
                {
                    var dataObject = WPF.Clipboard.GetDataObject();
                    if (dataObject != null)
                    {
                        bool handled = false;
                        
                        if (dataObject.GetDataPresent(WPF.DataFormats.Bitmap))
                        {
                            var bitmapSource = dataObject.GetData(WPF.DataFormats.Bitmap) as WPFBitmap.BitmapSource;
                            if (bitmapSource != null)
                            {
                                var bitmapImage = BitmapSourceToBitmapImage(bitmapSource);
                                if (bitmapImage != null)
                                {
                                    _editingNewImages.Add(bitmapImage);
                                    if (_editingCard != null)
                                    {
                                        ConvertToEditMode(_editingCard, _editingEntry);
                                    }
                                    handled = true;
                                }
                            }
                        }
                        else if (dataObject.GetDataPresent(WPF.DataFormats.FileDrop))
                        {
                            var files = dataObject.GetData(WPF.DataFormats.FileDrop) as string[];
                            if (files != null)
                            {
                                bool hasImage = false;
                                foreach (var file in files)
                                {
                                    if (IsImageFile(file))
                                    {
                                        try
                                        {
                                            var bitmap = new WPFBitmap.BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.UriSource = new Uri(file, UriKind.Absolute);
                                            bitmap.CacheOption = WPFBitmap.BitmapCacheOption.OnLoad;
                                            bitmap.EndInit();
                                            bitmap.Freeze();
                                            _editingNewImages.Add(bitmap);
                                            hasImage = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Error loading image: {ex.Message}");
                                        }
                                    }
                                }
                                if (hasImage && _editingCard != null)
                                {
                                    ConvertToEditMode(_editingCard, _editingEntry);
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

        private void DeleteEditImage_Click(object sender, WPF.RoutedEventArgs e)
        {
            var button = sender as WPFControls.Button;
            if (button?.Tag is string imagePath && _editingEntry != null)
            {
                if (_editingEntry.ImagePathList != null)
                {
                    // 删除物理文件
                    string dataStoragePath = SettingsManager.Current.DataStoragePath;
                    if (string.IsNullOrEmpty(dataStoragePath))
                    {
                        dataStoragePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "积微"
                        );
                    }
                    
                    try
                    {
                        string fullPath = Path.Combine(dataStoragePath, imagePath);
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting image: {ex.Message}");
                    }
                    
                    _editingEntry.ImagePathList.Remove(imagePath);
                    if (_editingCard != null)
                    {
                        ConvertToEditMode(_editingCard, _editingEntry);
                    }
                }
            }
        }

        private void DeleteNewEditImage_Click(object sender, WPF.RoutedEventArgs e)
        {
            var button = sender as WPFControls.Button;
            if (button?.Tag is WPFBitmap.BitmapImage bitmapImage)
            {
                _editingNewImages.Remove(bitmapImage);
                if (_editingCard != null && _editingEntry != null)
                {
                    ConvertToEditMode(_editingCard, _editingEntry);
                }
            }
        }

        private void AddEditImageButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            var dialog = new Win32Dialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
                Multiselect = true,
                Title = "选择图片"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    try
                    {
                        var bitmap = new WPFBitmap.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(file, UriKind.Absolute);
                        bitmap.CacheOption = WPFBitmap.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        _editingNewImages.Add(bitmap);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading image: {ex.Message}");
                    }
                }
                if (_editingCard != null && _editingEntry != null)
                {
                    ConvertToEditMode(_editingCard, _editingEntry);
                }
            }
        }

        private async void SaveEditButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            try
            {
                if (_editingCard == null || _editingEntry == null) return;

                var grid = _editingCard.Child as WPFControls.Grid;
                if (grid == null) return;

                var contentBorder = grid.Children[2] as WPFControls.Border;
                if (contentBorder == null) return;

                var contentStack = contentBorder.Child as WPFControls.StackPanel;
                if (contentStack == null) return;

                var textBox = contentStack.Children[1] as WPFControls.TextBox;
                if (textBox != null)
                {
                    _editingEntry.Content = textBox.Text;
                }

                if (_editingNewImages.Count > 0)
                {
                    if (_editingEntry.ImagePathList == null)
                    {
                        _editingEntry.ImagePathList = new List<string>();
                    }
                    foreach (var img in _editingNewImages)
                    {
                        _editingEntry.ImagePathList.Add(SaveImageToFile(img));
                    }
                }

                await DataStorageService.SaveGoalsAsync(GoalsPage.Goals);
                _editingEntry = null;
                _editingCard = null;
                _originalContent = null;
                _originalImages = null;
                _editingNewImages.Clear();
                LoadTimeline();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveEditButton_Click: {ex.Message}");
            }
        }

        private void CancelEditButton_Click(object sender, WPF.RoutedEventArgs e)
        {
            CancelEditing();
        }

        private void CancelEditing()
        {
            if (_editingEntry != null && _originalContent != null)
            {
                _editingEntry.Content = _originalContent;
                _editingEntry.ImagePathList = _originalImages != null ? new List<string>(_originalImages) : new List<string>();
            }
            _editingEntry = null;
            _editingCard = null;
            _originalContent = null;
            _originalImages = null;
            _editingNewImages.Clear();
            LoadTimeline();
        }

        private WPFControls.Button CreateRoundedButton(
            string content,
            double width,
            double height,
            string backgroundColor,
            string foregroundColor,
            WPF.Thickness margin)
        {
            var button = new WPFControls.Button
            {
                Content = content,
                Width = width,
                Height = height,
                Background = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString(backgroundColor)),
                Foreground = new WPFMedia.SolidColorBrush((WPFMedia.Color)WPFMedia.ColorConverter.ConvertFromString(foregroundColor)),
                Margin = margin,
                FontSize = 14,
                FontWeight = WPF.FontWeights.Medium,
                BorderThickness = new WPF.Thickness(0),
                Style = (WPF.Style)FindResource("InteractiveButton")
            };

            var template = new WPF.Controls.ControlTemplate(typeof(WPF.Controls.Button));
            var borderFactory = new WPF.FrameworkElementFactory(typeof(WPF.Controls.Border));
            borderFactory.SetValue(WPF.Controls.Border.CornerRadiusProperty, new WPF.CornerRadius(10));
            borderFactory.SetValue(WPF.Controls.Border.BackgroundProperty, new WPF.TemplateBindingExtension(WPF.Controls.Button.BackgroundProperty));
            borderFactory.SetValue(WPF.Controls.Border.PaddingProperty, new WPF.Thickness(0));

            var contentPresenterFactory = new WPF.FrameworkElementFactory(typeof(WPF.Controls.ContentPresenter));
            contentPresenterFactory.SetValue(WPF.Controls.ContentPresenter.HorizontalAlignmentProperty, WPF.HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(WPF.Controls.ContentPresenter.VerticalAlignmentProperty, WPF.VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenterFactory);

            template.VisualTree = borderFactory;
            button.Template = template;

            return button;
        }
    }
}
