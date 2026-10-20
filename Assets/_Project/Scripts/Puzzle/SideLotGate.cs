namespace CarMatch
{
    // Shared by input, the view, reveal, and the solver. The side car may
    // leave only when its lane cell and every cell closer to the exit are empty.
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
            for (int i = 0; i <= side.Cell; i++)
                if (!lane.Cells[i].Empty) return false;
            return true;
        }
    }
}
