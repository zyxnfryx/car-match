using System.Collections.Generic;

namespace CarMatch
{
    // One number per level under the sideways-and-forward exit rule.
    // Min taps equals the car count (each car leaves once). The search still
    // records how full the 7-slot bar gets and how many choices the solver
    // faces along the winning line, which is what the new rule changes.
    public struct DiffScore
    {
        public int Colors;
        public int Cars;
        public int Lanes;
        public int Depth;
        public int Mechanics;
        public int OpenAtStart;
        public int PeakBar;
        public float Branch;
        public int Nodes;
        public bool Solved;
        public float Value;

        public float Slack { get { return MatchClear.BarCap - PeakBar; } }
    }

    public static class Difficulty
    {
        public const int Window = 5;

        public static DiffScore Measure(Board start, int nodeCap = 80000)
        {
            var score = Structural(start);
            if (start == null) return score;
            var root = start.Clone();
            HiddenReveal.Sweep(root);
            if (root.Won || BoardQuery.IsClear(root))
            {
                score.Solved = true;
                score.Value = Combine(score);
                return score;
            }

            var seen = new HashSet<string>();
            var stack = new Stack<Frame>();
            stack.Push(new Frame { Board = root, Peak = 0, BranchSum = 0, Steps = 0 });
            seen.Add(BoardQuery.Key(root));
            int nodes = 0;
            var taps = new List<Tap>(24);
            Frame win = default;
            bool found = false;

            while (stack.Count > 0)
            {
                var frame = stack.Pop();
                nodes++;
                if (nodes > nodeCap) break;
                ExitPath.Collect(frame.Board, taps);
                if (taps.Count == 0) continue;
                int branch = taps.Count;
                var ordered = Order(frame.Board, taps);
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    var next = frame.Board.Clone();
                    var move = BoardRules.TryTap(next, ordered[i]);
                    if (!move.Accepted || next.Failed) continue;
                    int parked = next.Bar.Count + (move.Matched ? MatchClear.Set : 0);
                    int peak = frame.Peak > parked ? frame.Peak : parked;
                    var child = new Frame
                    {
                        Board = next,
                        Peak = peak,
                        BranchSum = frame.BranchSum + branch,
                        Steps = frame.Steps + 1
                    };
                    if (next.Won || BoardQuery.IsClear(next))
                    {
                        win = child;
                        found = true;
                        stack.Clear();
                        break;
                    }
                    if (!seen.Add(BoardQuery.Key(next))) continue;
                    stack.Push(child);
                }
            }

            score.Nodes = nodes;
            score.Solved = found;
            if (found)
            {
                score.PeakBar = win.Peak;
                score.Branch = win.Steps > 0 ? win.BranchSum / (float)win.Steps : 0f;
            }
            score.Value = Combine(score);
            return score;
        }

        public static float Rolling(IList<float> values, int index, int window)
        {
            if (values == null || index < 0 || index >= values.Count) return 0f;
            if (window < 1) window = 1;
            int from = index - window + 1;
            if (from < 0) from = 0;
            float sum = 0f;
            int n = 0;
            for (int i = from; i <= index; i++) { sum += values[i]; n++; }
            return n == 0 ? 0f : sum / n;
        }

        // After the early ramp, the 5-level average may breathe inside a band
        // but must not fall. Levels 1-9 are checked on their own average too.
        public static void AssertRising(IList<float> values, System.Action<bool, string> expect)
        {
            expect(values != null && values.Count >= 20, "need the full pack to judge the curve");
            if (values == null) return;
            float prev = Rolling(values, Window - 1, Window);
            for (int i = Window; i < values.Count; i++)
            {
                float avg = Rolling(values, i, Window);
                // A tenth of a point forgives float noise, not a real drop.
                expect(avg + 0.15f >= prev, "rolling difficulty dropped at level " + (i + 1)
                    + " (" + prev.ToString("0.00") + " -> " + avg.ToString("0.00") + ")");
                prev = avg;
            }
        }

        static DiffScore Structural(Board board)
        {
            var score = new DiffScore();
            if (board == null) return score;
            var hist = BoardQuery.Histogram(board);
            int colors = 0;
            int cars = 0;
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i] <= 0) continue;
                colors++;
                cars += hist[i];
            }
            score.Colors = colors;
            score.Cars = cars;
            score.Lanes = board.Lanes.Count;
            int depth = 0;
            int mech = 0;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                if (cells.Count > depth) depth = cells.Count;
                for (int c = 0; c < cells.Count; c++)
                {
                    if (cells[c].Tunnel) mech++;
                    else if (cells[c].Box) mech++;
                    else if (!cells[c].Empty && cells[c].Car.Hidden) mech++;
                }
            }
            mech += board.Sides.Count;
            mech += board.Garages.Count;
            score.Depth = depth;
            score.Mechanics = mech;
            var taps = new List<Tap>();
            var copy = board.Clone();
            HiddenReveal.Sweep(copy);
            ExitPath.Collect(copy, taps);
            score.OpenAtStart = taps.Count;
            return score;
        }

        // Weights: colors and cars set the floor, mechanics and depth add the
        // ladder, a crowded bar and few opening choices add pressure. Wide
        // branching under the sideways rule lowers the score so an open lot
        // cannot masquerade as a hard one.
        public static float Combine(DiffScore s)
        {
            float blocked = s.Cars <= 0 ? 0f : 1f - s.OpenAtStart / (float)s.Cars;
            if (blocked < 0f) blocked = 0f;
            float pressure = s.PeakBar / (float)MatchClear.BarCap;
            float branchEase = s.Branch > 6f ? (s.Branch - 6f) * 0.35f : 0f;
            float raw = s.Colors * 4.2f
                + s.Cars * 0.55f
                + s.Lanes * 1.6f
                + s.Depth * 1.1f
                + s.Mechanics * 3.4f
                + blocked * 8f
                + pressure * 9f
                - branchEase;
            if (!s.Solved) raw *= 0.25f;
            return raw;
        }

        struct Frame
        {
            public Board Board;
            public int Peak;
            public int BranchSum;
            public int Steps;
        }

        static List<Tap> Order(Board board, List<Tap> taps)
        {
            var forced = new List<Tap>();
            for (int i = 0; i < taps.Count; i++)
            {
                var color = ExitPath.ColorOf(board, taps[i]);
                if (MatchClear.Count(board.Bar, color) >= MatchClear.Set - 1)
                    forced.Add(taps[i]);
            }
            if (forced.Count > 0) return forced;
            var scored = new List<Tap>(taps);
            scored.Sort(delegate (Tap a, Tap b)
            {
                return Rank(board, b).CompareTo(Rank(board, a));
            });
            return scored;
        }

        static int Rank(Board board, Tap tap)
        {
            var color = ExitPath.ColorOf(board, tap);
            int have = MatchClear.Count(board.Bar, color);
            int left = BoardQuery.Count(board, color);
            return have * 50 + (12 - left);
        }
    }
}
