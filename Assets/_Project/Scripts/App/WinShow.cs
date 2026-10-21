using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // Level-clear celebration. Effects ported from Flock Five's FinaleShow/Wow (burst rays + flash,
    // popcorn letter-style pop with settle, breathe/pulse hold) and the coin flight + count-up from
    // FlockFiveApp.DrawCoinFly (stagger 0.045 s, 0.6 s flight, half-smoothstep/half-ease-out arc into
    // a piggy bank). Art is Lot Loop's own (render_ui.py).
    public static class WinShow
    {
        public struct Setup
        {
            public int Pay;
            public int CoinsBefore;
            public bool More;
            public Sprite Mascot;
            public Font Font;
            public Func<Vector2, Vector2, string, int, string, UnityEngine.Events.UnityAction, Button> MakeButton;
            public UnityEngine.Events.UnityAction OnNext;
            public UnityEngine.Events.UnityAction OnHome;
            public Func<Text> Hud;
            public UiShake Shake;
        }

        public const float CoinFlyStaggerSec = 0.09f;
        public const float CoinFlyDurSec = 0.60f;
        public const int CoinFlyMax = 10;

        // Same curve as Flock Five's CoinFlyEase: leaves fast, settles into the pig.
        public static float CoinFlyEase(float u)
        {
            u = Mathf.Clamp01(u);
            float smooth = u * u * (3f - 2f * u);
            float outQuad = 1f - (1f - u) * (1f - u);
            return 0.5f * (smooth + outQuad);
        }

        static RectTransform Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        static Text Txt(Transform parent, string value, Font font, int size, Vector2 pos, Vector2 box, Color color, Color outline)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.text = value;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(size / 16f, -size / 16f);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.4f);
            sh.effectDistance = new Vector2(0f, -size / 10f);
            return t;
        }

        public static void Build(MonoBehaviour host, Transform screen, Setup s)
        {
            var coinText = s.Hud != null ? s.Hud() : null;
            if (coinText != null) coinText.text = s.CoinsBefore.ToString();
            var dim = Img(screen, "Dim", null, Vector2.zero, new Vector2(3000f, 4000f), new Color(0.06f, 0.08f, 0.22f, 0.55f));
            dim.SetAsFirstSibling();
            dim.SetSiblingIndex(2);
            var rays = Img(screen, "Rays", UiArt.Get("rays"), new Vector2(0f, 260f), new Vector2(1500f, 1500f), new Color(1f, 1f, 1f, 0f));
            var glow = Img(screen, "Glow", UiArt.Get("glow"), new Vector2(0f, 260f), new Vector2(700f, 700f), new Color(1f, 0.9f, 0.5f, 0f));
            var mascot = Img(screen, "Mascot", s.Mascot, new Vector2(0f, 250f), new Vector2(460f, 460f), Color.white);
            mascot.localScale = Vector3.zero;
            var ribbon = Img(screen, "Ribbon", UiArt.Get("ribbon"), new Vector2(0f, 590f), new Vector2(1040f, 270f), Color.white);
            var title = Img(screen, "Title", UiArt.Get("title_win"), new Vector2(0f, 640f), new Vector2(980f, 287f), Color.white);
            if (UiArt.Get("title_win") == null)
                Txt(title, "PERFECT!", s.Font, 140, Vector2.zero, new Vector2(980f, 200f), new Color(1f, 0.85f, 0.3f), new Color(0.4f, 0.1f, 0.1f));
            ribbon.localScale = Vector3.zero;
            title.localScale = Vector3.zero;
            var piggy = Img(screen, "Piggy", UiArt.Get("piggy"), new Vector2(0f, -190f), new Vector2(360f, 360f), Color.white);
            // Reward plate: coin icon + big counting '+N'.
            var plate = Img(screen, "Plate", UiArt.Get("pill"), new Vector2(0f, -420f), new Vector2(420f, 136f), Color.white);
            plate.GetComponent<Image>().preserveAspect = false;
            Img(plate, "Coin", UiArt.Get("icon_coin") ?? SpriteKit.Coin, new Vector2(-130f, 4f), new Vector2(110f, 110f), Color.white);
            var plus = Txt(plate, "+0", s.Font, 96, new Vector2(40f, 6f), new Vector2(300f, 120f), new Color(1f, 0.88f, 0.32f), new Color(0.35f, 0.18f, 0.02f));
            plate.gameObject.SetActive(false); // shown when the coin count-up starts (no '+0' during confetti)
            Button next = null;
            if (s.More)
            {
                next = s.MakeButton(new Vector2(0f, -650f), new Vector2(780f, 270f), "Next", 96, /* thumb-sized: >=64pt tall on a 390pt-wide phone */ "btn_green", s.OnNext);
                next.gameObject.AddComponent<Pulse>();
            }
            else
                Txt(screen, "You cleared every lot!", s.Font, 52, new Vector2(0f, -600f), new Vector2(900f, 80f), Color.white, new Color(0.1f, 0.1f, 0.3f));
            var home = s.MakeButton(new Vector2(0f, s.More ? -890f : -800f), new Vector2(470f, 170f), "Home", 64, "btn_blue", s.OnHome);
            if (next != null) next.gameObject.SetActive(false);
            home.gameObject.SetActive(false);
            host.StartCoroutine(Run(host, screen, s, rays, glow, mascot, ribbon, title, piggy, plus, coinText, next, home));
        }

        static IEnumerator Run(MonoBehaviour host, Transform screen, Setup s, RectTransform rays, RectTransform glow,
            RectTransform mascot, RectTransform ribbon, RectTransform title, RectTransform piggy, Text plus, Text coinText,
            Button next, Button home)
        {
            // Every win opens with the LOT LOOP car finale (tap anywhere to skip), then the fanfare and coins.
            rays.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);
            yield return CarFinale(screen);
            rays.gameObject.SetActive(true);
            glow.gameObject.SetActive(true);
            AudioManager.Play(Sfx.Win);
