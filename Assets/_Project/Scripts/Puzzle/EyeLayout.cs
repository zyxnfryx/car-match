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
            new EyeLayout { Valid = true, MidX = -0.30f, MidY = -9.78f, TravelX = 4.40f, TravelY = 2.40f },
            new EyeLayout { Valid = true, MidX = 6.78f, MidY = -8.58f, TravelX = 4.00f, TravelY = 3.20f },
            new EyeLayout { Valid = true, MidX = 12.77f, MidY = -5.19f, TravelX = 3.20f, TravelY = 3.60f },
            new EyeLayout { Valid = true, MidX = 16.80f, MidY = -0.09f, TravelX = 1.60f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = 18.25f, MidY = 5.88f, TravelX = 0.40f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = 16.78f, MidY = 12.03f, TravelX = 2.00f, TravelY = 4.00f },
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            default(EyeLayout),
            new EyeLayout { Valid = true, MidX = -17.39f, MidY = 12.04f, TravelX = 1.60f, TravelY = 4.00f },
            new EyeLayout { Valid = true, MidX = -18.87f, MidY = 5.90f, TravelX = 0.80f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = -17.41f, MidY = -0.09f, TravelX = 1.60f, TravelY = 4.40f },
            new EyeLayout { Valid = true, MidX = -13.41f, MidY = -5.19f, TravelX = 2.80f, TravelY = 3.60f },
            new EyeLayout { Valid = true, MidX = -7.40f, MidY = -8.58f, TravelX = 3.60f, TravelY = 3.20f },
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
