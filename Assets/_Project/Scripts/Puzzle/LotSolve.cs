using System.Collections.Generic;

namespace CarMatch
{
    // Shared by the solvability tests and the Car Match/Solve Levels menu.
    // A match that completes a set is taken immediately: clearing bar space
    // never closes a path, and every car still has to leave.
    public static class LotSolve
    {
        public enum Outlook
        {
            Winnable,
            Stuck,
            Unknown
        }

        public struct Report
        {
            public Outlook Outlook;
            public int Moves;
            public int Nodes;
        }

        public static Report Search(Board start, int nodeCap = 150000)
        {
            if (start == null)
                return new Report { Outlook = Outlook.Stuck };
            var root = start.Clone();
            HiddenReveal.Sweep(root);
            if (root.Won || BoardQuery.IsClear(root))
                return new Report { Outlook = Outlook.Winnable, Moves = 0, Nodes = 1 };

            var seen = new HashSet<string>();
            var stack = new Stack<Frame>();
            stack.Push(new Frame { Board = root, Depth = 0 });
            seen.Add(BoardQuery.Key(root));
            int nodes = 0;
            var taps = new List<Tap>(16);

            while (stack.Count > 0)
            {
                var frame = stack.Pop();
                nodes++;
                if (nodes > nodeCap)
                    return new Report { Outlook = Outlook.Unknown, Moves = frame.Depth, Nodes = nodes };

                ExitPath.Collect(frame.Board, taps);
                if (taps.Count == 0) continue;

                var ordered = Order(frame.Board, taps);
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    // Park 3 Cars is a fail-screen continue. Searching it blows the node cap
                    // and a winning line never needs to overflow the bar.
                    if (ordered[i].Kind == TapKind.Parked && ordered[i].Cell < 0) continue;
                    var next = frame.Board.Clone();
                    var move = BoardRules.TryTap(next, ordered[i]);
                    if (!move.Accepted || next.Failed) continue;
                    if (next.Won || BoardQuery.IsClear(next))
                        return new Report
                        {
                            Outlook = Outlook.Winnable,
                            Moves = frame.Depth + 1,
                            Nodes = nodes
                        };
                    if (!seen.Add(BoardQuery.Key(next))) continue;
                    stack.Push(new Frame { Board = next, Depth = frame.Depth + 1 });
                }
            }

            return new Report { Outlook = Outlook.Stuck, Nodes = nodes };
        }

        struct Frame
        {
            public Board Board;
            public int Depth;
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
                return Score(board, b).CompareTo(Score(board, a));
            });
            return scored;
        }

        static int Score(Board board, Tap tap)
        {
            var color = ExitPath.ColorOf(board, tap);
            int have = MatchClear.Count(board.Bar, color);
            int left = BoardQuery.Count(board, color);
            return have * 50 + (12 - left);
        }
    }
}
