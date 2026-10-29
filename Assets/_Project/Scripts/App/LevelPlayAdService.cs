using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace CarMatch
{
    // Kid-directed. No ATT prompt, no IDFA. Non-personalized ads only.
    // is_child_directed is set before LevelPlay.Init.
    public sealed class LevelPlayAdService : IAdService
    {
        const float LoadTimeout = 10f;
        const float InitTimeout = 15f;

        readonly AdConfig _cfg;
        LevelPlayRewardedAd _ad;
        LevelPlayInterstitialAd _inter;
        bool _interLoaded, _interFailed, _interClosed;
        enum Init { None, Running, Ready, Failed }
        Init _init;
        bool _loaded, _loadFailed, _closed, _rewarded, _displayFailed;

        public bool RewardedReady { get { return _ad != null && _ad.IsAdReady(); } }

        public LevelPlayAdService(AdConfig cfg) { _cfg = cfg; }

        public IEnumerator ShowRewarded(string placement, Action<bool> onDone)
        {
            bool ok = false;
            yield return EnsureInit();
            if (_init == Init.Ready)
            {
                if (!_ad.IsAdReady())
                {
                    _loaded = _loadFailed = false;
                    _ad.LoadAd();
                    float t = 0f;
                    while (!_loaded && !_loadFailed && t < LoadTimeout) { t += Time.unscaledDeltaTime; yield return null; }
                }
                if (_ad.IsAdReady())
                {
                    _closed = _rewarded = _displayFailed = false;
                    _ad.ShowAd(placement);
                    while (!_closed && !_displayFailed) yield return null;
                    if (_closed) yield return new WaitForSecondsRealtime(0.5f);
                    ok = _rewarded;
                    _ad.LoadAd(); // preload next
                }
                else Debug.Log("Lot Loop: no rewarded ad available (no fill).");
            }
            if (onDone != null) onDone(ok);
        }

        public IEnumerator ShowInterstitial(string placement, Action<bool> onDone)
        {
            bool ok = false;
            if (_cfg == null || !_cfg.HasInterstitial)
            {
                if (onDone != null) onDone(false);
                yield break;
            }
            yield return EnsureInit();
            if (_init == Init.Ready)
            {
                if (_inter == null)
                {
                    _inter = new LevelPlayInterstitialAd(_cfg.InterstitialAdUnitId);
                    _inter.OnAdLoaded += delegate { _interLoaded = true; };
                    _inter.OnAdLoadFailed += delegate (LevelPlayAdError e) { _interFailed = true; Debug.Log("Lot Loop: interstitial load failed " + e); };
                    _inter.OnAdDisplayFailed += delegate (LevelPlayAdInfo i, LevelPlayAdError e) { _interClosed = true; Debug.Log("Lot Loop: interstitial show failed " + e); };
                    _inter.OnAdClosed += delegate { _interClosed = true; };
                    _interLoaded = _interFailed = false;
                    _inter.LoadAd();
                }
                if (!_inter.IsAdReady())
                {
                    _interLoaded = _interFailed = false;
                    _inter.LoadAd();
                    float t = 0f;
                    while (!_interLoaded && !_interFailed && t < LoadTimeout) { t += Time.unscaledDeltaTime; yield return null; }
                }
                if (_inter.IsAdReady())
                {
                    _interClosed = false;
                    _inter.ShowAd(placement);
                    while (!_interClosed) yield return null;
                    ok = true;
                    _inter.LoadAd();
                }
            }
            if (onDone != null) onDone(ok);
        }

        IEnumerator EnsureInit()
        {
            if (_init == Init.Ready) yield break;
            if (_init == Init.Running) { while (_init == Init.Running) yield return null; yield break; }
            _init = Init.Running;
            // Before Init. Child-directed and non-personalized. Never request ATT.
            LevelPlay.SetMetaData("is_child_directed", "true");
            LevelPlay.SetMetaData("do_not_sell", "true");
            LevelPlay.SetMetaData("AdMob_TFCD", "true");
            LevelPlay.SetMetaData("AdMob_TFUA", "true");
            LevelPlay.SetMetaData("UnityAds_coppa", "true");
            LevelPlay.SetMetaData("AppLovin_AgeRestrictedUser", "true");
            if (_cfg.TestMode)
            {
                LevelPlay.SetMetaData("is_test_suite", "enable");
                LevelPlay.ValidateIntegration();
            }
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            LevelPlay.Init(_cfg.AppKey);
            float t = 0f;
            while (_init == Init.Running && t < InitTimeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (_init != Init.Ready) _init = Init.None; // retry next time, like keno resetting nativeReady
        }

        void OnInitSuccess(LevelPlayConfiguration config)
        {
            if (_ad == null)
            {
                _ad = new LevelPlayRewardedAd(_cfg.RewardedAdUnitId);
                _ad.OnAdLoaded += delegate { _loaded = true; };
                _ad.OnAdLoadFailed += delegate (LevelPlayAdError e) { _loadFailed = true; Debug.Log("Lot Loop: ad load failed " + e); };
                _ad.OnAdDisplayFailed += delegate (LevelPlayAdInfo i, LevelPlayAdError e) { _displayFailed = true; Debug.Log("Lot Loop: ad show failed " + e); };
                _ad.OnAdRewarded += delegate (LevelPlayAdInfo i, LevelPlayReward r) { _rewarded = true; };
                _ad.OnAdClosed += delegate { _closed = true; };
                _ad.LoadAd();
            }
            _init = Init.Ready;
        }

        void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogWarning("Lot Loop: LevelPlay init failed " + error);
            _init = Init.Failed;
        }

        public void LaunchTestSuite() { LevelPlay.LaunchTestSuite(); }
    }
}
