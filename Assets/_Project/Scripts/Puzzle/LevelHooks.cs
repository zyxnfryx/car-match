using System.Collections.Generic;

namespace CarMatch
{
    // Data hooks for mechanics on the ladder (docs/mechanics-ladder.md) that are not playable yet.
    // A level may carry an optional "hooks" object; it is parsed and kept so content can be authored
    // ahead of the rules. The board, solver and tests ignore it until a mechanic is implemented.
    //   "hooks": {
    //     "conveyor": [{ "lane": 0, "every": 2, "cars": ["coral","teal"] }],
    //     "elevator": [{ "lane": 3, "floors": [["amber"],["lime","lime"]] }],
    //     "tow":      [{ "after": 6, "lane": 1, "at": 2, "car": "violet" }],
    //     "stacks":   [{ "lane": 2, "at": 1, "under": ["sky","sky"] }],
    //     "gates":    [{ "lane": 1, "at": 0, "key": "gold" }],
    //     "barriers": [{ "lane": 4, "at": 0, "opensAfter": 5 }]
    //   }
    // Tunnels / garages ("tunnel:a,b,c" cells), "?" covered cars and side lots are already live.
    public sealed class LevelHooks
    {
        public static readonly string[] Known = { "conveyor", "elevator", "tow", "stacks", "gates", "barriers" };

        public readonly Dictionary<string, List<object>> Entries = new Dictionary<string, List<object>>();

        public bool Has(string mechanic)
        {
            List<object> list;
            return Entries.TryGetValue(mechanic, out list) && list != null && list.Count > 0;
        }

        public static LevelHooks Read(Dictionary<string, object> row)
        {
            var hooks = new LevelHooks();
            object raw;
            if (row == null || !row.TryGetValue("hooks", out raw)) return hooks;
            var map = raw as Dictionary<string, object>;
            if (map == null) return hooks;
            foreach (var pair in map)
            {
                var list = pair.Value as List<object>;
                if (list != null) hooks.Entries[pair.Key] = list;
            }
            return hooks;
        }
    }
}
