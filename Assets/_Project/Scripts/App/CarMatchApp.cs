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
        bool _driving;
        bool _settled;
        bool _failPaid;
        bool _lastMatch;
        float _tick;
        Text _coinText;
        Text _livesText;
        Button _retryButton;

        static readonly Color Ink = new Color(0.15f, 0.17f, 0.22f, 1f);
        static readonly Color Cream = new Color(0.98f, 0.95f, 0.90f, 1f);
        static readonly Color Green = new Color(0.18f, 0.62f, 0.34f, 1f);
        static readonly Color Wood = new Color(0.45f, 0.26f, 0.20f, 1f);
        static readonly Color Slot = new Color(0.62f, 0.40f, 0.30f, 1f);
        static readonly Color Gold = new Color(0.96f, 0.78f, 0.28f, 1f);

        const float DriveSeconds = 0.42f;
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
            if (_coinText != null) _coinText.text = Purse.Wallet.Coins.ToString();
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
        }

        void ClearScreen()
        {
            StopAllCoroutines();
            _coinText = null;
            _livesText = null;
            _retryButton = null;
            for (int i = _screen.childCount - 1; i >= 0; i--)
                Destroy(_screen.GetChild(i).gameObject);
        }

        void ShowHome(bool celebrate = false)
        {
            _phase = Phase.Home;
            _busy = false;
            ClearScreen();
            Backdrop();
            Arch(new Vector2(-250f, 260f), new Vector2(180f, 320f), new Color(0.93f, 0.86f, 0.72f, 1f));
            Arch(new Vector2(230f, 300f), new Vector2(150f, 260f), new Color(0.55f, 0.70f, 0.78f, 1f));
            Arch(new Vector2(0f, 180f), new Vector2(220f, 280f), new Color(0.96f, 0.90f, 0.80f, 1f));
            Hud(820f);
            var mascot = Place(_screen, "Mascot", new Vector2(0f, 120f), new Vector2(520f, 520f));
            var pic = mascot.gameObject.AddComponent<Image>();
            pic.sprite = PaintedMascot(celebrate ? MascotArt.Happy : MascotArt.Idle);
            pic.preserveAspect = true;
            pic.raycastTarget = false;
            if (celebrate) StartCoroutine(CheerHome(mascot, pic));
            Label(new Vector2(0f, -250f), new Vector2(800f, 90f), "Lot Loop", 72, Cream, TextAnchor.MiddleCenter);
            Label(new Vector2(0f, -330f), new Vector2(800f, 50f), "Clear the lane. Match three.", 32, new Color(1f, 0.95f, 0.88f, 1f), TextAnchor.MiddleCenter);
            int number = NextNumber();
            bool canPlay = _levels.Count > 0 && Purse.Wallet.Lives > 0;
            var play = MakeButton(new Vector2(0f, -500f), new Vector2(640f, 170f), Green, "Level " + number, 64, canPlay ? (UnityEngine.Events.UnityAction)OnPlay : null);
            play.interactable = canPlay;
        }

        void OnPlay()
        {
            if (_levels.Count == 0 || Purse.Wallet.Lives <= 0) return;
            LoadLevel(Purse.LevelIndex);
            ShowPlay();
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
            _settled = false;
            _failPaid = false;
            _lastMatch = false;
            _busy = false;
            _driving = false;
        }

        void ShowPlay()
        {
            _phase = Phase.Play;
            ClearScreen();
            Backdrop();
            Hud(860f);
            var home = MakeButton(new Vector2(-430f, 860f), new Vector2(150f, 72f), new Color(0.20f, 0.24f, 0.34f, 0.85f), "Home", 28, OnHome);
            home.interactable = !_busy && !_driving;
            string title = _level != null ? "Level " + _level.Number : "Level";
            Label(new Vector2(0f, 760f), new Vector2(700f, 56f), title, 40, Cream, TextAnchor.MiddleCenter);
            string blurb = _level != null && !string.IsNullOrEmpty(_level.Blurb) ? _level.Blurb : "";
            if (_lastMatch) blurb = "Matched.";
            Label(new Vector2(0f, 700f), new Vector2(900f, 44f), blurb, 26, new Color(1f, 0.94f, 0.86f, 1f), TextAnchor.MiddleCenter);
            DrawBoard();
            DrawBar();
            DrawBoosters();
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
            float gutter = sides ? 150f : 24f;
            float gap = 16f;
            float availW = 1000f - gutter * 2f;
            float cellW = (availW - gap * (lanes - 1)) / lanes;
            if (cellW > 168f) cellW = 168f;
            float cellH = cellW * 1.18f;
            float availH = 980f;
            if (cellH * depth > availH) cellH = availH / depth;
            float boardW = lanes * cellW + (lanes - 1) * gap;
            float left = -boardW * 0.5f + cellW * 0.5f;
            float baseY = -360f + cellH * 0.5f;
            _metrics = new LotMetrics
            {
                Lanes = lanes,
                CellW = cellW,
                CellH = cellH,
                Gap = gap,
                Left = left,
                BaseY = baseY,
                Piece = new Vector2(cellW - 8f, cellH - 10f)
            };

            var road = Place(_screen, "Road", new Vector2(0f, baseY + (depth - 1) * cellH * 0.5f), new Vector2(boardW + gutter * 1.1f, depth * cellH + 36f));
            var roadImage = road.gameObject.AddComponent<Image>();
            roadImage.sprite = SpriteKit.Round;
            roadImage.type = Image.Type.Sliced;
            roadImage.color = new Color(0.93f, 0.84f, 0.70f, 1f);
            roadImage.raycastTarget = false;

            var piece = _metrics.Piece;
            for (int lane = 0; lane < lanes; lane++)
            {
                var cells = _board.Lanes[lane].Cells;
                for (int cell = 0; cell < cells.Count; cell++)
                {
                    var slot = cells[cell];
                    if (slot.Empty) continue;
                    var tap = new Tap(TapKind.Lane, lane, cell);
                    DrawPiece(LaneAnchor(lane, cell), piece, slot, EyeState.IsOpen(_board, tap), tap);
                }
            }
            for (int s = 0; s < _board.Sides.Count; s++)
            {
                var side = _board.Sides[s];
                if (side.Queue.Count == 0) continue;
                if ((uint)side.Lane >= (uint)lanes) continue;
                var tap = new Tap(TapKind.Side, s, side.Cell);
                DrawCar(SideAnchor(side), piece, side.Queue[0], EyeState.IsOpen(_board, tap), tap, side.Queue.Count);
            }
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
                image.sprite = SpriteKit.Tunnel;
                image.preserveAspect = true;
                image.color = open ? Color.white : new Color(0.70f, 0.70f, 0.74f, 1f);
                if (slot.Queue != null && slot.Queue.Count > 0 && !slot.Queue[0].Hidden)
                {
                    var peek = Place(rt, "Peek", new Vector2(0f, size.y * 0.08f), size * 0.46f);
                    var peekImage = peek.gameObject.AddComponent<Image>();
                    peekImage.sprite = CarArt.For(slot.Queue[0].Color, open);
                    peekImage.preserveAspect = true;
                    peekImage.raycastTarget = false;
                }
                var badge = Place(rt, "Count", new Vector2(0f, -size.y * 0.22f), new Vector2(64f, 48f));
                var badgeImage = badge.gameObject.AddComponent<Image>();
                badgeImage.sprite = SpriteKit.Round;
                badgeImage.type = Image.Type.Sliced;
                badgeImage.color = Cream;
                AddText(badge, slot.TunnelCount.ToString(), 28, Ink, TextAnchor.MiddleCenter);
                if (open) Hook(rt, tap);
                else image.raycastTarget = false;
                return;
            }
            DrawCar(pos, size, slot.Car, open, tap, 0);
        }

        void DrawCar(Vector2 pos, Vector2 size, Car car, bool open, Tap tap, int queueCount)
        {
            if (open)
            {
                var ring = Place(_screen, "Ring", pos, size + new Vector2(14f, 14f));
                var ringImage = ring.gameObject.AddComponent<Image>();
                ringImage.sprite = SpriteKit.Round;
                ringImage.type = Image.Type.Sliced;
                ringImage.color = Gold;
                ringImage.raycastTarget = false;
            }
            var rt = Place(_screen, "Car", pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = car.Hidden ? SpriteKit.Hidden : CarArt.For(car.Color, open);
            image.preserveAspect = true;
            image.color = open ? Color.white : new Color(0.62f, 0.62f, 0.66f, 1f);
            if (queueCount > 1)
            {
                var badge = Place(rt, "Count", new Vector2(size.x * 0.28f, size.y * 0.28f), new Vector2(48f, 40f));
                var badgeImage = badge.gameObject.AddComponent<Image>();
                badgeImage.sprite = SpriteKit.Round;
                badgeImage.type = Image.Type.Sliced;
                badgeImage.color = Cream;
                AddText(badge, queueCount.ToString(), 24, Ink, TextAnchor.MiddleCenter);
            }
            if (open) Hook(rt, tap);
            else image.raycastTarget = false;
        }

        void Hook(RectTransform rt, Tap tap)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            var captured = tap;
            var piece = rt;
            button.onClick.AddListener(delegate { OnTap(captured, piece); });
        }

        void DrawBar()
        {
            var tray = Place(_screen, "Bar", new Vector2(0f, BarY), new Vector2(980f, 130f));
            var trayImage = tray.gameObject.AddComponent<Image>();
            trayImage.sprite = SpriteKit.Round;
            trayImage.type = Image.Type.Sliced;
            trayImage.color = Wood;
            trayImage.raycastTarget = false;
            for (int i = 0; i < MatchClear.BarCap; i++)
            {
                var center = BarSlotCenter(i);
                var cell = Place(tray, "Slot", new Vector2(center.x, 0f), new Vector2(BarSlotWidth - 8f, BarCarH));
                var image = cell.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                if (_board != null && i < _board.Bar.Count)
                {
                    image.sprite = CarArt.For(_board.Bar[i], true);
                    image.preserveAspect = true;
                    image.color = Color.white;
                }
                else
                {
                    image.sprite = SpriteKit.Round;
                    image.type = Image.Type.Sliced;
                    image.color = Slot;
                }
            }
        }

        void DrawBoosters()
        {
            float y = -810f;
            Booster("Undo", Economy.UndoCost, -390f, y, _history.Count > 0, OnUndo);
            Booster("Shuffle", Economy.ShuffleCost, -130f, y, _board != null && BoardQuery.Total(_board) >= 2, OnShuffle);
            Booster("Super", Economy.SuperUndoCost, 130f, y, _history.Count > 0, OnSuper);
            Booster("Magnet", Economy.MagnetCost, 390f, y, CanMagnet(), OnMagnet);
        }

        void Booster(string name, int cost, float x, float y, bool legal, UnityEngine.Events.UnityAction action)
        {
            bool afford = Economy.CanAfford(Purse.Wallet, cost);
            var button = MakeButton(new Vector2(x, y), new Vector2(230f, 130f), new Color(0.22f, 0.45f, 0.82f, 1f), name, 32, action);
            button.interactable = legal && afford && !_busy && !_driving && _board != null && !_board.Won && !_board.Failed;
            var costLabel = Label(new Vector2(x, y - 88f), new Vector2(220f, 36f), cost.ToString(), 26, Gold, TextAnchor.MiddleCenter);
            costLabel.raycastTarget = false;
        }

        bool CanMagnet()
        {
            if (_board == null) return false;
            var hist = BoardQuery.Histogram(_board);
            for (int i = 0; i < hist.Length; i++)
                if (hist[i] >= MatchClear.Set) return true;
            return false;
        }

        void OnTap(Tap tap, RectTransform piece)
        {
            if (_busy || _driving || _phase != Phase.Play || _board == null) return;
            if (!EyeState.IsOpen(_board, tap) || _metrics.Lanes <= 0) return;
            int slot = _board.Bar.Count;
            if (slot >= MatchClear.BarCap) return;
            var points = BuildDrivePath(tap, slot);
            if (piece != null)
            {
                points[0] = piece.anchoredPosition;
                var button = piece.GetComponent<Button>();
                if (button != null) button.interactable = false;
            }
            bool hidden = PieceHidden(tap);
            var color = ExitPath.ColorOf(_board, tap);
            var size = piece != null ? piece.sizeDelta : _metrics.Piece;
            HidePiece(piece);
            var flyer = SpawnFlyer(color, hidden, points[0], size);
            LockInput();
            StartCoroutine(Drive(flyer, points, tap, color, hidden));
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

        List<Vector2> BuildDrivePath(Tap tap, int slotIndex)
        {
            var points = new List<Vector2>(8);
            int lane;
            int cell;
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
            for (int c = cell - 1; c >= 0; c--)
                points.Add(LaneAnchor(lane, c));
            var mouth = LaneAnchor(lane, 0);
            points.Add(new Vector2(mouth.x, mouth.y - _metrics.CellH));
            points.Add(BarSlotCenter(slotIndex));
            return points;
        }

        RectTransform SpawnFlyer(CarColor color, bool hidden, Vector2 pos, Vector2 size)
        {
            var rt = Place(_screen, "Flyer", pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = hidden ? SpriteKit.Hidden : CarArt.For(color, true);
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
            var previous = rt.parent.GetChild(index - 1);
            if (previous != null && previous.name == "Ring")
                previous.gameObject.SetActive(false);
        }

        void LockInput()
        {
            _driving = true;
            var buttons = _screen.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = false;
        }

        IEnumerator Drive(RectTransform flyer, List<Vector2> points, Tap tap, CarColor color, bool hidden)
        {
            var startSize = flyer.sizeDelta;
            var endSize = new Vector2(BarSlotWidth - 8f, BarCarH);
            var image = flyer.GetComponent<Image>();
            Vector2 facing;
            Along(points, 0f, out facing);
            Aim(flyer, image, color, hidden, facing);
            float elapsed = 0f;
            while (elapsed < DriveSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.05f) dt = 0.05f;
                elapsed += dt;
                float u = Mathf.Clamp01(elapsed / DriveSeconds);
                float eased = u * u * (3f - 2f * u);
                flyer.anchoredPosition = Along(points, eased, out facing);
                flyer.sizeDelta = Vector2.Lerp(startSize, endSize, eased);
                Aim(flyer, image, color, hidden, facing);
                yield return null;
            }
            flyer.anchoredPosition = points[points.Count - 1];
            flyer.sizeDelta = endSize;
            Along(points, 1f, out facing);
            Aim(flyer, image, color, hidden, facing);
            var move = BoardRules.Play(_board, _history, tap);
            _driving = false;
            _lastMatch = move.Matched;
            if (!move.Accepted)
            {
                ShowPlay();
                yield break;
            }
            if (move.Won) EnterWin();
            else if (move.Failed) ShowFail(null);
            else ShowPlay();
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
            if (!Ready(Economy.UndoCost)) return;
            Board restored;
            if (!BoosterKit.Undo(_history, out restored)) return;
            if (!Purse.TrySpend(Economy.UndoCost)) return;
            _board = restored;
            _lastMatch = false;
            ShowPlay();
        }

        void OnSuper()
        {
            if (!Ready(Economy.SuperUndoCost)) return;
            Board restored;
            if (!BoosterKit.SuperUndo(_history, out restored)) return;
            if (!Purse.TrySpend(Economy.SuperUndoCost)) return;
            _board = restored;
            _lastMatch = false;
            ShowPlay();
        }

        void OnShuffle()
        {
            if (!Ready(Economy.ShuffleCost)) return;
            _history.Add(_board.Clone());
            if (!BoosterKit.Shuffle(_board, new System.Random()))
            {
                _history.RemoveAt(_history.Count - 1);
                return;
            }
            if (!Purse.TrySpend(Economy.ShuffleCost))
            {
                Board back;
                BoosterKit.Undo(_history, out back);
                _board = back;
                return;
            }
            _lastMatch = false;
            ShowPlay();
        }

        void OnMagnet()
        {
            if (!Ready(Economy.MagnetCost)) return;
            _history.Add(_board.Clone());
            if (!BoosterKit.Magnet(_board))
            {
                _history.RemoveAt(_history.Count - 1);
                return;
            }
            if (!Purse.TrySpend(Economy.MagnetCost))
            {
                Board back;
                BoosterKit.Undo(_history, out back);
                _board = back;
                return;
            }
            _lastMatch = true;
            if (_board.Won) EnterWin();
            else ShowPlay();
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
                Purse.Grant(Economy.Pay(true));
                Purse.Advance(_levels.Count);
            }
            ShowWin();
        }

        void ShowWin()
        {
            _phase = Phase.Win;
            _busy = false;
            ClearScreen();
            Backdrop();
            var mascot = Place(_screen, "Mascot", new Vector2(0f, 240f), new Vector2(480f, 480f));
            var pic = mascot.gameObject.AddComponent<Image>();
            pic.sprite = PaintedMascot(MascotArt.Happy);
            pic.preserveAspect = true;
            pic.raycastTarget = false;
            string title = _level != null ? _level.Title + " is clear" : "Lot clear";
            Label(new Vector2(0f, -120f), new Vector2(900f, 80f), title, 48, Cream, TextAnchor.MiddleCenter);
            Label(new Vector2(0f, -210f), new Vector2(700f, 60f), "+" + Economy.Pay(true) + " coins", 40, Gold, TextAnchor.MiddleCenter);
            bool more = _index < _levels.Count - 1;
            if (more)
                MakeButton(new Vector2(0f, -400f), new Vector2(640f, 150f), Green, "Next", 56, OnNext);
            else
                Label(new Vector2(0f, -340f), new Vector2(800f, 50f), "That's the whole street for v1.", 30, Cream, TextAnchor.MiddleCenter);
            MakeButton(new Vector2(0f, more ? -590f : -500f), new Vector2(420f, 110f), new Color(0.20f, 0.24f, 0.34f, 0.9f), "Home", 36, OnHome);
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
            ShowPlay();
        }

        void ShowFail(string status)
        {
            _phase = Phase.Fail;
            ClearScreen();
            Backdrop();
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
            ShowPlay();
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

        void Backdrop()
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

        void Hud(float y)
        {
            Pill(new Vector2(-250f, y), SpriteKit.Coin, Purse.Wallet.Coins.ToString(), true);
            Pill(new Vector2(250f, y), SpriteKit.Heart, LivesLine(), false);
        }

        void Pill(Vector2 pos, Sprite icon, string value, bool coins)
        {
            var rt = Place(_screen, coins ? "Coins" : "Lives", pos, new Vector2(340f, 88f));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = SpriteKit.Round;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.12f, 0.14f, 0.20f, 0.78f);
            image.raycastTarget = false;
            var mark = Place(rt, "Icon", new Vector2(-110f, 0f), new Vector2(56f, 56f));
            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.sprite = icon;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            var text = AddText(rt, value, 32, Cream, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(70f, 0f);
            text.rectTransform.offsetMax = new Vector2(-16f, 0f);
            if (coins) _coinText = text;
            else _livesText = text;
        }

        Text Label(Vector2 pos, Vector2 size, string value, int fontSize, Color color, TextAnchor anchor)
        {
            var rt = Place(_screen, "Label", pos, size);
            return AddText(rt, value, fontSize, color, anchor);
        }

        Button MakeButton(Vector2 pos, Vector2 size, Color color, string value, int fontSize, UnityEngine.Events.UnityAction action)
        {
            var shadow = Place(_screen, "Shadow", pos + new Vector2(0f, -8f), size);
            var shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.sprite = SpriteKit.Round;
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0f, 0f, 0f, 0.25f);
            shadowImage.raycastTarget = false;
            var rt = Place(_screen, "Button", pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = SpriteKit.Round;
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            button.colors = colors;
            AddText(rt, value, fontSize, Color.white, TextAnchor.MiddleCenter);
            if (action != null) button.onClick.AddListener(action);
            return button;
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
