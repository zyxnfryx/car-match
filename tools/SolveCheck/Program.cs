using System;
using System.Diagnostics;
using System.IO;

namespace CarMatch
{
    static class Program
    {
        static int Main(string[] args)
        {
            // --each <levels.json>: one line per level with its solver outlook (used by tools/levels/gen_levels.py).
            if (args.Length == 2 && args[0] == "--each")
            {
                var defs = LevelJson.Parse(File.ReadAllText(args[1]));
                foreach (var d in defs)
                {
                    var r = LotSolve.Search(d.Board, 200000);
                    Console.WriteLine(d.Number + " " + r.Outlook + " " + r.Nodes + " " + r.Moves);
                }
                return 0;
            }
            // --rate <levels.json>: share of 400 random-legal-tap playthroughs that win (difficulty proxy).
            if (args.Length == 2 && args[0] == "--rate")
            {
                var defs = LevelJson.Parse(File.ReadAllText(args[1]));
                var taps = new System.Collections.Generic.List<Tap>();
                foreach (var d in defs)
                {
                    var rng = new Random(d.Number);
                    int wins = 0;
                    for (int run = 0; run < 400; run++)
                    {
                        var b = d.Board.Clone();
                        HiddenReveal.Sweep(b);
                        while (!b.Won && !b.Failed)
                        {
                            ExitPath.Collect(b, taps);
                            if (taps.Count == 0) break;
                            // Casual-player policy: take a tap that adds to the biggest bar group, else random.
                            int best = -1, pick = rng.Next(taps.Count);
                            for (int ti = 0; ti < taps.Count; ti++)
                            {
                                int have = MatchClear.Count(b.Bar, ExitPath.ColorOf(b, taps[ti])) * 10 + rng.Next(10);
                                if (have > best) { best = have; pick = ti; }
                            }
                            BoardRules.TryTap(b, taps[pick]);
                        }
                        if (b.Won) wins++;
                    }
                    Console.WriteLine(d.Number + " " + (wins / 4.0).ToString("0.0") + (d.SuperHard ? " SH" : ""));
                }
                return 0;
            }
            string jsonPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "Assets", "_Project", "Resources", "levels.json"));
            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets", "_Project", "Resources", "levels.json"));
            }
            if (!File.Exists(jsonPath))
            {
                Console.Error.WriteLine("levels.json not found.");
                return 2;
            }

            var clock = Stopwatch.StartNew();
            try
            {
                RuleChecks.RunAll(File.ReadAllText(jsonPath));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("FAIL " + e.Message);
                return 1;
            }
            clock.Stop();
            Console.WriteLine("OK rules and " + LevelJson.Parse(File.ReadAllText(jsonPath)).Count +
                              " levels solvable in " + clock.ElapsedMilliseconds + " ms");
            return 0;
        }
    }
}
