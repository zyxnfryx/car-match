using System;
using System.Diagnostics;
using System.IO;

namespace CarMatch
{
    static class Program
    {
        static int Main()
        {
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
