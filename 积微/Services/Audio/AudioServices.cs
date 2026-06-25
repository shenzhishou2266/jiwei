using System.Linq;
using 积微.Models;

namespace 积微.Services.Audio
{
    /// <summary>音频服务持有类，统一管理提示音和白噪音的运行时实例。</summary>
    public static class AudioServices
    {
        /// <summary>提示音管理器。</summary>
        public static NotificationSoundManager Notification { get; private set; } = null!;

        /// <summary>白噪音管理器。</summary>
        public static WhiteNoiseManager WhiteNoise { get; private set; } = null!;

        /// <summary>初始化音频服务。应在应用启动时调用一次。</summary>
        public static void Initialize()
        {
            Notification = new NotificationSoundManager();
            WhiteNoise = new WhiteNoiseManager();
        }

        /// <summary>应用保存的白噪音状态到管理器。</summary>
        public static void ApplyWhiteNoiseStates(string[]? whiteNoiseStates)
        {
            if (whiteNoiseStates == null) return;

            foreach (var state in whiteNoiseStates)
            {
                if (string.IsNullOrEmpty(state)) continue;

                var parts = state.Split(',');
                if (parts.Length >= 3)
                {
                    string name = parts[0];
                    bool isEnabled = bool.Parse(parts[1]);
                    int volume = int.Parse(parts[2]);

                    var whiteNoise = WhiteNoise.WhiteNoises.FirstOrDefault(wn => wn.Name == name);
                    if (whiteNoise != null)
                    {
                        WhiteNoise.UpdatePlayerState(whiteNoise, isEnabled, volume);
                    }
                }
            }
        }
    }
}
