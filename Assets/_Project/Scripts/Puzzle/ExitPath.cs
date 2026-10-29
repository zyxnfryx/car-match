using System.Collections.Generic;

namespace CarMatch
{
    public enum TapKind : byte
    {
        Lane = 0,
        Side = 1,
        Parked = 2
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

    // One cell of a drive. Cell < 0 means the car has left that lane's mouth onto the bar road.
    public struct DriveStep
    {
        public int Lane;
        public int Cell;

        public DriveStep(int lane, int cell)
        {
            Lane = lane;
            Cell = cell;
        }
    }

    // Shared by input, the board view, and the solver.
    // A car can leave when empty cells connect it to the bar, moving sideways
    // into a neighboring lane or forward (toward cell 0). The top of every lane
    // is a hard wall: a step never increases the cell index.
    public static class ExitPath
    {
        const int Stride = 64;

        static readonly List<int> Bfs = new List<int>(64);
        static readonly Dictionary<int, int> Prev = new Dictionary<int, int>(64);

        // Straight mouth check (no sideways). Side lots still use FrontClear.
        public static bool LaneOpen(Lane lane, int cell)
        {
            if (!Occupant(lane, cell)) return false;
            var slot = lane.Cells[cell];
            if (!slot.Tunnel && slot.Car.Hidden) return false;
            return FrontClear(lane, cell);
        }

        // Board-wide rule: sideways through empty stalls, then forward to the bar.
        public static bool LaneOpen(Board board, int lane, int cell)
        {
            return CanReachBar(board, lane, cell);
        }

        public static bool FrontClear(Lane lane, int cell)
        {
            for (int i = 0; i < cell; i++)
                if (!lane.Cells[i].Empty) return false;
            return true;
        }

        // Every cell behind this one is empty. That road is still walled off: it is not an exit.
        public static bool BackClear(Lane lane, int cell)
        {
            for (int i = cell + 1; i < lane.Cells.Count; i++)
                if (!lane.Cells[i].Empty) return false;
            return true;
        }

        // The back of every lane is a hard wall. Nothing drives out that way.
        public static bool ExitsBack(Board board, Tap tap)
        {
            return false;
        }

        public static bool CanReachBar(Board board, int lane, int cell)
        {
            return TryRoute(board, lane, cell, null);
        }

        // Route includes the parked cell and ends with Cell < 0 on the lane it actually leaves.
        // Cells along the way never increase. into may be null when only the yes/no is needed.
        public static bool TryRoute(Board board, int lane, int cell, List<DriveStep> into)
        {
            if (into != null) into.Clear();
            if (!CanStart(board, lane, cell)) return false;

            Bfs.Clear();
            Prev.Clear();
            int start = Pack(lane, cell);
            Prev[start] = -1;
            Bfs.Add(start);
            int found = -1;
            int read = 0;
            while (read < Bfs.Count)
            {
                int cur = Bfs[read++];
                Unpack(cur, out int cl, out int cc);
                if (cc == 0)
                {
                    found = cur;
                    break;
                }
                Visit(board, cur, cl, cc - 1);
                Visit(board, cur, cl - 1, cc);
                Visit(board, cur, cl + 1, cc);
            }
            if (found < 0) return false;
            if (into == null) return true;

            var rev = new List<int>(8);
            for (int n = found; n != -1; n = Prev[n]) rev.Add(n);
            for (int i = rev.Count - 1; i >= 0; i--)
            {
                Unpack(rev[i], out int rl, out int rc);
                into.Add(new DriveStep(rl, rc));
            }
            Unpack(found, out int exitLane, out int ignored);
            into.Add(new DriveStep(exitLane, -1));
            return true;
        }

        public static bool IsLegal(Board board, Tap tap)
        {
            if (board == null || board.Won) return false;
            if (tap.Kind == TapKind.Parked)
                return ParkStrip.Legal(board, tap);
            if (board.Failed) return false;
            if (tap.Kind == TapKind.Lane)
                return CanReachBar(board, tap.Index, tap.Cell);
            if ((uint)tap.Index >= (uint)board.Sides.Count) return false;
            return SideLotGate.IsOpen(board, tap.Index);
        }

        public static void Collect(Board board, List<Tap> into)
        {
            into.Clear();
            if (board == null || board.Won) return;
            if (board.Failed)
            {
                if (ParkStrip.CanReceive(board)) into.Add(ParkStrip.ContinueTap);
                return;
            }
            for (int lane = 0; lane < board.Lanes.Count; lane++)
            {
                var cells = board.Lanes[lane].Cells;
                for (int cell = 0; cell < cells.Count; cell++)
                {
                    if (!CanReachBar(board, lane, cell)) continue;
                    into.Add(new Tap(TapKind.Lane, lane, cell));
                }
            }
            for (int s = 0; s < board.Sides.Count; s++)
                if (SideLotGate.IsOpen(board, s))
                    into.Add(new Tap(TapKind.Side, s, board.Sides[s].Cell));
            for (int i = 0; i < board.Parked.Count; i++)
            {
                var back = new Tap(TapKind.Parked, 0, i);
                if (ParkStrip.Legal(board, back)) into.Add(back);
            }
        }

        public static CarColor ColorOf(Board board, Tap tap)
        {
            if (tap.Kind == TapKind.Parked)
            {
                if (tap.Cell < 0 || board.Parked == null || tap.Cell >= board.Parked.Count) return CarColor.Coral;
                return board.Parked[tap.Cell];
            }
            if (tap.Kind == TapKind.Lane)
            {
                var slot = board.Lanes[tap.Index].Cells[tap.Cell];
                if (slot.Tunnel) return slot.Queue[0].Color;
                return slot.Car.Color;
            }
            return board.Sides[tap.Index].Queue[0].Color;
        }

        static void Visit(Board board, int from, int lane, int cell)
        {
            if (!OpenCell(board, lane, cell)) return;
            int key = Pack(lane, cell);
            if (Prev.ContainsKey(key)) return;
            Prev[key] = from;
            Bfs.Add(key);
        }

        static bool OpenCell(Board board, int lane, int cell)
        {
            if (board == null || lane < 0 || lane >= board.Lanes.Count) return false;
            var cells = board.Lanes[lane].Cells;
            if (cell < 0 || cell >= cells.Count) return false;
            return cells[cell].Empty;
        }

        static bool CanStart(Board board, int lane, int cell)
        {
            if (board == null || board.Won || board.Failed) return false;
            if (lane < 0 || lane >= board.Lanes.Count) return false;
            return Occupant(board.Lanes[lane], cell) && !HiddenCar(board.Lanes[lane].Cells[cell]);
        }

        static bool Occupant(Lane lane, int cell)
        {
            if (lane == null || cell < 0 || cell >= lane.Cells.Count) return false;
            var slot = lane.Cells[cell];
            if (slot.Empty || slot.Box) return false;
            if (slot.Tunnel && slot.TunnelCount <= 0) return false;
            return true;
        }

        static bool HiddenCar(Slot slot)
        {
            return slot != null && !slot.Tunnel && !slot.Empty && slot.Car.Hidden;
        }

        static int Pack(int lane, int cell) { return lane * Stride + cell; }

        static void Unpack(int key, out int lane, out int cell)
        {
            lane = key / Stride;
            cell = key % Stride;
        }
    }
}
