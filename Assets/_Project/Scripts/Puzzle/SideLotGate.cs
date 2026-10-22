namespace CarMatch
{
    // Shared by input, the view, reveal, and the solver. The side car may
    // leave when its lane cell is empty and either end of the lane is clear from there.
    public static class SideLotGate
    {
        public static bool IsOpen(Board board, int sideIndex)
        {
            if (board == null || (uint)sideIndex >= (uint)board.Sides.Count) return false;
            var side = board.Sides[sideIndex];
            if (side.Queue.Count == 0) return false;
            if ((uint)side.Lane >= (uint)board.Lanes.Count) return false;
            var lane = board.Lanes[side.Lane];
            if ((uint)side.Cell >= (uint)lane.Cells.Count) return false;
            if (!lane.Cells[side.Cell].Empty) return false;
            // Into the lane at its cell, then out either end of the lane.
            return ExitPath.FrontClear(lane, side.Cell) || ExitPath.BackClear(lane, side.Cell);
        }

        public static bool FrontOpen(Board board, int sideIndex)
        {
            if (!IsOpen(board, sideIndex)) return false;
            var side = board.Sides[sideIndex];
            return ExitPath.FrontClear(board.Lanes[side.Lane], side.Cell);
        }
    }
}
