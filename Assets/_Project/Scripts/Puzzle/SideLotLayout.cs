namespace CarMatch
{
    // Side bays sit outside the lot, on the outer edge nearest their lane.
    // They never land on another lane's stalls or on that lane's end gate.
    public static class SideLotLayout
    {
        public struct Rect
        {
            public float X;
            public float Y;
            public float W;
            public float H;
        }

        // Lane centers are 0..lanes-1. A side returns a center outside that range.
        // ordinal is how many sides already claimed this same outer edge (0 = closest).
        public static float CenterIndex(int lanes, int sideLane, int ordinal)
        {
            if (lanes < 1) lanes = 1;
            if (ordinal < 0) ordinal = 0;
            bool left = sideLane * 2 < lanes;
            if (left) return -1f - ordinal;
            return lanes + ordinal;
        }

        public static bool Hits(Rect a, Rect b)
        {
            float hw = (a.W + b.W) * 0.5f;
            float hh = (a.H + b.H) * 0.5f;
            return Abs(a.X - b.X) < hw - 0.5f && Abs(a.Y - b.Y) < hh - 0.5f;
        }

        // Pixel center of a lane or of a side index (which may be negative or >= lanes).
        public static float CenterX(float left, float pitch, float index)
        {
            return left + index * pitch;
        }

        static float Abs(float v) { return v < 0f ? -v : v; }
    }
}
