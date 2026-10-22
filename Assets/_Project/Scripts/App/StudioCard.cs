using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // ZFX studio card (ported from Flock Five): warm-white cover, ZFX logo, 3 s jingle with a gold
    // sparkle tracing the Z swoosh (StudioSync). The game preloads and lays out Home underneath;
    // the card holds >= 3.0 s + 0.5 s (max 6 s), fades, then Home shows (music starts then).
    public sealed class StudioCard : MonoBehaviour
    {
        static readonly Color StudioWhite = new Color(0.992f, 0.984f, 0.965f, 1f);
        const float StudioWhiteSec = 0.15f, StudioLogoIn = 0.05f, StudioLogoSec = 0.30f;
        const float StudioMinSec = 3.0f, CardEndHoldSec = 0.5f, WarmCoverMaxSec = 6.0f, FadeSec = 0.25f;
        const int TrailN = 14;

        public static bool Active { get; private set; }

        CanvasGroup _group;
        RectTransform _logo, _star;
        Image _starImg;
        RectTransform[] _trail = new RectTransform[TrailN];
        float[] _trailAge = new float[TrailN];
        int _trailNext;
        float _t, _nextDrop;
        AudioSource _jingle;

        public static StudioCard Show(Transform host)
        {
            Active = true;
            var go = new GameObject("StudioCard");
            go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>(); // blocks input under the card (no tap-to-skip)
            var card = go.AddComponent<StudioCard>();
            card.Build();
            return card;
        }

        void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            var bg = Rect("Cover", transform, Vector2.zero, Vector2.one);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.1216f, 0.1216f, 0.1216f, 1f); // matches the Unity splash, then warms
            bgImg.raycastTarget = true;
            _logo = Rect("Logo", transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f));
            var logo = _logo.gameObject.AddComponent<Image>();
            logo.sprite = UiArt.Get("studio_zfx_logo");
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            logo.color = new Color(1f, 1f, 1f, 0f);
            var spark = UiArt.Get("sparkle") ?? SpriteKit.Round;
            for (int i = 0; i < TrailN; i++)
            {
                _trail[i] = Rect("Glitter", _logo, new Vector2(0f, 1f), new Vector2(0f, 1f));
                var im = _trail[i].gameObject.AddComponent<Image>();
                im.sprite = spark; im.raycastTarget = false; im.color = new Color(1f, 0.85f, 0.4f, 0f);
                _trailAge[i] = 99f;
            }
            _star = Rect("Star", _logo, new Vector2(0f, 1f), new Vector2(0f, 1f));
            _starImg = _star.gameObject.AddComponent<Image>();
            _starImg.sprite = spark; _starImg.raycastTarget = false; _starImg.color = new Color(1f, 0.9f, 0.5f, 0f);
            Layout();
            _bg = bgImg;
            StudioTruck.Attach((RectTransform)transform, _logo);
            _jingle = gameObject.AddComponent<AudioSource>();
            _jingle.playOnAwake = false;
            _jingle.clip = Resources.Load<AudioClip>("Audio/zfx_jingle");
            if (_jingle.clip != null && !AudioManager.Muted) _jingle.Play();
        }

        Image _bg;

        void Layout()
        {
            var crt = (RectTransform)transform;
            float w = crt.rect.width > 10f ? crt.rect.width : 1080f, h = crt.rect.height > 10f ? crt.rect.height : 1920f;
            float lw = Mathf.Min(w * 0.80f, h * 0.36f * (1024f / 492f));
            _logo.sizeDelta = new Vector2(lw, lw * 492f / 1024f);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 a0, Vector2 a1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = a0; rt.anchorMax = a1; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _t += dt;
            Layout();
            _bg.color = Color.Lerp(new Color(0.1216f, 0.1216f, 0.1216f, 1f), StudioWhite, Mathf.Clamp01(_t / StudioWhiteSec));
            var lc = Color.white; lc.a = Mathf.Clamp01((_t - StudioLogoIn) / StudioLogoSec);
            _logo.GetComponent<Image>().color = lc;
            var size = _logo.sizeDelta;
            float t = Mathf.Min(_t, StudioSync.Duration);
            if (t <= StudioSync.PopT + 0.8f)
            {
                var p = StudioSync.PathAt(StudioSync.U(t));
                var pos = new Vector2(p.x * size.x, -p.y * size.y);
                _star.anchoredPosition = pos;
                float px = StudioSync.StarNominalPx * size.x / 1600f * 4.2f;
                bool hook;
                float flash = StudioSync.Flash(t, out hook);
                float grow;
                float burst = StudioSync.Burst(t, out grow);
                float s = px * (StudioSync.Scale(t) + flash * 0.5f) * (1f + burst * grow * 2.5f);
                _star.sizeDelta = new Vector2(s, s);
                _star.localEulerAngles = new Vector3(0f, 0f, -StudioSync.Angle(t));
                float a = Mathf.Clamp01(StudioSync.Bright(t) + flash);
                if (t > StudioSync.FadeStart) a *= 1f - Mathf.Clamp01((t - StudioSync.FadeStart) / (StudioSync.FadeEnd - StudioSync.FadeStart));
                _starImg.color = new Color(1f, 0.88f, 0.45f, a);
                if (t < StudioSync.PopT && _t >= _nextDrop)
                {
                    _nextDrop = _t + 0.045f;
                    var g = _trail[_trailNext]; _trailAge[_trailNext] = 0f; _trailNext = (_trailNext + 1) % TrailN;
                    g.anchoredPosition = pos + Random.insideUnitCircle * px * 0.25f;
                    g.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
                }
            }
            for (int i = 0; i < TrailN; i++)
            {
                _trailAge[i] += dt;
                float u = Mathf.Clamp01(_trailAge[i] / 0.45f);
                float gs = StudioSync.StarNominalPx * size.x / 1600f * 1.6f * (1f - u);
                _trail[i].sizeDelta = new Vector2(gs, gs);
                _trail[i].GetComponent<Image>().color = new Color(1f, 0.85f, 0.4f, (1f - u) * 0.9f);
            }
        }

        // Hold until both the card's minimum and the preload are done (capped), then fade.
        public IEnumerator HoldAndFade(System.Func<bool> ready)
        {
            while (_t < WarmCoverMaxSec && !(_t >= StudioMinSec && ready())) yield return null;
            float hold = 0f;
            while (hold < CardEndHoldSec && _t < WarmCoverMaxSec + CardEndHoldSec) { hold += Time.unscaledDeltaTime; yield return null; }
            float f = 0f;
            while (f < FadeSec)
            {
                f += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(f / FadeSec);
                yield return null;
            }
            _group.blocksRaycasts = false;
            Active = false;
            Destroy(gameObject);
        }
    }
}
