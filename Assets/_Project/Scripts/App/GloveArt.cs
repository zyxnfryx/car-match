using UnityEngine;

namespace CarMatch
{
    // Flock Five tutorial glove, ported verbatim from FlockFiveApp.Coach.cs (flock-five pr217 @ 977d43e).
    // Procedurally rastered; same shape table, curl poses, shading and fingertip as Flock Five.
    public static class GloveArt
    {

        const int GloveW = 220;
        const int GloveH = 300;
        // Rest curl, then three presses. The step is about one screen pixel, so the bend eases.
        const int GloveCurlSteps = 4;
        const float GloveIndexBowRest = -14f;
        const float GloveIndexBowPress = -20f;

        static Texture2D[] _gloveCurls;
        static float[] _gloveBodyDist;
        static Color32[] _gloveRestPx;
        static float _gloveIndexBow = GloveIndexBowRest;
        static float _gloveTipU = 0.39f, _gloveTipV = 0.87f;

        // ---- Lot Loop entry points (the raster below is untouched) ----
        public static Texture2D Rest() { return CoachGloveTex(); }
        public static Texture2D Curl(float dip) { return CoachGloveCurl(dip); }
        // Fingertip in UV space (finger up, body below). Valid once a pose is rastered.
        public static float TipU { get { return _gloveTipU; } }
        public static float TipV { get { return _gloveTipV; } }
        public static float Aspect { get { return GloveW / (float)GloveH; } }
        // Splash warm-up: a band of rows per frame so the first lesson does not hitch.
        public static bool Warm(int rows) { return GloveWarmRows(rows); }

        // Outer silhouette. Ends and radii stay put, so the index tip (GloveTip reach 24),
        // the pivot, and the texture span stay put. Bow is the peak sagitta in pixels, to
        // the left of base→tip. The index bow is negative so it arches over the other
        // fingers, flat at the tip. A press deepens that bow. The thumb arches inward.
        // The pinky root overlaps the palm and sits against the ring.
        struct CoachCap
        {
            public float X0, Y0, X1, Y1, R0, R1, Bow, Curl;
            public CoachCap(float x0, float y0, float x1, float y1, float r0, float r1, float bow = 0f, float curl = 0f)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
                R0 = r0;
                R1 = r1;
                Bow = bow;
                Curl = curl;
            }
        }

        static readonly CoachCap[] _coachCaps =
        {
            new CoachCap(68f, 38f, 152f, 38f, 38f, 38f),
            new CoachCap(88f, 66f, 138f, 104f, 42f, 42f),
            new CoachCap(116f, 94f, 116f, 94f, 40f, 40f),
            new CoachCap(78f, 100f, 26f, 158f, 26f, 27f, -7f, -1f),
            new CoachCap(176f, 104f, 204f, 164f, 17f, 14f, -2.4f, 1f),
            new CoachCap(156f, 110f, 166f, 158f, 21f, 20f, -6f, 1f),
            new CoachCap(128f, 114f, 134f, 180f, 23f, 22f, -8f, 1f),
            new CoachCap(98f, 116f, 88f, 236f, 26f, 24f, -14f, 2f)
        };

        static Texture2D CoachGloveTex()
        {
            var rest = CoachGloveCurl(0f);
            if (_gloveCurls == null) return rest;
            for (int i = 1; i < GloveCurlSteps; i++)
            {
                if (_gloveCurls[i] == null)
                    CoachGloveCurl(i / (float)(GloveCurlSteps - 1));
            }
            return rest;
        }

