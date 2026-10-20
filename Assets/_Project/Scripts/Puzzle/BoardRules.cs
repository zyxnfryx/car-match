using System.Collections.Generic;

namespace CarMatch
{
    public struct MoveResult
    {
        public bool Accepted;
        public bool Matched;
        public bool Won;
        public bool Failed;
    }

    // The only tap mutation. The view, the solver, and the tests all call Play
    // so match, reveal, tunnel, and overflow stay in one place.
    public static class BoardRules
    {
        public static MoveResult Play(Board board, IList<Board> history, Tap tap)
        {
            if (history != null && board != null)
                history.Add(board.Clone());
            var result = TryTap(board, tap);
            if (!result.Accepted && history != null && history.Count > 0)
                history.RemoveAt(history.Count - 1);
            return result;
        }

        public static MoveResult TryTap(Board board, Tap tap)
        {
            if (board == null || !ExitPath.IsLegal(board, tap))
                return new MoveResult();

            Car car;
            if (tap.Kind == TapKind.Lane)
            {
                var slot = board.Lanes[tap.Index].Cells[tap.Cell];
                if (slot.Tunnel)
                {
                    if (!TunnelSpawn.TryRelease(slot, out car))
                        return new MoveResult();
                }
                else
                {
                    car = slot.Car;
                    slot.Empty = true;
                }
            }
            else
            {
                var side = board.Sides[tap.Index];
                car = side.Queue[0];
                side.Queue.RemoveAt(0);
            }

            var clear = MatchClear.Push(board.Bar, car.Color);
            HiddenReveal.Sweep(board);
            bool won = BoardQuery.IsClear(board);
            bool failed = !won && clear.Overflow;
            board.Won = won;
            board.Failed = failed;
            return new MoveResult
            {
                Accepted = true,
                Matched = clear.Matched,
                Won = won,
                Failed = failed
            };
        }
    }
}
