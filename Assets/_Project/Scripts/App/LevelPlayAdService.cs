using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace CarMatch
{
    // Unity LevelPlay rewarded ads, same flow as ZFX Casino / More More More Keno (LevelPlayRewarded.swift + ads.js):
    // 1. iOS ATT prompt before the first ad request (denied = no IDFA, non-personalized ads).
    // 2. testMode -> "is_test_suite" metadata + ValidateIntegration.
    // 3. LevelPlay.Init(appKey); create one reusable LevelPlayRewardedAd after OnInitSuccess. Init failure is retried on the next request.
    // 4. Load -> show if ready, else "no fill". Reward only from OnAdRewarded; wait 0.5 s after close for a late reward.
    // 5. Preload the next ad after close.
    // Rewarded only, like keno. No banner or interstitial.
    public sealed class LevelPlayAdService : IAdService
    {
        const float LoadTimeout = 10f;
        const float InitTimeout = 15f;

        readonly AdConfig _cfg;
        LevelPlayRewardedAd _ad;
        enum Init { None, Running, Ready, Failed }
        Init _init;
        bool _loaded, _loadFailed, _closed, _rewarded, _displayFailed;

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

        IEnumerator EnsureInit()
        {
            if (_init == Init.Ready) yield break;
            if (_init == Init.Running) { while (_init == Init.Running) yield return null; yield break; }
            _init = Init.Running;
            yield return RequestTracking();
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

        static IEnumerator RequestTracking()
        {
#if UNITY_IOS
            if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
                float t = 0f;
                while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                       ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED && t < 60f)
                { t += Time.unscaledDeltaTime; yield return null; }
            }
#endif
            yield break;
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