#if !UNITY_WEBGL
            Haptics.Success();
#endif
            host.StartCoroutine(Spin(rays, glow));
            host.StartCoroutine(Confetti(screen));
            host.StartCoroutine(Fireworks(screen));
            host.StartCoroutine(Pop(ribbon, 0.0f, 0.38f));
            host.StartCoroutine(Pop(title, 0.12f, 0.42f));
            yield return Pop(mascot, 0.18f, 0.40f);
            AudioManager.Play(Sfx.MascotCheer);
            mascot.gameObject.AddComponent<Bob>().Height = 22f;
            if (s.Shake != null) s.Shake.Punch(0.3f, 10f, 0.8f);
            // Coins pop out of the mascot, arc, and fly into the piggy's slot.
            int n = Mathf.Clamp(s.Pay, 1, CoinFlyMax);
            int shown = 0;
            int landed = 0;
            plus.transform.parent.gameObject.SetActive(true);
            host.StartCoroutine(Pop((RectTransform)plus.transform.parent, 0f, 0.25f));
            for (int i = 0; i < n; i++)
            {
                int worth = s.Pay / n + (i < s.Pay % n ? 1 : 0);
                host.StartCoroutine(FlyCoin(screen, i * CoinFlyStaggerSec, mascot.anchoredPosition + new Vector2(0f, -40f),
                    piggy.anchoredPosition + new Vector2(0f, 120f), () =>
                    {
                        landed++;
                        shown += worth;
                        plus.text = "+" + shown;
                        if (coinText != null) coinText.text = (s.CoinsBefore + shown).ToString();
                        AudioManager.Play(Sfx.Coin);
#if !UNITY_WEBGL
                        Haptics.Tap();
#endif
                        host.StartCoroutine(Wobble(piggy));
                        host.StartCoroutine(Punch(plus.rectTransform, 1.25f));
                    }));
            }
            float wait = (n - 1) * CoinFlyStaggerSec + CoinFlyDurSec + 0.15f;
            yield return new WaitForSecondsRealtime(wait);
            plus.text = "+" + s.Pay;
            if (coinText != null) coinText.text = (s.CoinsBefore + s.Pay).ToString();
            if (next != null) { next.gameObject.SetActive(true); host.StartCoroutine(Pop((RectTransform)next.transform, 0f, 0.3f)); }
            yield return new WaitForSecondsRealtime(0.12f);
            home.gameObject.SetActive(true);
            yield return Pop((RectTransform)home.transform, 0f, 0.3f);
        }

        // Finale after the last level: painted cars drive in and park to spell LOT LOOP (3x5 dot font),
        // then hop in a wave. Uses the car sprites only, so the art stays Lot Loop's own.
        static readonly string[] Glyph =
        {
            "L:100100100100111", "O:111101101101111", "T:111010010010010", "P:111101111100100"
        };

        static string Rows(char c)
        {
            for (int i = 0; i < Glyph.Length; i++) if (Glyph[i][0] == c) return Glyph[i].Substring(2);
            return "000000000000000";
        }

        static bool _skip;

        static IEnumerator CarFinale(Transform screen)
        {
            _skip = false;
            // Full-screen invisible catcher: a tap skips to the fanfare.
            var catcher = Img(screen, "SkipFinale", null, Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, 0.001f));
            catcher.GetComponent<Image>().raycastTarget = true;
            catcher.gameObject.AddComponent<Button>().onClick.AddListener(() => _skip = true);
            // Fit the letters and the drive-in lanes inside the visible canvas (390x844 and taller included).
            float halfW = 490f;
            var srt = screen as RectTransform;
            if (srt != null && srt.rect.width > 10f) halfW = srt.rect.width * 0.5f;
            float cell = Mathf.Min(40f, (halfW * 2f * 0.86f) / (16f * 1.0f));
            string[] words = { "LOT", "LOOP" };
            var cars = new System.Collections.Generic.List<RectTransform>();
            var homes = new System.Collections.Generic.List<Vector2>();
            var from = new System.Collections.Generic.List<Vector2>();
            int colorIndex = 0;
            for (int w = 0; w < words.Length; w++)
            {
                string word = words[w];
                float width = word.Length * 4 * cell - cell;
                float y0 = 420f - w * 6.2f * cell;
                for (int ch = 0; ch < word.Length; ch++)
                {
                    string g = Rows(word[ch]);
                    for (int r = 0; r < 5; r++)
                        for (int c = 0; c < 3; c++)
                        {
                            if (g[r * 3 + c] != '1') continue;
                            var color = (CarColor)(colorIndex++ % 8);
                            var home = new Vector2(-width * 0.5f + (ch * 4 + c) * cell + cell * 0.5f, y0 - r * cell);
                            // Drive in from the side nearest the spot, facing the way they travel (E or W).
                            bool left = home.x < 0f;
                            float edge = halfW - cell * 0.9f;
                            var start = new Vector2(left ? -edge : edge, home.y);
                            var rt = Img(screen, "FinaleCar", CarFrames.Heading(color, left ? 4 : 12) ?? CarArt.For(color, true), start, new Vector2(cell * 1.3f, cell * 1.3f), Color.white);
                            cars.Add(rt);
                            homes.Add(home);
                            from.Add(start);
                        }
                }
            }
            catcher.SetAsLastSibling();
            // 1) Drive in and park (~1.0 s, staggered).
            float t = 0f;
            const float drive = 0.45f, stagger = 0.012f;
            float parkEnd = drive + stagger * cars.Count;
            while (t < parkEnd && !_skip)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < cars.Count; i++)
                {
                    float u = Mathf.Clamp01((t - i * stagger) / drive);
                    float k = 1f - (1f - u) * (1f - u) * (1f - u);
                    cars[i].anchoredPosition = Vector2.Lerp(from[i], homes[i], k);
                    // Pop in at the safe edge instead of sliding in from off-canvas.
                    float pop = Mathf.Clamp01(u / 0.2f);
                    cars[i].localScale = new Vector3(pop, pop, 1f);
                }
                yield return null;
            }
            // Parked: everyone turns to face the player.
            for (int i = 0; i < cars.Count; i++)
            {
                cars[i].anchoredPosition = homes[i];
                cars[i].localScale = Vector3.one;
                var img = cars[i].GetComponent<Image>();
                var sprite = CarFrames.Heading((CarColor)(i % 8), 0);
                if (sprite != null) img.sprite = sprite;
            }
            // 2) Honk in unison and bounce (~0.6 s).
            if (!_skip)
            {
                AudioManager.Play(Sfx.Blocked, 0.9f);
#if !UNITY_WEBGL
                Haptics.Thud();
#endif
                t = 0f;
                while (t < 0.6f && !_skip)
                {
                    t += Time.unscaledDeltaTime;
                    float u = t / 0.6f;
                    float hop = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * 18f * (1f - u);
                    float sq = 1f + 0.12f * Mathf.Sin(u * Mathf.PI * 4f) * (1f - u);
                    for (int i = 0; i < cars.Count; i++)
                    {
                        cars[i].anchoredPosition = homes[i] + new Vector2(0f, hop);
                        cars[i].localScale = new Vector3(sq, 2f - sq, 1f);
                    }
                    yield return null;
                }
            }
            // 3) Clear the stage quickly so the fanfare, banner and coins own the screen.
            t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.2f);
                for (int i = 0; i < cars.Count; i++)
                {
                    cars[i].localScale = Vector3.one * (1f - u);
                }
                yield return null;
            }
            for (int i = 0; i < cars.Count; i++) UnityEngine.Object.Destroy(cars[i].gameObject);
            UnityEngine.Object.Destroy(catcher.gameObject);
        }

        // Popcorn: 0 -> 1.18 -> 0.94 -> 1 (Flock Five's letter pop + Settle).
        static IEnumerator Pop(RectTransform rt, float delay, float dur)
        {
            if (rt == null) yield break;
            rt.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float k = u < 0.6f ? Mathf.Lerp(0f, 1.18f, EaseOutCubic(u / 0.6f))
                    : u < 0.85f ? Mathf.Lerp(1.18f, 0.94f, (u - 0.6f) / 0.25f)
                    : Mathf.Lerp(0.94f, 1f, (u - 0.85f) / 0.15f);
                rt.localScale = new Vector3(k, k, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        static float EaseOutCubic(float u) { u = 1f - Mathf.Clamp01(u); return 1f - u * u * u; }

        static IEnumerator Spin(RectTransform rays, RectTransform glow)
        {
            var ri = rays.GetComponent<Image>();
            var gi = glow.GetComponent<Image>();
            float t = 0f;
            while (rays != null)
            {
                t += Time.unscaledDeltaTime;
                rays.localRotation = Quaternion.Euler(0f, 0f, -t * 14f);
                float a = Mathf.Clamp01(t / 0.4f);
                // Flash then breathe.
                float flash = t < 0.5f ? Mathf.Sin(t / 0.5f * Mathf.PI) * 0.6f : 0f;
                ri.color = new Color(1f, 1f, 1f, a * (0.75f + 0.1f * Mathf.Sin(t * 2f)));
                gi.color = new Color(1f, 0.92f, 0.55f, a * 0.7f + flash);
                float g = 1f + 0.05f * Mathf.Sin(t * 2.6f) + flash * 0.6f;
                glow.localScale = new Vector3(g, g, 1f);
                yield return null;
            }
        }

        static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.36f, 0.42f), new Color(1f, 0.82f, 0.25f), new Color(0.35f, 0.82f, 0.45f),
            new Color(0.30f, 0.62f, 1f), new Color(0.75f, 0.45f, 1f), new Color(0.2f, 0.85f, 0.82f)
        };

        static IEnumerator Confetti(Transform screen)
        {
            var art = UiArt.Get("confetti");
            const int count = 70;
            var bits = new RectTransform[count];
            var vel = new Vector2[count];
            var spin = new float[count];
            var flip = new float[count];
            for (int i = 0; i < count; i++)
            {
                bits[i] = Img(screen, "Confetti", art, new Vector2(UnityEngine.Random.Range(-200f, 200f), 380f), new Vector2(22f, 40f), ConfettiColors[i % ConfettiColors.Length]);
                bits[i].GetComponent<Image>().preserveAspect = false;
                float ang = UnityEngine.Random.Range(35f, 145f) * Mathf.Deg2Rad;
                float sp = UnityEngine.Random.Range(900f, 1700f);
                vel[i] = new Vector2(Mathf.Cos(ang) * sp, Mathf.Sin(ang) * sp);
                spin[i] = UnityEngine.Random.Range(-540f, 540f);
                flip[i] = UnityEngine.Random.Range(4f, 10f);
            }
            float wall = 470f;
            var srt = screen as RectTransform;
            if (srt != null && srt.rect.width > 10f) wall = srt.rect.width * 0.5f - 30f;
            float t = 0f;
            while (t < 3.2f)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                t += dt;
                for (int i = 0; i < count; i++)
                {
                    if (bits[i] == null) yield break;
                    vel[i].y -= 1900f * dt;
                    vel[i] *= 1f - 1.6f * dt;
                    if (vel[i].y < -320f) vel[i].y = -320f;
                    bits[i].anchoredPosition += vel[i] * dt;
                    // Bounce off the safe side edges so nothing clips at the canvas border.
                    var bp = bits[i].anchoredPosition;
                    if (Mathf.Abs(bp.x) > wall) { bp.x = Mathf.Sign(bp.x) * wall; vel[i].x = -vel[i].x * 0.5f; bits[i].anchoredPosition = bp; }
                    bits[i].localRotation = Quaternion.Euler(0f, 0f, spin[i] * t);
                    bits[i].localScale = new Vector3(Mathf.Sin(t * flip[i]), 1f, 1f);
                    if (t > 2.4f)
                    {
                        var img = bits[i].GetComponent<Image>();
                        var c = img.color; c.a = Mathf.Clamp01((3.2f - t) / 0.8f); img.color = c;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < count; i++) if (bits[i] != null) UnityEngine.Object.Destroy(bits[i].gameObject);
        }

        // A few firework bursts inside the safe area while the PERFECT! banner pops.
        static IEnumerator Fireworks(Transform screen)
        {
            float halfW = 470f;
            var srt = screen as RectTransform;
            if (srt != null && srt.rect.width > 10f) halfW = srt.rect.width * 0.5f - 80f;
            Color[] cols = { new Color(1f, 0.85f, 0.25f), new Color(0.35f, 0.8f, 1f), new Color(1f, 0.4f, 0.55f), new Color(0.5f, 1f, 0.5f) };
            for (int b = 0; b < 5; b++)
            {
                var center = new Vector2(UnityEngine.Random.Range(-halfW, halfW), UnityEngine.Random.Range(380f, 820f));
                var col = cols[b % cols.Length];
                AudioManager.Play(Sfx.SlotArrive, 0.6f);
                for (int i = 0; i < 18; i++)
                {
                    var spark = Img(screen, "Firework", UiArt.Get("glow") ?? SpriteKit.Round, center, new Vector2(30f, 30f), col);
                    spark.GetComponent<Image>().raycastTarget = false;
                    var puff = spark.gameObject.AddComponent<Puff>();
                    float a = i / 18f * Mathf.PI * 2f;
                    puff.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(280f, 420f);
                    puff.Life = 0.8f;
                    puff.Grow = 0.3f;
                }
                yield return new WaitForSecondsRealtime(0.35f);
            }
        }

        static IEnumerator FlyCoin(Transform screen, float delay, Vector2 from, Vector2 to, Action landed)
        {
            yield return new WaitForSecondsRealtime(delay);
            var coin = Img(screen, "Coin", UiArt.Get("coin_big") ?? SpriteKit.Coin, from, new Vector2(110f, 110f), Color.white);
            // Pop out: a little burst sideways before the arc.
            var start = from + new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(40f, 140f));
            float spin = UnityEngine.Random.Range(540f, 900f);
            float t = 0f;
            while (t < CoinFlyDurSec && coin != null)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float u = Mathf.Clamp01(t / CoinFlyDurSec);
                float k = CoinFlyEase(u);
                var p = Vector2.Lerp(start, to, k);
                p.y += Mathf.Sin(u * Mathf.PI) * 220f;
                if (u < 0.15f) p = Vector2.Lerp(from, p, u / 0.15f);
                coin.anchoredPosition = p;
                float sc = Mathf.Lerp(1.2f, 0.55f, k);
                float sx = Mathf.Cos(spin * k * Mathf.Deg2Rad);
                if (Mathf.Abs(sx) < 0.12f) sx = 0.12f * Mathf.Sign(sx == 0f ? 1f : sx);
                coin.localScale = new Vector3(sc * sx, sc, 1f);
                yield return null;
            }
            if (coin != null) UnityEngine.Object.Destroy(coin.gameObject);
            landed();
        }

        static IEnumerator Wobble(RectTransform pig)
        {
            float t = 0f;
            while (t < 0.35f && pig != null)
            {
                t += Time.unscaledDeltaTime;
                float u = t / 0.35f;
                float w = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
                pig.localScale = new Vector3(1f + 0.12f * w, 1f - 0.12f * w, 1f);
                pig.localRotation = Quaternion.Euler(0f, 0f, w * 5f);
                yield return null;
            }
            if (pig != null) { pig.localScale = Vector3.one; pig.localRotation = Quaternion.identity; }
        }

        static IEnumerator Punch(RectTransform rt, float peak)
        {
            float t = 0f;
            while (t < 0.18f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Lerp(peak, 1f, t / 0.18f);
                rt.localScale = new Vector3(k, k, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }
    }
}
