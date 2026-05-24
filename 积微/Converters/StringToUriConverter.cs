using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace 积微.Converters
{
    /// <summary>字符串路径到 BitmapImage 的转换器，用于将相对路径转为图片资源。</summary>
    public class StringToUriConverter : IValueConverter
    {
        /// <summary>将字符串路径转换为 BitmapImage 对象。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    // 尝试创建绝对路径
                    string[] possibleBasePaths = new string[]
                    {
                        System.IO.Directory.GetCurrentDirectory(),
                        AppDomain.CurrentDomain.BaseDirectory,
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
                    };

                    foreach (string basePath in possibleBasePaths)
                    {
                        string fullPath = System.IO.Path.Combine(basePath, path);
                        if (System.IO.File.Exists(fullPath))
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(fullPath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            return bitmap;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>不支持反向转换。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}