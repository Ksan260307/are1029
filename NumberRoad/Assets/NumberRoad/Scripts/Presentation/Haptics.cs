using System.Runtime.InteropServices;
using UnityEngine;

namespace NumberRoad.Presentation
{
    /// <summary>
    /// Short vibrations for big moments. Browsers use navigator.vibrate (Android Chrome; iOS Safari ignores it),
    /// native phones use the OS vibration only for the strongest events because it cannot be made shorter.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;
        static float _last;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void NR_Vibrate(int milliseconds);
#endif

        public static void Tap(int milliseconds = 12, bool strong = false)
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            if (now - _last < 0.08f) return;
            _last = now;
#if UNITY_WEBGL && !UNITY_EDITOR
            NR_Vibrate(milliseconds);
#elif UNITY_ANDROID || UNITY_IOS
            if (strong) Handheld.Vibrate();
#endif
        }
    }
}
