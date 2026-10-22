using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // Small uGUI motion pieces for the home, play and win screens. All run on unscaled time.

    // Idle bob with a soft squash at the bottom of the bob.
    public sealed class Bob : MonoBehaviour
    {
        public float Height = 14f;
        public float Speed = 2.1f;
        Vector2 _rest; float _t; bool _set;
        void Update()
        {
            var rt = (RectTransform)transform;
            if (!_set) { _rest = rt.anchoredPosition; _set = true; }
            _t += Time.unscaledDeltaTime * Speed;
            float s = Mathf.Sin(_t);
            rt.anchoredPosition = _rest + new Vector2(0f, s * Height);
            float sq = 1f - 0.025f * Mathf.Max(0f, -s);
            rt.localScale = new Vector3(2f - sq, sq, 1f);
        }
    }

    // Swaps to a blink sprite for ~0.12 s every 2.5-5 s (sometimes a double blink).
    public sealed class Blinker : MonoBehaviour
    {
        public Sprite Open;
        public Sprite Closed;
        Image _img; float _next; float _until; int _double;
        void Start() { _img = GetComponent<Image>(); _next = Time.unscaledTime + Random.Range(1.2f, 3f); }
        void Update()
        {
            if (_img == null || Closed == null) return;
            float now = Time.unscaledTime;
            if (_until > 0f && now >= _until)
            {
                _img.sprite = Open; _until = 0f;
                if (_double > 0) { _double--; _next = now + 0.14f; }
                else _next = now + Random.Range(2.5f, 5f);
            }
            if (_until == 0f && now >= _next)
            {
                _img.sprite = Closed; _until = now + 0.12f;
                if (_double == 0 && Random.value < 0.25f) _double = 1;
            }
        }
    }

    // Parked trucks feel alive. Awake (movable) trucks look around with eased glances
    // (left, right, eye contact with the player, a peek at the bar), blink every 2-5 s with
    // occasional double-blinks and happy squints, bounce with a 3-4% squash/stretch on a
    // random phase, and puff from the tailpipe every 1.5-3 s. Blocked or sleeping trucks only
    // breathe. Pure view state: no raycasts, no input gating, no per-frame allocations.
    public sealed class CarIdle : MonoBehaviour
    {
        public Sprite Open, Blink, Squint, Wide, LookLeft, LookLeftFar, LookRight, LookRightFar;
        public System.Action<RectTransform> Puff;
        public bool Awake;
        AliveIdle _idle; Image _img; RectTransform _rt; EyeAnim _wake; Vector3 _rest; bool _live;
        static uint _seed = 1;

        void Start()
        {
            _img = GetComponent<Image>();
            _rt = (RectTransform)transform;
            _rest = _rt.localScale;
            _idle = AliveIdle.Create(_seed++ * 7919u + (uint)GetInstanceID(), Awake);
            TryGetComponent(out _wake);
            _live = true;
        }

        void OnDisable() { if (_live && _rt != null) { _rt.localScale = _rest; _rt.localRotation = Quaternion.identity; } }

        void Update()
        {
            if (_img == null) return;
            if (_wake != null) return;              // let the wake-up / doze-off play first
            _idle.Tick(Time.unscaledDeltaTime);
            float up = Awake ? _idle.LookUp : 0f, down = Awake ? _idle.LookDown : 0f;
            float sy = _idle.ScaleY + 0.02f * up - 0.025f * down;
            float sx = _idle.ScaleX - 0.01f * up + 0.012f * down;
            _rt.localScale = new Vector3(_rest.x * sx, _rest.y * sy, 1f);
            if (!Awake) return;
            _rt.localRotation = Quaternion.Euler(0f, 0f, -_idle.Look * 1.6f);
            if (_idle.PuffNow && Puff != null) Puff(_rt);
            Sprite pick;
            switch (_idle.Eyes)
            {
                case IdleEyes.Blink: pick = Blink; break;
                case IdleEyes.Squint: pick = Squint; break;
                case IdleEyes.Wide: pick = Wide; break;
                default:
                    float l = _idle.Look;
                    pick = l < -0.8f ? LookLeftFar : l < -0.45f ? LookLeft : l > 0.8f ? LookRightFar : l > 0.45f ? LookRight : Open;
                    break;
            }
            if (pick == null) pick = Open;
            if (_img.sprite != pick) _img.sprite = pick;
        }
    }

    // Horizontal drift that wraps (clouds) and a small device/time parallax.
    public sealed class Drift : MonoBehaviour
    {
        public float Speed = 12f;
        public float Wrap = 1080f;
        public float Parallax = 10f;
        Vector2 _rest; float _x; bool _set; float _t;
        void Update()
        {
            var rt = (RectTransform)transform;
            if (!_set) { _rest = rt.anchoredPosition; _set = true; }
            _t += Time.unscaledDeltaTime;
            _x += Speed * Time.unscaledDeltaTime;
            if (_x > Wrap) _x -= Wrap * 2f;
            Vector3 tilt = Vector3.zero;
            var acc = UnityEngine.InputSystem.Accelerometer.current;
            if (acc != null)
            {
                if (!acc.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(acc);
                tilt = acc.acceleration.ReadValue();
            }
            float px = Mathf.Clamp(tilt.x, -1f, 1f) * Parallax + Mathf.Sin(_t * 0.25f) * Parallax * 0.5f;
            rt.anchoredPosition = _rest + new Vector2(_x + px, Mathf.Sin(_t * 0.4f) * 4f);
        }
    }

    // A glossy streak that sweeps across a button every few seconds (masked by the button).
    public sealed class ShineSweep : MonoBehaviour
    {
        public float Every = 3.2f;
        public float Duration = 0.65f;
        RectTransform _streak; float _t; float _width;
        public void Init(Sprite shine, float width, float height)
        {
            _width = width;
            var go = new GameObject("Shine", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _streak = go.GetComponent<RectTransform>();
            _streak.sizeDelta = new Vector2(height * 0.45f, height * 1.4f);
            _streak.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var img = go.AddComponent<Image>();
            img.sprite = shine;
            img.color = new Color(1f, 1f, 1f, 0.55f);
            img.raycastTarget = false;
            _t = Every - 0.8f;
            Place(-10f);
        }
        void Place(float u) { if (_streak != null) _streak.anchoredPosition = new Vector2(Mathf.Lerp(-_width * 0.6f, _width * 0.6f, u), 0f); }
        void Update()
        {
            if (_streak == null) return;
            _t += Time.unscaledDeltaTime;
            if (_t > Every) _t -= Every;
            float u = _t / Duration;
            _streak.gameObject.SetActive(u <= 1f);
            if (u <= 1f) Place(u * u * (3f - 2f * u));
        }
    }

    // Plays a sprite sequence once with a squash-bounce (eyes opening / closing).
    public sealed class EyeAnim : MonoBehaviour
    {
        public Sprite[] Frames;
        public Sprite Final;
        public float FrameTime = 0.045f;
        Image _img; float _t; Vector3 _rest;
        void Start() { _img = GetComponent<Image>(); _rest = transform.localScale; if (_img != null && Frames != null && Frames.Length > 0) _img.sprite = Frames[0]; }
        void Update()
        {
            if (_img == null || Frames == null) { Destroy(this); return; }
            _t += Time.unscaledDeltaTime;
            int i = (int)(_t / FrameTime);
            float total = FrameTime * (Frames.Length + 2);
            float u = Mathf.Clamp01(_t / total);
            // Squash then a damped bounce on the body.
            float b = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u) * 0.07f;
            transform.localScale = new Vector3(_rest.x * (1f - b), _rest.y * (1f + b), 1f);
            if (i < Frames.Length) { if (Frames[i] != null) _img.sprite = Frames[i]; return; }
            if (u >= 1f)
            {
                if (Final != null) _img.sprite = Final;
                transform.localScale = _rest;
                Destroy(this);
            }
        }
    }

    // Fades and scales a puff (dust) then destroys it.
    public sealed class Puff : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Life = 0.45f;
        public float Grow = 1.8f;
        float _t; Image _img; Vector3 _start; float _a0;
        void Start() { _img = GetComponent<Image>(); _start = transform.localScale; _a0 = _img != null ? _img.color.a : 1f; }
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float u = _t / Life;
            if (u >= 1f) { Destroy(gameObject); return; }
            var rt = (RectTransform)transform;
            rt.anchoredPosition += Velocity * Time.unscaledDeltaTime;
            transform.localScale = _start * Mathf.Lerp(1f, Grow, 1f - (1f - u) * (1f - u));
            if (_img != null) { var c = _img.color; c.a = _a0 * (1f - u); _img.color = c; }
        }
    }

    // Plays a sprite sheet once over 'life' seconds (FX hook for Character Creator's sheets).
    public sealed class Flipbook : MonoBehaviour
    {
        Sprite[] _frames; float _fps = 20f; bool _loop; bool _destroyAtEnd; float _t; Image _img;
        public void Init(Sprite[] frames, float life) { Play(frames, frames.Length / Mathf.Max(0.05f, life), false); _destroyAtEnd = false; }
        // One-shot sheets destroy their object after the last frame; looping sheets run until removed.
        public void Play(Sprite[] frames, float fps, bool loop) { _frames = frames; _fps = Mathf.Max(1f, fps); _loop = loop; _destroyAtEnd = !loop; _img = GetComponent<Image>(); }
        void Update()
        {
            if (_frames == null || _img == null) return;
            _t += Time.unscaledDeltaTime;
            int i = (int)(_t * _fps);
            if (i >= _frames.Length)
            {
                if (_loop) i %= _frames.Length;
                else if (_destroyAtEnd) { Destroy(gameObject); return; }
                else i = _frames.Length - 1;
            }
            _img.sprite = _frames[i];
        }
    }

    // Port of Flock Five's CamShake punch for a uGUI root (the screen rect, not a camera).
    public sealed class UiShake : MonoBehaviour
    {
        float _t, _dur, _amp, _twist, _seed;
        public void Punch(float dur, float amp, float twist)
        {
            _dur = Mathf.Max(0.08f, dur); _amp = amp; _twist = twist; _t = _dur; _seed = Random.Range(0f, 80f);
        }
        void LateUpdate()
        {
            if (_t <= 0f) return;
            _t -= Time.unscaledDeltaTime;
            var rt = (RectTransform)transform;
            if (_t <= 0f) { rt.anchoredPosition = Vector2.zero; rt.localRotation = Quaternion.identity; return; }
            float u = Mathf.Clamp01(_t / _dur), decay = u * u, w = Time.unscaledTime * 62f + _seed;
            rt.anchoredPosition = new Vector2(Mathf.Sin(w) * _amp * decay, Mathf.Cos(w * 1.17f) * _amp * 0.72f * decay);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(w * 1.3f) * _twist * decay);
        }
    }

    // 16-heading drive frames and the eye-open frames (Resources/Cars16, Resources/CarEyes).
    public static class CarFrames
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Load(string path)
        {
            Sprite s;
            if (Cache.TryGetValue(path, out s)) return s;
            s = Resources.Load<Sprite>(path);
            Cache[path] = s;
            return s;
        }

        // Screen direction -> heading index 0..15 (0 = facing down/south, clockwise toward east).
        public static float HeadingAngle(Vector2 dir)
        {
            // Angle measured from south (0,-1) toward east (+x).
            float a = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            return a;
        }

        public static Sprite Heading(CarColor color, int index)
        {
            index = ((index % 16) + 16) % 16;
            return Load("Cars16/car_" + EyeState.Token(color) + "_h" + index.ToString("00"));
        }

        // frame 0 = closed, 1-2 lids lifting, 3 wide overshoot, 4 open.
        public static Sprite Eye(CarColor color, int frame)
        {
            return Load("CarEyes/car_" + EyeState.Token(color) + "_eye" + frame + "_h00");
        }
    }
}
