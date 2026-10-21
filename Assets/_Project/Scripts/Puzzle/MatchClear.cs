using System.Collections.Generic;

namespace CarMatch
{
    // Shared by taps and the tests: one car enters the 7-slot bar, then every
    // set of three matching colors leaves. Overflow is "still 7 after the clear".
    public static class MatchClear
    {
        public const int BarCap = 7;
        public const int Set = 3;

        public struct Result
        {
            public bool Matched;
            public bool Overflow;
            public CarColor Cleared;
            // Where the car went in, and (when Matched) the first of the three cleared slots.
            public int Index;
            public int ClearedFrom;
        }

        // Like Car Match: a car goes right after the last car of its color so colors stay grouped;
        // a new color goes to the end.
        public static int InsertIndex(IList<CarColor> bar, CarColor color)
        {
            if (bar == null) return 0;
            for (int i = bar.Count - 1; i >= 0; i--)
                if (bar[i] == color) return i + 1;
            return bar.Count;
        }

        public static int Count(IList<CarColor> bar, CarColor color)
        {
            int n = 0;
            if (bar == null) return 0;
            for (int i = 0; i < bar.Count; i++)
                if (bar[i] == color) n++;
            return n;
        }

        public static Result Push(List<CarColor> bar, CarColor color)
        {
            int index = InsertIndex(bar, color);
            bar.Insert(index, color);
            int n = Count(bar, color);
            bool matched = n >= Set;
            int from = -1;
            if (matched)
            {
                // Grouped, so the set is the three slots ending at the new car.
                from = index - (Set - 1);
                bar.RemoveRange(from, Set);
            }
            return new Result
            {
                Matched = matched,
                Overflow = bar.Count >= BarCap,
                Cleared = color,
                Index = index,
                ClearedFrom = from
            };
        }
    }
}
