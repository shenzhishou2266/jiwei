using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using 积微.Models.Audio;

namespace 积微.Services.Audio
{
    /// <summary>白噪音播放器状态</summary>
    public class WhiteNoisePlayer
    {
        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; }
        /// <summary>音量（0-100）</summary>
        public int Volume { get; set; }
        /// <summary>媒体播放器</summary>
        public MediaPlayer Player { get; set; }

        /// <summary>构造白噪音播放器状态</summary>
        public WhiteNoisePlayer()
        {
            IsEnabled = false;
            Volume = 50;
            Player = new MediaPlayer();
        }
    }

    /// <summary>白噪音管理器，负责白噪音的加载、播放和音量控制</summary>
    public class WhiteNoiseManager : INotifyPropertyChanged
    {
        private List<WhiteNoise> _whiteNoises;
        private Dictionary<WhiteNoise, WhiteNoisePlayer> _players;
        private bool _isPlaying;

        /// <summary>白噪音列表</summary>
        public List<WhiteNoise> WhiteNoises => _whiteNoises;

        /// <summary>是否正在播放</summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                }
            }
        }

        /// <summary>属性变更事件</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>构造白噪音管理器</summary>
        public WhiteNoiseManager()
        {
            _whiteNoises = new List<WhiteNoise>();
            _players = new Dictionary<WhiteNoise, WhiteNoisePlayer>();
            _isPlaying = false;
            InitializeDefaultWhiteNoises();
            InitializePlayers();
        }

        /// <summary>触发属性变更事件</summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void InitializeDefaultWhiteNoises()
        {
            // 添加默认的白噪音
            _whiteNoises.Add(new WhiteNoise("春山鸟鸣", "Resources/audio/birds-in-spring-scotland.aac", "Resources/audioCover/0.png"));
            _whiteNoises.Add(new WhiteNoise("雨落伞面", "Resources/audio/snow-on-umbrella-eq-130214_01.aac", "Resources/audioCover/1.png"));
            _whiteNoises.Add(new WhiteNoise("北风呼啸", "Resources/audio/wind-at-door-howling-4.aac", "Resources/audioCover/2.png"));
            _whiteNoises.Add(new WhiteNoise("时钟滴答", "Resources/audio/wall-clock-ticking.aac", "Resources/audioCover/3.png"));
            _whiteNoises.Add(new WhiteNoise("云阗雷动", "Resources/audio/rbh-thunder-storm.aac", "Resources/audioCover/4.png"));
            _whiteNoises.Add(new WhiteNoise("冰破雪融", "Resources/audio/relaxing-mountains-rivers-streams-running-water.aac", "Resources/audioCover/5.png"));
            _whiteNoises.Add(new WhiteNoise("雨打芭蕉", "Resources/audio/undertreeinrain.aac", "Resources/audioCover/6.png"));
            _whiteNoises.Add(new WhiteNoise("晚潮拍岸", "Resources/audio/oceanwavescrushing.aac", "Resources/audioCover/7.png"));
            _whiteNoises.Add(new WhiteNoise("酒馆人声", "Resources/audio/crowd-in-a-bar-lcr.aac", "Resources/audioCover/8.png"));
            _whiteNoises.Add(new WhiteNoise("深空独行", "Resources/audio/space-atmosphere-02-remastered.aac", "Resources/audioCover/9.png"));
        }

        private void InitializePlayers()
        {
            foreach (var whiteNoise in _whiteNoises)
            {
                _players[whiteNoise] = new WhiteNoisePlayer();
            }
        }

        /// <summary>根据索引获取白噪音</summary>
        public WhiteNoise GetWhiteNoise(int index)
        {
            if (index >= 0 && index < _whiteNoises.Count)
            {
                return _whiteNoises[index];
            }
            return null;
        }

        /// <summary>获取白噪音对应的播放器状态</summary>
        public WhiteNoisePlayer GetPlayer(WhiteNoise whiteNoise)
        {
            if (_players.ContainsKey(whiteNoise))
            {
                return _players[whiteNoise];
            }
            return null;
        }

        /// <summary>更新白噪音播放器状态</summary>
        public void UpdatePlayerState(WhiteNoise whiteNoise, bool isEnabled, int volume)
        {
            if (_players.ContainsKey(whiteNoise))
            {
                var player = _players[whiteNoise];
                player.IsEnabled = isEnabled;
                player.Volume = volume;

                if (_isPlaying && isEnabled)
                {
                    // 如果正在播放，更新音量
                    if (player.Player != null)
                    {
                        player.Player.Volume = volume / 100.0;
                    }
                }
            }
        }

        /// <summary>开始播放所有已启用的白噪音</summary>
        public void Play()
        {
            try
            {
                foreach (var pair in _players)
                {
                    var whiteNoise = pair.Key;
                    var playerInfo = pair.Value;

                    if (playerInfo.IsEnabled)
                    {
                        PlayWhiteNoise(whiteNoise, playerInfo);
                    }
                }
                IsPlaying = true;
            }
            catch
            {
                // 忽略播放错误
            }
        }

        /// <summary>停止所有白噪音播放</summary>
        public void Stop()
        {
            try
            {
                foreach (var playerInfo in _players.Values)
                {
                    if (playerInfo.Player != null)
                    {
                        playerInfo.Player.Stop();
                        playerInfo.Player.Close();
                    }
                }
                IsPlaying = false;
            }
            catch
            {
                // 忽略停止错误
            }
        }

        /// <summary>播放指定白噪音</summary>
        public void Play(WhiteNoise whiteNoise)
        {
            try
            {
                var player = GetPlayer(whiteNoise);
                if (player != null && player.IsEnabled)
                {
                    PlayWhiteNoise(whiteNoise, player);
                }
            }
            catch
            {
                // 忽略播放错误
            }
        }

        /// <summary>停止指定白噪音</summary>
        public void Stop(WhiteNoise whiteNoise)
        {
            try
            {
                var player = GetPlayer(whiteNoise);
                if (player != null)
                {
                    player.Player.Stop();
                    player.Player.Close();
                }
            }
            catch
            {
                // 忽略停止错误
            }
        }

        private void PlayWhiteNoise(WhiteNoise whiteNoise, WhiteNoisePlayer playerInfo)
        {
            try
            {
                string fullPath = FindWhiteNoiseFile(whiteNoise.FilePath);
                if (fullPath != null)
                {
                    var player = playerInfo.Player;

                    // 先移除之前的事件处理程序，避免重复添加
                    player.MediaEnded -= Player_MediaEnded;
                    // 设置循环播放
                    player.MediaEnded += Player_MediaEnded;

                    player.Stop();
                    player.Close();

                    // 使用存储的音量
                    player.Volume = playerInfo.Volume / 100.0;
                    player.Open(new Uri(fullPath));
                    player.Play();
                }
            }
            catch
            {
                // 忽略播放错误
            }
        }

        private void Player_MediaEnded(object sender, EventArgs e)
        {
            try
            {
                var player = sender as MediaPlayer;
                if (player != null)
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                }
            }
            catch
            {
                // 忽略循环播放错误
            }
        }

        private string FindWhiteNoiseFile(string relativePath)
        {
            string[] possibleBasePaths = new string[]
            {
                Directory.GetCurrentDirectory(),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
            };

            foreach (string basePath in possibleBasePaths)
            {
                string fullPath = Path.Combine(basePath, relativePath);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
    }
}