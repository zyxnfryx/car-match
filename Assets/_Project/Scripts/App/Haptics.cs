using System.Runtime.InteropServices;
using UnityEngine;

namespace CarMatch
{
    // Light UIImpactFeedbackGenerator taps on iOS (Plugins/iOS/LotLoopHaptics.mm). Rate-limited like
    // Flock Five's Haptics so a burst of taps does not buzz. Silent everywhere else.
    public static class Haptics
    {
        public const string PrefKey = "lotloop.haptics";
        const float Gap = 0.06f;
        static float _next;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LotLoopHaptic(int style);
#else
        static void LotLoopHaptic(int style) { }
#endif

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) != 0;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Tap() { Play(0); }
        public static void Thud() { Play(1); }
        public static void Success() { Play(2); }

        static void Play(int style)
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + Gap;
            try { LotLoopHaptic(style); }
            catch (System.Exception) { }
        }
    }
}
