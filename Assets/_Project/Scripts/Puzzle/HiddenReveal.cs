namespace CarMatch
{
    // Shared after every removal, on level load, and after a shuffle.
    // A '?' car shows its color once the cell directly in front of it is empty (or it can exit out the back).
    // Tunnel and side-lot mouths reveal when that dispenser can actually exit.
    public static class HiddenReveal
    {
        public static int Sweep(Board board)
        {
            if (board == null) return 0;
            int n = 0;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var lane = board.Lanes[l];
                for (int c = 0; c < lane.Cells.Count; c++)
                {
                    var slot = lane.Cells[c];
                    if (slot.Empty) continue;
                    // Uncovered when the cell in front is empty, or when it can leave out the back.
                    bool covered = c > 0 && !lane.Cells[c - 1].Empty && !ExitPath.BackClear(lane, c);
                    if (covered) continue;
                    if (slot.Tunnel)
                    {
                        if (slot.Queue == null || slot.Queue.Count == 0) continue;
                        var mouth = slot.Queue[0];
                        if (!mouth.Hidden) continue;
                        mouth.Hidden = false;
                        slot.Queue[0] = mouth;
                        n++;
                    }
                    else if (slot.Car.Hidden)
                    {
                        var car = slot.Car;
                        car.Hidden = false;
                        slot.Car = car;
                        n++;
                    }
                }
            }
            for (int s = 0; s < board.Sides.Count; s++)
            {
                if (!SideLotGate.IsOpen(board, s)) continue;
                var queue = board.Sides[s].Queue;
                if (queue.Count == 0 || !queue[0].Hidden) continue;
                var car = queue[0];
                car.Hidden = false;
                queue[0] = car;
                n++;
            }
            return n;
        }
    }
}
