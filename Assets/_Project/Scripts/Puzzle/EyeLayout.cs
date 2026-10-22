namespace CarMatch
{
    // Live-eye layout per heading (from Character Creator's layers/eyes.json, 192 px canvas).
    // Mid = midpoint of the two eye centers relative to the canvas center (px, y up);
    // Travel = max pupil offset (px) on each axis, clamped as an ellipse. Invalid = eyes face
    // away (h06-h10): use the baked frames.
    public struct EyeLayout
    {
        public bool Valid;
        public float MidX, MidY, TravelX, TravelY;
        public const float Canvas = 192f;
        public const float WideScale = 1.12f;

        static readonly EyeLayout[] Table =
        {
            new EyeLayout { Valid = true, MidX = -0.31f, MidY = -6.81f, TravelX = 4.80f, TravelY = 2.80f },
            new EyeLayout { Valid = true, MidX = 7.38f, MidY = -5.52f, TravelX = 4.40f, TravelY = 3.20f },
            new EyeLayout { Valid = true, MidX = 13.91f, MidY = -1.84f, TravelX = 3.60f, TravelY = 4.00f },
            new EyeLayout { Valid = true, MidX = 18.28f, MidY = 3.70f, TravelX = 1.60f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = 19.84f, MidY = 10.24f, TravelX = 0.80f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = 18.30f, MidY = 16.84f, TravelX = 2.40f, TravelY = 4.80f },
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            new EyeLayout { Valid = true, MidX = -18.93f, MidY = 16.81f, TravelX = 1.60f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = -20.47f, MidY = 10.22f, TravelX = 0.80f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = -18.91f, MidY = 3.70f, TravelX = 2.00f, TravelY = 4.80f },
            new EyeLayout { Valid = true, MidX = -14.53f, MidY = -1.84f, TravelX = 3.20f, TravelY = 4.00f },
            new EyeLayout { Valid = true, MidX = -8.02f, MidY = -5.52f, TravelX = 4.40f, TravelY = 3.20f },
        };

        public static EyeLayout For(int heading) { return Table[((heading % 16) + 16) % 16]; }

        // Pupil offset (px, y down like the art) for a look vector; the vector is clamped to the
        // unit circle so the pupil stays inside the travel ellipse.
        public void PupilOffset(float lookX, float lookY, out float dx, out float dy)
        {
            float m = lookX * lookX + lookY * lookY;
            if (m > 1f) { float k = 1f / (float)System.Math.Sqrt(m); lookX *= k; lookY *= k; }
            dx = lookX * TravelX;
            dy = lookY * TravelY;
        }
    }
}
