using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // Lot Loop's take on Flock Five's birds halo: a little truck laps the ZFX logo with a
    // loop-de-loop at the top, leaving a sparkly tyre trail paced by the gold sparkle, parks
    // centred under the logo, blinks, and gives a soft two-tap "beep beep" with flashing
    // headlights right as the jingle ends. Pure view, existing sprites only, no input.
    public sealed class StudioTruck : MonoBehaviour
    {
        const float LapStart = 0.10f, LapEnd = 2.35f, BlinkAt = 2.62f, BeepAt = 3.00f, TapGap = 0.19f;
        const CarColor Color0 = CarColor.Red;
        const int TrailN = 22;

        RectTransform _logo, _truck;
        Image _img;
        readonly RectTransform[] _trail = new RectTransform[TrailN];
        readonly Image[] _trailImg = new Image[TrailN];
        readonly float[] _age = new float[TrailN];
        Image _lampL, _lampR;
        int _next;
        float _t, _drop;
        bool _beeped;
        Vector2 _prev;
        AudioSource _audio;

        public static StudioTruck Attach(RectTransform card, RectTransform logo)
        {
            var go = new GameObject("StudioTruck", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = logo.anchorMin;
            rt.sizeDelta = Vector2.zero;
            var s = go.AddComponent<StudioTruck>();
            s._logo = logo;
            s.Build(rt);
            return s;
        }

        static RectTransform Node(string name, Transform parent, Sprite sprite, Color c, out Image img)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = c; img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        void Build(RectTransform root)
        {
            var spark = UiArt.Get("sparkle") ?? SpriteKit.Round;
            for (int i = 0; i < TrailN; i++)
            {
                _trail[i] = Node("TyreGlitter", root, spark, new Color(1f, 0.85f, 0.4f, 0f), out _trailImg[i]);
                _age[i] = 99f;
            }
            _truck = Node("Truck", root, CarFrames.Heading(Color0, 4), Color.white, out _img);
            _img.preserveAspect = true;
            var glow = UiArt.Get("glow") ?? SpriteKit.Round;
            _lampL = LampNode(glow, "LampL");
            _lampR = LampNode(glow, "LampR");
            _truck.localScale = Vector3.zero;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.clip = Resources.Load<AudioClip>("Audio/beep_beep");
        }

        Image LampNode(Sprite glow, string name)
        {
            Image img;
            Node(name, _truck, glow, new Color(1f, 0.95f, 0.7f, 0f), out img);
            return img;
        }

        // Logo-centred lap: ellipse just outside the logo, plus one vertical loop at the top.
        Vector2 Lap(float u, float lw, float lh)
        {
            float rx = lw * 0.56f, ry = lh * 0.5f + lw * 0.12f;
            float th = Mathf.PI * 2f * u;
            var p = new Vector2(rx * Mathf.Sin(th), -ry * Mathf.Cos(th));
            const float d = 0.42f; // loop spans th in [pi-d, pi+d]
            float k = Mathf.Clamp01((th - (Mathf.PI - d)) / (2f * d));
            if (k > 0f && k < 1f)
            {
                float psi = Mathf.PI * 2f * (k * k * (3f - 2f * k));
                float rl = lw * 0.075f;
                p += new Vector2(rl * Mathf.Sin(psi), rl * (1f - Mathf.Cos(psi)));
            }
            return p;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _t += dt;
            float lw = _logo.sizeDelta.x, lh = _logo.sizeDelta.y;
            float size = lw * 0.15f;
            _truck.sizeDelta = new Vector2(size, size);
            float u = Mathf.Clamp01((_t - LapStart) / (LapEnd - LapStart));
            float e = u * u * u * (u * (u * 6f - 15f) + 10f); // smootherstep: ease out of and into the spot
            var pos = Lap(e, lw, lh) + new Vector2(0f, -lh * 0.02f);
            float pop = Mathf.Clamp01((_t - LapStart) / 0.18f);
            if (u < 1f)
            {
                var v = pos - _prev;
                if (v.sqrMagnitude > 0.04f)
                {
                    int h = Mathf.RoundToInt(CarFrames.HeadingAngle(v) / 22.5f) % 16;
                    var sp = CarFrames.Heading(Color0, h);
                    if (sp != null) _img.sprite = sp;
                }
                _truck.anchoredPosition = pos;
                _truck.localScale = new Vector3(pop, pop, 1f);
                // Tyre glitter: denser while the gold sparkle is bright.
                float rate = Mathf.Lerp(0.07f, 0.03f, Mathf.Clamp01(StudioSync.Bright(_t)));
                if (_t > LapStart + 0.08f && _t >= _drop && v.sqrMagnitude > 0.04f)
                {
                    _drop = _t + rate;
                    var back = -v.normalized * size * 0.32f;
                    var g = _trail[_next]; _age[_next] = 0f; _next = (_next + 1) % TrailN;
                    g.anchoredPosition = pos + back + Random.insideUnitCircle * size * 0.08f;
                    g.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
                }
            }
            else
            {
                // Parked, facing the player: settle bounce, then blink.
                _truck.anchoredPosition = pos;
                float s = _t - LapEnd;
                float sq = 1f + 0.06f * Mathf.Sin(s * 18f) * Mathf.Exp(-s * 7f);
                _truck.localScale = new Vector3(sq, 2f - sq, 1f);
                int frame = 4;
                float b = _t - BlinkAt;
                if (b >= 0f && b < 0.16f) frame = b < 0.04f ? 2 : b < 0.10f ? 0 : 1;
                var eye = CarFrames.Eye(Color0, frame);
                _img.sprite = eye ?? CarFrames.Heading(Color0, 0);
            }
            _prev = pos;
            for (int i = 0; i < TrailN; i++)
            {
                _age[i] += dt;
                float a = Mathf.Clamp01(_age[i] / 0.5f);
                float gs = size * 0.22f * (1f - a * 0.6f);
                _trail[i].sizeDelta = new Vector2(gs, gs);
                _trailImg[i].color = new Color(1f, 0.85f, 0.4f, (1f - a) * 0.85f);
            }
            // Beep beep with headlight flashes, right at the end of the jingle.
            if (!_beeped && _t >= BeepAt)
            {
                _beeped = true;
                if (_audio.clip != null && !AudioManager.Muted) _audio.Play();
            }
            float f = _t - BeepAt;
            float lamp = 0f;
            if (f >= 0f) lamp = Mathf.Max(Flash(f), Flash(f - TapGap));
            Lamp(_lampL, -0.29f, size, lamp);
            Lamp(_lampR, 0.29f, size, lamp);
        }

        static float Flash(float x) { return x < 0f || x > 0.16f ? 0f : Mathf.Sin(x / 0.16f * Mathf.PI); }

        static void Lamp(Image img, float fx, float size, float a)
        {
            var rt = img.rectTransform;
            rt.anchoredPosition = new Vector2(fx * size, -0.08f * size);
            float s = size * (0.30f + 0.12f * a);
            rt.sizeDelta = new Vector2(s, s);
            img.color = new Color(1f, 0.95f, 0.7f, a * 0.95f);
        }
    }
}
