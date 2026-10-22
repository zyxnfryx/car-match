namespace CarMatch
{
    // Pure idle "alive" scheduler for a parked truck. A struct with no allocations:
    // the view calls Tick(dt) each frame and reads the pose. It never touches the board or input,
    // so taps stay instant; the view just stops ticking when the car drives off.
    public enum IdleEyes { Open, Blink, Squint, Wide }
    public enum IdleLook { Ahead, Left, Right, Player, Bar }

    public struct AliveIdle
    {
        public bool Awake;
        // Outputs (read after Tick).
        public float Look;      // -1 (left) .. +1 (right), eased
        public float LookUp;    // 0..1 toward player (wide eyes, small stretch)
        public float LookDown;  // 0..1 glance at the bar (small dip)
        public float ScaleX, ScaleY;
        public IdleEyes Eyes;
        public bool PuffNow;    // true for exactly one tick when a tailpipe puff is due

        uint _rng;
        float _phase, _period, _t;
        float _lookFrom, _lookTo, _lookT, _lookDur, _nextLook;
        float _upTo, _downTo, _upFrom, _downFrom;
        float _nextBlink, _eyesUntil; int _doubleLeft; IdleEyes _held;
        float _nextPuff;

        public const float AwakeAmp = 0.035f, CalmAmp = 0.012f;

        public static AliveIdle Create(uint seed, bool awake)
        {
            var a = new AliveIdle();
            a._rng = seed * 2654435761u + 0x9E3779B9u; if (a._rng == 0) a._rng = 1;
            a.Awake = awake;
            a._period = awake ? a.Range(1.2f, 1.6f) : a.Range(3.2f, 4.2f);
            a._phase = a.Range(0f, 1f);
            a._nextLook = a.Range(0.4f, 1.6f);
            a._nextBlink = a.Range(0.8f, 3f);
            a._nextPuff = a.Range(0.5f, 2.5f);
            a._lookDur = 0.25f; a._lookT = 1f;
            a.ScaleX = a.ScaleY = 1f;
            return a;
        }

        public void SetAwake(bool awake)
        {
            if (awake == Awake) return;
            Awake = awake;
            _period = awake ? Range(1.2f, 1.6f) : Range(3.2f, 4.2f);
            if (!awake) { _lookFrom = Look; _lookTo = 0f; _upFrom = LookUp; _downFrom = LookDown; _upTo = _downTo = 0f; _lookT = 0f; _lookDur = 0.3f; }
        }

        public void Tick(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            _t += dt;
            PuffNow = false;

            // Body: bouncy squash/stretch (awake) or slow breathing (calm), random phase per truck.
            float amp = Awake ? AwakeAmp : CalmAmp;
            float w = (_t / _period + _phase) * 6.2831853f;
            float s = Sin(w);
            // Sharper bounce at the bottom when awake: a little extra squash on the down beat.
            float sq = Awake && s < 0f ? s * (1f + 0.35f * -s) : s;
            ScaleY = 1f + amp * sq;
            ScaleX = 1f - amp * 0.8f * sq;

            // Eased look travel.
            _lookT += dt / _lookDur;
            float u = _lookT >= 1f ? 1f : _lookT;
            float e = u * u * (3f - 2f * u);
            Look = _lookFrom + (_lookTo - _lookFrom) * e;
            LookUp = _upFrom + (_upTo - _upFrom) * e;
            LookDown = _downFrom + (_downTo - _downFrom) * e;

            if (Awake)
            {
                if (_t >= _nextLook) PickLook();
                if (_t >= _nextPuff) { PuffNow = true; _nextPuff = _t + Range(1.5f, 3f); }
            }

            // Eyes: blinks every 2-5 s, sometimes double; awake trucks sometimes squint happily.
            if (_eyesUntil > 0f && _t >= _eyesUntil)
            {
                _eyesUntil = 0f; Eyes = IdleEyes.Open;
                if (_doubleLeft > 0) { _doubleLeft--; _nextBlink = _t + 0.12f; }
                else _nextBlink = _t + Range(2f, 5f);
            }
            if (_eyesUntil == 0f && _t >= _nextBlink)
            {
                float r = Next01();
                if (Awake && _doubleLeft == 0 && r < 0.12f) { _held = IdleEyes.Squint; _eyesUntil = _t + Range(0.45f, 0.8f); }
                else
                {
                    _held = IdleEyes.Blink; _eyesUntil = _t + 0.11f;
                    if (_doubleLeft == 0 && r > 0.78f) _doubleLeft = 1;
                }
                Eyes = _held;
            }
            if (_eyesUntil == 0f) Eyes = Awake && LookUp > 0.5f ? IdleEyes.Wide : IdleEyes.Open;
        }

        void PickLook()
        {
            _lookFrom = Look; _upFrom = LookUp; _downFrom = LookDown;
            _lookT = 0f;
            _lookDur = Range(0.16f, 0.32f);
            _upTo = 0f; _downTo = 0f;
            float r = Next01();
            if (r < 0.25f) _lookTo = -Range(0.5f, 1f);
            else if (r < 0.5f) _lookTo = Range(0.5f, 1f);
            else if (r < 0.68f) { _lookTo = 0f; _upTo = 1f; }
            else if (r < 0.84f) { _lookTo = Range(-0.3f, 0.3f); _downTo = 1f; }
            else _lookTo = 0f;
            _nextLook = _t + Range(0.7f, 2f);
        }

        public static IdleLook Classify(float look, float up, float down)
        {
            if (up > 0.5f) return IdleLook.Player;
            if (down > 0.5f) return IdleLook.Bar;
            if (look < -0.35f) return IdleLook.Left;
            if (look > 0.35f) return IdleLook.Right;
            return IdleLook.Ahead;
        }

        uint NextU() { uint x = _rng; x ^= x << 13; x ^= x >> 17; x ^= x << 5; _rng = x; return x; }
        float Next01() { return (NextU() >> 8) * (1f / 16777216f); }
        float Range(float a, float b) { return a + (b - a) * Next01(); }
        static float Sin(float x) { return (float)System.Math.Sin(x); }
    }
}
