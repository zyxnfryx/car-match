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
