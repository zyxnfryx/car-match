using System;
using System.Diagnostics;
using System.IO;

namespace CarMatch
{
    static class Program
    {
        static int Main(string[] args)
        {
            // --diff <levels.json>: one line per level with the difficulty score.
            if (args.Length == 2 && args[0] == "--diff")
            {
                var defs = LevelJson.Parse(File.ReadAllText(args[1]));
                var values = new System.Collections.Generic.List<float>(defs.Count);
                for (int i = 0; i < defs.Count; i++)
                {
                    var s = Difficulty.Measure(defs[i].Board);
                    values.Add(s.Value);
                    float roll = Difficulty.Rolling(values, i, Difficulty.Window);
                    Console.WriteLine(defs[i].Number + " " + s.Value.ToString("0.00")
                        + " roll" + roll.ToString("0.00")
                        + " col" + s.Colors + " cars" + s.Cars + " L" + s.Lanes + " d" + s.Depth
                        + " mech" + s.Mechanics + " open" + s.OpenAtStart
                        + " peak" + s.PeakBar + " slack" + s.Slack.ToString("0")
                        + " br" + s.Branch.ToString("0.00") + " nodes" + s.Nodes
                        + " " + (s.Solved ? "ok" : "UNSOLVED"));
                }
                return 0;
            }
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
            if (args.Length == 2 && args[0] == "--raise")
                return CurveRaise.Run(args[1]);
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
