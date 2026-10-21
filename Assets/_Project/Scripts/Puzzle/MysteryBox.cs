using System.Collections.Generic;

namespace CarMatch
{
    // Cardboard '?' boxes (Car Match style). A box breaks when one of the three cars cleared in a match
    // came from a cell next to it (same lane one cell either way, or the same cell in a neighboring lane).
    public static class MysteryBox
    {
        public static int BreakNear(Board board, IList<int> origins, List<int[]> broken)
        {
            int n = 0;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    if (!cells[c].Box) continue;
                    bool near = false;
                    for (int i = 0; i < origins.Count && !near; i++)
                    {
                        int o = origins[i];
                        if (o < 0) continue;
                        int ol = o / 100, oc = o % 100;
                        int dl = ol - l, dc = oc - c;
                        if ((dl == 0 && (dc == 1 || dc == -1)) || (dc == 0 && (dl == 1 || dl == -1))) near = true;
                    }
                    if (!near) continue;
                    cells[c].Box = false;
                    if (broken != null) broken.Add(new[] { l, c });
                    n++;
                }
            }
            return n;
        }
    }
}
