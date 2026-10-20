using UnityEngine;

namespace CarMatch
{
    // Original procedural placeholders. Nothing here is traced from another game.
    public static class SpriteKit
    {
        static Sprite _round;
        static Sprite _hidden;
        static Sprite _tunnel;
        static Sprite _mascot;
        static Sprite _coin;
        static Sprite _heart;
        static Sprite _sky;
        static Sprite[] _cars;

        public static Sprite Round => _round;
        public static Sprite Hidden => _hidden;
        public static Sprite Tunnel => _tunnel;
        public static Sprite Mascot => _mascot;
        public static Sprite Coin => _coin;
        public static Sprite Heart => _heart;
        public static Sprite Sky => _sky;

        public static Sprite Car(CarColor color)
        {
            Warm();
            int i = (int)color;
            if (_cars == null || (uint)i >= (uint)_cars.Length) return _round;
            return _cars[i];
        }

        public static void Warm()
        {
            if (_round != null) return;
            _round = Make(RoundPixels(96, 96, 28), 96, 96, 28f);
            _hidden = Make(HiddenPixels(), 96, 128);
            _tunnel = Make(TunnelPixels(), 96, 128);
            _mascot = Make(MascotPixels(), 256, 320);
            _coin = Make(CoinPixels(), 64, 64);
            _heart = Make(HeartPixels(), 64, 64);
            _sky = Make(SkyPixels(), 16, 64);
            _cars = new Sprite[Palette.Count];
            for (int i = 0; i < _cars.Length; i++)
                _cars[i] = Make(CarPixels(Body((CarColor)i)), 96, 128);
        }

        public static Color Body(CarColor color)
        {
            switch (color)
            {
                case CarColor.Coral: return new Color(0.89f, 0.36f, 0.31f);
                case CarColor.Amber: return new Color(0.95f, 0.62f, 0.22f);
                case CarColor.Lime: return new Color(0.55f, 0.76f, 0.28f);
                case CarColor.Teal: return new Color(0.16f, 0.70f, 0.64f);
                case CarColor.Violet: return new Color(0.46f, 0.40f, 0.86f);
                case CarColor.Rose: return new Color(0.92f, 0.42f, 0.60f);
                case CarColor.Sky: return new Color(0.28f, 0.62f, 0.90f);
                default: return new Color(0.90f, 0.74f, 0.28f);
            }
        }

        static Color32[] RoundPixels(int w, int h, int rad)
        {
            var px = new Color32[w * h];
            FillRound(px, w, h, 0, 0, w, h, rad, new Color32(255, 255, 255, 255));
            return px;
        }

        static Color32[] CarPixels(Color body)
        {
            const int w = 96;
            const int h = 128;
            var px = new Color32[w * h];
            var paint = C32(body);
            var roof = C32(Color.Lerp(body, Color.white, 0.35f));
            var glass = new Color32(226, 242, 246, 255);
            var ink = new Color32(42, 46, 58, 255);
            var wheel = new Color32(38, 40, 48, 255);
            var hub = new Color32(214, 214, 220, 255);
            FillDisc(px, w, h, 48, 16, 28, new Color32(0, 0, 0, 50));
            FillRound(px, w, h, 14, 22, 68, 78, 22, paint);
            FillRound(px, w, h, 24, 68, 48, 36, 16, roof);
            FillRound(px, w, h, 30, 78, 36, 18, 8, glass);
            FillDisc(px, w, h, 28, 26, 12, wheel);
            FillDisc(px, w, h, 68, 26, 12, wheel);
            FillDisc(px, w, h, 28, 26, 5, hub);
            FillDisc(px, w, h, 68, 26, 5, hub);
            FillDisc(px, w, h, 36, 58, 7, new Color32(255, 255, 255, 255));
            FillDisc(px, w, h, 60, 58, 7, new Color32(255, 255, 255, 255));
            FillDisc(px, w, h, 37, 57, 3, ink);
            FillDisc(px, w, h, 61, 57, 3, ink);
            FillDisc(px, w, h, 48, 46, 3, new Color32(255, 170, 170, 255));
            return px;
        }

        static Color32[] HiddenPixels()
        {
            const int w = 96;
            const int h = 128;
            var px = new Color32[w * h];
            var box = new Color32(88, 96, 112, 255);
            var edge = new Color32(206, 210, 216, 255);
            FillRound(px, w, h, 12, 18, 72, 96, 18, box);
            FillRound(px, w, h, 22, 30, 52, 72, 12, edge);
            // A blocky question mark, so the cover reads with no font.
            var ink = new Color32(48, 54, 66, 255);
            FillRound(px, w, h, 40, 78, 16, 16, 8, ink);
            FillRound(px, w, h, 30, 62, 14, 22, 6, ink);
            FillRound(px, w, h, 52, 62, 14, 22, 6, ink);
            FillRound(px, w, h, 30, 48, 36, 16, 6, ink);
            FillDisc(px, w, h, 48, 40, 5, ink);
            return px;
        }

