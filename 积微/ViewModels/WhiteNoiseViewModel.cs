using System.ComponentModel;
using 积微.Models.Audio;

namespace 积微.ViewModels
{
    /// <summary>白噪音视图模型</summary>
    public class WhiteNoiseViewModel : INotifyPropertyChanged
    {
        private WhiteNoise _whiteNoise;
        private bool _isEnabled;
        private int _volume;

        /// <summary>白噪音实例</summary>
        public WhiteNoise WhiteNoise => _whiteNoise;

        /// <summary>是否启用</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(MaskOpacity));
                }
            }
        }

        /// <summary>音量（0-100）</summary>
        public int Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    OnPropertyChanged(nameof(Volume));
                }
            }
        }

        /// <summary>遮罩透明度（未启用时显示遮罩）</summary>
        public double MaskOpacity => IsEnabled ? 0 : 0.8;

        /// <summary>构造白噪音视图模型</summary>
        public WhiteNoiseViewModel(WhiteNoise whiteNoise, bool isEnabled, int volume)
        {
            _whiteNoise = whiteNoise;
            _isEnabled = isEnabled;
            _volume = volume;
        }

        /// <summary>属性变更事件</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}