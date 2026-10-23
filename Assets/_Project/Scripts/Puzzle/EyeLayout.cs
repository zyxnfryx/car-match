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
            new EyeLayout { Valid = true, MidX = -0.31f, MidY = -8.34f, TravelX = 3.60f, TravelY = 2.00f },
            new EyeLayout { Valid = true, MidX = 6.49f, MidY = -7.21f, TravelX = 3.20f, TravelY = 2.00f },
            new EyeLayout { Valid = true, MidX = 12.26f, MidY = -3.97f, TravelX = 2.80f, TravelY = 3.20f },
            new EyeLayout { Valid = true, MidX = 16.16f, MidY = 0.88f, TravelX = 1.20f, TravelY = 3.60f },
            new EyeLayout { Valid = true, MidX = 17.58f, MidY = 6.57f, TravelX = 0.40f, TravelY = 3.60f },
            new EyeLayout { Valid = true, MidX = 16.44f, MidY = 12.14f, TravelX = 1.20f, TravelY = 3.20f },
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            new EyeLayout { Valid = true, MidX = -17.08f, MidY = 12.11f, TravelX = 1.20f, TravelY = 2.80f },
            new EyeLayout { Valid = true, MidX = -18.19f, MidY = 6.62f, TravelX = 0.00f, TravelY = 3.60f },
            new EyeLayout { Valid = true, MidX = -16.77f, MidY = 0.88f, TravelX = 1.20f, TravelY = 4.00f },
            new EyeLayout { Valid = true, MidX = -12.88f, MidY = -3.97f, TravelX = 2.40f, TravelY = 3.20f },
            new EyeLayout { Valid = true, MidX = -7.10f, MidY = -7.21f, TravelX = 3.20f, TravelY = 2.00f },
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
