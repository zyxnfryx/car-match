using UnityEngine;

namespace CarMatch
{
    // The one place LevelPlay keys live (Resources/AdConfig.asset). Mirrors keno's www/core/ads.config.js.
    // PLACEHOLDERS: car-match needs its own LevelPlay apps. Never paste ZFX Casino / keno keys here.
    [CreateAssetMenu(fileName = "AdConfig", menuName = "Car Match/Ad Config")]
    public sealed class AdConfig : ScriptableObject
    {
        public const string Placeholder = "REPLACE_ME";

        [Header("LevelPlay > Apps > Lot Loop (iOS, com.zfxgames.carmatch)")]
        public string IosAppKey = Placeholder;
        public string IosRewardedAdUnitId = Placeholder;
        public string IosInterstitialAdUnitId = "";

        [Header("LevelPlay > Apps > Lot Loop (Android, com.zfxgames.carmatch)")]
        public string AndroidAppKey = Placeholder;
        public string AndroidRewardedAdUnitId = Placeholder;
        public string AndroidInterstitialAdUnitId = "";

        [Tooltip("Keno's testMode: enables the LevelPlay test suite + integration validation. Set false for store builds.")]
        public bool TestMode = true;
        [Tooltip("Keno's fakeInWeb: with no SDK/keys (editor, placeholder keys) fall back to the stub ad.")]
        public bool StubWhenUnavailable = true;

        public string AppKey
        {
            get { return Application.platform == RuntimePlatform.Android ? AndroidAppKey : IosAppKey; }
        }

        public string RewardedAdUnitId
        {
            get { return Application.platform == RuntimePlatform.Android ? AndroidRewardedAdUnitId : IosRewardedAdUnitId; }
        }

        public string InterstitialAdUnitId
        {
            get { return Application.platform == RuntimePlatform.Android ? AndroidInterstitialAdUnitId : IosInterstitialAdUnitId; }
        }

        public bool HasInterstitial
        {
            get { return Usable(InterstitialAdUnitId); }
        }

        public bool HasKeys
        {
            get { return Usable(AppKey) && Usable(RewardedAdUnitId); }
        }

        static bool Usable(string s) { return !string.IsNullOrEmpty(s) && s != Placeholder; }

        public static AdConfig Load()
        {
            var c = Resources.Load<AdConfig>("AdConfig");
            return c != null ? c : CreateInstance<AdConfig>();
        }
    }
}
