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
        Button _playButton;
        UiShake _shake;
        readonly List<RectTransform> _barCars = new List<RectTransform>();
        readonly Dictionary<string, bool> _eyes = new Dictionary<string, bool>();

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
            Clouds();
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
            _eyes.Clear();
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
            AudioManager.Music(AudioManager.GameMusic);
            Backdrop();
            var safe = HudPlay();
            string title = _level != null ? "Level " + _level.Number : "Level";
            LevelWordArt(title);
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
            // Tight packing: thin painted lines between lanes instead of wide gaps.
            float gutter = sides ? 130f : 12f;
            float gap = 8f;
            float availW = 1064f - gutter * 2f;
            float cellW = (availW - gap * (lanes - 1)) / lanes;
            if (cellW > 196f) cellW = 196f;
            float cellH = cellW * 1.04f;
            float availH = 1000f;
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

        // The playfield as a real lot: asphalt across the full width, painted lane lines, stop bars
        // at each lane mouth, and an exit road along the bottom that every car drives out on.
        void DrawLot(int lanes, int depth, float boardW, float gutter)
        {
            float top = _metrics.BaseY + (depth - 0.5f) * _metrics.CellH + 40f;
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

        // Eased slide of every bar car at or after 'from' by 'by' slots (+1 opens a gap).
        IEnumerator SlideBar(int from, int by, float seconds)
        {
            var moving = new List<RectTransform>();
            var start = new List<float>();
            for (int i = from; i < _barCars.Count; i++)
            {
                if (_barCars[i] == null) continue;
                moving.Add(_barCars[i]);
                start.Add(_barCars[i].anchoredPosition.x);
            }
            float t = 0f;
            float dx = by * (BarSlotWidth + BarGap);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                float k = 1f - (1f - u) * (1f - u) * (1f - u);
                for (int i = 0; i < moving.Count; i++)
                    if (moving[i] != null)
                        moving[i].anchoredPosition = new Vector2(start[i] + dx * k, moving[i].anchoredPosition.y);
                yield return null;
            }
        }

        IEnumerator ClearSet(int from)
        {
            var set = new List<RectTransform>();
            for (int i = from; i < from + MatchClear.Set && i < _barCars.Count; i++) set.Add(_barCars[i]);
            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.22f);
                float k = u < 0.4f ? 1f + 0.25f * (u / 0.4f) : 1.25f * (1f - (u - 0.4f) / 0.6f);
                for (int i = 0; i < set.Count; i++) if (set[i] != null) set[i].localScale = new Vector3(k, k, 1f);
                yield return null;
            }
            for (int i = 0; i < set.Count; i++) if (set[i] != null) set[i].gameObject.SetActive(false);
            _barCars.RemoveRange(from, Mathf.Min(MatchClear.Set, _barCars.Count - from));
            yield return SlideBar(from, -MatchClear.Set, 0.24f);
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
                CountChip(rt, new Vector2(size.x * 0.34f, size.y * 0.36f), slot.TunnelCount);
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
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = car.Hidden ? HiddenSprite() : CarArt.For(car.Color, open);
            image.preserveAspect = true;
            image.color = open ? Color.white : new Color(0.62f, 0.62f, 0.66f, 1f);
            AnimateEyes(rt, image, car, open, tap);
            if (queueCount > 1)
            {
                CountChip(rt, new Vector2(size.x * 0.34f, size.y * 0.34f), queueCount);
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
                if (_busy || _driving) return;
                AudioManager.Play(Sfx.Blocked);
#if !UNITY_WEBGL
                Haptics.Thud();
#endif
                StartCoroutine(Wiggle(piece));
            });
        }

        static IEnumerator Wiggle(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.32f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 50f) * 7f * (1f - t / 0.32f));
                yield return null;
            }
            if (rt != null) rt.localRotation = Quaternion.identity;
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

        void DrawBar()
        {
            _barCars.Clear();
            var trayArt = UiArt.Get("tray");
            var tray = Place(_screen, "Bar", new Vector2(0f, BarY - 6f), trayArt != null ? new Vector2(1060f, 210f) : new Vector2(980f, 130f));
            var trayImage = tray.gameObject.AddComponent<Image>();
            trayImage.sprite = trayArt != null ? trayArt : SpriteKit.Round;
            trayImage.type = trayArt != null ? Image.Type.Simple : Image.Type.Sliced;
            trayImage.color = trayArt != null ? Color.white : Wood;
            trayImage.raycastTarget = false;
            var slotArt = UiArt.Get("slot");
            for (int i = 0; i < MatchClear.BarCap; i++)
            {
                var center = BarSlotCenter(i);
                var cell = Place(tray, "Slot", new Vector2(center.x, trayArt != null ? 6f : 0f), new Vector2(BarSlotWidth - 8f, BarCarH));
                if (slotArt != null)
                {
                    var well = cell.gameObject.AddComponent<Image>();
                    well.sprite = slotArt;
                    well.raycastTarget = false;
                    cell = Place(cell, "Car", Vector2.zero, new Vector2(BarSlotWidth + 4f, BarCarH + 8f));
                }
                var image = cell.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                if (_board != null && i < _board.Bar.Count)
                {
                    image.sprite = CarArt.For(_board.Bar[i], true);
                    _barCars.Add(cell);
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
            float y = -820f;
            Booster("Undo", Economy.UndoCost, -390f, y, _history.Count > 0, OnUndo);
            Booster("Shuffle", Economy.ShuffleCost, -130f, y, _board != null && BoardQuery.Total(_board) >= 2, OnShuffle);
            Booster("Super", Economy.SuperUndoCost, 130f, y, _history.Count > 0, OnSuper);
            Booster("Magnet", Economy.MagnetCost, 390f, y, CanMagnet(), OnMagnet);
        }

        void Booster(string name, int cost, float x, float y, bool legal, UnityEngine.Events.UnityAction action)
        {
            bool afford = Economy.CanAfford(Purse.Wallet, cost);
            var button = MakeButton(new Vector2(x, y), new Vector2(220f, 200f), new Color(0.22f, 0.45f, 0.82f, 1f), name, 40, action, "btn_booster");
            button.interactable = legal && afford && !_busy && !_driving && _board != null && !_board.Won && !_board.Failed;
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
            if (_board.Bar.Count >= MatchClear.BarCap) return;
            // Grouped bar: the car goes right after its color; cars to the right slide over.
            int slot = MatchClear.InsertIndex(_board.Bar, ExitPath.ColorOf(_board, tap));
            StartCoroutine(SlideBar(slot, 1, 0.22f));
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
            AudioManager.CarTap();
            AudioManager.Play(Sfx.DriveOut);
#if !UNITY_WEBGL
            Haptics.Tap();
#endif
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

        void LockInput()
        {
            _driving = true;
            var buttons = _screen.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = false;
        }

        IEnumerator Drive(RectTransform flyer, List<Vector2> points, Tap tap, CarColor color, bool hidden)
        {
            // Round the lane corners so the car carves a curve instead of snapping at waypoints.
            points = Smooth(points, 3);
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
            float lineClock = 0f;
            Vector2 last = flyer.anchoredPosition;
            while (elapsed < DriveSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.05f) dt = 0.05f;
                elapsed += dt;
                float u = Mathf.Clamp01(elapsed / DriveSeconds);
                float eased = u * u * (3f - 2f * u);
                var pos = Along(points, eased, out facing);
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
                lineClock += dt;
                if (lineClock > 0.04f && speed > 900f)
                {
                    lineClock = 0f;
                    SpeedLine(pos - facing.normalized * flyer.sizeDelta.y * 0.45f, facing);
                }
                yield return null;
            }
            flyer.anchoredPosition = points[points.Count - 1];
            flyer.sizeDelta = endSize;
            Steer(flyer, image, color, hidden, 0f, 0f, 1f, 1f);
            flyer.localScale = Vector3.one;
            var move = BoardRules.Play(_board, _history, tap);
            _driving = false;
            _lastMatch = move.Matched;
            AudioManager.Play(Sfx.SlotArrive);
            if (move.Matched && move.ClearedFrom >= 0)
            {
                // The three pop out, then everything to their right slides left to close the gap.
                _barCars.Insert(Mathf.Clamp(move.BarIndex, 0, _barCars.Count), flyer);
                yield return ClearSet(move.ClearedFrom);
            }
            if (move.Matched)
            {
                AudioManager.Play(Sfx.MatchClear);
#if !UNITY_WEBGL
                Haptics.Success();
#endif
                if (_shake != null) _shake.Punch(0.22f, 9f, 0.6f);
            }
            if (!move.Accepted)
            {
                ShowPlay();
                yield break;
            }
            if (move.Won) EnterWin();
            else if (move.Failed) ShowFail(null);
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
            if (!Ready(Economy.UndoCost)) return;
            Board restored;
            if (!BoosterKit.Undo(_history, out restored)) return;
            if (!Purse.TrySpend(Economy.UndoCost)) return;
            AudioManager.Play(Sfx.BoosterUndo);
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
            AudioManager.Play(Sfx.BoosterSuper);
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
            AudioManager.Play(Sfx.BoosterShuffle);
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
            AudioManager.Play(Sfx.BoosterMagnet);
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
            int pay = Economy.Pay(true);
            WinShow.Build(this, _screen, new WinShow.Setup
            {
                LevelName = _level != null ? "Level " + _level.Number + " \u00B7 " + _level.Title : "Lot clear",
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
            ShowPlay();
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
                plusButton.onClick.AddListener(OnPlus);
            }
            if (coins) _coinText = text;
            else _livesText = text;
        }

        void OnPlus()
        {
            AudioManager.Play(Sfx.Click);
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
