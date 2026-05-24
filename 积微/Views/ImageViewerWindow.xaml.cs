using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using 积微.Models;

namespace 积微.Views
{
    /// <summary>图片查看器窗口，支持缩放、拖拽和复制到剪贴板。</summary>
    public partial class ImageViewerWindow : Window
    {
        private double _currentZoom = 1.0;
        private const double _zoomStep = 0.2;
        private const double _minZoom = 0.2;
        private const double _maxZoom = 5.0;
        private bool _isDragging = false;
        private System.Windows.Point _dragStartPoint;
        private double _initialZoom = 1.0;
        private string _imagePath;

        /// <summary>使用 BitmapImage 构造图片查看器。</summary>
        public ImageViewerWindow(BitmapImage image, string imagePath = null)
        {
            InitializeComponent();
            ViewerImage.Source = image;
            _imagePath = imagePath;
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        /// <summary>使用 Base64 字符串构造图片查看器。</summary>
        public ImageViewerWindow(string base64Image)
        {
            InitializeComponent();
            try
            {
                byte[] bytes = Convert.FromBase64String(base64Image);
                using (var stream = new System.IO.MemoryStream(bytes))
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    ViewerImage.Source = bitmapImage;
                }
                SourceInitialized += OnSourceInitialized;
                Loaded += OnLoaded;
                Closed += OnClosed;
            }
            catch (Exception ex)
            {
                var messageBox = new MessageBoxWindow("错误", $"加载图片失败: {ex.Message}");
                messageBox.Owner = this;
                messageBox.ShowDialog();
                Close();
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            App.RegisterThemeWindow(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CalculateInitialZoom();
        }

        private void CalculateInitialZoom()
        {
            var image = ViewerImage.Source as BitmapImage;
            if (image == null) return;

            double containerWidth = ImageContainer.ActualWidth;
            double containerHeight = ImageContainer.ActualHeight;

            if (containerWidth < 10 || containerHeight < 10)
            {
                Dispatcher.BeginInvoke(() => CalculateInitialZoom(), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            double imageWidth = image.PixelWidth;
            double imageHeight = image.PixelHeight;

            double widthRatio = containerWidth / imageWidth;
            double heightRatio = containerHeight / imageHeight;
            double fitZoom = Math.Min(widthRatio, heightRatio);

            _initialZoom = Math.Min(fitZoom, 1.0);
            _currentZoom = _initialZoom;
            ImageScale.ScaleX = _currentZoom;
            ImageScale.ScaleY = _currentZoom;
            ImageTranslate.X = 0;
            ImageTranslate.Y = 0;
            UpdateZoomInfo();
        }

        private void Container_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(ImageContainer);
                ImageContainer.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Container_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ImageContainer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Container_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && ImageContainer.IsMouseCaptured)
            {
                System.Windows.Point currentPosition = e.GetPosition(ImageContainer);
                double deltaX = currentPosition.X - _dragStartPoint.X;
                double deltaY = currentPosition.Y - _dragStartPoint.Y;
                
                ImageTranslate.X += deltaX;
                ImageTranslate.Y += deltaY;
                
                _dragStartPoint = currentPosition;
                e.Handled = true;
            }
        }

        private void Container_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var mousePosition = e.GetPosition(ImageContainer);

                if (e.Delta > 0)
                {
                    ZoomAtPoint(mousePosition, true);
                }
                else
                {
                    ZoomAtPoint(mousePosition, false);
                }

                e.Handled = true;
            }
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ZoomAtPoint(System.Windows.Point center, bool zoomIn)
        {
            double oldZoom = _currentZoom;
            double newZoom = zoomIn ? _currentZoom + _zoomStep : _currentZoom - _zoomStep;

            // 限制缩放范围
            if (zoomIn && newZoom > _maxZoom) newZoom = _maxZoom;
            if (!zoomIn && newZoom < _minZoom) newZoom = _minZoom;

            if (Math.Abs(newZoom - oldZoom) < 0.001) return;

            _currentZoom = newZoom;
            ImageScale.ScaleX = _currentZoom;
            ImageScale.ScaleY = _currentZoom;

            double zoomDelta = _currentZoom / oldZoom;

            // 计算容器中心点
            double containerCenterX = ImageContainer.ActualWidth / 2.0;
            double containerCenterY = ImageContainer.ActualHeight / 2.0;

            // 以指定点为中心进行缩放的正确公式
            ImageTranslate.X = center.X - containerCenterX - zoomDelta * (center.X - containerCenterX - ImageTranslate.X);
            ImageTranslate.Y = center.Y - containerCenterY - zoomDelta * (center.Y - containerCenterY - ImageTranslate.Y);

            UpdateZoomInfo();
        }

        private void ZoomIn()
        {
            // 默认以容器中心为中心进行放大
            System.Windows.Point center = new System.Windows.Point(ImageContainer.ActualWidth / 2.0, ImageContainer.ActualHeight / 2.0);
            ZoomAtPoint(center, true);
        }

        private void ZoomOut()
        {
            // 默认以容器中心为中心进行缩小
            System.Windows.Point center = new System.Windows.Point(ImageContainer.ActualWidth / 2.0, ImageContainer.ActualHeight / 2.0);
            ZoomAtPoint(center, false);
        }

        private void ResetZoom()
        {
            _currentZoom = _initialZoom;
            ImageScale.ScaleX = _currentZoom;
            ImageScale.ScaleY = _currentZoom;
            ImageTranslate.X = 0;
            ImageTranslate.Y = 0;
            UpdateZoomInfo();
        }

        private void UpdateZoomInfo()
        {
            ZoomInfo.Text = $"缩放: {(_currentZoom * 100):F0}%";
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewerImage.Source is BitmapImage bitmapImage)
                {
                    System.Windows.Clipboard.SetImage(bitmapImage);
                    var messageBox = new MessageBoxWindow("提示", "图片已复制到剪贴板");
                    messageBox.Owner = this;
                    messageBox.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var messageBox = new MessageBoxWindow("错误", $"复制图片失败: {ex.Message}");
                messageBox.Owner = this;
                messageBox.ShowDialog();
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_imagePath))
                {
                    string dataStoragePath = SettingsManager.Current.DataStoragePath;
                    if (string.IsNullOrEmpty(dataStoragePath))
                    {
                        dataStoragePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "积微"
                        );
                    }

                    string fullPath = Path.Combine(dataStoragePath, _imagePath);
                    if (File.Exists(fullPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                    }
                    else
                    {
                        string folderPath = Path.GetDirectoryName(fullPath);
                        if (Directory.Exists(folderPath))
                        {
                            Process.Start("explorer.exe", folderPath);
                        }
                        else
                        {
                            var messageBox = new MessageBoxWindow("提示", "图片文件不存在");
                            messageBox.Owner = this;
                            messageBox.ShowDialog();
                        }
                    }
                }
                else
                {
                    string dataStoragePath = SettingsManager.Current.DataStoragePath;
                    if (string.IsNullOrEmpty(dataStoragePath))
                    {
                        dataStoragePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "积微"
                        );
                    }

                    string imagesPath = Path.Combine(dataStoragePath, "images");
                    if (Directory.Exists(imagesPath))
                    {
                        Process.Start("explorer.exe", imagesPath);
                    }
                    else
                    {
                        var messageBox = new MessageBoxWindow("提示", "图片目录不存在");
                        messageBox.Owner = this;
                        messageBox.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                var messageBox = new MessageBoxWindow("错误", $"打开目录失败: {ex.Message}");
                messageBox.Owner = this;
                messageBox.ShowDialog();
            }
        }
    }
}
