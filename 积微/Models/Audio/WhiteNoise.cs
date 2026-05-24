using System;

namespace 积微.Models.Audio
{
    /// <summary>白噪音定义</summary>
    public class WhiteNoise
    {
        private string _name;
        private string _filePath;
        private string _coverImagePath;

        /// <summary>白噪音名称</summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>白噪音文件路径</summary>
        public string FilePath
        {
            get => _filePath;
            set => _filePath = value;
        }

        /// <summary>封面图片路径</summary>
        public string CoverImagePath
        {
            get => _coverImagePath;
            set => _coverImagePath = value;
        }

        /// <summary>构造白噪音实例</summary>
        public WhiteNoise(string name, string filePath, string coverImagePath)
        {
            _name = name;
            _filePath = filePath;
            _coverImagePath = coverImagePath;
        }

        /// <summary>构造白噪音实例（无封面）</summary>
        public WhiteNoise(string name, string filePath) : this(name, filePath, string.Empty)
        {
        }
    }
}
