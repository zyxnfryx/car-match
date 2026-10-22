using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CarMatch
{
    public sealed class CarMatchApp : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindAnyObjectByType<CarMatchApp>() != null) return;
            var go = new GameObject("CarMatchApp");
            DontDestroyOnLoad(go);
            go.AddComponent<CarMatchApp>();
        }

        enum Phase
        {
            Home,
            Play,
            Win,
            Fail
        }

        readonly List<LevelDef> _levels = new List<LevelDef>();
        readonly List<Board> _history = new List<Board>();

        IAdService _ads;
        Font _font;
        Transform _screen;
        Board _board;
        Board _seed;
        LevelDef _level;
        int _index;
        Phase _phase;
        bool _busy;
        // Cars in flight never lock input: the board changes at tap time and the view catches up.
        bool _driving => !_barView.Settled;
        readonly LegalMoves _legal = new LegalMoves();
        readonly BarReservations _barView = new BarReservations();
        readonly Dictionary<BarEntry, float> _barX = new Dictionary<BarEntry, float>();
        RectTransform _flyLayer;
        FlyHost _fly;
        RectTransform _tray;

        // Runs drives and set-pops on their own object so a board redraw (ClearScreen) never stops them.
        sealed class FlyHost : MonoBehaviour { }
        bool _settled;
        bool _failPaid;
        bool _lastMatch;
        float _tick;
        Text _coinText;
        Text _livesText;
        Button _retryButton;
        Button _playButton;
        UiShake _shake;
        readonly List<RectTransform> _barCars = new List<RectTransform>();
        readonly Dictionary<string, bool> _eyes = new Dictionary<string, bool>();
        readonly Dictionary<string, RectTransform> _pieceAt = new Dictionary<string, RectTransform>();

        static readonly Color Ink = new Color(0.15f, 0.17f, 0.22f, 1f);
        static readonly Color Cream = new Color(0.98f, 0.95f, 0.90f, 1f);
        static readonly Color Green = new Color(0.18f, 0.62f, 0.34f, 1f);
        static readonly Color Wood = new Color(0.45f, 0.26f, 0.20f, 1f);
        static readonly Color Slot = new Color(0.62f, 0.40f, 0.30f, 1f);
        static readonly Color Gold = new Color(0.96f, 0.78f, 0.28f, 1f);

        const float DriveSeconds = 0.62f;
        const float BarY = -620f;
        const float BarSlotWidth = 112f;
        const float BarGap = 10f;
        const float BarCarH = 100f;

        struct LotMetrics
        {
            public int Lanes;
            public float CellW;
            public float CellH;
            public float Gap;
            public float Left;
            public float BaseY;
            public Vector2 Piece;
            // Rounded ring road around the lot (centre line): side roads at RingL/RingR,
            // the back road at RingTop, the bar road at RingBottom, corner radius RingR0.
            public float RingL, RingRt, RingTop, RingBottom, RingR0, RoadW;
        }

        LotMetrics _metrics;

        void Start()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.14f, 0.20f, 0.36f, 1f);
            }
            SpriteKit.Warm();
            _ads = AdServices.Create();
            Purse.Boot();
            DebugCoinGrant();
            LoadCatalog();
            BuildShell();
            ShowHome();
        }

        void Update()
        {
            if (Time.unscaledTime < _tick) return;
            _tick = Time.unscaledTime + 0.5f;
            int lives = Purse.Wallet.Lives;
            Purse.Tick();
            if (_phase == Phase.Home && Purse.Wallet.Lives != lives)
            {
                ShowHome();
                return;
            }
            if (_coinText != null && _phase != Phase.Win) _coinText.text = Purse.Wallet.Coins.ToString();
            if (_livesText != null) _livesText.text = LivesLine();
            if (_phase == Phase.Fail && _retryButton != null)
                _retryButton.interactable = !_busy && Purse.Wallet.Lives > 0;
        }

        void LoadCatalog()
        {
            _levels.Clear();
            var asset = Resources.Load<TextAsset>("levels");
            if (asset == null)
            {
                Debug.LogError("Lot Loop: Resources/levels.json is missing.");
                return;
            }
            _levels.AddRange(LevelJson.Parse(asset.text));
            if (Purse.LevelIndex >= _levels.Count)
                Purse.SetLevel(_levels.Count - 1);
        }

        void BuildShell()
        {
            var canvasGo = new GameObject("UI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var events = new GameObject("Events");
            events.transform.SetParent(transform, false);
            events.AddComponent<EventSystem>();
            var input = events.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();

            var screen = new GameObject("Screen", typeof(RectTransform));
            screen.transform.SetParent(canvasGo.transform, false);
            var rt = screen.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _screen = screen.transform;
            _shake = screen.AddComponent<UiShake>();

            var fly = new GameObject("Flights", typeof(RectTransform));
            fly.transform.SetParent(canvasGo.transform, false);
            _flyLayer = fly.GetComponent<RectTransform>();
            _flyLayer.anchorMin = Vector2.zero;
            _flyLayer.anchorMax = Vector2.one;
            _flyLayer.offsetMin = Vector2.zero;
            _flyLayer.offsetMax = Vector2.zero;
            _fly = fly.AddComponent<FlyHost>();
            canvasGo.AddComponent<AspectFit>().Scaler = scaler;
        }

        void ClearScreen()
        {
            StopAllCoroutines();
            _coinText = null;
            _livesText = null;
            _retryButton = null;
            _pieceAt.Clear();
            _busy = false;
            for (int i = _screen.childCount - 1; i >= 0; i--)
                Destroy(_screen.GetChild(i).gameObject);
        }

        void ShowHome(bool celebrate = false)
        {
            _phase = Phase.Home;
            _busy = false;
            KillFlights();
            ClearScreen();
            Backdrop(true);
            Clouds();
            HomeCars();
            Hud(0f);
            AudioManager.Music(AudioManager.HomeMusic);
            // Hierarchy: HUD (safe top) > title > mascot on its light pool > tagline > Play.
            var pool = Place(_screen, "MascotPool", new Vector2(0f, -40f), new Vector2(760f, 260f));
            var poolImage = pool.gameObject.AddComponent<Image>();
            poolImage.sprite = UiArt.Get("glow") ?? SpriteKit.Round;
            poolImage.color = new Color(1f, 0.95f, 0.80f, 0.55f);
            poolImage.raycastTarget = false;
            var mascot = Place(_screen, "Mascot", new Vector2(0f, 110f), new Vector2(560f, 560f));
            var pic = mascot.gameObject.AddComponent<Image>();
            pic.sprite = PaintedMascot(celebrate ? MascotArt.Happy : MascotArt.Idle);
            pic.preserveAspect = true;
            pic.raycastTarget = false;
            if (celebrate) StartCoroutine(CheerHome(mascot, pic));
            else
            {
                mascot.gameObject.AddComponent<Bob>();
                var blink = mascot.gameObject.AddComponent<Blinker>();
                blink.Open = pic.sprite;
                blink.Closed = UiArt.Get("mascot_blink");
            }
            Logo(new Vector2(0f, 560f), new Vector2(940f, 398f));
            var tag = Label(new Vector2(0f, -250f), new Vector2(900f, 64f), "Clear the lane. Match three.", 44, Cream, TextAnchor.MiddleCenter);
            Outlined(tag, new Color(0.10f, 0.12f, 0.28f, 0.8f), 3f);
            int number = NextNumber();
            bool canPlay = _levels.Count > 0 && Purse.Wallet.Lives > 0;
            var play = MakeButton(new Vector2(0f, -470f), new Vector2(700f, 256f), Green, "Level " + number, 84, canPlay ? (UnityEngine.Events.UnityAction)OnPlay : null, "btn_green");
            _playButton = play;
            if (number >= 1 && number <= _levels.Count && _levels[number - 1].SuperHard)
                SuperHardBadge(new Vector2(0f, -320f));
            play.interactable = canPlay;
            if (canPlay)
            {
                play.gameObject.AddComponent<Pulse>();
                var mask = play.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
                var shine = UiArt.Get("shine");
                if (shine != null) play.gameObject.AddComponent<ShineSweep>().Init(shine, 640f, 150f);
            }
        }

        void OnPlay()
        {
            if (_levels.Count == 0 || Purse.Wallet.Lives <= 0) return;
            LoadLevel(Purse.LevelIndex);
            StartLevel();
        }

        void LoadLevel(int index)
        {
            if (_levels.Count == 0) return;
            if (index < 0) index = 0;
            if (index >= _levels.Count) index = _levels.Count - 1;
            _index = index;
            _level = _levels[_index];
            _board = _level.Board.Clone();
            _seed = _board.Clone();
            _history.Clear();
            _eyes.Clear();
            _settled = false;
            _failPaid = false;
            _lastMatch = false;
            _busy = false;
            KillFlights();
        }

        void KillFlights()
        {
            if (_fly != null) _fly.StopAllCoroutines();
            if (_flyLayer != null)
                for (int i = _flyLayer.childCount - 1; i >= 0; i--) Destroy(_flyLayer.GetChild(i).gameObject);
            _barView.Reset(_board != null ? _board.Bar : null);
            _barX.Clear();
        }

        void ShowPlay()
        {
            _phase = Phase.Play;
            ClearScreen();
            if (_barView.Settled && _board != null) _barView.Reset(_board.Bar);
            _legal.Rebuild(_board);
            AudioManager.Music(AudioManager.GameMusic);
            Backdrop();
            var safe = HudPlay();
            string title = _level != null ? "Level " + _level.Number : "Level";
            LevelWordArt(title);
            DrawBoard();
            DrawBar();
            DrawBoosters();
            if (_level != null && _level.SuperHard) SuperHardBadge(new Vector2(0f, 650f));
            if (_pendingBoxBreaks.Count > 0)
            {
                var breaks = new List<int[]>(_pendingBoxBreaks);
                _pendingBoxBreaks.Clear();
                StartCoroutine(BoxBreakFx(breaks));
            }
        }

        readonly List<int[]> _pendingBoxBreaks = new List<int[]>();

        static List<int[]> BoxCells(Board board)
        {
            var list = new List<int[]>();
            for (int l = 0; l < board.Lanes.Count; l++)
                for (int c = 0; c < board.Lanes[l].Cells.Count; c++)
                    if (board.Lanes[l].Cells[c].Box) list.Add(new[] { l, c });
            return list;
        }

        // A glowing orb flies from the bar to the box, the box shatters into cardboard bits with a tear, and the car shows.
        IEnumerator BoxBreakFx(List<int[]> cells)
        {
            _busy = true;
            foreach (var cell in cells)
            {
                RectTransform car;
                _pieceAt.TryGetValue(PieceKey(new Tap(TapKind.Lane, cell[0], cell[1])), out car);
                var target = LaneAnchor(cell[0], cell[1]);
                var size = car != null ? car.sizeDelta : _metrics.Piece;
                CanvasGroup hide = null;
                if (car != null) { hide = car.gameObject.AddComponent<CanvasGroup>(); hide.alpha = 0f; }
                var box = Place(_screen, "BoxBreak", target, size * 0.96f);
                var bimg = box.gameObject.AddComponent<Image>();
                bimg.sprite = UiArt.Get("box_mystery") ?? SpriteKit.Round;
                bimg.preserveAspect = true;
                bimg.raycastTarget = false;
                var orb = FxImage("fx_orb", new Vector2(0f, BarY), new Vector2(90f, 90f), new Color(1f, 0.95f, 0.6f, 1f), 0.4f);
                float t = 0f;
                var start = orb.anchoredPosition;
                var ctrl = new Vector2((start.x + target.x) * 0.5f + 160f, Mathf.Max(start.y, target.y) + 120f);
                while (t < 0.38f)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / 0.38f);
                    orb.anchoredPosition = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * ctrl + u * u * target;
                    float pulse = 1f + 0.25f * Mathf.Sin(t * 30f);
                    orb.localScale = new Vector3(pulse, pulse, 1f);
                    yield return null;
                }
                Destroy(orb.gameObject);
                AudioManager.Play(Sfx.BoxTear);
#if !UNITY_WEBGL
                Haptics.Thud();
#endif
                Destroy(box.gameObject);
                for (int i = 0; i < 8; i++)
                {
                    var bit = Place(_screen, "BoxBit", target + new Vector2(Random.Range(-30f, 30f), Random.Range(-30f, 30f)), new Vector2(70f, 70f));
                    var bitImg = bit.gameObject.AddComponent<Image>();
                    bitImg.sprite = UiArt.Get("box_piece_" + (i % 4)) ?? SpriteKit.Round;
                    bitImg.raycastTarget = false;
                    bit.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                    var puff = bit.gameObject.AddComponent<Puff>();
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    puff.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(300f, 650f);
                    puff.Life = 0.55f;
                    puff.Grow = 0.6f;
                }
                Sparkle(target, Color.white, 6);
                if (car != null)
                {
                    hide.alpha = 1f;
                    car.localScale = new Vector3(0.6f, 0.6f, 1f);
                    t = 0f;
                    while (t < 0.2f && car != null)
                    {
                        t += Time.unscaledDeltaTime;
                        float k = Mathf.Lerp(0.6f, 1f, t / 0.2f) + 0.12f * Mathf.Sin(t / 0.2f * Mathf.PI);
                        car.localScale = new Vector3(k, k, 1f);
                        yield return null;
                    }
                    if (car != null) car.localScale = Vector3.one;
                }
            }
            _busy = false;
        }

        // Red 'SUPER HARD' badge (shown on the level button and over the lot title).
        void SuperHardBadge(Vector2 pos, Transform parent = null)
        {
            var rt = Place(parent ?? _screen, "SuperHard", pos, new Vector2(420f, 90f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Get("pill") ?? SpriteKit.Round;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.90f, 0.12f, 0.16f, 1f);
            img.raycastTarget = false;
            var t = AddText(rt, "SUPER HARD", 48, Color.white, TextAnchor.MiddleCenter);
            t.font = ChunkyFont();
            t.raycastTarget = false;
            Outlined(t, new Color(0.35f, 0.02f, 0.05f, 1f), 3f);
            rt.gameObject.AddComponent<Pulse>();
        }

        void Exhaust(Vector2 pos, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var art = UiArt.Get("dust") ?? UiArt.Get("glow");
                var rt = Place(_screen, "Exhaust", pos + new Vector2(Random.Range(-14f, 14f), 0f), new Vector2(34f, 34f));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = art ?? SpriteKit.Round;
                img.color = new Color(1f, 1f, 1f, 0.75f);
                img.raycastTarget = false;
                var puff = rt.gameObject.AddComponent<Puff>();
                puff.Velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(60f, 130f));
                puff.Life = 0.45f;
                puff.Grow = 2.2f;
            }
        }

        void DrawBoard()
        {
            if (_board == null) return;
            int lanes = _board.Lanes.Count;
            if (lanes == 0) return;
            int depth = 1;
            for (int i = 0; i < lanes; i++)
                if (_board.Lanes[i].Cells.Count > depth)
                    depth = _board.Lanes[i].Cells.Count;
            bool sides = _board.Sides.Count > 0;
            // Tight packing: thin painted lines between lanes instead of wide gaps.
            // Leave room for the ring road on both sides (Lot Loop: cars loop around the lot).
            float gutter = sides ? 130f : 74f;
            float gap = 8f;
            float availW = 1064f - gutter * 2f;
            float cellW = (availW - gap * (lanes - 1)) / lanes;
            if (cellW > 420f) cellW = 420f;
            float availH = 930f;
            // Fill the width: cars grow until the deepest lane would overflow the lot height.
            if (cellW * 1.04f * depth > availH) cellW = Mathf.Max(availH / (1.04f * depth), Mathf.Min(cellW, 150f));
            float cellH = cellW * 1.04f;
            if (cellH * depth > availH) cellH = availH / depth;
            float boardW = lanes * cellW + (lanes - 1) * gap;
            float left = -boardW * 0.5f + cellW * 0.5f;
            float baseY = -370f + cellH * 0.5f;
            _metrics = new LotMetrics
            {
                Lanes = lanes,
                CellW = cellW,
                CellH = cellH,
                Gap = gap,
                Left = left,
                BaseY = baseY,
                Piece = new Vector2(cellW + 6f, cellH + 6f)
            };
            float roadW = 58f;
            float lotEdge = boardW * 0.5f;
            _metrics.RoadW = roadW;
            _metrics.RingL = -Mathf.Min(lotEdge + gutter * 0.5f, 520f);
            _metrics.RingRt = Mathf.Min(lotEdge + gutter * 0.5f, 520f);
            _metrics.RingTop = baseY + (depth - 0.5f) * cellH + roadW * 0.5f + 10f;
            _metrics.RingBottom = baseY - cellH * 0.5f - Mathf.Min(cellH * 0.45f, 64f) - 8f;
            _metrics.RingR0 = Mathf.Min(90f, (_metrics.RingTop - _metrics.RingBottom) * 0.3f);
            DrawLot(lanes, depth, boardW, gutter);

            var piece = _metrics.Piece;
            for (int lane = 0; lane < lanes; lane++)
            {
                var cells = _board.Lanes[lane].Cells;
                for (int cell = 0; cell < cells.Count; cell++)
                {
                    var slot = cells[cell];
                    if (slot.Empty) continue;
                    var tap = new Tap(TapKind.Lane, lane, cell);
                    DrawPiece(LaneAnchor(lane, cell), piece, slot, _legal.IsLegal(tap), tap);
                }
            }
            for (int s = 0; s < _board.Sides.Count; s++)
            {
                var side = _board.Sides[s];
                if (side.Queue.Count == 0) continue;
                if ((uint)side.Lane >= (uint)lanes) continue;
                var tap = new Tap(TapKind.Side, s, side.Cell);
                DrawCar(SideAnchor(side), piece, side.Queue[0], _legal.IsLegal(tap), tap, side.Queue.Count);
            }
        }

        // The playfield as a real lot: asphalt across the full width, painted lane lines, stop bars
        // at each lane mouth, and an exit road along the bottom that every car drives out on.
        void DrawLot(int lanes, int depth, float boardW, float gutter)
        {
            float top = _metrics.RingTop + _metrics.RoadW * 0.5f + 16f;
            float mouth = _metrics.BaseY - _metrics.CellH * 0.5f;
            float roadH = _metrics.CellH * 0.9f;
            float bottom = BarY + 60f;
            var asphalt = UiArt.Get("asphalt");
            var lot = Place(_screen, "Lot", new Vector2(0f, (top + bottom) * 0.5f), new Vector2(1400f, top - bottom));
            var lotImage = lot.gameObject.AddComponent<Image>();
            lotImage.raycastTarget = false;
            if (asphalt != null)
            {
                lotImage.sprite = asphalt;
                lotImage.type = Image.Type.Tiled;
            }
            else lotImage.color = new Color(0.30f, 0.31f, 0.34f, 1f);
            DrawRing();
            // Curb along the top of the lot.
            Bar("Curb", new Vector2(0f, top + 8f), new Vector2(1400f, 18f), new Color(0.86f, 0.84f, 0.78f, 1f));
            Bar("CurbShade", new Vector2(0f, top - 4f), new Vector2(1400f, 8f), new Color(0.55f, 0.54f, 0.50f, 1f));
            var line = new Color(0.97f, 0.96f, 0.90f, 0.92f);
            float h = depth * _metrics.CellH;
            float cy = _metrics.BaseY + (depth - 1) * _metrics.CellH * 0.5f;
            for (int i = 0; i <= lanes; i++)
            {
                float x = _metrics.Left - _metrics.CellW * 0.5f - _metrics.Gap * 0.5f + i * (_metrics.CellW + _metrics.Gap);
                Bar("LaneLine", new Vector2(x, cy + 10f), new Vector2(8f, h + 20f), line);
            }
            for (int i = 0; i < lanes; i++)
            {
                var a = LaneAnchor(i, 0);
                Bar("StopBar", new Vector2(a.x, mouth - 4f), new Vector2(_metrics.CellW - 20f, 10f), new Color(0.98f, 0.82f, 0.22f, 0.95f));
            }
            // Exit road: dashed centre line running to the bar.
            float roadY = mouth - roadH * 0.5f - 10f;
            for (float x = -520f; x < 540f; x += 110f)
                Bar("Dash", new Vector2(x, roadY - roadH * 0.32f), new Vector2(60f, 8f), new Color(0.98f, 0.82f, 0.22f, 0.8f));
        }

        // The loop: a rounded-rectangle road around the lot with a dashed centre line, plus
        // the two loop-de-loop on-ramps the level intro uses at the top corners.
        void DrawRing()
        {
            var road = new Color(0.20f, 0.21f, 0.24f, 1f);
            var edge = new Color(0.97f, 0.96f, 0.90f, 0.55f);
            var pts = RingLoop(26f);
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                Segment("RingEdge", a, b, _metrics.RoadW + 8f, edge);
            }
            for (int i = 0; i < pts.Count; i++)
                Segment("Ring", pts[i], pts[(i + 1) % pts.Count], _metrics.RoadW, road);
            for (int i = 0; i < pts.Count; i += 2)
                Segment("RingDash", pts[i], Vector2.Lerp(pts[i], pts[(i + 1) % pts.Count], 0.6f), 5f, new Color(0.98f, 0.82f, 0.22f, 0.85f));
            for (int side = -1; side <= 1; side += 2)
            {
                var loop = LoopCircle(side);
                for (int i = 0; i < 36; i++)
                {
                    float a0 = i / 36f * Mathf.PI * 2f, a1 = (i + 1) / 36f * Mathf.PI * 2f;
                    Segment("LoopTrack", loop.center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * loop.radius,
                        loop.center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * loop.radius, 12f, new Color(0.85f, 0.30f, 0.25f, 0.9f));
                }
            }
        }

        // Loop-de-loop track at the top-left (side -1) or top-right (+1) corner of the ring.
        (Vector2 center, float radius) LoopCircle(int side)
        {
            float r = 46f;
            float x = side < 0 ? _metrics.RingL : _metrics.RingRt;
            return (new Vector2(x, _metrics.RingTop + r + 6f), r);
        }

        void Segment(string name, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var rt = Place(_screen, name, (a + b) * 0.5f, new Vector2(len + width * 0.15f, width));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // Centre line of the ring road, clockwise from the top-left corner, sampled every 'step' units.
        List<Vector2> RingLoop(float step)
        {
            float l = _metrics.RingL, r = _metrics.RingRt, t = _metrics.RingTop, b = _metrics.RingBottom, R = _metrics.RingR0;
            var corners = new[]
            {
                new Vector2(r - R, t - R), new Vector2(r - R, b + R), new Vector2(l + R, b + R), new Vector2(l + R, t - R)
            };
            float[] startAng = { 90f, 0f, -90f, -180f };
            var pts = new List<Vector2>();
            for (int c = 0; c < 4; c++)
            {
                int arcN = Mathf.Max(4, Mathf.CeilToInt(R * Mathf.PI * 0.5f / step));
                for (int i = 0; i < arcN; i++)
                {
                    float a = (startAng[c] - 90f * i / arcN) * Mathf.Deg2Rad;
                    pts.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R);
                }
                var from = corners[c] + (Vector2)(Quaternion.Euler(0, 0, startAng[c] - 90f) * Vector3.right) * R;
                var nxt = corners[(c + 1) % 4] + (Vector2)(Quaternion.Euler(0, 0, startAng[(c + 1) % 4]) * Vector3.right) * R;
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, nxt) / step));
                for (int i = 0; i < n; i++) pts.Add(Vector2.Lerp(from, nxt, i / (float)n));
            }
            return pts;
        }

        void Bar(string name, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Place(_screen, name, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        Vector2 LaneAnchor(int lane, int cell)
        {
            return new Vector2(
                _metrics.Left + lane * (_metrics.CellW + _metrics.Gap),
                _metrics.BaseY + cell * _metrics.CellH);
        }

        Vector2 SideAnchor(SideLot side)
        {
            float x = _metrics.Left + side.Lane * (_metrics.CellW + _metrics.Gap);
            float dir = side.Lane < _metrics.Lanes * 0.5f ? -1f : 1f;
            if (_metrics.Lanes <= 1) dir = -1f;
            x += dir * (_metrics.CellW * 0.92f);
            return new Vector2(x, _metrics.BaseY + side.Cell * _metrics.CellH);
        }

        // Slot centers for 'count' cars: the usual seven, squeezed a little when matched sets
        // are briefly still parked alongside newer reservations.
        static Vector2 BarSlotCenter(int index, int count)
        {
            if (count <= MatchClear.BarCap) return BarSlotCenter(index);
            float span = (MatchClear.BarCap - 1) * (BarSlotWidth + BarGap);
            float step = span / (count - 1);
            return new Vector2(-span * 0.5f + index * step, BarY);
        }

        static Vector2 BarSlotCenter(int index)
        {
            float width = MatchClear.BarCap * BarSlotWidth + (MatchClear.BarCap - 1) * BarGap;
            float start = -width * 0.5f + BarSlotWidth * 0.5f;
            return new Vector2(start + index * (BarSlotWidth + BarGap), BarY);
        }

        void DrawPiece(Vector2 pos, Vector2 size, Slot slot, bool open, Tap tap)
        {
            if (slot.Tunnel)
            {
                var rt = Place(_screen, "Tunnel", pos, size);
                var image = rt.gameObject.AddComponent<Image>();
                image.sprite = UiArt.Get("garage") ?? SpriteKit.Tunnel;
                image.preserveAspect = true;
                image.color = open ? Color.white : new Color(0.70f, 0.70f, 0.74f, 1f);
                if (slot.Queue != null && slot.Queue.Count > 0 && !slot.Queue[0].Hidden)
                {
                    var peek = Place(rt, "Peek", new Vector2(0f, -size.y * 0.06f), size * 0.52f);
                    var peekImage = peek.gameObject.AddComponent<Image>();
                    peekImage.sprite = CarArt.For(slot.Queue[0].Color, open);
                    peekImage.preserveAspect = true;
                    peekImage.raycastTarget = false;
                }
                _pieceAt[PieceKey(tap)] = rt;
                // Car Match style: a big white count sitting on top of the garage arch.
                var count = Place(rt, "Count", new Vector2(0f, size.y * 0.40f), new Vector2(size.x * 0.8f, size.y * 0.34f));
                var countText = AddText(count, slot.TunnelCount.ToString(), Mathf.RoundToInt(Mathf.Clamp(size.y * 0.30f, 36f, 96f)), Color.white, TextAnchor.MiddleCenter);
                countText.font = ChunkyFont();
                countText.raycastTarget = false;
                Outlined(countText, new Color(0.08f, 0.10f, 0.28f, 1f), 4f);
                string tunnelKey = PieceKey(tap);
                int lastCount;
                if (_tunnelCounts.TryGetValue(tunnelKey, out lastCount) && lastCount > slot.TunnelCount && rt.Find("Peek") != null)
                    StartCoroutine(RollOut((RectTransform)rt.Find("Peek"), count));
                _tunnelCounts[tunnelKey] = slot.TunnelCount;
                if (open) Hook(rt, tap);
                else image.raycastTarget = false;
                return;
            }
            if (slot.Box)
            {
                // Mystery box: cardboard '?' over the car; tapping it just wiggles.
                var box = Place(_screen, "Box", pos, size * 0.96f);
                _pieceAt[PieceKey(tap)] = box;
                var boxImage = box.gameObject.AddComponent<Image>();
                boxImage.sprite = UiArt.Get("box_mystery") ?? SpriteKit.Round;
                boxImage.preserveAspect = true;
                BlockedHook(box);
                return;
            }
            DrawCar(pos, size, slot.Car, open, tap, 0);
        }

        readonly Dictionary<string, int> _tunnelCounts = new Dictionary<string, int>();

        // The next car rolls out of the garage mouth and the count ticks down with a pop.
        IEnumerator RollOut(RectTransform peek, RectTransform count)
        {
            if (peek == null) yield break;
            var home = peek.anchoredPosition;
            float t = 0f;
            while (t < 0.3f && peek != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.3f);
                float k = 1f - (1f - u) * (1f - u);
                peek.anchoredPosition = home + new Vector2(0f, 40f * (1f - k));
                peek.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), Mathf.Lerp(0.6f, 1f, k), 1f);
                if (count != null) { float c = 1f + 0.35f * Mathf.Sin(u * Mathf.PI); count.localScale = new Vector3(c, c, 1f); }
                yield return null;
            }
            if (peek != null) { peek.anchoredPosition = home; peek.localScale = Vector3.one; }
            if (count != null) count.localScale = Vector3.one;
        }

        void DrawCar(Vector2 pos, Vector2 size, Car car, bool open, Tap tap, int queueCount)
        {
            if (open)
            {
                var ring = Place(_screen, "Ring", pos + new Vector2(0f, -size.y * 0.06f), size * 1.15f);
                var ringImage = ring.gameObject.AddComponent<Image>();
                var glow = UiArt.Get("glow");
                ringImage.sprite = glow != null ? glow : SpriteKit.Round;
                if (glow == null) ringImage.type = Image.Type.Sliced;
                ringImage.color = glow != null ? new Color(1f, 0.86f, 0.35f, 0.75f) : Gold;
                ringImage.raycastTarget = false;
            }
            if (queueCount > 0)
            {
                // Side lot: a 3D carport bay under the waiting car.
                var bay = Place(_screen, "Carport", pos, size * 1.05f);
                var bayImage = bay.gameObject.AddComponent<Image>();
                bayImage.sprite = UiArt.Get("carport");
                bayImage.preserveAspect = true;
                bayImage.raycastTarget = false;
                if (bayImage.sprite == null) bayImage.enabled = false;
            }
            var rt = Place(_screen, "Car", pos, size);
            _pieceAt[PieceKey(tap)] = rt;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = car.Hidden ? HiddenSprite() : CarArt.For(car.Color, open);
            image.preserveAspect = true;
            image.color = open ? Color.white : new Color(0.84f, 0.84f, 0.84f, 1f) /* keep hue, slight darken */;
            AnimateEyes(rt, image, car, open, tap);
            if (queueCount > 1)
            {
                CountChip(rt, new Vector2(size.x * 0.34f, size.y * 0.34f), queueCount);
            }
            if (open && !car.Hidden)
            {
                var idle = rt.gameObject.AddComponent<CarIdle>();
                idle.Open = image.sprite;
                idle.Blink = CarFrames.Eye(car.Color, 1);
                idle.LookLeft = CarFrames.Heading(car.Color, 15);
                idle.LookRight = CarFrames.Heading(car.Color, 1);
            }
            if (open) Hook(rt, tap);
            else BlockedHook(rt);
        }

        void BlockedHook(RectTransform rt)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var piece = rt;
            button.onClick.AddListener(delegate
            {
                if (_busy) return;
                AudioManager.PlayPitched(Sfx.Blocked, 1f, 0.82f);
                Exhaust(piece.anchoredPosition + new Vector2(0f, piece.sizeDelta.y * 0.42f), 3);
#if !UNITY_WEBGL
                Haptics.Thud();
#endif
                StartCoroutine(Wiggle(piece));
            });
        }

        static IEnumerator Wiggle(RectTransform rt)
        {
            float t = 0f;
            var home = rt.localPosition;
            while (t < 0.32f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                // Side-to-side shimmy in its bay (no travel), plus a tiny rock.
                float fade = 1f - t / 0.32f;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 50f) * 4f * fade);
                rt.localPosition = new Vector3(home.x + Mathf.Sin(t * 60f) * 10f * fade, home.y, home.z);
                yield return null;
            }
            if (rt != null) { rt.localRotation = Quaternion.identity; rt.localPosition = home; }
        }

        // Eyes lift open (with an overshoot blink and a body bounce) when a car's lane clears,
        // and close in reverse when it gets blocked again.
        void AnimateEyes(RectTransform rt, Image image, Car car, bool open, Tap tap)
        {
            if (car.Hidden) return;
            string key = (int)tap.Kind + ":" + tap.Index + ":" + tap.Cell + ":" + (int)car.Color;
            bool before;
            bool had = _eyes.TryGetValue(key, out before);
            _eyes[key] = open;
            if (!had || before == open) return;
            var frames = new Sprite[5];
            for (int f = 0; f < 5; f++) frames[f] = CarFrames.Eye(car.Color, f);
            if (frames[0] == null) return;
            var anim = rt.gameObject.AddComponent<EyeAnim>();
            if (open)
            {
                anim.Frames = frames;
                anim.Final = image.sprite;
            }
            else
            {
                anim.Frames = new[] { frames[4], frames[2], frames[1], frames[0] };
                anim.Final = image.sprite;
            }
        }

        static Sprite HiddenSprite()
        {
            return UiArt.Get("tarp") ?? SpriteKit.Hidden;
        }

        void CountChip(RectTransform parent, Vector2 pos, int count)
        {
            var chip = Place(parent, "Count", pos, new Vector2(60f, 60f));
            var img = chip.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Get("chip") ?? SpriteKit.Round;
            img.raycastTarget = false;
            var t = AddText(chip, count.ToString(), 34, Color.white, TextAnchor.MiddleCenter);
            t.font = ChunkyFont();
            Outlined(t, new Color(0.45f, 0.05f, 0.12f, 1f), 2f);
        }

        void Hook(RectTransform rt, Tap tap)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            var captured = tap;
            var piece = rt;
            button.onClick.AddListener(delegate { OnTap(captured, piece); });
        }

        void RedrawBar()
        {
            if (_tray != null) Destroy(_tray.gameObject);
            DrawBar();
        }

        // Seven wells, then one car per landed bar entry. Reserved slots (car still driving) stay empty.
        // Cars ease from where they were last drawn, so inserts and pops slide instead of snapping.
        void DrawBar()
        {
            _barCars.Clear();
            var trayArt = UiArt.Get("tray");
            var tray = Place(_screen, "Bar", new Vector2(0f, BarY - 6f), trayArt != null ? new Vector2(1060f, 210f) : new Vector2(980f, 130f));
            _tray = tray;
            var trayImage = tray.gameObject.AddComponent<Image>();
            trayImage.sprite = trayArt != null ? trayArt : SpriteKit.Round;
            trayImage.type = trayArt != null ? Image.Type.Simple : Image.Type.Sliced;
            trayImage.color = trayArt != null ? Color.white : Wood;
            trayImage.raycastTarget = false;
            var slotArt = UiArt.Get("slot");
            float y = trayArt != null ? 6f : 0f;
            for (int i = 0; i < MatchClear.BarCap; i++)
            {
                var cell = Place(tray, "Slot", new Vector2(BarSlotCenter(i).x, y), new Vector2(BarSlotWidth - 8f, BarCarH));
                var image = cell.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                if (slotArt != null) image.sprite = slotArt;
                else
                {
                    image.sprite = SpriteKit.Round;
                    image.type = Image.Type.Sliced;
                    image.color = Slot;
                }
            }
            int n = _barView.Entries.Count;
            var seen = new HashSet<BarEntry>();
            for (int i = 0; i < n; i++)
            {
                var e = _barView.Entries[i];
                seen.Add(e);
                if (!e.Landed) { _barCars.Add(null); continue; }
                float x = BarSlotCenter(i, n).x;
                var size = slotArt != null ? new Vector2(BarSlotWidth + 4f, BarCarH + 8f) : new Vector2(BarSlotWidth - 8f, BarCarH);
                float from;
                if (!_barX.TryGetValue(e, out from)) from = x;
                var car = Place(tray, "Car", new Vector2(from, y), size);
                var img = car.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.sprite = CarArt.For(e.Color, true);
                img.preserveAspect = true;
                if (Mathf.Abs(from - x) > 0.5f) car.gameObject.AddComponent<SlideX>().Init(x, 0.2f);
                _barX[e] = x;
                _barCars.Add(car);
            }
            var stale = new List<BarEntry>();
            foreach (var k in _barX.Keys) if (!seen.Contains(k)) stale.Add(k);
            for (int i = 0; i < stale.Count; i++) _barX.Remove(stale[i]);
        }

        sealed class SlideX : MonoBehaviour
        {
            float _from, _to, _dur, _t;
            public void Init(float to, float dur) { _from = ((RectTransform)transform).anchoredPosition.x; _to = to; _dur = dur; }
            void Update()
            {
                _t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(_t / _dur);
                float k = 1f - (1f - u) * (1f - u) * (1f - u);
                var rt = (RectTransform)transform;
                rt.anchoredPosition = new Vector2(Mathf.Lerp(_from, _to, k), rt.anchoredPosition.y);
                if (u >= 1f) Destroy(this);
            }
        }

        void DrawBoosters()
        {
            // Keep the row above the home indicator on short/wide screens (iPad).
            float half = 960f;
            var srt = _screen as RectTransform;
            if (srt != null && srt.rect.height > 10f) half = srt.rect.height * 0.5f;
            float y = Mathf.Max(-820f, -half + AspectFit.BottomInset(half * 2f) + 24f + 100f);
            Booster("Undo", Economy.UndoCost, -390f, y, _history.Count > 0, OnUndo);
            Booster("Shuffle", Economy.ShuffleCost, -130f, y, BoosterKit.CanShuffle(_board), OnShuffle);
            Booster("Super", Economy.SuperUndoCost, 130f, y, CanSuper(), OnSuper);
            Booster("Magnet", Economy.MagnetCost, 390f, y, CanMagnet(), OnMagnet);
        }

        void Booster(string name, int cost, float x, float y, bool legal, UnityEngine.Events.UnityAction action)
        {
            bool free = FreeBoosters.IsFree(PrefsFlags.Shared, name);
            bool afford = free || Economy.CanAfford(Purse.Wallet, cost);
            var button = MakeButton(new Vector2(x, y), new Vector2(220f, 200f), new Color(0.22f, 0.45f, 0.82f, 1f), name, 40, action, "btn_booster");
            // Unaffordable boosters stay tappable so the tap can explain itself (shake + toast + coin popup).
            if (!afford)
            {
                button.onClick.RemoveAllListeners();
                var rt = (RectTransform)button.transform;
                button.onClick.AddListener(delegate { NotEnoughCoins(rt); });
            }
            button.interactable = legal && !_busy && !_driving && _board != null && !_board.Won && !_board.Failed;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.rectTransform.offsetMin = new Vector2(0f, 90f);
            // Cost chip: coin icon + price in a small pill on the button face.
            var chip = Place(button.transform, "Cost", new Vector2(0f, -10f), new Vector2(150f, 56f));
            var chipImage = chip.gameObject.AddComponent<Image>();
            chipImage.sprite = UiArt.Get("pill") ?? SpriteKit.Round;
            chipImage.raycastTarget = false;
            var coin = Place(chip, "Coin", new Vector2(-48f, 2f), new Vector2(46f, 46f));
            var coinImage = coin.gameObject.AddComponent<Image>();
            coinImage.sprite = UiArt.Get("icon_coin") ?? SpriteKit.Coin;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;
            var costLabel = AddText(chip, cost.ToString(), 30, afford ? Cream : new Color(1f, 0.55f, 0.5f, 1f), TextAnchor.MiddleCenter);
            costLabel.rectTransform.offsetMin = new Vector2(30f, 0f);
            costLabel.font = ChunkyFont();
            if (free)
            {
                // '1 FREE' badge replaces the price until the free use is spent.
                coin.gameObject.SetActive(false);
                chipImage.color = new Color(0.30f, 0.80f, 0.35f, 1f);
                costLabel.text = "1 FREE";
                costLabel.color = Color.white;
                costLabel.rectTransform.offsetMin = Vector2.zero;
            }
            // Illegal boosters (e.g. Shuffle with one color left) read as clearly greyed out.
            if (!legal) button.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
        }

        bool CanMagnet()
        {
            if (_board == null) return false;
            var hist = BoardQuery.Histogram(_board);
            for (int i = 0; i < hist.Length; i++)
                if (hist[i] >= MatchClear.Set) return true;
            return false;
        }

        // Taps never wait: the move is applied to the board right now (the car leaves its spot,
        // its bar slot is reserved, matches and the fail check are decided), the board redraws,
        // and the drive plays on the flight layer. Any number of cars can be in flight at once.
        void OnTap(Tap tap, RectTransform piece)
        {
            if (_busy || _phase != Phase.Play || _board == null || _board.Won || _board.Failed) return;
            if (_metrics.Lanes <= 0 || !_legal.IsLegal(tap)) return;
            var color = ExitPath.ColorOf(_board, tap);
            bool hidden = PieceHidden(tap);
            var points = BuildDrivePath(tap, _barView.InsertIndex(color));
            if (piece != null) points[0] = piece.anchoredPosition;
            var size = piece != null ? piece.sizeDelta : _metrics.Piece;
            var boxesBefore = BoxCells(_board);
            var move = BoardRules.Play(_board, _history, tap);
            if (!move.Accepted) return;
            foreach (var bc in boxesBefore)
                if (!_board.Lanes[bc[0]].Cells[bc[1]].Box) _pendingBoxBreaks.Add(bc);
            _lastMatch = move.Matched;
            var entry = _barView.Reserve(color, move.Matched);
            var flyer = SpawnFlyer(color, hidden, points[0], size);
            AudioManager.CarTap();
            AudioManager.Play(Sfx.DriveOut);
#if !UNITY_WEBGL
            Haptics.Tap();
#endif
            _fly.StartCoroutine(Drive(flyer, points, entry, color, hidden));
            ShowPlay();
        }

        bool PieceHidden(Tap tap)
        {
            if (tap.Kind == TapKind.Side)
            {
                var queue = _board.Sides[tap.Index].Queue;
                return queue.Count > 0 && queue[0].Hidden;
            }
            var slot = _board.Lanes[tap.Index].Cells[tap.Cell];
            if (slot.Tunnel) return slot.Queue != null && slot.Queue.Count > 0 && slot.Queue[0].Hidden;
            return slot.Car.Hidden;
        }

        // Control points for a curvy drive (smoothed with Catmull-Rom in Drive). Front exits swoop
        // out of the lane mouth onto the bar road; rear exits pull out the back onto the ring road,
        // round the nearest corners, run down the side road and sweep along the bar road. Every
        // drive ends with a curved approach into the slot from below-and-beside.
        List<Vector2> BuildDrivePath(Tap tap, int slotIndex)
        {
            var points = new List<Vector2>(16);
            int lane, cell;
            bool back = ExitPath.ExitsBack(_board, tap);
            if (tap.Kind == TapKind.Side)
            {
                var side = _board.Sides[tap.Index];
                points.Add(SideAnchor(side));
                lane = side.Lane;
                cell = side.Cell;
                points.Add(LaneAnchor(lane, cell));
            }
            else
            {
                lane = tap.Index;
                cell = tap.Cell;
                points.Add(LaneAnchor(lane, cell));
            }
            var slotPos = BarSlotCenter(slotIndex);
            var m = _metrics;
            var start = points[points.Count - 1];
            float bottom = m.RingBottom, R = m.RingR0;
            if (back)
            {
                // Out the rear, bending toward the nearer side road.
                float sx = start.x <= 0f ? -1f : 1f;
                float roadX = sx < 0f ? m.RingL : m.RingRt;
                float top = m.RingTop;
                points.Add(new Vector2(start.x, Mathf.Lerp(start.y, top, 0.6f)));
                points.Add(new Vector2(start.x + sx * R * 0.6f, top));
                points.Add(new Vector2(roadX - sx * R, top));
                points.Add(new Vector2(roadX - sx * R * 0.29f, top - R * 0.29f));
                points.Add(new Vector2(roadX, top - R));
                points.Add(new Vector2(roadX, bottom + R));
                points.Add(new Vector2(roadX - sx * R * 0.29f, bottom + R * 0.29f));
                points.Add(new Vector2(roadX - sx * R, bottom));
            }
            else
            {
                // Arc out of the lane mouth, leaning away from the slot first.
                var mouth = LaneAnchor(lane, 0);
                float sway = Mathf.Sign(slotPos.x - mouth.x);
                if (Mathf.Abs(slotPos.x - mouth.x) < 1f) sway = lane % 2 == 0 ? 1f : -1f;
                float y0 = mouth.y - m.CellH * 0.5f;
                points.Add(new Vector2(mouth.x - sway * m.CellW * 0.10f, Mathf.Lerp(start.y, y0, 0.7f)));
                points.Add(new Vector2(mouth.x + sway * m.CellW * 0.22f, bottom + 6f));
            }
            // Along the bar road toward the slot, then a scooping curve down into it.
            float dir = Mathf.Sign(slotPos.x - points[points.Count - 1].x);
            if (dir == 0f) dir = 1f;
            float mid = Mathf.Lerp(points[points.Count - 1].x, slotPos.x, 0.6f);
            points.Add(new Vector2(mid, bottom - 4f));
            points.Add(new Vector2(slotPos.x + dir * 34f, Mathf.Lerp(bottom, slotPos.y, 0.55f)));
            points.Add(slotPos);
            return points;
        }

        // Catmull-Rom spline through the control points (centripetal-ish via uniform with clamped ends).
        static List<Vector2> CatmullRom(List<Vector2> p, int perSeg)
        {
            if (p == null || p.Count < 3) return p;
            var outPts = new List<Vector2>(p.Count * perSeg + 1);
            for (int i = 0; i < p.Count - 1; i++)
            {
                var p0 = p[Mathf.Max(0, i - 1)];
                var p1 = p[i];
                var p2 = p[i + 1];
                var p3 = p[Mathf.Min(p.Count - 1, i + 2)];
                for (int k = 0; k < perSeg; k++)
                {
                    float t = k / (float)perSeg, t2 = t * t, t3 = t2 * t;
                    outPts.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            outPts.Add(p[p.Count - 1]);
            return outPts;
        }

        static float PathLength(List<Vector2> p)
        {
            float n = 0f;
            for (int i = 1; i < p.Count; i++) n += Vector2.Distance(p[i - 1], p[i]);
            return n;
        }

        // Fast and length-aware: short hops ~0.45 s, the longest loop stays under 0.9 s.
        public const float DriveMinSec = 0.45f, DriveMaxSec = 0.82f, DriveSpeed = 3000f;

        RectTransform SpawnFlyer(CarColor color, bool hidden, Vector2 pos, Vector2 size)
        {
            var rt = Place(_flyLayer, "Flyer", pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = hidden ? HiddenSprite() : CarArt.For(color, true);
            image.preserveAspect = true;
            image.raycastTarget = false;
            rt.SetAsLastSibling();
            return rt;
        }

        static void HidePiece(RectTransform rt)
        {
            if (rt == null) return;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            int index = rt.GetSiblingIndex();
            if (index <= 0 || rt.parent == null) return;
            for (int back = 1; back <= 2 && index - back >= 0; back++)
            {
                var previous = rt.parent.GetChild(index - back);
                if (previous != null && previous.name == "Ring")
                    previous.gameObject.SetActive(false);
            }
        }

        IEnumerator Drive(RectTransform flyer, List<Vector2> points, BarEntry entry, CarColor color, bool hidden)
        {
            // Round the lane corners so the car carves a curve instead of snapping at waypoints.
            points = CatmullRom(points, 10);
            float driveSec = Mathf.Clamp(PathLength(points) / DriveSpeed, DriveMinSec, DriveMaxSec);
            // The reserved slot can shift while we drive (later taps insert before it); steer to where it is now.
            var plannedEnd = points[points.Count - 1];
            var startSize = flyer.sizeDelta;
            var endSize = new Vector2(BarSlotWidth - 8f, BarCarH);
            var image = flyer.GetComponent<Image>();
            Vector2 facing;
            Along(points, 0f, out facing);
            float heading = CarFrames.HeadingAngle(facing);
            float lean = 0f;
            Steer(flyer, image, color, hidden, heading, 0f, 1f, 1f);
            // Squash on start: the car crouches before it pulls out, and kicks up dust.
            for (int k = 0; k < 3; k++) DustPuff(points[0] + new Vector2(Random.Range(-30f, 30f), -startSize.y * 0.3f), new Vector2(Random.Range(-60f, 60f), Random.Range(-20f, 30f)));
            float crouch = 0f;
            while (crouch < 0.08f)
            {
                crouch += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float q = Mathf.Sin(crouch / 0.08f * Mathf.PI);
                flyer.localScale = new Vector3(1f + 0.10f * q, 1f - 0.12f * q, 1f);
                yield return null;
            }
            float elapsed = 0f;
            float trailClock = 0f;
            int puffClock = 0;
                        Vector2 last = flyer.anchoredPosition;
            while (elapsed < driveSec)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.05f) dt = 0.05f;
                elapsed += dt;
                float u = Mathf.Clamp01(elapsed / driveSec);
                // Quick ease-in, then a brisk steady speed all the way to the slot (Car Match feel).
                const float accel = 0.15f;
                float eased = (u < accel ? u * u / (2f * accel) : u - accel * 0.5f) / (1f - accel * 0.5f);
                var pos = Along(points, eased, out facing) + (SlotOf(entry) - plannedEnd) * eased;
                trailClock += dt;
                if (trailClock > 0.035f)
                {
                    trailClock = 0f;
                    var rear = pos - facing.normalized * flyer.sizeDelta.y * 0.42f;
                    TrailDot(rear);
                    puffClock++;
                    if (puffClock % 3 == 0) Exhaust(rear, 1);
                }
                flyer.anchoredPosition = pos;
                flyer.sizeDelta = Vector2.Lerp(startSize, endSize, eased);
                // Heading eases toward the path tangent (no snapping); lean follows the turn rate.
                float target = CarFrames.HeadingAngle(facing);
                float delta = Mathf.DeltaAngle(heading, target);
                float step = delta * Mathf.Clamp01(dt * 18f);
                heading = Mathf.Repeat(heading + step, 360f);
                lean = Mathf.Lerp(lean, Mathf.Clamp(-step / Mathf.Max(dt, 0.001f) * 0.02f, -9f, 9f), Mathf.Clamp01(dt * 12f));
                float speed = (pos - last).magnitude / Mathf.Max(dt, 0.001f);
                last = pos;
                float stretch = Mathf.Clamp01(speed / 4000f) * 0.10f;
                Steer(flyer, image, color, hidden, heading, lean, 1f - stretch * 0.5f, 1f + stretch);
                yield return null;
            }
            flyer.anchoredPosition = SlotOf(entry);
            flyer.sizeDelta = endSize;
            Steer(flyer, image, color, hidden, 0f, 0f, 1f, 1f);
            // Squash-stretch on the stop.
            float stop = 0f;
            while (stop < 0.12f)
            {
                stop += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float q = Mathf.Sin(stop / 0.12f * Mathf.PI);
                flyer.localScale = new Vector3(1f + 0.14f * q, 1f - 0.12f * q, 1f);
                yield return null;
            }
            flyer.localScale = Vector3.one;
            // Parked: remember where every slot was, then let any completed sets pop in order.
            var where = new Dictionary<BarEntry, Vector2>();
            for (int i = 0; i < _barView.Entries.Count; i++) where[_barView.Entries[i]] = BarSlotCenter(i, _barView.Entries.Count);
            var ready = _barView.Land(entry);
            Destroy(flyer.gameObject);
            AudioManager.Play(Sfx.SlotArrive);
            if (_phase == Phase.Play) RedrawBar();
            for (int k = 0; k < ready.Count; k++)
            {
                var spots = new List<Vector2>();
                for (int i = 0; i < ready[k].Count; i++)
                {
                    Vector2 at;
                    if (where.TryGetValue(ready[k][i], out at)) spots.Add(at);
                }
                _fly.StartCoroutine(PopSet(spots, ready[k][0].Color));
            }
            if (_barView.Settled) _fly.StartCoroutine(AfterSettle(ready.Count > 0 ? 0.32f : 0f));
        }

        Vector2 SlotOf(BarEntry entry)
        {
            int i = _barView.IndexOf(entry);
            return BarSlotCenter(i < 0 ? 0 : i, _barView.Entries.Count);
        }

        // A matched set: copies of the three cars glow white, burst into light, and vanish.
        IEnumerator PopSet(List<Vector2> spots, CarColor color)
        {
            AudioManager.Play(Sfx.MatchClear);
#if !UNITY_WEBGL
            Haptics.Success();
#endif
            if (_shake != null) _shake.Punch(0.22f, 9f, 0.6f);
            var set = new List<RectTransform>();
            for (int i = 0; i < spots.Count; i++)
            {
                var rt = Place(_flyLayer, "Pop", spots[i], new Vector2(BarSlotWidth - 8f, BarCarH));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = CarArt.For(color, true);
                img.preserveAspect = true;
                img.raycastTarget = false;
                var g = Place(rt, "WhiteGlow", Vector2.zero, rt.sizeDelta * 1.5f);
                var gi = g.gameObject.AddComponent<Image>();
                gi.sprite = UiArt.Get("glow") ?? SpriteKit.Round;
                gi.color = new Color(1f, 1f, 1f, 0f);
                gi.raycastTarget = false;
                set.Add(rt);
            }
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.2f);
                for (int i = 0; i < set.Count; i++)
                {
                    if (set[i] == null) continue;
                    float k = 1f - 0.15f * u;
                    set[i].localScale = new Vector3(k, k, 1f);
                    var img = set[i].GetComponent<Image>();
                    img.color = Color.Lerp(Color.white, new Color(1.6f, 1.6f, 1.6f, 1f), u);
                    var gi = set[i].GetChild(0).GetComponent<Image>();
                    gi.color = new Color(1f, 1f, 1f, 0.95f * u);
                }
                yield return null;
            }
            AudioManager.Play(Sfx.SlotArrive, 1.1f);
            for (int i = 0; i < set.Count; i++)
            {
                if (set[i] == null) continue;
                if (_phase == Phase.Play) LightBurst(set[i].anchoredPosition);
                Destroy(set[i].gameObject);
            }
        }

        // Once nothing is driving and no set is waiting: win, fail, or refresh boosters/HUD.
        IEnumerator AfterSettle(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (!_barView.Settled || _phase != Phase.Play || _board == null) yield break;
            if (_board.Won) EnterWin();
            else if (_board.Failed) ShowFail(null);
            else ShowPlay();
        }

        // Chaikin corner cutting; keeps the first and last points fixed.
        static List<Vector2> Smooth(List<Vector2> pts, int passes)
        {
            if (pts == null || pts.Count < 3) return pts;
            var cur = pts;
            for (int p = 0; p < passes; p++)
            {
                var next = new List<Vector2>(cur.Count * 2) { cur[0] };
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    var a = cur[i];
                    var b = cur[i + 1];
                    if (i > 0) next.Add(Vector2.Lerp(a, b, 0.25f));
                    if (i < cur.Count - 2) next.Add(Vector2.Lerp(a, b, 0.75f));
                }
                next.Add(cur[cur.Count - 1]);
                cur = next;
            }
            return cur;
        }

        // 16 painted headings; the leftover angle (at most 11.25 deg) plus the lean is applied as a
        // small rotation so the turn reads continuous. Falls back to the 8-heading set, then the code car.
        static void Steer(RectTransform rt, Image image, CarColor color, bool hidden, float heading, float lean, float sx, float sy)
        {
            if (rt == null) return;
            rt.localScale = new Vector3(sx, sy, 1f);
            if (hidden || image == null)
            {
                rt.localRotation = Quaternion.Euler(0f, 0f, lean);
                return;
            }
            int index = Mathf.RoundToInt(heading / 22.5f) % 16;
            var sprite = CarFrames.Heading(color, index);
            if (sprite != null)
            {
                image.sprite = sprite;
                float residual = Mathf.DeltaAngle(index * 22.5f, heading);
                rt.localRotation = Quaternion.Euler(0f, 0f, residual + lean);
                return;
            }
            float rad = heading * Mathf.Deg2Rad;
            Aim(rt, image, color, hidden, new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)));
        }

        // Faint white dotted trail that fades behind a driving car.
        void TrailDot(Vector2 pos)
        {
            var rt = Place(_screen, "Trail", pos, new Vector2(16f, 16f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteKit.Round;
            img.color = new Color(1f, 1f, 1f, 0.38f);
            img.raycastTarget = false;
            var puff = rt.gameObject.AddComponent<Puff>();
            puff.Life = 0.6f;
            puff.Grow = 1.0f;
            rt.SetSiblingIndex(Mathf.Max(0, _screen.childCount - 2));
        }

        void DustPuff(Vector2 pos, Vector2 velocity)
        {
            var art = UiArt.Get("dust");
            if (art == null) return;
            var rt = Place(_screen, "Dust", pos, new Vector2(70f, 70f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.color = new Color(1f, 1f, 1f, 0.7f);
            img.raycastTarget = false;
            var puff = rt.gameObject.AddComponent<Puff>();
            puff.Velocity = velocity;
            // Dust sits under the car so it never washes the sprite out.
            rt.SetSiblingIndex(Mathf.Max(0, _screen.childCount - 2));
        }

        void SpeedLine(Vector2 pos, Vector2 dir)
        {
            var rt = Place(_screen, "SpeedLine", pos, new Vector2(6f, 46f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.55f);
            img.raycastTarget = false;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            var puff = rt.gameObject.AddComponent<Puff>();
            puff.Life = 0.22f;
            puff.Grow = 0.6f;
            puff.Velocity = -dir.normalized * 120f;
            rt.SetAsFirstSibling();
            rt.SetSiblingIndex(Mathf.Max(0, _screen.childCount - 2));
        }

        static Vector2 Along(List<Vector2> points, float t, out Vector2 facing)
        {
            facing = Vector2.down;
            if (points == null || points.Count == 0) return Vector2.zero;
            if (points.Count == 1 || t <= 0f)
            {
                if (points.Count > 1) facing = points[1] - points[0];
                return points[0];
            }
            if (t >= 1f)
            {
                facing = points[points.Count - 1] - points[points.Count - 2];
                return points[points.Count - 1];
            }
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
                total += Vector2.Distance(points[i - 1], points[i]);
            if (total < 0.001f) return points[points.Count - 1];
            float target = total * t;
            float walked = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                float seg = Vector2.Distance(a, b);
                if (seg < 0.001f) continue;
                if (walked + seg >= target || i == points.Count - 1)
                {
                    float u = (target - walked) / seg;
                    if (u < 0f) u = 0f;
                    if (u > 1f) u = 1f;
                    facing = b - a;
                    return Vector2.Lerp(a, b, u);
                }
                walked += seg;
            }
            facing = points[points.Count - 1] - points[points.Count - 2];
            return points[points.Count - 1];
        }

        static void Aim(RectTransform rt, Image image, CarColor color, bool hidden, Vector2 dir)
        {
            if (rt == null) return;
            if (hidden || image == null)
            {
                rt.localRotation = Quaternion.identity;
                return;
            }
            CarFacing facing;
            Sprite painted;
            if (EyeState.TryNearest(dir.x, dir.y, out facing) && CarArt.TryHeading(color, facing, out painted))
            {
                image.sprite = painted;
                rt.localRotation = Quaternion.identity;
                return;
            }
            image.sprite = SpriteKit.Car(color, true);
            Face(rt, dir);
        }

        static void Face(RectTransform rt, Vector2 dir)
        {
            if (rt == null || dir.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        void OnUndo()
        {
            if (!Ready(FreeBoosters.IsFree(PrefsFlags.Shared, "Undo") ? 0 : Economy.UndoCost)) return;
            Board restored;
            if (!BoosterKit.Undo(_history, out restored)) return;
            if (!FreeBoosters.Pay(PrefsFlags.Shared, "Undo", Economy.UndoCost, Purse.TrySpend)) return;
            AudioManager.Play(Sfx.BoosterUndo);
            var before = _board;
            _board = restored;
            _lastMatch = false;
            ShowPlay();
            StartCoroutine(UndoFx(before, restored));
        }

        void OnSuper()
        {
            if (!Ready(FreeBoosters.IsFree(PrefsFlags.Shared, "Super") ? 0 : Economy.SuperUndoCost)) return;
            // Super clears one set of three for you (bar color first), with a gold sweep. It never refills the lot.
            var before = _board.Clone();
            var pulled = new List<int[]>();
            CarColor color;
            int fromBar;
            if (!BoosterKit.Super(_board, pulled, out color, out fromBar))
            {
                _board = before;
                return;
            }
            if (!FreeBoosters.Pay(PrefsFlags.Shared, "Super", Economy.SuperUndoCost, Purse.TrySpend))
            {
                _board = before;
                return;
            }
            _history.Add(before);
            AudioManager.Play(Sfx.BoosterSuper);
            _lastMatch = true;
            StartCoroutine(SuperFx(before, pulled, color, fromBar));
        }

        bool CanSuper()
        {
            if (_board == null) return false;
            var trial = _board.Clone();
            CarColor c;
            int b;
            return BoosterKit.Super(trial, null, out c, out b);
        }

        void OnShuffle()
        {
            // Nothing to rearrange: no charge, free use kept.
            if (!BoosterKit.CanShuffle(_board)) return;
            if (!Ready(FreeBoosters.IsFree(PrefsFlags.Shared, "Shuffle") ? 0 : Economy.ShuffleCost)) return;
            var before = _board.Clone();
            _history.Add(_board.Clone());
            if (!BoosterKit.Shuffle(_board, new System.Random()))
            {
                _history.RemoveAt(_history.Count - 1);
                return;
            }
            if (!FreeBoosters.Pay(PrefsFlags.Shared, "Shuffle", Economy.ShuffleCost, Purse.TrySpend))
            {
                Board back;
                BoosterKit.Undo(_history, out back);
                _board = back;
                return;
            }
            AudioManager.Play(Sfx.BoosterShuffle);
            _lastMatch = false;
            ShowPlay();
            StartCoroutine(ShuffleFx(before));
        }

        void OnMagnet()
        {
            if (!Ready(FreeBoosters.IsFree(PrefsFlags.Shared, "Magnet") ? 0 : Economy.MagnetCost)) return;
            _history.Add(_board.Clone());
            var pulled = new List<int[]>();
            var moves = new List<int[]>();
            if (!BoosterKit.Magnet(_board, pulled, moves))
            {
                // Magnet can pull some cars before it gives up; put the untouched snapshot back.
                _board = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                return;
            }
            if (!FreeBoosters.Pay(PrefsFlags.Shared, "Magnet", Economy.MagnetCost, Purse.TrySpend))
            {
                Board back;
                BoosterKit.Undo(_history, out back);
                _board = back;
                return;
            }
            AudioManager.Play(Sfx.BoosterMagnet);
            _lastMatch = true;
            StartCoroutine(MagnetFx(pulled, moves));
        }


        // ===================== Booster FX =====================
        // Sprite hooks: drop sheets into Resources/FX (fx_sparkle, fx_magnet_trail, fx_gold_glow); multi-sprite sheets
        // play as flipbooks. Until then everything falls back to procedural glow/round particles.
        static string PieceKey(Tap tap) { return (int)tap.Kind + ":" + tap.Index + ":" + tap.Cell; }

        static Sprite[] FxFrames(string name)
        {
            var frames = Resources.LoadAll<Sprite>("FX/" + name);
            if (frames == null || frames.Length == 0) return null;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        // Plays one of Character Creator's sheets (Resources/FX/<sheet>/). Returns null if the sheet is missing.
        RectTransform FxSheet(string sheet, Vector2 pos, Vector2 size, float fps, bool loop)
        {
            var frames = FxFrames(sheet);
            if (frames == null) return null;
            var rt = Place(_screen, "Fx", pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = frames[0];
            rt.gameObject.AddComponent<Flipbook>().Play(frames, fps, loop);
            return rt;
        }

        RectTransform FxImage(string sheet, Vector2 pos, Vector2 size, Color color, float life)
        {
            var rt = Place(_screen, "Fx", pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var frames = FxFrames(sheet);
            if (frames != null)
            {
                img.sprite = frames[0];
                if (frames.Length > 1) rt.gameObject.AddComponent<Flipbook>().Init(frames, life);
            }
            else img.sprite = UiArt.Get("glow") ?? SpriteKit.Round;
            img.color = color;
            return rt;
        }

        void LightBurst(Vector2 pos)
        {
            for (int i = 0; i < 10; i++)
            {
                var rt = Place(_screen, "Light", pos, new Vector2(26f, 26f));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = UiArt.Get("glow") ?? SpriteKit.Round;
                img.color = new Color(1f, 1f, 0.9f, 1f);
                img.raycastTarget = false;
                var puff = rt.gameObject.AddComponent<Puff>();
                float a = i / 10f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                puff.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(260f, 480f);
                puff.Life = 0.4f;
                puff.Grow = 0.4f;
            }
            var flash = Place(_screen, "Flash", pos, new Vector2(120f, 120f));
            var fi = flash.gameObject.AddComponent<Image>();
            fi.sprite = UiArt.Get("glow") ?? SpriteKit.Round;
            fi.color = new Color(1f, 1f, 1f, 0.9f);
            fi.raycastTarget = false;
            var fp = flash.gameObject.AddComponent<Puff>();
            fp.Life = 0.25f;
            fp.Grow = 2.2f;
        }

        void Sparkle(Vector2 pos, Color tint, int count)
        {
            if (FxSheet("fx_sparkle_burst", pos, new Vector2(300f, 300f), 20f, false) != null) return;
            for (int i = 0; i < count; i++)
            {
                var rt = FxImage("fx_sparkle", pos, new Vector2(46f, 46f), tint, 0.5f);
                var puff = rt.gameObject.AddComponent<Puff>();
                float a = Random.Range(0f, Mathf.PI * 2f);
                puff.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(160f, 420f);
                puff.Life = 0.5f;
                puff.Grow = 0.3f;
            }
        }

        static List<Tap> Appeared(Board from, Board to)
        {
            var list = new List<Tap>();
            for (int l = 0; l < to.Lanes.Count && l < from.Lanes.Count; l++)
                for (int c = 0; c < to.Lanes[l].Cells.Count && c < from.Lanes[l].Cells.Count; c++)
                {
                    var a = from.Lanes[l].Cells[c];
                    var b = to.Lanes[l].Cells[c];
                    if (!b.Tunnel && !b.Empty && (a.Empty || a.Tunnel)) list.Add(new Tap(TapKind.Lane, l, c));
                }
            for (int i = 0; i < to.Sides.Count && i < from.Sides.Count; i++)
                if (to.Sides[i].Queue.Count > from.Sides[i].Queue.Count) list.Add(new Tap(TapKind.Side, i, 0));
            return list;
        }

        static Vector2 PathAt(List<Vector2> pts, float t, out Vector2 facing) { return Along(pts, t, out facing); }

        // Undo: the car reverse-drives out of the bar along its S-curve back to its spot (~0.4 s).
        IEnumerator UndoFx(Board before, Board after)
        {
            var back = Appeared(before, after);
            if (back.Count == 0) yield break;
            _busy = true;
            var tap = back[0];
            RectTransform piece;
            _pieceAt.TryGetValue(PieceKey(tap), out piece);
            CarColor color = CarColor.Coral;
            if (tap.Kind == TapKind.Lane) color = after.Lanes[tap.Index].Cells[tap.Cell].Car.Color;
            else if (after.Sides[tap.Index].Queue.Count > 0) color = after.Sides[tap.Index].Queue[0].Color;
            int slot = Mathf.Clamp(after.Bar.Count, 0, MatchClear.BarCap - 1);
            for (int i = before.Bar.Count - 1; i >= 0; i--) if (before.Bar[i] == color) { slot = i; break; }
            var pts = CatmullRom(BuildDrivePath(tap, slot), 10);
            pts.Reverse();
            if (piece != null) piece.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var flyer = SpawnFlyer(color, false, pts[0], new Vector2(BarSlotWidth - 8f, BarCarH));
            var image = flyer.GetComponent<Image>();
            var endSize = piece != null ? piece.sizeDelta : _metrics.Piece;
            float t = 0f;
            const float dur = 0.4f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float k = u * u * (3f - 2f * u);
                Vector2 facing;
                flyer.anchoredPosition = Along(pts, k, out facing);
                flyer.sizeDelta = Vector2.Lerp(new Vector2(BarSlotWidth - 8f, BarCarH), endSize, k);
                // Reversing: the nose points against the travel direction.
                Steer(flyer, image, color, false, CarFrames.HeadingAngle(-facing), 0f, 1f, 1f);
                yield return null;
            }
            Destroy(flyer.gameObject);
            // Big bright landing glow.
            var land = FxImage("fx_land_glow", pts[pts.Count - 1], endSize * 2.4f, new Color(1f, 0.95f, 0.55f, 1f), 0.45f);
            var lp = land.gameObject.AddComponent<Puff>();
            lp.Life = 0.45f;
            lp.Grow = 1.6f;
            var core = FxImage("fx_land_glow", pts[pts.Count - 1], endSize * 1.3f, new Color(1f, 1f, 1f, 1f), 0.3f);
            var cp = core.gameObject.AddComponent<Puff>();
            cp.Life = 0.3f;
            cp.Grow = 1.4f;
            if (piece != null)
            {
                var g = piece.GetComponent<CanvasGroup>();
                if (g != null) g.alpha = 1f;
                StartCoroutine(Wiggle(piece));
            }
            _busy = false;
        }

        public const float ShuffleDealSec = 0.7f;

        // Shuffle: an animated re-deal. Every car lifts, cars whose color changed glide (eased)
        // from a spot that held their color to their new spot, then land with a squash, and
        // Character Creator's sparkle sheet (fx_sparkle_burst) fires on every car as it lands.
        IEnumerator ShuffleFx(Board before)
        {
            _busy = true;
            var cars = new List<RectTransform>();
            var homes = new List<Vector2>();
            var froms = new List<Vector2>();
            var changed = new List<bool>();
            var nows = new List<CarColor>();
            var sources = new List<KeyValuePair<CarColor, Vector2>>();
            var used = new List<bool>();
            for (int l = 0; l < _board.Lanes.Count; l++)
                for (int c = 0; c < _board.Lanes[l].Cells.Count; c++)
                {
                    var now = _board.Lanes[l].Cells[c];
                    var was = before.Lanes[l].Cells[c];
                    if (now.Empty || now.Tunnel || now.Car.Hidden || was.Empty || was.Tunnel) continue;
                    RectTransform rt;
                    if (!_pieceAt.TryGetValue(PieceKey(new Tap(TapKind.Lane, l, c)), out rt) || rt == null) continue;
                    cars.Add(rt);
                    homes.Add(rt.anchoredPosition);
                    froms.Add(rt.anchoredPosition);
                    bool diff = was.Car.Color != now.Car.Color;
                    changed.Add(diff);
                    nows.Add(now.Car.Color);
                    if (diff) { sources.Add(new KeyValuePair<CarColor, Vector2>(was.Car.Color, rt.anchoredPosition)); used.Add(false); }
                }
            // Each changed car starts at a spot that used to hold its new color.
            for (int i = 0; i < cars.Count; i++)
            {
                if (!changed[i]) continue;
                for (int j = 0; j < sources.Count; j++)
                {
                    if (used[j] || sources[j].Key != nows[i]) continue;
                    used[j] = true;
                    froms[i] = sources[j].Value;
                    break;
                }
            }
            int n = cars.Count;
            float stagger = n > 1 ? Mathf.Min(0.02f, 0.12f / n) : 0f;
            const float lift = 0.15f, land = 0.12f;
            float move = ShuffleDealSec - lift - land - stagger * n;
            if (move < 0.3f) move = 0.3f;
            float per = lift + move + land;
            float total = per + stagger * n;
            var sparked = new bool[n];
            for (int i = 0; i < n; i++) cars[i].SetAsLastSibling();
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < n; i++)
                {
                    var rt = cars[i];
                    if (rt == null) continue;
                    float lt = t - i * stagger;
                    if (lt <= 0f) { rt.anchoredPosition = froms[i]; continue; }
                    Vector2 pos;
                    float sc;
                    if (lt < lift)
                    {
                        float u = EaseOut(lt / lift);
                        pos = froms[i] + new Vector2(0f, 26f * u);
                        sc = 1f + 0.15f * u;
                    }
                    else if (lt < lift + move)
                    {
                        float u = EaseInOut((lt - lift) / move);
                        var arc = new Vector2(0f, 26f + 40f * Mathf.Sin(u * Mathf.PI) * (changed[i] ? 1f : 0.2f));
                        pos = Vector2.Lerp(froms[i], homes[i], u) + arc;
                        sc = 1.15f;
                        rt.localRotation = Quaternion.Euler(0f, 0f, changed[i] ? Mathf.Sin(u * Mathf.PI) * 10f * (homes[i].x >= froms[i].x ? -1f : 1f) : 0f);
                    }
                    else
                    {
                        float u = Mathf.Clamp01((lt - lift - move) / land);
                        pos = homes[i] + new Vector2(0f, 26f * (1f - EaseOut(u)));
                        sc = 1f;
                        rt.localRotation = Quaternion.identity;
                        float sq = 0.14f * Mathf.Sin(u * Mathf.PI);
                        rt.localScale = new Vector3(1f + sq, 1f - sq, 1f);
                        rt.anchoredPosition = pos;
                        if (!sparked[i]) { sparked[i] = true; Sparkle(homes[i], new Color(1f, 0.95f, 0.6f, 0.95f), 5); }
                        continue;
                    }
                    rt.anchoredPosition = pos;
                    rt.localScale = new Vector3(sc, sc, 1f);
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                if (cars[i] == null) continue;
                if (!sparked[i]) Sparkle(homes[i], new Color(1f, 0.95f, 0.6f, 0.95f), 5);
                cars[i].anchoredPosition = homes[i];
                cars[i].localScale = Vector3.one;
                cars[i].localRotation = Quaternion.identity;
            }
            _busy = false;
        }

        static float EaseOut(float u) { u = 1f - Mathf.Clamp01(u); return 1f - u * u * u; }
        static float EaseInOut(float u) { u = Mathf.Clamp01(u); return u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f; }

        // Super: a gold glow sweeps across the lot and bar, then the chosen three glow and pop away.
        IEnumerator SuperFx(Board before, List<int[]> pulled, CarColor color, int fromBar)
        {
            _busy = true;
            var sweepLot = FxSheet("fx_super_glow_sweep", new Vector2(0f, _metrics.BaseY + 300f), new Vector2(1100f, 1100f), 20f, false);
            var sweepBar = FxSheet("fx_super_glow_sweep", new Vector2(0f, BarY), new Vector2(1060f, 260f), 20f, false);
            if (sweepLot == null && sweepBar == null)
            {
                var band = FxImage("fx_gold_glow", new Vector2(-900f, -100f), new Vector2(420f, 1500f), new Color(1f, 0.82f, 0.25f, 0.75f), 0.45f);
                band.localRotation = Quaternion.Euler(0f, 0f, -14f);
                float tb = 0f;
                while (tb < 0.45f)
                {
                    tb += Time.unscaledDeltaTime;
                    band.anchoredPosition = new Vector2(Mathf.Lerp(-900f, 900f, Mathf.Clamp01(tb / 0.45f)), -100f);
                    yield return null;
                }
                Destroy(band.gameObject);
            }
            else yield return new WaitForSecondsRealtime(0.5f);
            var targets = new List<RectTransform>();
            foreach (var p in pulled)
            {
                RectTransform rt;
                if (_pieceAt.TryGetValue(PieceKey(new Tap(TapKind.Lane, p[0], p[1])), out rt) && rt != null) targets.Add(rt);
            }
            int taken = 0;
            for (int i = before.Bar.Count - 1; i >= 0 && taken < fromBar; i--)
                if (before.Bar[i] == color && i < _barCars.Count && _barCars[i] != null) { targets.Add(_barCars[i]); taken++; }
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.3f);
                float k = 1f + 0.25f * Mathf.Sin(u * Mathf.PI * 0.5f);
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] == null) continue;
                    targets[i].localScale = new Vector3(k, k, 1f);
                    var img = targets[i].GetComponent<Image>();
                    if (img != null) img.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f, 1f), u);
                }
                yield return null;
            }
            AudioManager.Play(Sfx.MatchClear);
#if !UNITY_WEBGL
            Haptics.Success();
#endif
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                var w = _screen.InverseTransformPoint(targets[i].position);
                var pos = new Vector2(w.x, w.y);
                if (FxSheet("fx_super_glow_pop", pos, targets[i].sizeDelta * 2f, 20f, false) == null)
                    Sparkle(pos, new Color(1f, 0.85f, 0.3f, 1f), 7);
                targets[i].gameObject.SetActive(false);
            }
            yield return new WaitForSecondsRealtime(0.35f);
            if (_board.Won)
            {
                EnterWin();
                yield break;
            }
            ShowPlay();
        }

        // Magnet: a magnet pops in over the bar and pulls the three cars along trails, ending in a burst;
        // then the cars behind any hole slide up to close the gap.
        IEnumerator MagnetFx(List<int[]> pulled, List<int[]> moves)
        {
            _busy = true;
            var magnetPos = new Vector2(0f, BarY + 40f); // anchored on the bar: cars pull into the slots
            var magnet = Place(_screen, "Magnet", magnetPos + new Vector2(0f, 150f), new Vector2(340f, 340f));
            var mImg = magnet.gameObject.AddComponent<Image>();
            mImg.sprite = UiArt.Get("icon_magnet") ?? SpriteKit.Round;
            mImg.preserveAspect = true;
            mImg.raycastTarget = false;
            var mLoop = FxFrames("fx_magnet_icon_loop");
            if (mLoop != null) { mImg.sprite = mLoop[0]; magnet.gameObject.AddComponent<Flipbook>().Play(mLoop, 11f, true); magnet.sizeDelta = new Vector2(460f, 460f); }
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.18f);
                float k = u < 0.7f ? 1.2f * u / 0.7f : 1.2f - 0.2f * (u - 0.7f) / 0.3f;
                magnet.localScale = new Vector3(k, k, 1f);
                magnet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * 8f);
                yield return null;
            }
            var cars = new List<RectTransform>();
            var starts = new List<Vector2>();
            foreach (var p in pulled)
            {
                RectTransform rt;
                if (_pieceAt.TryGetValue(PieceKey(new Tap(TapKind.Lane, p[0], p[1])), out rt) && rt != null)
                {
                    rt.SetAsLastSibling();
                    cars.Add(rt);
                    starts.Add(rt.anchoredPosition);
                }
            }
            // Pull streaks (sheet converges at x=0.81, y=0.5 of its cell): pivot there on the magnet, aimed at each car.
            var streaks = new List<RectTransform>();
            for (int i = 0; i < starts.Count; i++)
            {
                var d = magnetPos - starts[i];
                float len = d.magnitude;
                var st = FxSheet("fx_magnet_pull", magnetPos, new Vector2(len / 0.81f, Mathf.Min(len / 0.81f, 720f)), 16f, true);
                if (st == null) break;
                st.pivot = new Vector2(0.81f, 0.5f);
                st.anchoredPosition = magnetPos;
                st.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                st.SetAsFirstSibling();
                st.SetSiblingIndex(Mathf.Max(0, cars.Count > 0 && cars[0] != null ? cars[0].GetSiblingIndex() : 0));
                streaks.Add(st);
            }
            bool sheetTrails = streaks.Count > 0;
            magnet.SetAsLastSibling();
            t = 0f;
            float trailClock = 0f;
            const float pull = 1.6f;
            while (t < pull)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                trailClock += dt;
                float u = Mathf.Clamp01(t / pull);
                float k = u * u;
                for (int i = 0; i < cars.Count; i++)
                {
                    if (cars[i] == null) continue;
                    var a = starts[i];
                    var slotTarget = BarSlotCenter(Mathf.Clamp(_board.Bar.Count + i, 0, MatchClear.BarCap - 1));
                    var ctrl = new Vector2(a.x * 0.4f, Mathf.Max(a.y, slotTarget.y) + 160f);
                    var pos = (1 - k) * (1 - k) * a + 2 * (1 - k) * k * ctrl + k * k * slotTarget;
                    cars[i].anchoredPosition = pos;
                    float s = Mathf.Lerp(1f, 0.55f, k);
                    cars[i].localScale = new Vector3(s, s, 1f);
                    cars[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * 25f * (i % 2 == 0 ? 1f : -1f));
                    if (trailClock > 0.03f && !sheetTrails)
                    {
                        var dot = FxImage("fx_magnet_trail", pos, new Vector2(60f, 60f), new Color(0.75f, 0.85f, 1f, 0.8f), 0.3f);
                        var puff = dot.gameObject.AddComponent<Puff>();
                        puff.Life = 0.3f;
                        puff.Grow = 0.2f;
                        dot.SetSiblingIndex(Mathf.Max(0, cars[i].GetSiblingIndex()));
                    }
                }
                if (trailClock > 0.03f) trailClock = 0f;
                magnet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 50f) * 6f);
                yield return null;
            }
            for (int i = 0; i < cars.Count; i++) if (cars[i] != null) cars[i].gameObject.SetActive(false);
            for (int i = 0; i < streaks.Count; i++) if (streaks[i] != null) Destroy(streaks[i].gameObject);
            AudioManager.Play(Sfx.MatchClear);
