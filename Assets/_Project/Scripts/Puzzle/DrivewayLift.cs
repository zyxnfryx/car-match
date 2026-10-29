namespace CarMatch
{
    // The level-21 driveway is an elevator, not a timer. When its spots are clear the
    // empty deck drops and a new deck rises with the next cars. The rules refill
    // immediately (solver and tests stay instant). The view plays this timing and
    // refuses taps on that lane until the deck has settled. Other lanes stay open.
    public static class DrivewayLift
    {
        public const float Seconds = 0.78f;
        public const float DropShare = 0.42f;

        public static bool Locked(float startedAt, float now)
        {
            return startedAt > 0f && now < startedAt + Seconds;
        }

        public static float Along(float startedAt, float now)
        {
            if (startedAt <= 0f) return 1f;
            float u = (now - startedAt) / Seconds;
            if (u < 0f) return 0f;
            if (u > 1f) return 1f;
            return u;
        }

        public static bool Dropping(float u) { return u < DropShare; }

        // 0 at the parked height, negative while the deck is below the stalls.
        public static float DeckOffset(float u, float travel)
        {
            if (u < DropShare)
            {
                float k = u / DropShare;
                return -travel * k;
            }
            float rise = (u - DropShare) / (1f - DropShare);
            float ease = 1f - (1f - rise) * (1f - rise);
            return -travel * (1f - ease);
        }

        // The badge keeps the old "still below" count until the new deck arrives.
        public static int Badge(int before, int after, float u)
        {
            return u >= 0.92f ? after : before;
        }

        public static bool FinalWave(int queueAfter) { return queueAfter <= 0; }
    }
}
