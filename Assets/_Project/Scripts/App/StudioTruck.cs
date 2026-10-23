using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // Lot Loop's take on Flock Five's birds halo: three trucks (coral, teal, sky) follow each other
    // nose-to-tail around the ZFX logo on one clean elliptical loop with a sparkly tyre trail,
    // then park in a row under the logo, turn to face the player, blink, and give a soft two-tap
    // "beep beep" with flashing headlights right as the jingle ends (3.0 s).
    // Smooth by construction: the lap is sampled once into an arc-length table, so speed along the
    // path is even (trapezoid speed profile: ease out of the start, ease into the spot); headings
    // come from the path tangent and are eased with an exponential (frame-rate independent) filter;
    // the 16 heading sprites are blended by rotating the residual angle, so turning never snaps.
    // Pure view, existing sprites only, no input.
    public sealed class StudioTruck : MonoBehaviour
    {
        const float LapStart = 0.10f, LapEnd = 2.30f, FaceDur = 0.26f, BlinkAt = 2.62f, BeepAt = 3.00f, TapGap = 0.19f;
        const float Accel = 0.30f, Decel = 0.45f;   // seconds of ease-in / ease-out in the speed profile
        const float TurnRate = 16f;                  // 1/s, heading ease
        static readonly CarColor[] Colors = { CarColor.Coral, CarColor.Teal, CarColor.Sky };
        const int N = 3, TrailN = 30, Samples = 480;

        sealed class Truck
        {
            public RectTransform Rt; public Image Img; public Image LampL, LampR;
            public CarColor Color; public float Heading; public bool HeadingSet;
        }

        RectTransform _logo, _root;
        readonly Truck[] _trucks = new Truck[N];
        readonly RectTransform[] _trail = new RectTransform[TrailN];
        readonly Image[] _trailImg = new Image[TrailN];
        readonly float[] _age = new float[TrailN];
        readonly Vector2[] _pts = new Vector2[Samples + 1];
        readonly float[] _len = new float[Samples + 1];
        float _total, _lw, _lh, _gap;
        int _next;
        float _t, _drop;
        bool _beeped;
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
            _root = root;
            var spark = UiArt.Get("sparkle") ?? SpriteKit.Round;
            for (int i = 0; i < TrailN; i++)
            {
                _trail[i] = Node("TyreGlitter", root, spark, new Color(1f, 0.85f, 0.4f, 0f), out _trailImg[i]);
                _age[i] = 99f;
            }
            var glow = UiArt.Get("glow") ?? SpriteKit.Round;
            // Last truck first so the leader draws on top.
            for (int i = N - 1; i >= 0; i--)
            {
                var k = new Truck { Color = Colors[i] };
                k.Rt = Node("Truck" + i, root, CarFrames.Heading(k.Color, 4), Color.white, out k.Img);
                k.Img.preserveAspect = true;
                Node("LampL", k.Rt, glow, new Color(1f, 0.95f, 0.7f, 0f), out k.LampL);
                Node("LampR", k.Rt, glow, new Color(1f, 0.95f, 0.7f, 0f), out k.LampR);
                k.Rt.localScale = Vector3.zero;
                _trucks[i] = k;
            }
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.clip = Resources.Load<AudioClip>("Audio/beep_beep");
        }

        // Logo-centred lap: one clean ellipse just outside the logo (constant shape, no bump or
        // mini-loop at the top), closed and tangent-continuous where it meets the parking straight.
        Vector2 Lap(float u)
        {
            float rx = _lw * 0.60f, ry = _lh * 0.5f + _lw * 0.15f;
            float th = Mathf.PI * 2f * u;
            return new Vector2(rx * Mathf.Sin(th), -ry * Mathf.Cos(th) - _lh * 0.02f);
        }

        void BuildPath()
        {
            _pts[0] = Lap(0f); _len[0] = 0f;
            for (int i = 1; i <= Samples; i++)
            {
                _pts[i] = Lap(i / (float)Samples);
                _len[i] = _len[i - 1] + (_pts[i] - _pts[i - 1]).magnitude;
            }
            _total = _len[Samples];
        }

        // Position at arc length s. Before 0 / after the lap the path continues straight along
        // the bottom tangent (east), so trucks can queue in and park in a row.
        Vector2 At(float s)
        {
            var east = new Vector2(1f, 0f);
            if (s <= 0f) return _pts[0] + east * s;
            if (s >= _total) return _pts[Samples] + east * (s - _total);
            int lo = 0, hi = Samples;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (_len[m] <= s) lo = m; else hi = m; }
            float seg = _len[hi] - _len[lo];
            float f = seg > 1e-4f ? (s - _len[lo]) / seg : 0f;
            return Vector2.Lerp(_pts[lo], _pts[hi], f);
        }

        Vector2 Tangent(float s)
        {
            float h = Mathf.Max(2f, _total * 0.004f);
            var v = At(s + h) - At(s - h);
            return v.sqrMagnitude > 1e-6f ? v.normalized : new Vector2(1f, 0f);
        }

        // Leader distance: trapezoid speed profile (constant speed in the middle).
        float Leader(float t, float dist)
        {
            float T = LapEnd - LapStart;
            float x = Mathf.Clamp(t - LapStart, 0f, T);
            float vmax = dist / (T - 0.5f * Accel - 0.5f * Decel);
            if (x < Accel) return 0.5f * vmax * x * x / Accel;
            float sA = 0.5f * vmax * Accel;
            if (x < T - Decel) return sA + vmax * (x - Accel);
            float y = T - x;
            return dist - 0.5f * vmax * y * y / Decel;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _t += dt;
            float lw = _logo.sizeDelta.x, lh = _logo.sizeDelta.y;
            if (lw != _lw || lh != _lh) { _lw = lw; _lh = lh; BuildPath(); }
            float size = lw * 0.22f;
            _gap = size * 0.70f;
            // Leader ends one gap past the lap start, so the row is centred under the logo.
            float dist = _total + _gap;
            float lead = Leader(_t, dist);
            float face = Mathf.Clamp01((_t - LapEnd) / FaceDur);
            face = face * face * (3f - 2f * face);
            float pop = Mathf.Clamp01((_t - LapStart) / 0.18f);
            pop = 1f - (1f - pop) * (1f - pop);
            float ease = 1f - Mathf.Exp(-TurnRate * dt);

            for (int i = 0; i < N; i++)
            {
                var k = _trucks[i];
                k.Rt.sizeDelta = new Vector2(size, size);
                float s = lead - i * _gap;
                var pos = At(s);
                k.Rt.anchoredPosition = pos;
                float target = CarFrames.HeadingAngle(Tangent(s));
                if (!k.HeadingSet) { k.Heading = target; k.HeadingSet = true; }
                else k.Heading = Mathf.Repeat(k.Heading + Mathf.DeltaAngle(k.Heading, target) * ease, 360f);
                float hdg = k.Heading;
                if (face > 0f) hdg = Mathf.Repeat(hdg + Mathf.DeltaAngle(hdg, 0f) * face, 360f);

                if (_t < BlinkAt - 0.12f || face < 1f)
                {
                    int index = Mathf.RoundToInt(hdg / 22.5f) % 16;
                    var sp = CarFrames.Heading(k.Color, index);
                    if (sp != null) k.Img.sprite = sp;
                    float residual = Mathf.DeltaAngle(index * 22.5f, hdg);
                    k.Rt.localRotation = Quaternion.Euler(0f, 0f, residual);
                    k.Rt.localScale = new Vector3(pop, pop, 1f);
                }
                else
                {
                    // Parked, facing the player: tiny settle, then a staggered blink.
                    k.Rt.localRotation = Quaternion.identity;
                    float st = _t - (LapEnd + FaceDur);
                    float sq = 1f + 0.05f * Mathf.Sin(st * 18f) * Mathf.Exp(-st * 7f);
                    k.Rt.localScale = new Vector3(sq, 2f - sq, 1f);
                    int frame = 4;
                    float b = _t - (BlinkAt + i * 0.07f);
                    if (b >= 0f && b < 0.16f) frame = b < 0.04f ? 2 : b < 0.10f ? 0 : 1;
                    k.Img.sprite = CarFrames.Eye(k.Color, frame) ?? CarFrames.Heading(k.Color, 0);
                }
                float lamp = 0f;
                float f = _t - BeepAt;
                if (f >= 0f) lamp = Mathf.Max(Flash(f), Flash(f - TapGap));
                Lamp(k.LampL, -0.29f, size, lamp);
                Lamp(k.LampR, 0.29f, size, lamp);
            }

            // Tyre glitter behind the last truck, denser while the gold sparkle is bright.
            float tail = lead - (N - 1) * _gap;
            if (_t > LapStart + 0.08f && _t < LapEnd && _t >= _drop && tail > 0f)
            {
                _drop = _t + Mathf.Lerp(0.06f, 0.03f, Mathf.Clamp01(StudioSync.Bright(_t)));
                var g = _trail[_next]; _age[_next] = 0f; _next = (_next + 1) % TrailN;
                g.anchoredPosition = At(tail) - Tangent(tail) * size * 0.32f + Random.insideUnitCircle * size * 0.08f;
                g.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            }
            for (int i = 0; i < TrailN; i++)
            {
                _age[i] += dt;
                float a = Mathf.Clamp01(_age[i] / 0.5f);
                float gs = size * 0.22f * (1f - a * 0.6f);
                _trail[i].sizeDelta = new Vector2(gs, gs);
                _trailImg[i].color = new Color(1f, 0.85f, 0.4f, (1f - a) * 0.85f);
            }
            if (!_beeped && _t >= BeepAt)
            {
                _beeped = true;
                if (_audio.clip != null && !AudioManager.Muted) _audio.Play();
            }
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
