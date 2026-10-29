using System.Collections.Generic;
using System.Text;

namespace CarMatch
{
    public static class BoardQuery
    {
        public static bool IsClear(Board board)
        {
            if (board == null) return true;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    var slot = cells[c];
                    if (!slot.Empty) return false;
                    if (slot.Tunnel && slot.Queue != null && slot.Queue.Count > 0) return false;
                }
            }
            for (int s = 0; s < board.Sides.Count; s++)
                if (board.Sides[s].Queue.Count > 0) return false;
            for (int g = 0; g < board.Garages.Count; g++)
                if (board.Garages[g].Queue.Count > 0) return false;
            return true;
        }

        public static int Count(Board board, CarColor color)
        {
            int n = 0;
            Each(board, delegate (Car car)
            {
                if (car.Color == color) n++;
            });
            return n;
        }

        public static int Total(Board board)
        {
            int n = 0;
            Each(board, delegate { n++; });
            return n;
        }

        // Colors on the surface only (not still underground in a garage); what the magnet can reach.
        public static int[] SurfaceHistogram(Board board)
        {
            var n = Histogram(board);
            if (board == null) return n;
            for (int g = 0; g < board.Garages.Count; g++)
            {
                var queue = board.Garages[g].Queue;
                for (int q = 0; q < queue.Count; q++)
                {
                    int i = (int)queue[q].Color;
                    if ((uint)i < (uint)n.Length) n[i]--;
                }
            }
            return n;
        }

        public static int[] Histogram(Board board)
        {
            var n = new int[Palette.Count];
            Each(board, delegate (Car car)
            {
                int i = (int)car.Color;
                if ((uint)i < (uint)n.Length) n[i]++;
            });
            return n;
        }

        public static bool HasHidden(Board board)
        {
            bool found = false;
            Each(board, delegate (Car car)
            {
                if (car.Hidden) found = true;
            });
            return found;
        }

        public static bool HasTunnel(Board board)
        {
            if (board == null) return false;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                    if (cells[c].Tunnel && cells[c].TunnelCount > 0) return true;
            }
            return false;
        }

        public static void Each(Board board, System.Action<Car> visit)
        {
            if (board == null || visit == null) return;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    var slot = cells[c];
                    if (slot.Tunnel && slot.Queue != null)
                    {
                        for (int q = 0; q < slot.Queue.Count; q++)
                            visit(slot.Queue[q]);
                    }
                    else if (!slot.Empty)
                    {
                        visit(slot.Car);
                    }
                }
            }
            for (int s = 0; s < board.Sides.Count; s++)
            {
                var queue = board.Sides[s].Queue;
                for (int q = 0; q < queue.Count; q++)
                    visit(queue[q]);
            }
            for (int g = 0; g < board.Garages.Count; g++)
            {
                var queue = board.Garages[g].Queue;
                for (int q = 0; q < queue.Count; q++)
                    visit(queue[q]);
            }
        }

        // Bar is a multiset. Hidden flags are view-only, so they stay out of the key.
        public static string Key(Board board)
        {
            var sb = new StringBuilder(128);
            if (board == null) return "";
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                sb.Append('|');
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    var slot = cells[c];
                    if (slot.Empty && !slot.Tunnel)
                    {
                        sb.Append('_');
                        continue;
                    }
                    if (slot.Tunnel)
                    {
                        sb.Append('T');
                        if (slot.Queue != null)
                        {
                            for (int q = 0; q < slot.Queue.Count; q++)
                                sb.Append((int)slot.Queue[q].Color);
                        }
                        sb.Append(';');
                        continue;
                    }
                    if (slot.Box) sb.Append('B');
                    sb.Append((int)slot.Car.Color);
                }
            }
            sb.Append('#');
            for (int s = 0; s < board.Sides.Count; s++)
            {
                var side = board.Sides[s];
                sb.Append(side.Lane).Append('@').Append(side.Cell).Append(':');
                for (int q = 0; q < side.Queue.Count; q++)
                    sb.Append((int)side.Queue[q].Color);
                sb.Append(';');
            }
            for (int g = 0; g < board.Garages.Count; g++)
            {
                sb.Append('G');
                var queue = board.Garages[g].Queue;
                for (int q = 0; q < queue.Count; q++)
                    sb.Append((int)queue[q].Color);
            }
            sb.Append('B');
            var bar = new List<CarColor>(board.Bar);
            bar.Sort();
            for (int i = 0; i < bar.Count; i++)
                sb.Append((int)bar[i]);
            sb.Append('P');
            for (int i = 0; i < board.Parked.Count; i++)
                sb.Append((int)board.Parked[i]);
            if (board.Won) sb.Append('W');
            if (board.Failed) sb.Append('F');
            return sb.ToString();
        }
    }
}
