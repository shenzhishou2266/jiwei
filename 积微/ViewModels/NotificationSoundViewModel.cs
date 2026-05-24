using System.ComponentModel;
using 积微.Models.Audio;

namespace 积微.ViewModels
{
    /// <summary>提示音视图模型</summary>
    public class NotificationSoundViewModel : INotifyPropertyChanged
    {
        private NotificationSound _notificationSound;
        private bool _isSelected;

        /// <summary>提示音实例</summary>
        public NotificationSound NotificationSound => _notificationSound;

        /// <summary>是否选中</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>构造提示音视图模型</summary>
        public NotificationSoundViewModel(NotificationSound notificationSound, bool isSelected)
        {
            _notificationSound = notificationSound;
            _isSelected = isSelected;
        }

        /// <summary>属性变更事件</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}