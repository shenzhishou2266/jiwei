using System.Collections.Generic;
using System.Windows.Media;
using System;
using System.Reflection;
using 积微.Models.Audio;

namespace 积微.Services.Audio
{
    /// <summary>提示音管理器，负责提示音的加载、播放和音量控制</summary>
    public class NotificationSoundManager
    {
        private List<NotificationSound> _notificationSounds;
        private MediaPlayer _player;
        private double _currentVolume;
        private List<MediaPlayer> _activePlayers;

        /// <summary>提示音列表</summary>
        public List<NotificationSound> NotificationSounds => _notificationSounds;

        /// <summary>构造提示音管理器</summary>
        public NotificationSoundManager()
        {
            _notificationSounds = new List<NotificationSound>();
            _activePlayers = new List<MediaPlayer>();
            _currentVolume = 1.0; // 默认音量为100%
            InitializeDefaultNotificationSounds();
            InitializePlayer();
        }

        private void InitializeDefaultNotificationSounds()
        {
            // 添加默认的提示音
            _notificationSounds.Add(new NotificationSound("音效一", "Resources/audio/intro-01.aac"));
            _notificationSounds.Add(new NotificationSound("音效二", "Resources/audio/old-church-bell.aac"));
            _notificationSounds.Add(new NotificationSound("音效三", "Resources/audio/achievement_s.aac"));
            _notificationSounds.Add(new NotificationSound("音效四", "Resources/audio/achievement.aac"));
        }
        private void InitializePlayer()
        {
            _player = new MediaPlayer();
        }
        /// <summary>根据名称获取提示音</summary>
        public NotificationSound GetNotificationSound(string name)
        {
            return _notificationSounds.Find(sound => sound.Name == name);
        }

        /// <summary>根据索引获取提示音</summary>
        public NotificationSound GetNotificationSound(int index)
        {
            if (index >= 0 && index < _notificationSounds.Count)
            {
                return _notificationSounds[index];
            }
            return null;
        }

        /// <summary>播放提示音（复用播放器）</summary>
        public void Play(NotificationSound sound)
        {
            try
            {
                string soundPath = FindNotificationSoundFile(sound.FilePath);
                if (soundPath != null)
                {
                    _player.Open(new Uri(soundPath));
                    // 应用当前音量设置
                    _player.Volume = _currentVolume;
                    _player.Play();
                }
            }
            catch
            {
                // 忽略播放错误
            }
        }

        /// <summary>使用新播放器播放提示音（支持同时播放多个音效）</summary>
        public void PlayWithNewPlayer(NotificationSound sound)
        {
            try
            {
                string soundPath = FindNotificationSoundFile(sound.FilePath);
                if (soundPath != null)
                {
                    var mediaPlayer = new MediaPlayer();
                    mediaPlayer.MediaEnded += (sender, e) =>
                    {
                        var player = sender as MediaPlayer;
                        if (player != null)
                        {
                            lock (_activePlayers)
                            {
                                _activePlayers.Remove(player);
                            }
                        }
                    };
                    mediaPlayer.Open(new Uri(soundPath));
                    // 应用当前音量设置
                    mediaPlayer.Volume = _currentVolume;
                    lock (_activePlayers)
                    {
                        _activePlayers.Add(mediaPlayer);
                    }
                    mediaPlayer.Play();
                }
            }
            catch
            {
                // 忽略播放错误
            }
        }

        private string FindNotificationSoundFile(string relativePath)
        {
            string[] possibleBasePaths = new string[]
            {
                // 当前工作目录
                Environment.CurrentDirectory,
                // 应用程序基目录
                AppDomain.CurrentDomain.BaseDirectory,
                // 应用程序基目录的上级目录
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."),
                // 应用程序基目录的上上级目录
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."),
                // 应用程序基目录的上上级目录的do子目录
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "do"),
                // 当前工作目录的do子目录
                System.IO.Path.Combine(Environment.CurrentDirectory, "do")
            };

            foreach (string basePath in possibleBasePaths)
            {
                string fullPath = System.IO.Path.Combine(basePath, relativePath);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>设置播放音量</summary>
        public void SetVolume(double volume)
        {
            try
            {
                _currentVolume = volume / 100.0;
                _player.Volume = _currentVolume;
            }
            catch
            {
                // 忽略音量设置错误
            }
        }

        /// <summary>停止所有播放</summary>
        public void Stop()
        {
            try
            {
                _player.Stop();
                lock (_activePlayers)
                {
                    foreach (var player in _activePlayers)
                    {
                        try { player.Stop(); }
                        catch { }
                    }
                    _activePlayers.Clear();
                }
            }
            catch
            {
                // 忽略停止错误
            }
        }
    }
}