        static Color32[] TunnelPixels()
        {
            const int w = 96;
            const int h = 128;
            var px = new Color32[w * h];
            var tube = new Color32(70, 128, 196, 255);
            var inner = new Color32(28, 36, 58, 255);
            var rim = new Color32(214, 228, 238, 255);
            FillRound(px, w, h, 10, 16, 76, 100, 28, tube);
            FillRound(px, w, h, 22, 28, 52, 76, 20, inner);
            FillRound(px, w, h, 30, 40, 36, 52, 16, rim);
            FillRound(px, w, h, 38, 48, 20, 36, 10, inner);
            return px;
        }

        static Color32[] MascotPixels()
        {
            const int w = 256;
            const int h = 320;
            var px = new Color32[w * h];
            var teal = new Color32(32, 168, 156, 255);
            var roof = new Color32(242, 176, 64, 255);
            var glass = new Color32(230, 246, 244, 255);
            var ink = new Color32(36, 42, 54, 255);
            var blush = new Color32(244, 140, 150, 255);
            FillDisc(px, w, h, 128, 36, 70, new Color32(0, 0, 0, 40));
            FillRound(px, w, h, 36, 48, 184, 190, 56, teal);
            FillRound(px, w, h, 62, 168, 132, 92, 40, roof);
            FillRound(px, w, h, 78, 196, 100, 46, 18, glass);
            FillDisc(px, w, h, 78, 70, 28, new Color32(42, 44, 52, 255));
            FillDisc(px, w, h, 178, 70, 28, new Color32(42, 44, 52, 255));
            FillDisc(px, w, h, 78, 70, 12, new Color32(230, 230, 234, 255));
            FillDisc(px, w, h, 178, 70, 12, new Color32(230, 230, 234, 255));
            FillDisc(px, w, h, 96, 150, 16, new Color32(255, 255, 255, 255));
            FillDisc(px, w, h, 160, 150, 16, new Color32(255, 255, 255, 255));
            FillDisc(px, w, h, 100, 148, 7, ink);
            FillDisc(px, w, h, 164, 148, 7, ink);
            FillDisc(px, w, h, 128, 118, 8, blush);
            FillDisc(px, w, h, 70, 128, 10, blush);
            FillDisc(px, w, h, 186, 128, 10, blush);
            return px;
        }

        static Color32[] CoinPixels()
        {
            var px = new Color32[64 * 64];
            FillDisc(px, 64, 64, 32, 32, 26, new Color32(232, 176, 48, 255));
            FillDisc(px, 64, 64, 32, 32, 16, new Color32(255, 214, 96, 255));
            return px;
        }

        static Color32[] HeartPixels()
        {
            var px = new Color32[64 * 64];
            var red = new Color32(226, 64, 86, 255);
            FillDisc(px, 64, 64, 22, 38, 14, red);
            FillDisc(px, 64, 64, 42, 38, 14, red);
            FillRound(px, 64, 64, 14, 12, 36, 28, 8, red);
            return px;
        }

        static Color32[] SkyPixels()
        {
            const int w = 16;
            const int h = 64;
            var px = new Color32[w * h];
            var top = new Color32(36, 52, 92, 255);
            var mid = new Color32(244, 176, 122, 255);
            var road = new Color32(176, 104, 78, 255);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color32 c = t > 0.28f
                    ? Lerp(mid, top, (t - 0.28f) / 0.72f)
                    : Lerp(road, mid, t / 0.28f);
                for (int x = 0; x < w; x++)
                    px[y * w + x] = c;
            }
            return px;
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }

        static Color32 C32(Color c)
        {
            return new Color32(
                (byte)(Mathf.Clamp01(c.r) * 255f),
                (byte)(Mathf.Clamp01(c.g) * 255f),
                (byte)(Mathf.Clamp01(c.b) * 255f),
                255);
        }

        static void FillRound(Color32[] px, int w, int h, int x, int y, int rw, int rh, int rad, Color32 c)
        {
            if (rad < 1) rad = 1;
            if (rad * 2 > rw) rad = rw / 2;
            if (rad * 2 > rh) rad = rh / 2;
            int r2 = rad * rad;
            for (int yy = 0; yy < rh; yy++)
            {
                for (int xx = 0; xx < rw; xx++)
                {
                    int dx = 0;
                    int dy = 0;
                    if (xx < rad) dx = rad - xx;
                    else if (xx >= rw - rad) dx = xx - (rw - rad - 1);
                    if (yy < rad) dy = rad - yy;
                    else if (yy >= rh - rad) dy = yy - (rh - rad - 1);
                    if (dx * dx + dy * dy > r2) continue;
                    Set(px, w, h, x + xx, y + yy, c);
                }
            }
        }

        static void FillDisc(Color32[] px, int w, int h, int cx, int cy, int rad, Color32 c)
        {
            int r2 = rad * rad;
            for (int y = -rad; y <= rad; y++)
            {
                for (int x = -rad; x <= rad; x++)
                {
                    if (x * x + y * y > r2) continue;
                    Set(px, w, h, cx + x, cy + y, c);
                }
            }
        }

        static void Set(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
            px[y * w + x] = c;
        }

        static Sprite Make(Color32[] px, int w, int h, float border = 0f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var edge = new Vector4(border, border, border, border);
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                edge);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
