using System.Collections.Generic;

namespace CarMatch
{
    // Every legal tap for the current board, rebuilt once per board change (tap, undo,
    // booster, level load). A tap then resolves with one O(1) lookup instead of a lane scan.
    public sealed class LegalMoves
    {
        readonly HashSet<int> _open = new HashSet<int>();
        readonly List<Tap> _scratch = new List<Tap>(16);

        public int Count => _open.Count;

        public static int Key(Tap tap)
        {
            return ((int)tap.Kind << 24) | (tap.Index << 12) | (tap.Cell & 0xFFF);
        }

        public void Rebuild(Board board)
        {
            _open.Clear();
            ExitPath.Collect(board, _scratch);
            for (int i = 0; i < _scratch.Count; i++) _open.Add(Key(_scratch[i]));
        }

        public bool IsLegal(Tap tap)
        {
            return _open.Contains(Key(tap));
        }
    }
}
