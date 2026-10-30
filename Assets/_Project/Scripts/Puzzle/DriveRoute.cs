using System.Collections.Generic;

namespace CarMatch
{
    // Grid route for every drive the view plays (Brandon, Oct 29: cars never drive over other cars).
    // A step is a lane cell. Cell < 0 is the lane mouth (the exit road toward the bar).
    // Cell >= the lane's length is the outer road above the top of that lane.
    // The view turns these steps into points; the tests check them against the board.
    public static class DriveRoute
    {
        public const int MouthCell = -1;

        // Exit from a parked spot or a side lot: through real empty cells, sideways or forward,
        // never out the back. Includes the start cell (side lots: the lane cell they pull into)
        // and ends with a mouth step on the lane it actually leaves.
        public static bool Exit(Board board, Tap tap, List<DriveStep> into)
        {
            into.Clear();
            if (board == null) return false;
            if (tap.Kind == TapKind.Lane)
                return ExitPath.TryRoute(board, tap.Index, tap.Cell, into);
            if (tap.Kind == TapKind.Side)
            {
                if (!SideLotGate.IsOpen(board, tap.Index)) return false;
                var side = board.Sides[tap.Index];
                for (int c = side.Cell; c >= 0; c--) into.Add(new DriveStep(side.Lane, c));
                into.Add(new DriveStep(side.Lane, MouthCell));
                return true;
            }
            return false;
        }

        // Back into (lane, cell), e.g. Undo. Steps run from where the car enters the lot to the spot.
        // First choice: the reverse of a clear exit route from that spot (in through the mouth).
        // Otherwise, if every cell behind the spot is empty, it goes around the outer road and
        // pulls in from the top (fromTop; the first step is the road above the lane).
        // False: there is no clear way in, so the view must not drive it there.
        public static bool Return(Board board, int lane, int cell, List<DriveStep> into, out bool fromTop)
        {
            fromTop = false;
            into.Clear();
            if (board == null || (uint)lane >= (uint)board.Lanes.Count) return false;
            var cells = board.Lanes[lane].Cells;
            if ((uint)cell >= (uint)cells.Count) return false;
            if (cells[cell].Tunnel || cells[cell].Box) return false;

            var probe = board.Clone();
            probe.Won = false;
            probe.Failed = false;
            var spot = probe.Lanes[lane].Cells[cell];
            spot.Empty = false;
            spot.Car = new Car(spot.Car.Color, false);
            var route = new List<DriveStep>(8);
            if (ExitPath.TryRoute(probe, lane, cell, route))
            {
                for (int i = route.Count - 1; i >= 0; i--) into.Add(route[i]);
                return true;
            }
            for (int c = cell + 1; c < cells.Count; c++)
                if (!cells[c].Empty) return false;
            fromTop = true;
            into.Add(new DriveStep(lane, cells.Count));
            for (int c = cells.Count - 1; c >= cell; c--) into.Add(new DriveStep(lane, c));
            return true;
        }

        // Every on-lot step except the moving car's own spot is an empty cell, and each step moves
        // one cell sideways or along the lane (no diagonal cuts across a corner car).
        public static bool Clear(Board board, List<DriveStep> steps, int ownLane, int ownCell, out string why)
        {
            why = "";
            if (board == null || steps == null || steps.Count == 0) { why = "no steps"; return false; }
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if ((uint)s.Lane >= (uint)board.Lanes.Count) { why = "lane " + s.Lane + " is off the lot"; return false; }
                var cells = board.Lanes[s.Lane].Cells;
                bool onLot = s.Cell >= 0 && s.Cell < cells.Count;
                if (onLot && !(s.Lane == ownLane && s.Cell == ownCell) && !cells[s.Cell].Empty)
                {
                    why = "step " + i + " (" + s.Lane + "," + s.Cell + ") is occupied";
                    return false;
                }
                if (i == 0) continue;
                var p = steps[i - 1];
                int dl = s.Lane - p.Lane; if (dl < 0) dl = -dl;
                int dc = s.Cell - p.Cell; if (dc < 0) dc = -dc;
                if (dl + dc != 1)
                {
                    why = "step " + i + " jumps from (" + p.Lane + "," + p.Cell + ") to (" + s.Lane + "," + s.Cell + ")";
                    return false;
                }
            }
            return true;
        }
    }
}
