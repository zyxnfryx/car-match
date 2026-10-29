using System.Collections.Generic;

namespace CarMatch
{
    // Park 3 Cars undoes the last three taps. Those cars leave the bar and
    // return to the stalls they drove out of. They are not deleted and they
    // are not kept in a side lot.
    public static class ContinuePark
    {
        public struct Trip
        {
            public CarColor Color;
            public int Lane;
            public int Cell;
            public bool Side;
        }

        public static List<Trip> Returning(Board failed, Board restored)
        {
            var trips = new List<Trip>();
            if (failed == null || restored == null) return trips;
            int lanes = failed.Lanes.Count < restored.Lanes.Count ? failed.Lanes.Count : restored.Lanes.Count;
            for (int l = 0; l < lanes; l++)
            {
                var a = failed.Lanes[l].Cells;
                var b = restored.Lanes[l].Cells;
                int n = a.Count < b.Count ? a.Count : b.Count;
                for (int c = 0; c < n; c++)
                {
                    if (b[c].Empty || b[c].Tunnel || b[c].Box) continue;
                    if (!a[c].Empty && !a[c].Tunnel) continue;
                    trips.Add(new Trip { Color = b[c].Car.Color, Lane = l, Cell = c });
                }
            }
            int sides = failed.Sides.Count < restored.Sides.Count ? failed.Sides.Count : restored.Sides.Count;
            for (int s = 0; s < sides; s++)
            {
                var back = restored.Sides[s].Queue;
                int extra = back.Count - failed.Sides[s].Queue.Count;
                for (int i = 0; i < extra; i++)
                    trips.Add(new Trip { Color = back[i].Color, Lane = restored.Sides[s].Lane, Cell = restored.Sides[s].Cell, Side = true });
            }
            return trips;
        }
    }

    // What the Park 3 Cars button may show. VIP skips the ad. No fill stays shut.
    public enum ContinueFace
    {
        Ad = 0,
        Crown = 1,
        Unavailable = 2
    }

    public static class ContinueOffer
    {
        public static ContinueFace Face(bool vip, bool adReady)
        {
            if (vip) return ContinueFace.Crown;
            if (!adReady) return ContinueFace.Unavailable;
            return ContinueFace.Ad;
        }

        public static bool CanPress(ContinueFace face)
        {
            return face != ContinueFace.Unavailable;
        }
    }
}
