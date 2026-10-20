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
            bar.Add(color);
            int n = Count(bar, color);
            bool matched = n >= Set;
            if (matched)
            {
                int left = Set;
                for (int i = bar.Count - 1; i >= 0 && left > 0; i--)
                {
                    if (bar[i] != color) continue;
                    bar.RemoveAt(i);
                    left--;
                }
            }
            return new Result
            {
                Matched = matched,
                Overflow = bar.Count >= BarCap,
                Cleared = color
            };
        }
    }
}
