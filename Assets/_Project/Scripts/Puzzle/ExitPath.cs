using System.Collections.Generic;

namespace CarMatch
{
    public enum TapKind : byte
    {
        Lane = 0,
        Side = 1
    }

    public readonly struct Tap
    {
        public readonly TapKind Kind;
        public readonly int Index;
        public readonly int Cell;

        public Tap(TapKind kind, int index, int cell)
        {
            Kind = kind;
            Index = index;
            Cell = cell;
        }
    }

    // Shared by input, the board view, and the solver. A car can leave only
    // when every cell between it and the exit is empty.
    public static class ExitPath
    {
        // A car can leave from either end of its lane: forward to the bar road (cell 0 side),
        // or backward onto the ring road behind the lot when every cell behind it is empty.
        public static bool LaneOpen(Lane lane, int cell)
        {
            if (lane == null || (uint)cell >= (uint)lane.Cells.Count) return false;
            var slot = lane.Cells[cell];
            if (slot.Empty) return false;
            if (slot.Tunnel && slot.TunnelCount <= 0) return false;
            if (slot.Box) return false;
            return FrontClear(lane, cell) || BackClear(lane, cell);
        }

        public static bool FrontClear(Lane lane, int cell)
        {
            for (int i = 0; i < cell; i++)
                if (!lane.Cells[i].Empty) return false;
            return true;
        }

        public static bool BackClear(Lane lane, int cell)
        {
            for (int i = cell + 1; i < lane.Cells.Count; i++)
                if (!lane.Cells[i].Empty) return false;
            return true;
        }

        // Which way a legal lane tap drives: the front (bar side) is preferred when both are clear.
        public static bool ExitsBack(Board board, Tap tap)
        {
            if (board == null) return false;
            int lane = tap.Kind == TapKind.Lane ? tap.Index : board.Sides[tap.Index].Lane;
            int cell = tap.Kind == TapKind.Lane ? tap.Cell : board.Sides[tap.Index].Cell;
            if ((uint)lane >= (uint)board.Lanes.Count) return false;
            var l = board.Lanes[lane];
            if (tap.Kind == TapKind.Side)
                return !SideLotGate.FrontOpen(board, tap.Index) && SideLotGate.IsOpen(board, tap.Index);
            return !FrontClear(l, cell) && BackClear(l, cell);
        }

        public static bool IsLegal(Board board, Tap tap)
        {
            if (board == null || board.Won || board.Failed) return false;
            if (tap.Kind == TapKind.Lane)
            {
                if ((uint)tap.Index >= (uint)board.Lanes.Count) return false;
                var lane = board.Lanes[tap.Index];
                if ((uint)tap.Cell >= (uint)lane.Cells.Count) return false;
                return LaneOpen(lane, tap.Cell);
            }
            if ((uint)tap.Index >= (uint)board.Sides.Count) return false;
            return SideLotGate.IsOpen(board, tap.Index);
        }

        public static void Collect(Board board, List<Tap> into)
        {
            into.Clear();
            if (board == null || board.Won || board.Failed) return;
            for (int lane = 0; lane < board.Lanes.Count; lane++)
            {
                var cells = board.Lanes[lane].Cells;
                int front = -1;
                for (int cell = 0; cell < cells.Count; cell++)
                {
                    if (!LaneOpen(board.Lanes[lane], cell)) continue;
                    into.Add(new Tap(TapKind.Lane, lane, cell));
                    front = cell;
                    break;
                }
                for (int cell = cells.Count - 1; cell > front; cell--)
                {
                    if (!LaneOpen(board.Lanes[lane], cell)) continue;
                    into.Add(new Tap(TapKind.Lane, lane, cell));
                    break;
                }
            }
            for (int s = 0; s < board.Sides.Count; s++)
                if (SideLotGate.IsOpen(board, s))
                    into.Add(new Tap(TapKind.Side, s, board.Sides[s].Cell));
        }

        public static CarColor ColorOf(Board board, Tap tap)
        {
            if (tap.Kind == TapKind.Lane)
            {
                var slot = board.Lanes[tap.Index].Cells[tap.Cell];
                if (slot.Tunnel) return slot.Queue[0].Color;
                return slot.Car.Color;
            }
            return board.Sides[tap.Index].Queue[0].Color;
        }
    }
}
