using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    public sealed partial class CarMatchApp
    {
        sealed class LiftRun
        {
            public int Lane;
            public int[] Cells;
            public Car[] Riders;
            public int Before;
            public int After;
            public float Start;
            public bool Final;
            public bool GearPlayed;
            public bool Celebrated;
        }

        // Active elevator runs. Cleared when a level starts so a previous deck cannot lock the next lot.
        readonly List<LiftRun> _lifts = new List<LiftRun>();
        bool _liftMoving;

        void SettleLifts()
        {
            bool moving = false;
            float now = Time.unscaledTime;
            for (int i = 0; i < _lifts.Count; i++)
                if (DrivewayLift.Locked(_lifts[i].Start, now)) moving = true;
            if (_liftMoving && !moving)
            {
                _liftMoving = false;
                ShowPlay();
                return;
            }
            _liftMoving = moving;
        }

        int[] GarageOpens()
        {
            int n = _board != null ? _board.Garages.Count : 0;
            var opens = new int[n];
            for (int i = 0; i < n; i++) opens[i] = _board.Garages[i].Opens;
            return opens;
        }

        void NoteLifts(int[] before)
        {
            if (_board == null || before == null) return;
            for (int i = 0; i < _board.Garages.Count && i < before.Length; i++)
            {
                var g = _board.Garages[i];
                if (g.Opens <= before[i]) continue;
                if (_cue.Lesson == CoachLesson.Underground)
                    LotCoach.Complete(PrefsFlags.Shared, CoachLesson.Underground);
                _garageOpens[i] = g.Opens;
                var riders = new List<Car>();
                for (int c = 0; c < g.Cells.Count; c++)
                {
                    var slot = _board.Lanes[g.Lane].Cells[g.Cells[c]];
                    if (!slot.Empty && !slot.Tunnel) riders.Add(slot.Car);
                }
                int after = g.Queue.Count;
                _lifts.Add(new LiftRun
                {
                    Lane = g.Lane,
                    Cells = g.Cells.ToArray(),
                    Riders = riders.ToArray(),
                    Before = after + riders.Count,
                    After = after,
                    Start = Time.unscaledTime,
                    Final = DrivewayLift.FinalWave(after)
                });
                AudioManager.Play(Sfx.Clunk, 0.55f);
                if (_cue.Lesson == CoachLesson.Underground)
                    LotCoach.Complete(PrefsFlags.Shared, CoachLesson.Underground);
            }
        }

        bool CellOnLift(int lane, int cell)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _lifts.Count; i++)
            {
                var run = _lifts[i];
                if (run.Lane != lane || !DrivewayLift.Locked(run.Start, now)) continue;
                for (int c = 0; c < run.Cells.Length; c++)
                    if (run.Cells[c] == cell) return true;
            }
            return false;
        }

        bool TapBlockedByLift(Tap tap)
        {
            int lane = -1;
            if (tap.Kind == TapKind.Lane) lane = tap.Index;
            else if (_board != null && tap.Kind == TapKind.Side && (uint)tap.Index < (uint)_board.Sides.Count)
                lane = _board.Sides[tap.Index].Lane;
            if (lane < 0) return false;
            float now = Time.unscaledTime;
            for (int i = 0; i < _lifts.Count; i++)
                if (_lifts[i].Lane == lane && DrivewayLift.Locked(_lifts[i].Start, now))
                    return true;
            return false;
        }

        void DrawLiftBays()
        {
            if (_board == null) return;
            float now = Time.unscaledTime;
            for (int gi = 0; gi < _board.Garages.Count; gi++)
            {
                var g = _board.Garages[gi];
                if (g.Cells.Count == 0) continue;
                LiftRun run = null;
                for (int i = 0; i < _lifts.Count; i++)
                    if (_lifts[i].Lane == g.Lane && (_lifts[i].Final || DrivewayLift.Locked(_lifts[i].Start, now)))
                        run = _lifts[i];
                float y0 = float.MaxValue, y1 = float.MinValue;
                for (int i = 0; i < g.Cells.Count; i++)
                {
                    float y = LaneAnchor(g.Lane, g.Cells[i]).y;
                    y0 = Mathf.Min(y0, y);
                    y1 = Mathf.Max(y1, y);
                }
                var center = new Vector2(LaneAnchor(g.Lane, 0).x, (y0 + y1) * 0.5f);
                var size = new Vector2(_metrics.CellW - 8f, y1 - y0 + _metrics.CellH * 0.92f);
                float u = run != null ? DrivewayLift.Along(run.Start, now) : 1f;
                bool moving = run != null && DrivewayLift.Locked(run.Start, now);
                Bar("LiftShade", center + new Vector2(0f, -size.y * 0.48f), new Vector2(size.x * 0.8f, 16f), new Color(0.08f, 0.07f, 0.06f, 0.35f));
                Bar("LiftShaft", center, size, new Color(0.22f, 0.24f, 0.28f, moving || g.Queue.Count > 0 ? 0.92f : 0.35f));
                Bar("LiftRailL", center + new Vector2(-size.x * 0.42f, 0f), new Vector2(8f, size.y - 8f), new Color(0.55f, 0.58f, 0.62f, 1f));
                Bar("LiftRailR", center + new Vector2(size.x * 0.42f, 0f), new Vector2(8f, size.y - 8f), new Color(0.55f, 0.58f, 0.62f, 1f));
                float travel = size.y * 0.42f;
                float deckY = moving && run != null ? DrivewayLift.DeckOffset(u, travel) : 0f;
                var plat = center + new Vector2(0f, deckY);
                var deck = Place(_screen, "LiftDeck", plat, new Vector2(size.x * 0.78f, 18f));
                var deckImg = deck.gameObject.AddComponent<Image>();
                deckImg.sprite = SpriteKit.Round;
                deckImg.color = new Color(0.45f, 0.32f, 0.18f, 1f);
                deckImg.raycastTarget = false;
                if (moving && run != null)
                    deck.gameObject.AddComponent<DeckMotion>().Init(run.Start, travel, center);
                float spin = moving ? now * 240f : 0f;
                var gear = Place(_screen, "LiftGear", center + new Vector2(size.x * 0.5f - 8f, size.y * 0.5f - 8f), new Vector2(36f, 36f));
                var gearImg = gear.gameObject.AddComponent<Image>();
                gearImg.sprite = SpriteKit.Round;
                gearImg.color = new Color(0.72f, 0.74f, 0.78f, 1f);
                gearImg.raycastTarget = false;
                gear.localRotation = Quaternion.Euler(0f, 0f, spin);
                if (moving && run != null)
                {
                    for (int r = 0; r < run.Riders.Length; r++)
                    {
                        float x = run.Riders.Length <= 1 ? 0f : Mathf.Lerp(-size.x * 0.22f, size.x * 0.22f, r / (float)(run.Riders.Length - 1));
                        var car = Place(deck, "LiftCar", new Vector2(x, _metrics.CellH * 0.28f), _metrics.Piece * 0.72f);
                        var img = car.gameObject.AddComponent<Image>();
                        img.sprite = run.Riders[r].Hidden ? HiddenSprite() : CarArt.For(run.Riders[r].Color, false);
                        img.preserveAspect = true;
                        img.raycastTarget = false;
                        car.gameObject.AddComponent<ShowOnRise>().StartAt = run.Start;
                    }
                }
                int badge = g.Queue.Count;
                if (moving && run != null) badge = DrivewayLift.Badge(run.Before, run.After, u);
                if (badge > 0 || moving)
                    LiftLock(center, size, moving && run != null ? run : null, badge);
                if (run != null && run.Final && !moving && !run.Celebrated)
                {
                    run.Celebrated = true;
                    CelebrateLift(center);
                }
            }
        }

        // Locked lift lane (Oct 29): a faded white overlay with light diagonal stripes over the
        // lane, and a big soft painted countdown (cars still below: 3, 2, 1) that ticks as matches
        // happen and fades out as the gate opens. Never takes a tap.
        void LiftLock(Vector2 center, Vector2 size, LiftRun run, int count)
        {
            var veil = Place(_screen, "LiftVeil", center, size);
            var raw = veil.gameObject.AddComponent<RawImage>();
            raw.texture = StripeTex();
            raw.uvRect = new Rect(0f, 0f, size.x / 96f, size.y / 96f);
            raw.color = new Color(1f, 1f, 1f, 0.55f);
            raw.raycastTarget = false;
            var veilGroup = veil.gameObject.AddComponent<CanvasGroup>();
            veilGroup.blocksRaycasts = false;
            veilGroup.interactable = false;

            // PinBadges keeps the number above the parked cars (not in the outlined Badges layer).
            var holder = Place(_screen, "LiftCount", center, size);
            var holderGroup = holder.gameObject.AddComponent<CanvasGroup>();
            holderGroup.blocksRaycasts = false;
            holderGroup.interactable = false;
            int font = Mathf.RoundToInt(Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.62f, 60f, 150f));
            var t = AddText(holder, count.ToString(), font, new Color(1f, 0.99f, 0.95f, 0.88f), TextAnchor.MiddleCenter);
            t.font = ChunkyFont();
            t.raycastTarget = false;
            Outlined(t, new Color(0.30f, 0.36f, 0.48f, 0.45f), 3f);
            var tick = holder.gameObject.AddComponent<LiftCountdown>();
            tick.Init(run, t, holderGroup, veilGroup, count);
        }

        static Texture2D _stripeTex;

        static Texture2D StripeTex()
        {
            if (_stripeTex != null) return _stripeTex;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Soft diagonal band every 32 px; base wash so the lane reads as closed.
                    float d = Mathf.Repeat(x + y, 32f);
                    float band = Mathf.Clamp01(1f - Mathf.Abs(d - 8f) / 7f);
                    float a = 0.30f + 0.35f * band * band * (3f - 2f * band);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _stripeTex = tex;
            return tex;
        }

        sealed class LiftCountdown : MonoBehaviour
        {
            LiftRun _run;
            Text _text;
            CanvasGroup _number, _veil;
            int _shown;
            float _pop;

            public void Init(LiftRun run, Text text, CanvasGroup number, CanvasGroup veil, int count)
            {
                _run = run; _text = text; _number = number; _veil = veil; _shown = count;
                if (count <= 0 && run == null) { number.alpha = 0f; veil.alpha = 0f; }
            }

            void Update()
            {
                if (_text == null) return;
                if (_run == null) return;
                float u = DrivewayLift.Along(_run.Start, Time.unscaledTime);
                int n = DrivewayLift.Badge(_run.Before, _run.After, u);
                if (n != _shown) { _shown = n; _pop = 0.25f; }
                _text.text = n > 0 ? n.ToString() : (_run.Before > 0 ? "1" : "");
                if (_pop > 0f)
                {
                    _pop -= Time.unscaledDeltaTime;
                    // Soft fade-dip on the number change (no scale: Oct 29 no pulses).
                    if (_number != null) _number.alpha = 0.55f + 0.45f * (1f - Mathf.Clamp01(_pop / 0.25f));
                }
                else if (_number != null && _run.After > 0) _number.alpha = 1f;
                if (_run.After == 0)
                {
                    // Last wave: the gate opens; the countdown and the stripes fade away.
                    float f = 1f - Mathf.Clamp01((u - 0.35f) / 0.5f);
                    if (_number != null) _number.alpha = f;
                    if (_veil != null) _veil.alpha = f;
                }
            }
        }

        sealed class ShowOnRise : MonoBehaviour
        {
            public float StartAt;
            Image _img;
            void Update()
            {
                if (_img == null) _img = GetComponent<Image>();
                if (_img == null) return;
                _img.enabled = !DrivewayLift.Dropping(DrivewayLift.Along(StartAt, Time.unscaledTime));
            }
        }

        sealed class DeckMotion : MonoBehaviour
        {
            float _start, _travel;
            Vector2 _home;
            bool _gear;
            public void Init(float start, float travel, Vector2 home) { _start = start; _travel = travel; _home = home; }
            void Update()
            {
                var rt = (RectTransform)transform;
                float u = DrivewayLift.Along(_start, Time.unscaledTime);
                rt.anchoredPosition = _home + new Vector2(0f, DrivewayLift.DeckOffset(u, _travel));
                if (!_gear && !DrivewayLift.Dropping(u))
                {
                    _gear = true;
                    AudioManager.Play(Sfx.Gear, 0.4f);
                }
            }
        }

        // Kept for reference by older code paths; unused since the Oct 29 lift countdown.
        sealed class BadgeTick : MonoBehaviour
        {
            float _start;
            int _before, _after;
            Text _text;
            public void Init(float start, int before, int after, Text text)
            {
                _start = start; _before = before; _after = after; _text = text;
            }
            void Update()
            {
                if (_text == null) return;
                _text.text = DrivewayLift.Badge(_before, _after, DrivewayLift.Along(_start, Time.unscaledTime)).ToString();
            }
        }

        void CelebrateLift(Vector2 pos)
        {
            if (_fly == null) return;
            AudioManager.Play(Sfx.Chime, 0.5f);
            _fly.StartCoroutine(LiftPop(pos));
        }

        System.Collections.IEnumerator LiftPop(Vector2 pos)
        {
            var rt = Place(_flyLayer, "LiftDone", pos, new Vector2(120f, 120f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Get("sparkle") ?? SpriteKit.Round;
            img.color = new Color(1f, 0.92f, 0.45f, 1f);
            img.raycastTarget = false;
            float t = 0f;
            while (t < 0.7f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.7f);
                float s = Mathf.Lerp(0.4f, 1.3f, 1f - (1f - Mathf.Clamp01(u / 0.35f)) * (1f - Mathf.Clamp01(u / 0.35f)));
                if (u > 0.55f) s = Mathf.Lerp(s, 0.2f, (u - 0.55f) / 0.45f);
                rt.localScale = new Vector3(s, s, 1f);
                var c = img.color;
                c.a = 1f - Mathf.Clamp01((u - 0.45f) / 0.55f);
                img.color = c;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        CoachCue _cue;
        HintCue _hint;
        CoachGlove _glove;

        void DrawCoach()
        {
            _hint = default;
            _glove = null;
            if (_board == null || _level == null) return;
            var world = new CoachWorld
            {
                Level = _level.Number,
                Board = _board,
                Moves = _history.Count,
                CanShuffle = BoosterKit.CanShuffle(_board),
                CanMagnet = CanMagnet(),
                CanSuper = CanSuper()
            };
            _cue = LotCoach.Cue(PrefsFlags.Shared, world);
            var screen = _screen as RectTransform;
            var rect = screen != null ? screen.rect : new Rect(-540f, -960f, 1080f, 1920f);
            if (_cue.Lesson == CoachLesson.None)
            {
                _hint = LotCoach.Hint(PrefsFlags.Shared, world);
                if (_hint.Hint != CoachHint.None) DrawLookHint(_hint, rect);
                return;
            }
            Vector2 at;
            Vector2 hole = _metrics.Piece;
            if (_cue.IsBooster)
            {
                var named = _screen.Find("Booster" + _cue.Booster) as RectTransform;
                if (named == null) { _cue = default; return; }
                at = named.anchoredPosition;
                hole = named.sizeDelta;
            }
            else if (_cue.HasCar && _pieceAt.TryGetValue(PieceKey(_cue.Car), out var piece) && piece != null)
            {
                at = piece.anchoredPosition;
                hole = piece.sizeDelta;
            }
            else if (_cue.HasCar)
                at = _cue.Car.Kind == TapKind.Side && (uint)_cue.Car.Index < (uint)_board.Sides.Count
                    ? SideAnchor(_board.Sides[_cue.Car.Index])
                    : LaneAnchor(_cue.Car.Index, _cue.Car.Cell);
            else { _cue = default; return; }
            // Forced lesson: everything but the target is dimmed and takes no taps.
            DrawCoachDim(rect, new Rect(at - hole * 0.5f - new Vector2(14f, 14f), hole + new Vector2(28f, 28f)));
            float h = CoachGlove.HeightFor(rect.height, CoachGlove.Wide());
            _glove = CoachGlove.Make(screen, at, h, rect);
            _glove.transform.SetAsLastSibling();
        }

        // Four dim panels around the hole. They catch every tap outside the glove's target.
        void DrawCoachDim(Rect screen, Rect hole)
        {
            var layer = Place(_screen, "CoachLayer", Vector2.zero, new Vector2(8f, 8f));
            float big = 4000f;
            var dim = new Color(0.02f, 0.03f, 0.08f, 0.50f);
            void Panel(string name, float x0, float y0, float x1, float y1)
            {
                if (x1 <= x0 || y1 <= y0) return;
                var rt = Place(layer, name, new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f), new Vector2(x1 - x0, y1 - y0));
                var img = rt.gameObject.AddComponent<Image>();
                img.color = dim;
                img.raycastTarget = true;
            }
            Panel("DimBelow", -big, -big, big, hole.yMin);
            Panel("DimAbove", -big, hole.yMax, big, big);
            Panel("DimLeft", -big, hole.yMin, hole.xMin, hole.yMax);
            Panel("DimRight", hole.xMax, hole.yMin, big, hole.yMax);
        }

        // Look-here hint: soft glow on the thing, a bobbing arrow above it, a short label. No dim, no lock.
        void DrawLookHint(HintCue hint, Rect screen)
        {
            Vector2 at;
            Vector2 size = _metrics.Piece;
            if (hint.Hint == CoachHint.SideLot && (uint)hint.Spot.Index < (uint)_board.Sides.Count)
                at = SideAnchor(_board.Sides[hint.Spot.Index]);
            else if (hint.Hint == CoachHint.LiftGate && (uint)hint.Garage < (uint)_board.Garages.Count)
            {
                var g = _board.Garages[hint.Garage];
                float y0 = float.MaxValue, y1 = float.MinValue;
                for (int i = 0; i < g.Cells.Count; i++) { float y = LaneAnchor(g.Lane, g.Cells[i]).y; y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                at = new Vector2(LaneAnchor(g.Lane, 0).x, (y0 + y1) * 0.5f);
                size = new Vector2(_metrics.CellW, y1 - y0 + _metrics.CellH);
            }
            else at = LaneAnchor(hint.Spot.Index, hint.Spot.Cell);
            var root = Place(_screen, "LookHint", Vector2.zero, new Vector2(8f, 8f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            var glow = Place(root, "Glow", at, size * 1.45f);
            var gi = glow.gameObject.AddComponent<Image>();
            gi.sprite = UiArt.Get("fx_land_glow") ?? SpriteKit.Round;
            gi.color = new Color(1f, 0.93f, 0.55f, 0.45f);
            gi.raycastTarget = false;
            var arrowHome = at + new Vector2(0f, size.y * 0.5f + 70f);
            var arrow = Place(root, "Arrow", arrowHome, new Vector2(72f, 84f));
            var ai = arrow.gameObject.AddComponent<RawImage>();
            ai.texture = LookHint.ArrowTex();
            ai.raycastTarget = false;
            float labelW = Mathf.Min(760f, 26f * hint.Label.Length + 80f);
            float lx = Mathf.Clamp(at.x, screen.xMin + labelW * 0.5f + 16f, screen.xMax - labelW * 0.5f - 16f);
            var pill = Place(root, "Label", new Vector2(lx, arrowHome.y + 92f), new Vector2(labelW, 84f));
            var pi = pill.gameObject.AddComponent<Image>();
            pi.sprite = UiArt.Get("pill") ?? SpriteKit.Round;
            pi.type = Image.Type.Sliced;
            pi.color = new Color(0.12f, 0.16f, 0.30f, 0.92f);
            pi.raycastTarget = false;
            var text = AddText(pill, hint.Label, 34, Cream, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            var look = root.gameObject.AddComponent<LookHint>();
            look.Glow = gi;
            look.Arrow = arrow;
            look.ArrowHome = arrowHome;
            look.Group = group;
        }
    }
}
