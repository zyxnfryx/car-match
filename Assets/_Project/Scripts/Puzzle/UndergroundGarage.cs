using System.Collections.Generic;

namespace CarMatch
{
    // Underground garage: a hatch under a run of parked spots in one lane. When every spot on top
    // of it is empty, the hatch opens and the next cars from its queue drive up the ramp into those
    // spots (hidden, as '?' cars, until they can exit). Repeats until the queue is empty.
    // Shared by the board, the solver and the tests; HiddenReveal.Sweep calls Refill after every change.
    public sealed class Garage
    {
        public int Lane;
        public readonly List<int> Cells = new List<int>();
        public readonly List<Car> Queue = new List<Car>();
        // Bumped every time the hatch opens (the view uses it to play the ramp-up drive).
        public int Opens;

        public Garage Clone()
        {
            var g = new Garage { Lane = Lane, Opens = Opens };
            g.Cells.AddRange(Cells);
            g.Queue.AddRange(Queue);
            return g;
        }
    }

    public static class UndergroundGarage
    {
        public static bool SpotsClear(Board board, Garage g)
        {
            if (board == null || g == null || (uint)g.Lane >= (uint)board.Lanes.Count) return false;
            var cells = board.Lanes[g.Lane].Cells;
            for (int i = 0; i < g.Cells.Count; i++)
            {
                int c = g.Cells[i];
                if ((uint)c >= (uint)cells.Count) return false;
                if (!cells[c].Empty || cells[c].Tunnel) return false;
            }
            return true;
        }

        // Returns how many cars came up. Spots fill in hatch order; each arrives hidden.
        public static int Refill(Board board)
        {
            if (board == null) return 0;
            int n = 0;
            for (int i = 0; i < board.Garages.Count; i++)
            {
                var g = board.Garages[i];
                if (g.Queue.Count == 0 || !SpotsClear(board, g)) continue;
                var cells = board.Lanes[g.Lane].Cells;
                for (int k = 0; k < g.Cells.Count && g.Queue.Count > 0; k++)
                {
                    var car = g.Queue[0];
                    g.Queue.RemoveAt(0);
                    car.Hidden = true;
                    var slot = cells[g.Cells[k]];
                    slot.Empty = false;
                    slot.Car = car;
                    n++;
                }
                g.Opens++;
            }
            return n;
        }
    }
}