        // dip 0 is the resting point. dip 1 curls the index a little further over the other fingers.
        static Texture2D CoachGloveCurl(float dip)
        {
            if (_gloveCurls == null) _gloveCurls = new Texture2D[GloveCurlSteps];
            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(dip) * (GloveCurlSteps - 1)), 0, GloveCurlSteps - 1);
            var cached = _gloveCurls[step];
            if (cached != null) return cached;
            if (step != 0 && _gloveRestPx == null)
            {
                CoachGloveCurl(0f);
                return CoachGloveCurl(dip);
            }
            EnsureGloveBody();
            _gloveIndexBow = Mathf.Lerp(GloveIndexBowRest, GloveIndexBowPress, step / (float)(GloveCurlSteps - 1));
            var tex = RasterCoachGlove(step == 0);
            _gloveCurls[step] = tex;
            GloveTip(98f, 116f, 88f, 236f, 13f, 11f, GloveW, GloveH);
            return tex;
        }

        static void EnsureGloveBody()
        {
            if (_gloveBodyDist != null) return;
            _gloveWarmBody = null;
            var body = new float[GloveW * GloveH];
            for (int y = 0; y < GloveH; y++)
            {
                int row = y * GloveW;
                float fy = y + 0.5f;
                for (int x = 0; x < GloveW; x++)
                    body[row + x] = GloveBodyRaw(x + 0.5f, fy);
            }
            _gloveBodyDist = body;
        }

        static Texture2D RasterCoachGlove(bool rest)
        {
            var px = new Color32[GloveW * GloveH];
            RasterGloveRows(px, rest, 0, GloveH);
            return FinishGloveTex(px, rest);
        }

        // Rows [y0, y1) of one glove pose. The whole-texture raster and the splash
        // warm-up both go through here, so a warmed glove is the same pixels.
        static void RasterGloveRows(Color32[] px, bool rest, int y0, int y1)
        {
            for (int y = y0; y < y1; y++)
            {
                int row = y * GloveW;
                float fy = y + 0.5f;
                for (int x = 0; x < GloveW; x++)
                {
                    if (!rest && GloveIndexFar(x + 0.5f, fy))
                        px[row + x] = _gloveRestPx[row + x];
                    else
                        px[row + x] = GlovePixel(x + 0.5f, fy);
                }
            }
        }

        static Texture2D FinishGloveTex(Color32[] px, bool rest)
        {
            var tex = new Texture2D(GloveW, GloveH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = rest ? "CoachGlove" : "CoachGloveCurl"
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            if (rest) _gloveRestPx = px;
            return tex;
        }

        // ---- splash warm-up for the shared glove ----
        // The first glove used to raster the body field plus all four curl poses
        // (5 x 66k pixels) on the frame the hand first showed. The splash now does
        // it a band of rows per frame, in the same order and with the same functions.
        // A tutorial that starts first still gets the glove synchronously, as before.
        static float[] _gloveWarmBody;
        static Color32[] _gloveWarmPx;
        static int _gloveWarmFor = -1;
        static int _gloveWarmRow;

        public static bool GloveWarmDone
        {
            get
            {
                if (_gloveBodyDist == null || _gloveCurls == null) return false;
                for (int i = 0; i < GloveCurlSteps; i++)
                    if (_gloveCurls[i] == null) return false;
                return true;
            }
        }

        // Rasters up to `rows` rows of whatever the glove still needs. True while work remains.
        static bool GloveWarmRows(int rows)
        {
            if (rows < 1) rows = 1;
            if (_gloveBodyDist == null)
            {
                if (_gloveWarmBody == null)
                {
                    _gloveWarmBody = new float[GloveW * GloveH];
                    _gloveWarmRow = 0;
                }
                int end = Mathf.Min(GloveH, _gloveWarmRow + rows);
                for (int y = _gloveWarmRow; y < end; y++)
                {
                    int row = y * GloveW;
                    float fy = y + 0.5f;
                    for (int x = 0; x < GloveW; x++)
                        _gloveWarmBody[row + x] = GloveBodyRaw(x + 0.5f, fy);
                }
                _gloveWarmRow = end;
                if (end >= GloveH)
                {
                    _gloveBodyDist = _gloveWarmBody;
                    _gloveWarmBody = null;
                    _gloveWarmRow = 0;
                }
                return true;
            }
            if (_gloveCurls == null) _gloveCurls = new Texture2D[GloveCurlSteps];
            int step = -1;
            for (int i = 0; i < GloveCurlSteps; i++)
            {
                if (_gloveCurls[i] != null) continue;
                step = i;
                break;
            }
            if (step < 0)
            {
                _gloveWarmPx = null;
                _gloveWarmFor = -1;
                return false;
            }
            if (step != 0 && _gloveRestPx == null) step = 0;
            if (_gloveWarmPx == null || _gloveWarmFor != step)
            {
                _gloveWarmPx = new Color32[GloveW * GloveH];
                _gloveWarmFor = step;
                _gloveWarmRow = 0;
            }
            _gloveIndexBow = Mathf.Lerp(GloveIndexBowRest, GloveIndexBowPress, step / (float)(GloveCurlSteps - 1));
            int stop = Mathf.Min(GloveH, _gloveWarmRow + rows);
            RasterGloveRows(_gloveWarmPx, step == 0, _gloveWarmRow, stop);
            _gloveWarmRow = stop;
            if (stop >= GloveH)
            {
                _gloveCurls[step] = FinishGloveTex(_gloveWarmPx, step == 0);
                GloveTip(98f, 116f, 88f, 236f, 13f, 11f, GloveW, GloveH);
                _gloveWarmPx = null;
                _gloveWarmFor = -1;
                _gloveWarmRow = 0;
            }
            return true;
        }

        // A pixel the index never reaches, at rest or at full press, keeps the resting color.
        static bool GloveIndexFar(float x, float y)
        {
            float saved = _gloveIndexBow;
            bool far = GloveIndexDist(x, y, GloveIndexBowRest) > 24f
                && GloveIndexDist(x, y, GloveIndexBowPress) > 24f;
            _gloveIndexBow = saved;
            return far;
        }

        static float GloveIndexDist(float x, float y, float bow)
        {
            _gloveIndexBow = bow;
            return GloveCapDist(x, y, _coachCaps[7]);
        }

        static void GloveTip(float x0, float y0, float x1, float y1, float rad, float outline, float w, float h)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;
            float reach = rad + outline;
            _gloveTipU = (x1 + dx / len * reach) / w;
            _gloveTipV = (y1 + dy / len * reach) / h;
        }

        static Color32 GlovePixel(float x, float y)
        {
            float dist = GloveField(x, y);
            const float aa = 1.25f;
            float alpha = Mathf.Clamp01(0.5f - dist / aa);
            if (alpha <= 0f)
            {
                float sd = GloveField(x - 6f, y + 8f);
                if (sd >= -0.4f) return default;
                float sa = Mathf.Clamp01((-sd) / 14f);
                sa = sa * sa;
                if (sa < 0.04f) return default;
                return new Color32(48, 32, 22, GloveByte(sa * 78f));
            }
            float ow = Mathf.Lerp(5.6f, 4.0f, Mathf.SmoothStep(30f, 250f, y));
            float fillT = Mathf.Clamp01((-dist - ow) / aa + 0.5f);
            GloveFill(x, y, dist, out float fr, out float fg, out float fb);
            float rib = GloveRib(x, y, dist);
            if (rib > 0f)
            {
                fr = Mathf.Lerp(fr, 132f, rib);
                fg = Mathf.Lerp(fg, 86f, rib);
                fb = Mathf.Lerp(fb, 58f, rib);
            }
            return new Color32(
                GloveByte(Mathf.Lerp(90f, fr, fillT)),
                GloveByte(Mathf.Lerp(68f, fg, fillT)),
                GloveByte(Mathf.Lerp(52f, fb, fillT)),
                GloveByte(alpha * 255f));
        }

        static void GloveFill(float x, float y, float dist, out float r, out float g, out float b)
        {
            const float e = 1.35f;
            float nx = GloveField(x + e, y) - GloveField(x - e, y);
            float ny = GloveField(x, y + e) - GloveField(x, y - e);
            float mag = Mathf.Sqrt(nx * nx + ny * ny);
            if (mag < 1e-4f) mag = 1f;
            nx /= mag;
            ny /= mag;
            // Lower-right of each puff. The middle stays bright so it reads as round.
            float down = Mathf.Clamp01(-ny * 0.55f + nx * 0.85f);
            float depth = dist < 0f ? -dist : 0f;
            float rim = Mathf.Clamp01((18f - depth) / 13f);
            float puff = 0f;
            var caps = _coachCaps;
            for (int i = 3; i <= 7; i++)
            {
                GloveAxis(x, y, caps[i], out float cx, out float cy, out float rad);
                float rx = x - cx;
                float ry = y - cy;
                float side = Mathf.Sqrt(rx * rx + ry * ry);
                if (side > rad || side < 0.4f || rad < 0.5f) continue;
                float edge = side / rad;
                edge *= edge;
                float face = Mathf.Clamp01((-ry / side) * 0.70f + (rx / side) * 0.62f);
                float here = face * edge;
                if (here > puff) puff = here;
            }
            float shade = Mathf.Clamp01(down * rim * 0.85f + puff * 0.55f);
            float soft = 1f - shade * 0.62f;
            float warm = Mathf.SmoothStep(36f, 220f, y);
            r = Mathf.Lerp(168f, 228f, soft);
            g = Mathf.Lerp(118f, 196f, soft);
            b = Mathf.Lerp(86f, 158f, soft);
            r = Mathf.Lerp(r * 0.92f, r, warm);
            g = Mathf.Lerp(g * 0.78f, g, warm);
            b = Mathf.Lerp(b * 0.62f, b, warm);
            float hi = 0f;
            const float lx = -0.30f, ly = 0.95f;
            for (int i = 3; i <= 7; i++)
            {
                var tip = caps[i];
                float rad = tip.R1;
                float hx = tip.X1 + lx * rad * 0.26f;
                float hy = tip.Y1 + ly * rad * 0.20f;
                float dx = x - hx;
                float dy = y - hy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float spot = Mathf.Clamp01(1f - d / (rad * 0.50f));
                float core = Mathf.Clamp01(1f - d / (rad * 0.26f));
                spot = spot * 0.55f + core * core * 0.95f;
                if (spot > hi) hi = spot;
            }
            float px = (x - 104f) / 16f;
            float py = (y - 114f) / 11f;
            float palm = Mathf.Sqrt(px * px + py * py);
            float pad = Mathf.Clamp01(1f - palm);
            pad = pad * 0.5f + pad * pad * 0.7f;
            if (pad > hi) hi = pad;
            hi = Mathf.Clamp01(hi);
            r = Mathf.Lerp(r, 242f, hi * 0.42f);
            g = Mathf.Lerp(g, 220f, hi * 0.34f);
            b = Mathf.Lerp(b, 186f, hi * 0.22f);
        }

        static float GloveRib(float x, float y, float dist)
        {
            if (y > 68f || dist > -12f) return 0f;
            if (GloveCapDist(x, y, _coachCaps[0]) > -4f) return 0f;
            float best = 0f;
            float arc = (x - 110f) * (x - 110f) * 0.00085f;
            best = GloveBand(y, 30f - arc, 0.78f);
            float upper = GloveBand(y, 46f - arc, 0.62f);
            if (upper > best) best = upper;
            float lip = GloveBand(y, 64f - arc * 0.35f, 0.70f);
            if (lip > best) best = lip;
            return best;
        }

        static float GloveBand(float y, float ry, float amp)
        {
            float d = y - ry;
            if (d < 0f) d = -d;
            float band = Mathf.Clamp01(1f - d / 4.2f);
            band = band * band * (3f - 2f * band);
            return band * amp;
        }

        // Cuff, palm, and thumb blend together. Ring, middle, and pinky join that
        // mass; the pinky root overlaps it so the fill stays one piece. The index
        // joins last, and its bow is the press curl.
        static float GloveField(float x, float y)
        {
            return GloveSmin(SampleGloveBody(x, y), GloveCapDist(x, y, _coachCaps[7]), 11f);
        }

        static float GloveBodyRaw(float x, float y)
        {
            var c = _coachCaps;
            float body = GloveCapDist(x, y, c[0]);
            body = GloveSmin(body, GloveCapDist(x, y, c[1]), 16f);
            body = GloveSmin(body, GloveCapDist(x, y, c[2]), 14f);
            body = GloveSmin(body, GloveCapDist(x, y, c[3]), 13f);
            body = GloveSmin(body, GloveCapDist(x, y, c[4]), 11f);
            body = GloveSmin(body, GloveCapDist(x, y, c[5]), 11f);
            body = GloveSmin(body, GloveCapDist(x, y, c[6]), 11f);
            return body;
        }

        static float SampleGloveBody(float x, float y)
        {
            var body = _gloveBodyDist;
            if (body == null) return GloveBodyRaw(x, y);
            float fx = x - 0.5f;
            float fy = y - 0.5f;
            if (fx <= 0f || fy <= 0f || fx >= GloveW - 1f || fy >= GloveH - 1f)
                return GloveBodyRaw(x, y);
            int x0 = (int)fx;
            int y0 = (int)fy;
            float tx = fx - x0;
            float ty = fy - y0;
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            float d00 = body[y0 * GloveW + x0];
            float d10 = body[y0 * GloveW + x1];
            float d01 = body[y1 * GloveW + x0];
            float d11 = body[y1 * GloveW + x1];
            return (d00 + (d10 - d00) * tx) * (1f - ty) + (d01 + (d11 - d01) * tx) * ty;
        }

        static float GloveSmin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        static float GloveCapDist(float x, float y, CoachCap c)
        {
            GloveAxis(x, y, c, out float cx, out float cy, out float rad);
            float dx = x - cx;
            float dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - rad;
        }

        static void GloveAxis(float x, float y, CoachCap c, out float cx, out float cy, out float rad)
        {
            // Curl > 1.5 is the index. Its bow follows the press.
            if (c.Curl > 1.5f) c.Bow = _gloveIndexBow;
            float ax = c.X1 - c.X0;
            float ay = c.Y1 - c.Y0;
            float den = ax * ax + ay * ay;
            if (Mathf.Abs(c.Bow) < 0.01f || den < 0.0001f)
            {
                float t = den < 0.0001f ? 0f : ((x - c.X0) * ax + (y - c.Y0) * ay) / den;
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;
                rad = c.R0 + (c.R1 - c.R0) * t;
                cx = c.X0 + ax * t;
                cy = c.Y0 + ay * t;
                return;
            }
            float best = 0f;
            float bestD = 1e30f;
            for (int i = 0; i <= 8; i++)
            {
                float sample = i / 8f;
                GloveBowPoint(c, sample, out float sx, out float sy);
                float ddx = x - sx;
                float ddy = y - sy;
                float d = ddx * ddx + ddy * ddy;
                if (d < bestD)
                {
                    bestD = d;
                    best = sample;
                }
            }
            float t0 = best - 0.125f;
            float t1 = best + 0.125f;
            if (t0 < 0f) t0 = 0f;
            if (t1 > 1f) t1 = 1f;
            for (int i = 0; i < 8; i++)
            {
                float m0 = (2f * t0 + t1) / 3f;
                float m1 = (t0 + 2f * t1) / 3f;
                GloveBowPoint(c, m0, out float ax0, out float ay0);
                GloveBowPoint(c, m1, out float bx, out float by);
                float d0 = (x - ax0) * (x - ax0) + (y - ay0) * (y - ay0);
                float d1 = (x - bx) * (x - bx) + (y - by) * (y - by);
                if (d0 < d1) t1 = m1;
                else t0 = m0;
            }
            float tf = (t0 + t1) * 0.5f;
            GloveBowPoint(c, tf, out cx, out cy);
            rad = c.R0 + (c.R1 - c.R0) * tf;
        }

        // Curl > 1 arches over the top and is flat at the tip, so the fingertip keeps
        // aiming along the finger instead of kinking back. Curl > 0 hooks toward the
        // tip. Curl < 0 peaks mid-shaft and is flat at the tip.
        static void GloveBowPoint(CoachCap c, float t, out float x, out float y)
        {
            float ax = c.X1 - c.X0;
            float ay = c.Y1 - c.Y0;
            float len = Mathf.Sqrt(ax * ax + ay * ay);
            float px = 0f;
            float py = 0f;
            if (len > 0.001f)
            {
                px = -ay / len;
                py = ax / len;
            }
            float u = 1f - t;
            float amt;
            if (c.Curl > 1.5f)
            {
                // t^6 (1-t)^2 peaks at t=0.75 and has zero slope at the tip.
                float f = t * t * t * t * t * t * u * u;
                const float peak = 0.0111236572f;
                amt = f / peak;
            }
            else if (c.Curl > 0.5f) amt = 6.75f * t * t * u;
            else if (c.Curl < -0.5f) amt = 16f * t * t * u * u;
            else amt = 4f * t * u;
            float b = c.Bow * amt;
            x = c.X0 + ax * t + px * b;
            y = c.Y0 + ay * t + py * b;
        }

        static byte GloveByte(float v)
        {
            return (byte)Mathf.Clamp(v + 0.5f, 0f, 255f);
        }
    }
}
