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
                    LiftBadge(center + new Vector2(size.x * 0.5f - 18f, -size.y * 0.5f + 18f), moving && run != null ? run : null, badge);
                if (run != null && run.Final && !moving && !run.Celebrated)
                {
                    run.Celebrated = true;
                    CelebrateLift(center);
                }
            }
        }

        void LiftBadge(Vector2 pos, LiftRun run, int count)
        {
            // Blue chevron plate: cars still below. Not the red side chip or the tunnel's round tag.
            var chip = Place(_screen, "LiftBadge", pos, new Vector2(78f, 64f));
            var img = chip.gameObject.AddComponent<Image>();
            img.sprite = SpriteKit.Round;
            img.color = new Color(0.20f, 0.45f, 0.72f, 1f);
            img.raycastTarget = false;
            var chev = Place(chip, "Down", new Vector2(-22f, -2f), new Vector2(22f, 18f));
            var cd = chev.gameObject.AddComponent<Image>();
            cd.sprite = SpriteKit.Round;
            cd.color = new Color(0.95f, 0.85f, 0.35f, 1f);
            cd.raycastTarget = false;
            var t = AddText(chip, count.ToString(), 30, Color.white, TextAnchor.MiddleCenter);
            t.font = ChunkyFont();
            t.rectTransform.offsetMin = new Vector2(18f, 0f);
            Outlined(t, new Color(0.05f, 0.12f, 0.22f, 1f), 2f);
            if (run != null) chip.gameObject.AddComponent<BadgeTick>().Init(run.Start, run.Before, run.After, t);
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

        void DrawCoach()
        {
            if (_board == null || _level == null) return;
            _cue = LotCoach.Cue(PrefsFlags.Shared, new CoachWorld
            {
                Level = _level.Number,
                Board = _board,
                Moves = _history.Count,
                CanShuffle = BoosterKit.CanShuffle(_board),
                CanMagnet = CanMagnet(),
                CanSuper = CanSuper()
            });
            if (_cue.Lesson == CoachLesson.None) return;
            Vector2 at;
            if (_cue.IsBooster)
            {
                var named = _screen.Find("Booster" + _cue.Booster) as RectTransform;
                if (named == null) return;
                at = named.anchoredPosition;
            }
            else if (_cue.Lesson == CoachLesson.Underground && _board.Garages.Count > 0)
            {
                var g = _board.Garages[0];
                at = LaneAnchor(g.Lane, g.Cells.Count > 0 ? g.Cells[0] : 0);
            }
            else if (_cue.HasCar && _pieceAt.TryGetValue(PieceKey(_cue.Car), out var piece) && piece != null)
                at = piece.anchoredPosition;
            else if (_cue.HasCar)
                at = _cue.Car.Kind == TapKind.Side && (uint)_cue.Car.Index < (uint)_board.Sides.Count
                    ? SideAnchor(_board.Sides[_cue.Car.Index])
                    : LaneAnchor(_cue.Car.Index, _cue.Car.Cell);
            else return;
            var screen = _screen as RectTransform;
            var rect = screen != null ? screen.rect : new Rect(-540f, -960f, 1080f, 1920f);
            UnityFreeVec center;
            float deg;
            LotCoach.PlaceGlove(new UnityFreeVec(at.x, at.y), LotCoach.GloveUnits,
                new UnityFreeVec(rect.xMin, rect.yMin), new UnityFreeVec(rect.xMax, rect.yMax), 16f, out center, out deg);
            var glove = Place(_screen, "Glove", new Vector2(center.X, center.Y), new Vector2(LotCoach.GloveUnits, LotCoach.GloveUnits));
            var img = glove.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Get("glove") ?? SpriteKit.Round;
            img.preserveAspect = true;
            img.raycastTarget = false;
            glove.localRotation = Quaternion.Euler(0f, 0f, deg);
            glove.SetAsLastSibling();
        }
    }
}