#if !UNITY_WEBGL
            Haptics.Success();
#endif
            if (_shake != null) _shake.Punch(0.2f, 8f, 0.5f);
            if (FxSheet("fx_magnet_impact", magnetPos, new Vector2(840f, 840f), 20f, false) == null)
            {
                Sparkle(magnetPos, new Color(1f, 0.9f, 0.4f, 1f), 16);
                var ring = FxImage("fx_gold_glow", magnetPos, new Vector2(120f, 120f), new Color(1f, 0.9f, 0.5f, 0.9f), 0.3f);
                var rp = ring.gameObject.AddComponent<Puff>();
                rp.Life = 0.3f;
                rp.Grow = 3.5f;
            }
            t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f - t / 0.22f;
                magnet.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (_board.Won)
            {
                EnterWin();
                yield break;
            }
            ShowPlay();
            StartCoroutine(MagnetSlide(moves));
        }

        IEnumerator MagnetSlide(List<int[]> moves)
        {
            _busy = true;
            float t;
            // Close the gap: cars that moved up glide from their old cell to the new one.
            var slides = new List<RectTransform>();
            var from = new List<Vector2>();
            var to = new List<Vector2>();
            foreach (var m in moves)
            {
                RectTransform rt;
                if (_pieceAt.TryGetValue(PieceKey(new Tap(TapKind.Lane, m[0], m[2])), out rt) && rt != null)
                {
                    slides.Add(rt);
                    from.Add(LaneAnchor(m[0], m[1]));
                    to.Add(rt.anchoredPosition);
                    rt.anchoredPosition = LaneAnchor(m[0], m[1]);
                }
            }
            t = 0f;
            while (t < 0.28f && slides.Count > 0)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.28f);
                float k = 1f - (1f - u) * (1f - u);
                for (int i = 0; i < slides.Count; i++) if (slides[i] != null) slides[i].anchoredPosition = Vector2.Lerp(from[i], to[i], k);
                yield return null;
            }
            for (int i = 0; i < slides.Count; i++) if (slides[i] != null) slides[i].anchoredPosition = to[i];
            _busy = false;
        }

        bool Ready(int cost)
        {
            return !_busy && !_driving && _phase == Phase.Play && _board != null && !_board.Won && !_board.Failed
                && Economy.CanAfford(Purse.Wallet, cost);
        }

        void EnterWin()
        {
            if (!_settled)
            {
                _settled = true;
                Purse.Grant(Economy.WinPayFor(_level != null && _level.SuperHard));
                Purse.Advance(_index, _levels.Count);
            }
            ShowWin();
        }

        void ShowWin()
        {
            _phase = Phase.Win;
            _busy = false;
            ClearScreen();
            Backdrop();
            int pay = Economy.WinPayFor(_level != null && _level.SuperHard);
            WinShow.Build(this, _screen, new WinShow.Setup
            {
                Pay = pay,
                CoinsBefore = Purse.Wallet.Coins - pay,
                More = _index < _levels.Count - 1,
                Mascot = PaintedMascot(MascotArt.Cheer ?? MascotArt.Happy),
                Font = ChunkyFont(),
                MakeButton = (pos, size, label, fontSize, style, action) => MakeButton(pos, size, Green, label, fontSize, action, style),
                OnNext = OnNext,
                OnHome = OnHome,
                Hud = () => { Hud(0f); return _coinText; },
                Shake = _shake
            });
        }

        static Sprite PaintedMascot(Sprite painted)
        {
            return painted != null ? painted : SpriteKit.Mascot;
        }

        IEnumerator CheerHome(RectTransform rt, Image pic)
        {
            var happy = MascotArt.Happy;
            var cheer = MascotArt.Cheer;
            if (happy != null) pic.sprite = happy;
            rt.localRotation = Quaternion.identity;
            yield return new WaitForSecondsRealtime(0.5f);
            if (cheer != null) pic.sprite = cheer;
            var origin = rt.anchoredPosition;
            float t = 0f;
            const float duration = 1.05f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float hop = Mathf.Abs(Mathf.Sin(t * 9f)) * 22f;
                rt.anchoredPosition = origin + new Vector2(0f, hop);
                float s = 1f + 0.04f * Mathf.Sin(t * 9f);
                rt.localScale = new Vector3(s, s, 1f);
                rt.localRotation = Quaternion.identity;
                yield return null;
            }
            rt.anchoredPosition = origin;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        void OnNext()
        {
            LoadLevel(Purse.LevelIndex);
            StartLevel();
        }

        void StartLevel()
        {
            ShowPlay();
            if (_fly != null && _board != null) _fly.StartCoroutine(IntroFx());
        }

        public const float IntroStagger = 0.04f, IntroCarSec = 0.75f, IntroMaxSec = 1.9f;
        bool _introSkip;

        // Level intro: cars pour in from loop-de-loop tracks at the left and right top corners
        // (alternating sides, ~40 ms apart). Each goes once around its vertical loop (full spin),
        // rides the back road and noses down into its spot. They park sleepy and the open ones
        // wake as usual. A tap anywhere skips to the parked board. Input is live the moment the
        // last car parks (or on skip).
        IEnumerator IntroFx()
        {
            var cars = new List<KeyValuePair<RectTransform, Car>>();
            for (int l = 0; l < _board.Lanes.Count; l++)
                for (int c = 0; c < _board.Lanes[l].Cells.Count; c++)
                {
                    var slot = _board.Lanes[l].Cells[c];
                    if (slot.Empty || slot.Tunnel || slot.Box) continue;
                    RectTransform rt;
                    if (_pieceAt.TryGetValue(PieceKey(new Tap(TapKind.Lane, l, c)), out rt) && rt != null)
                        cars.Add(new KeyValuePair<RectTransform, Car>(rt, slot.Car));
                }
            if (cars.Count == 0) yield break;
            // Fill from the back rows forward so arriving cars do not cross parked ones.
            cars.Sort((a, b) => b.Key.anchoredPosition.y.CompareTo(a.Key.anchoredPosition.y));
            _busy = true;
            _introSkip = false;
            var catcher = Place(_flyLayer, "IntroSkip", Vector2.zero, new Vector2(4000f, 4000f));
            var ci = catcher.gameObject.AddComponent<Image>();
            ci.color = new Color(0f, 0f, 0f, 0.001f);
            catcher.gameObject.AddComponent<Button>().onClick.AddListener(() => _introSkip = true);
            var ghosts = new List<RectTransform>();
            var paths = new List<List<Vector2>>();
            var groups = new List<CanvasGroup>();
            for (int i = 0; i < cars.Count; i++)
            {
                var piece = cars[i].Key;
                var g = piece.GetComponent<CanvasGroup>() ?? piece.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                groups.Add(g);
                int side = i % 2 == 0 ? -1 : 1;
                var loop = LoopCircle(side);
                float edgeX = side * 640f;
                var pts = new List<Vector2> { new Vector2(edgeX, loop.center.y - loop.radius) };
                // Full vertical loop (drawn as a circle): enter at the bottom, go all the way round.
                for (int k = 0; k <= 16; k++)
                {
                    float a = -Mathf.PI * 0.5f - side * k / 16f * Mathf.PI * 2f;
                    pts.Add(loop.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * loop.radius);
                }
                var target = piece.anchoredPosition;
                pts.Add(new Vector2(Mathf.Lerp(loop.center.x, target.x, 0.5f), _metrics.RingTop));
                pts.Add(new Vector2(target.x, _metrics.RingTop - 10f));
                pts.Add(target);
                paths.Add(CatmullRom(pts, 6));
                var gh = Place(_flyLayer, "IntroCar", pts[0], piece.sizeDelta);
                var img = gh.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.sprite = CarArt.For(cars[i].Value.Color, false);
                ghosts.Add(gh);
            }
            catcher.SetAsLastSibling();
            float stagger = Mathf.Min(IntroStagger, (IntroMaxSec - IntroCarSec) / Mathf.Max(1, cars.Count - 1));
            float total = IntroCarSec + stagger * (cars.Count - 1);
            float t = 0f;
            var parked = new bool[cars.Count];
            while (t < total && !_introSkip)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                for (int i = 0; i < ghosts.Count; i++)
                {
                    float u = Mathf.Clamp01((t - i * stagger) / IntroCarSec);
                    var gh = ghosts[i];
                    if (u <= 0f) { gh.gameObject.SetActive(false); continue; }
                    gh.gameObject.SetActive(!parked[i]);
                    if (parked[i]) continue;
                    float k = u < 0.85f ? u / 0.85f * 0.93f : 0.93f + (u - 0.85f) / 0.15f * 0.07f;
                    Vector2 facing;
                    gh.anchoredPosition = Along(paths[i], k, out facing);
                    var img = gh.GetComponent<Image>();
                    Steer(gh, img, cars[i].Value.Color, cars[i].Value.Hidden, CarFrames.HeadingAngle(facing), 0f, 1f, 1f);
                    if (u >= 1f)
                    {
                        parked[i] = true;
                        gh.gameObject.SetActive(false);
                        groups[i].alpha = 1f;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < ghosts.Count; i++) if (ghosts[i] != null) Destroy(ghosts[i].gameObject);
            Destroy(catcher.gameObject);
            _busy = false;
            // Parked sleepy; redraw so cars with a clear exit play their wake-up.
            SleepAll();
            ShowPlay();
        }

        void SleepAll()
        {
            _eyes.Clear();
            for (int l = 0; l < _board.Lanes.Count; l++)
                for (int c = 0; c < _board.Lanes[l].Cells.Count; c++)
                {
                    var slot = _board.Lanes[l].Cells[c];
                    if (slot.Empty || slot.Tunnel) continue;
                    _eyes[(int)TapKind.Lane + ":" + l + ":" + c + ":" + (int)slot.Car.Color] = false;
                }
        }

        void ShowFail(string status)
        {
            _phase = Phase.Fail;
            ClearScreen();
            Backdrop();
            AudioManager.Play(Sfx.Fail);
#if !UNITY_WEBGL
            Haptics.Thud();
#endif
            Label(new Vector2(0f, 360f), new Vector2(900f, 90f), "Bar's full", 68, Cream, TextAnchor.MiddleCenter);
            string body = "There's no open slot left. Retry uses a life, starts this lot over, and adds " +
                          Economy.FailPay + " coins. Home does the same and leaves. Continue plays a rewarded ad and puts the last three cars back.";
            if (!string.IsNullOrEmpty(status)) body = status;
            Label(new Vector2(0f, 140f), new Vector2(860f, 220f), body, 32, Cream, TextAnchor.MiddleCenter);
            _livesText = Label(new Vector2(0f, -40f), new Vector2(700f, 50f), LivesLine(), 32, Gold, TextAnchor.MiddleCenter);
            _retryButton = MakeButton(new Vector2(0f, -220f), new Vector2(640f, 140f), Green, "Retry", 52, OnRetry);
            _retryButton.interactable = !_busy && Purse.Wallet.Lives > 0;
            var cont = MakeButton(new Vector2(0f, -400f), new Vector2(640f, 140f), new Color(0.22f, 0.45f, 0.82f, 1f), "Continue", 48, OnContinue);
            cont.interactable = !_busy;
            Label(new Vector2(0f, -530f), new Vector2(800f, 40f), "Watch a rewarded ad to continue.", 26, new Color(1f, 0.94f, 0.86f, 0.8f), TextAnchor.MiddleCenter);
            MakeButton(new Vector2(0f, -680f), new Vector2(320f, 90f), new Color(0.20f, 0.24f, 0.34f, 0.9f), "Home", 32, OnHome);
        }

        void OnRetry()
        {
            if (_busy || _phase != Phase.Fail || Purse.Wallet.Lives <= 0) return;
            AcceptFail();
            _board = _seed.Clone();
            _history.Clear();
            _settled = false;
            _failPaid = false;
            _lastMatch = false;
            KillFlights();
            StartLevel();
        }

        void AcceptFail()
        {
            if (_failPaid) return;
            _failPaid = true;
            Purse.TrySpendLife();
            Purse.Grant(Economy.Pay(false));
        }

        void OnContinue()
        {
            if (_busy) return;
            StartCoroutine(Continue());
        }

        IEnumerator Continue()
        {
            _busy = true;
            if (_retryButton != null) _retryButton.interactable = false;
            bool granted = false;
            yield return _ads.ShowRewarded("continue", delegate (bool ok) { granted = ok; });
            _busy = false;
            if (!granted)
            {
                ShowFail(null);
                yield break;
            }
            Board restored;
            if (!BoosterKit.SuperUndo(_history, out restored))
            {
                ShowFail(null);
                yield break;
            }
            _board = restored;
            _lastMatch = false;
            ShowPlay();
        }

        void OnHome()
        {
            if (_busy || _driving) return;
            bool celebrate = _phase == Phase.Win;
            if (_phase == Phase.Fail) AcceptFail();
            ShowHome(celebrate);
        }

        int NextNumber()
        {
            if (_levels.Count == 0) return 1;
            int i = Purse.LevelIndex;
            if (i < 0) i = 0;
            if (i >= _levels.Count) i = _levels.Count - 1;
            return _levels[i].Number;
        }

        string LivesLine()
        {
            var wallet = Purse.Wallet;
            if (wallet.Lives >= Economy.MaxLives) return wallet.Lives + "   Full";
            return wallet.Lives + "   " + Clock(Economy.SecondsUntilLife(wallet, Purse.Now()));
        }

        static string Clock(int seconds)
        {
            if (seconds < 0) seconds = 0;
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        void Backdrop(bool home = false)
        {
            var rt = Place(_screen, "Sky", Vector2.zero, new Vector2(1080f, 1920f));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = SpriteKit.Sky;
            image.raycastTarget = false;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            // The painted home backdrop has parked cars in it; only the home screen shows it.
            if (!home) return;
            var painted = UiArt.Get("home_bg");
            if (painted == null) return;
            var bg = Place(_screen, "Backdrop", Vector2.zero, new Vector2(1080f, 2340f));
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = painted;
            bgImage.raycastTarget = false;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = painted.rect.width / painted.rect.height;
        }

        // Decorative parked cars on the home lot, laid out from the real canvas bottom so they stay
        // fully visible at 390x844 and taller and keep clear of the Level button (y -342..-598).
        void HomeCars()
        {
            var area = ((RectTransform)_screen).rect;
            float halfW = area.width > 10f ? area.width * 0.5f : 490f;
            float bottom = area.height > 10f ? -area.height * 0.5f : -1060f;
            float size = Mathf.Min(190f, halfW * 0.36f);
            float rowLow = bottom + size * 0.62f;
            float rowHigh = Mathf.Min(rowLow + size * 1.15f, -598f - 30f - size * 0.5f);
            if (rowHigh < rowLow + size * 0.8f) rowHigh = rowLow + size * 0.8f;
            var spots = new[]
            {
                new Vector3(-0.72f, rowHigh, 0), new Vector3(0.72f, rowHigh, 1), new Vector3(-0.24f, rowLow, 2),
                new Vector3(0.26f, rowLow, 3), new Vector3(-0.78f, rowLow - size * 0.05f, 4), new Vector3(0.78f, rowLow, 5)
            };
            var colors = new[] { CarColor.Coral, CarColor.Sky, CarColor.Gold, CarColor.Teal, CarColor.Lime, CarColor.Violet };
            for (int i = 0; i < spots.Length; i++)
            {
                var sp = spots[i];
                // Keep the low outer pair only on tall screens where there is room between the inner cars and the edge.
                if (i >= 4 && halfW * 0.52f < size) continue;
                var sprite = CarFrames.Heading(colors[(int)sp.z], 0) ?? CarArt.For(colors[(int)sp.z], true);
                if (sprite == null) continue;
                var rt = Place(_screen, "HomeCar", new Vector2(sp.x * (halfW - size * 0.5f), sp.y), new Vector2(size, size));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(0.9f, 0.9f, 0.92f, 1f);
            }
        }

        // Drifting cloud band over the sky with a little tilt parallax.
        void Clouds()
        {
            var art = UiArt.Get("clouds");
            if (art == null) return;
            for (int i = 0; i < 2; i++)
            {
                var rt = Place(_screen, "Clouds", new Vector2(i == 0 ? 0f : -900f, i == 0 ? 700f : 420f), i == 0 ? new Vector2(2160f, 520f) : new Vector2(1620f, 390f));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = art;
                img.color = new Color(1f, 1f, 1f, i == 0 ? 0.85f : 0.6f);
                img.raycastTarget = false;
                var d = rt.gameObject.AddComponent<Drift>();
                d.Speed = i == 0 ? 9f : 16f;
                d.Parallax = i == 0 ? 8f : 16f;
            }
        }

        static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        // Safe-area aware layer for anything pinned to the top edge.
        RectTransform SafeLayer()
        {
            var rt = Stretch(_screen, "Safe");
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        static void Outlined(Text text, Color color, float size)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(size, -size);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -size * 1.6f);
        }

        void Arch(Vector2 pos, Vector2 size, Color color)
        {
            var rt = Place(_screen, "Arch", pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = SpriteKit.Round;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        RectTransform Hud(float y)
        {
            var safe = SafeLayer();
            Pill(safe, new Vector2(-250f, -70f), new Vector2(340f, 96f), UiArt.Get("icon_coin") ?? SpriteKit.Coin, Purse.Wallet.Coins.ToString(), true);
            Pill(safe, new Vector2(250f, -70f), new Vector2(340f, 96f), UiArt.Get("icon_heart") ?? SpriteKit.Heart, LivesLine(), false);
            return safe;
        }

        // Play HUD: Home in its own pill on the left, then coins and lives.
        RectTransform HudPlay()
        {
            var safe = SafeLayer();
            var home = MakeButton(new Vector2(-400f, -70f), new Vector2(200f, 100f), new Color(0.20f, 0.24f, 0.34f, 1f), "Home", 38, OnHome, "pill", safe);
            ((RectTransform)home.transform).anchorMin = ((RectTransform)home.transform).anchorMax = new Vector2(0.5f, 1f);
            ((RectTransform)home.transform).anchoredPosition = new Vector2(-400f, -70f);
            home.interactable = !_busy && !_driving;
            Pill(safe, new Vector2(-40f, -70f), new Vector2(290f, 96f), UiArt.Get("icon_coin") ?? SpriteKit.Coin, Purse.Wallet.Coins.ToString(), true);
            Pill(safe, new Vector2(320f, -70f), new Vector2(290f, 96f), UiArt.Get("icon_heart") ?? SpriteKit.Heart, LivesLine(), false);
            return safe;
        }

        // Chunky 3D 'Level N': Lilita One, yellow-orange gradient face, stamped dark rim, drop shadow,
        // bounce-in once per level (not on every redraw).
        int _wordArtShown = -1;

        void LevelWordArt(string title)
        {
            var rt = Place(_screen, "LevelTitle", new Vector2(0f, 740f), new Vector2(760f, 130f));
            var text = AddText(rt, title, 104, Color.white, TextAnchor.MiddleCenter);
            text.font = ChunkyFont();
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.FitFont(text, 740f, 104, 48);
            UiKit.StampOutlined(text, new Color(0.30f, 0.10f, 0.06f, 1f), 5f, new Color(0f, 0f, 0f, 0.45f));
            var g = text.gameObject.AddComponent<Gradient2>();
            g.Top = new Color(1f, 0.95f, 0.45f, 1f);
            g.Bottom = new Color(1f, 0.55f, 0.12f, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 2f);
            int number = _level != null ? _level.Number : 0;
            if (_wordArtShown != number)
            {
                _wordArtShown = number;
                StartCoroutine(UiKit.BounceIn(rt, 0.05f));
            }
        }

        void Logo(Vector2 pos, Vector2 size)
        {
            var art = UiArt.Get("logo");
            if (art == null)
            {
                var title = Label(pos, new Vector2(980f, 200f), "Lot Loop", 150, Cream, TextAnchor.MiddleCenter);
                Outlined(title, new Color(0.35f, 0.12f, 0.10f, 1f), 6f);
                return;
            }
            var rt = Place(_screen, "Logo", pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        void Pill(RectTransform parent, Vector2 pos, Vector2 size, Sprite icon, string value, bool coins)
        {
            var rt = Place(parent, coins ? "Coins" : "Lives", pos, size);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            var image = rt.gameObject.AddComponent<Image>();
            var pill = UiArt.Get("pill");
            if (pill != null)
            {
                // The painted pill carries its own shadow padding (16px each side at 360x96).
                image.sprite = pill;
                rt.sizeDelta = size * new Vector2(392f / 360f, 128f / 96f);
            }
            else
            {
                image.sprite = SpriteKit.Round;
                image.type = Image.Type.Sliced;
                image.color = new Color(0.12f, 0.14f, 0.20f, 0.78f);
            }
            image.raycastTarget = false;
            var mark = Place(rt, "Icon", new Vector2(-size.x * 0.5f + 20f, 2f), new Vector2(92f, 92f));
            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.sprite = icon;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            var text = AddText(rt, value, 40, Cream, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(70f, 0f);
            text.rectTransform.offsetMax = new Vector2(-60f, 0f);
            Outlined(text, new Color(0.05f, 0.06f, 0.15f, 0.9f), 2f);
            // '+' affordance: coins open the (future) shop, hearts the refill. Today it just answers the tap.
            var plusArt = UiArt.Get("plus");
            if (plusArt != null)
            {
                var plus = Place(rt, "Plus", new Vector2(size.x * 0.5f - 22f, 0f), new Vector2(76f, 76f));
                var plusImage = plus.gameObject.AddComponent<Image>();
                plusImage.sprite = plusArt;
                plusImage.preserveAspect = true;
                var plusButton = plus.gameObject.AddComponent<Button>();
                plusButton.transition = Selectable.Transition.None;
                plus.gameObject.AddComponent<PressSquash>().Sink = 3f;
                plusButton.onClick.AddListener(coins ? (UnityEngine.Events.UnityAction)OpenCoinPopup : OnPlus);
            }
            if (coins) _coinText = text;
            else _livesText = text;
        }

        void OnPlus()
        {
            AudioManager.Play(Sfx.Click);
        }

        // ---- Coin feedback: shake + toast + a small "how to earn coins" popup (no prices changed). ----
        RectTransform _coinPopup;

        void NotEnoughCoins(RectTransform source)
        {
            AudioManager.Play(Sfx.Blocked);
#if !UNITY_WEBGL
            Haptics.Thud();
#endif
            if (source != null) StartCoroutine(Shake(source));
            Toast("Not enough coins");
            OpenCoinPopup();
        }

        IEnumerator Shake(RectTransform rt)
        {
            var home = rt.anchoredPosition;
            float t = 0f;
            while (t < 0.35f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * 60f) * 18f * (1f - t / 0.35f), 0f);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = home;
        }

        void Toast(string message)
        {
            var rt = Place(_screen, "Toast", new Vector2(0f, 260f), new Vector2(720f, 120f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Get("pill") ?? SpriteKit.Round;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.08f, 0.09f, 0.16f, 0.92f);
            img.raycastTarget = false;
            var text = AddText(rt, message, 46, Cream, TextAnchor.MiddleCenter);
            text.font = ChunkyFont();
            text.raycastTarget = false;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            StartCoroutine(FadeToast(rt, group));
        }

        IEnumerator FadeToast(RectTransform rt, CanvasGroup group)
        {
            float t = 0f;
            var start = rt.anchoredPosition;
            while (t < 1.6f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = t < 1.1f ? 1f : 1f - (t - 1.1f) / 0.5f;
                rt.anchoredPosition = start + new Vector2(0f, t * 30f);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        void OpenCoinPopup()
        {
            if (_coinPopup != null) return;
            var dim = Place(_screen, "CoinPopup", Vector2.zero, new Vector2(1080f, 2400f));
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            _coinPopup = dim;
            var panel = Place(dim, "Panel", new Vector2(0f, 40f), new Vector2(860f, 760f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = UiArt.Get("pill") ?? SpriteKit.Round;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.16f, 0.22f, 0.42f, 0.98f);
            var icon = Place(panel, "Coin", new Vector2(0f, 270f), new Vector2(150f, 150f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = UiArt.Get("icon_coin") ?? SpriteKit.Coin;
            iconImage.preserveAspect = true;
            var title = Place(panel, "Title", new Vector2(0f, 150f), new Vector2(800f, 90f));
            var titleText = AddText(title, "Need more coins?", 60, Cream, TextAnchor.MiddleCenter);
            titleText.font = ChunkyFont();
            var body = Place(panel, "Body", new Vector2(0f, 30f), new Vector2(760f, 150f));
            AddText(body, "Earn " + Economy.WinPay + " coins for every lot you play.\nWatch a short video for bonus coins.", 38, Cream, TextAnchor.MiddleCenter);
            MakeButton(new Vector2(0f, -130f), new Vector2(620f, 150f), new Color(0.2f, 0.7f, 0.3f, 1f), "Watch ad  +" + Economy.AdCoinReward, 46, OnAdCoins, "btn_green", panel);
            MakeButton(new Vector2(0f, -290f), new Vector2(360f, 110f), new Color(0.2f, 0.4f, 0.8f, 1f), "Close", 42, CloseCoinPopup, "btn_blue", panel);
        }

        void CloseCoinPopup()
        {
            if (_coinPopup != null) Destroy(_coinPopup.gameObject);
            _coinPopup = null;
        }

        void OnAdCoins()
        {
            if (_busy) return;
            StartCoroutine(AdCoins());
        }

        IEnumerator AdCoins()
        {
            _busy = true;
            bool granted = false;
            yield return _ads.ShowRewarded("coins", delegate (bool ok) { granted = ok; });
            _busy = false;
            CloseCoinPopup();
            if (!granted)
            {
                Toast("No video right now. Try again later");
                yield break;
            }
            Purse.Grant(Economy.AdCoinReward);
            AudioManager.Play(Sfx.Coin);
            if (_coinText != null) _coinText.text = Purse.Wallet.Coins.ToString();
            Toast("+" + Economy.AdCoinReward + " coins");
            if (_phase == Phase.Play) ShowPlay();
        }

        // Debug-only coin grant: ?coins=5000 in the page URL. Compiled only into WebGL and development builds,
        // never into the iOS release build.
        static void DebugCoinGrant()
        {
#if UNITY_WEBGL || DEVELOPMENT_BUILD
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return;
            var m = System.Text.RegularExpressions.Regex.Match(url, @"[?&]coins=(\d{1,7})");
            int n;
            if (m.Success && int.TryParse(m.Groups[1].Value, out n) && n > 0) Purse.Grant(n);
#endif
        }

        Text Label(Vector2 pos, Vector2 size, string value, int fontSize, Color color, TextAnchor anchor)
        {
            var rt = Place(_screen, "Label", pos, size);
            return AddText(rt, value, fontSize, color, anchor);
        }

        Button MakeButton(Vector2 pos, Vector2 size, Color color, string value, int fontSize, UnityEngine.Events.UnityAction action, string style = null, RectTransform parent = null)
        {
            if (style == null) style = StyleFor(color);
            var art = UiArt.Get(style);
            var host = parent != null ? (Transform)parent : _screen;
            if (art == null)
            {
                var shadow = Place(host, "Shadow", pos + new Vector2(0f, -8f), size);
                var shadowImage = shadow.gameObject.AddComponent<Image>();
                shadowImage.sprite = SpriteKit.Round;
                shadowImage.type = Image.Type.Sliced;
                shadowImage.color = new Color(0f, 0f, 0f, 0.25f);
                shadowImage.raycastTarget = false;
            }
            var rt = Place(host, "Button", pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            if (art != null)
            {
                image.sprite = art;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.sprite = SpriteKit.Round;
                image.type = Image.Type.Sliced;
                image.color = color;
            }
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.disabledColor = new Color(0.60f, 0.60f, 0.62f, 0.9f);
            button.colors = colors;
            var label = AddText(rt, value, fontSize, Color.white, TextAnchor.MiddleCenter);
            label.font = ChunkyFont();
            if (art != null)
            {
                // The face sits above the baked lip and shadow; centre the label on the face.
                label.rectTransform.offsetMin = new Vector2(0f, size.y * 0.20f);
                label.rectTransform.offsetMax = new Vector2(0f, -size.y * 0.06f);
            }
            Outlined(label, LabelInk(style), Mathf.Max(2f, fontSize / 18f));
            rt.gameObject.AddComponent<PressSquash>().Sink = size.y * 0.05f;
            button.onClick.AddListener(delegate { AudioManager.Play(Sfx.Click); });
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        static string StyleFor(Color color)
        {
            if (color.g > color.r && color.g > color.b) return "btn_green";
            if (color.b > 0.6f && color.b > color.r) return "btn_blue";
            return "btn_navy";
        }

        static Color LabelInk(string style)
        {
            switch (style)
            {
                case "btn_green": return new Color(0.06f, 0.32f, 0.12f, 1f);
                case "btn_blue":
                case "btn_booster": return new Color(0.07f, 0.18f, 0.45f, 1f);
                case "btn_orange": return new Color(0.50f, 0.22f, 0.04f, 1f);
                default: return new Color(0.04f, 0.05f, 0.12f, 1f);
            }
        }

        Font _chunky;

        Font ChunkyFont()
        {
            if (_chunky != null) return _chunky;
            _chunky = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            if (_chunky == null) _chunky = UiFont();
            return _chunky;
        }

        static RectTransform Place(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        Text AddText(RectTransform parent, string value, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = go.AddComponent<Text>();
            text.font = UiFont();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Font UiFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }
}
