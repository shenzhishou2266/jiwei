using System;

namespace 积微.Models.Audio
{
    /// <summary>提示音定义</summary>
    public class NotificationSound
    {
        private string _name;
        private string _filePath;

        /// <summary>提示音名称</summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>提示音文件路径</summary>
        public string FilePath
        {
            get => _filePath;
            set => _filePath = value;
        }

        /// <summary>构造提示音实例</summary>
        public NotificationSound(string name, string filePath)
        {
            _name = name;
            _filePath = filePath;
        }
    }
}
