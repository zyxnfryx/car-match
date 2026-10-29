using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // The Flock Five glove (docs/glove-standard.md), one helper for every Lot Loop lesson.
    // Fingertip pixel sits on the target (pivot = tip); the body trails away from it.
    // Tap cycle hover 0.56 / press 0.26 / lift 0.22 / pause 0.16 s; press widens 7% and squashes 12%
    // with the curled index pose; idle bob along the finger axis sin(t*2.35)*h*0.028*(1-dip);
    // fade in 0.18 s, fade out 0.16 s on a tap, back after 0.9 s if the step did not advance.
    public sealed class CoachGlove : MonoBehaviour
    {
        public const float TapHover = 0.56f, TapPress = 0.26f, TapLift = 0.22f, TapPause = 0.16f;
        public const float TapCycle = TapHover + TapPress + TapLift + TapPause;
        public const float FadeIn = 0.18f, FadeOut = 0.16f, ReturnSec = 0.9f;
        public const float HeightOfCanvas = 0.1311f, TabletScale = 0.80f;

        RawImage _img;
        RectTransform _rt;
        Vector2 _target;
        float _h, _mirror = 1f, _born, _alpha, _fadeFrom, _tapAt = -1f;
        Vector2 _axis;
        int _step = -1;

        // canvasH: the screen rect height in canvas units. Wide: 0 on phones (9:16), 1 at iPad 3:4.
        public static float HeightFor(float canvasH, float wide)
        {
            return canvasH * HeightOfCanvas * Mathf.Lerp(1f, TabletScale, Mathf.Clamp01(wide));
        }

        public static float Wide()
        {
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 9f / 16f;
            if (aspect > 1f) aspect = 1f / aspect;
            return Mathf.Clamp01((Mathf.Min(aspect, 0.75f) - 9f / 16f) / (0.75f - 9f / 16f));
        }

        // Places the glove with its fingertip on target. The body goes below the target, to the side
        // away from the screen centre (a left-half target is met from the right, mirrored with negative X).
        // flip: no room below (e.g. the booster row) so the hand comes from above, finger down.
        public static CoachGlove Make(RectTransform parent, Vector2 target, float height, Rect screen)
        {
            var go = new GameObject("Glove", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var tex = GloveArt.Rest();
            rt.pivot = new Vector2(GloveArt.TipU, GloveArt.TipV);
            rt.sizeDelta = new Vector2(height * GloveArt.Aspect, height);
            rt.anchoredPosition = target;
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            var glove = go.AddComponent<CoachGlove>();
            glove._img = img;
            glove._rt = rt;
            glove._target = target;
            glove._h = height;
            bool rightHalf = target.x > screen.center.x;
            glove._mirror = rightHalf ? -1f : 1f;
            bool flip = target.y - height * 0.95f < screen.yMin + 12f;
            float tilt = rightHalf ? -18f : 18f;
            float deg = flip ? 180f - tilt : tilt;
            rt.localRotation = Quaternion.Euler(0f, 0f, deg);
            float r = deg * Mathf.Deg2Rad;
            glove._axis = new Vector2(-Mathf.Sin(r), Mathf.Cos(r)); // finger direction (toward the tip)
            glove._born = Time.unscaledTime;
            glove.Apply(0f, 0f);
            return glove;
        }

        // A valid tap on the target: fade out now; if the lesson is still on, the hand comes back.
        public void NoteTap()
        {
            _tapAt = Time.unscaledTime;
            _fadeFrom = _alpha;
        }

        public static float Dip(float t)
        {
            float c = Mathf.Repeat(t, TapCycle);
            if (c < TapHover) return 0f;
            c -= TapHover;
            if (c < TapPress) return Smooth(c / TapPress);
            c -= TapPress;
            if (c < TapLift) return 1f - Smooth(c / TapLift);
            return 0f;
        }

        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        void Update()
        {
            float now = Time.unscaledTime;
            float alpha;
            if (_tapAt >= 0f)
            {
                float since = now - _tapAt;
                if (since < FadeOut) alpha = Mathf.Lerp(_fadeFrom, 0f, since / FadeOut);
                else if (since < FadeOut + ReturnSec) alpha = 0f;
                else { _tapAt = -1f; _born = now; alpha = 0f; }
            }
            else alpha = Mathf.Clamp01((now - _born) / FadeIn);
            float dip = _tapAt >= 0f ? 0f : Dip(now - _born);
            Apply(dip, alpha);
        }

        void Apply(float dip, float alpha)
        {
            if (_img == null || _rt == null) return;
            _alpha = alpha;
            int step = Mathf.Clamp(Mathf.RoundToInt(dip * 3f), 0, 3);
            if (step != _step)
            {
                _step = step;
                _img.texture = GloveArt.Curl(step / 3f);
            }
            _img.color = new Color(1f, 1f, 1f, alpha);
            _rt.localScale = new Vector3(_mirror * (1f + 0.07f * dip), 1f - 0.12f * dip, 1f);
            float bob = Mathf.Sin(Time.unscaledTime * 2.35f) * _h * 0.028f * (1f - dip);
            // Bob pokes along the finger axis; at rest the tip hovers just off the target.
            _rt.anchoredPosition = _target - _axis * (bob + _h * 0.03f * (1f - dip));
        }
    }

    // "Look here" (Oct 29): for things that cannot be tapped yet. A soft glow, a gently bobbing
    // arrow (~1.2 s eased hover) and a short label. Never takes a tap, never locks anything.
    public sealed class LookHint : MonoBehaviour
    {
        public Image Glow;
        public RectTransform Arrow;
        public Vector2 ArrowHome;
        public CanvasGroup Group;
        float _born;

        void Start() { _born = Time.unscaledTime; }

        void Update()
        {
            float t = Time.unscaledTime - _born;
            if (Group != null) Group.alpha = Mathf.Clamp01(t / 0.25f);
            if (Glow != null)
            {
                var c = Glow.color;
                c.a = 0.30f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 1.6f));
                Glow.color = c;
            }
            if (Arrow != null)
            {
                float u = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 1.2f);
                Arrow.anchoredPosition = ArrowHome + new Vector2(0f, 14f * u);
            }
        }

        static Texture2D _arrow;

        // Flat-top, pointed-bottom marker with an outline (Flock Five's look-at arrow shape).
        public static Texture2D ArrowTex()
        {
            if (_arrow != null) return _arrow;
            const int w = 96, h = 112;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            var fill = new Color32(255, 214, 72, 255);
            var ink = new Color32(70, 48, 20, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Dist(x + 0.5f, y + 0.5f, w, h);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (d < -6f) c = fill;
                    else if (d < 0f) c = ink;
                    else if (d < 1f) c = new Color32(ink.r, ink.g, ink.b, (byte)(255 * (1f - d)));
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _arrow = tex;
            return tex;
        }

        // Signed distance-ish: negative inside. Stem on top (y from 0.5h up), triangle pointing down below.
        static float Dist(float x, float y, int w, int h)
        {
            float cx = w * 0.5f;
            float stemHalf = w * 0.22f;
            float split = h * 0.50f;
            float stem = Mathf.Max(Mathf.Abs(x - cx) - stemHalf, Mathf.Max(split - y, y - (h - 2f)));
            // Triangle: apex at (cx, 2), base at y = split + 2, half-width w/2 - 2.
            float half = (w * 0.5f - 2f) * Mathf.Clamp01((y - 2f) / (split - 0f));
            float tri = Mathf.Max(Mathf.Abs(x - cx) - half, Mathf.Max(2f - y, y - (split + 2f)));
            if (y < 2f) tri = 2f - y + Mathf.Abs(x - cx);
            return Mathf.Min(stem, tri);
        }
    }
}
