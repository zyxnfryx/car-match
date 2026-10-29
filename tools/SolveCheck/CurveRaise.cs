using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarMatch
{
    // Lifts levels from 10 up until the 5-level rolling average no longer falls
    // and each Super Hard score beats both neighbors. Hides a back-row car, or
    // adds a hidden set of three behind the lanes, and keeps the result solvable.
    static class CurveRaise
    {
        static readonly string[] Names =
        {
            "coral", "amber", "lime", "teal", "violet", "rose", "sky", "gold"
        };

        public static int Run(string path)
        {
            string text = File.ReadAllText(path);
            var root = MiniJson.Parse(text) as Dictionary<string, object>;
            var rows = root["levels"] as List<object>;
            var defs = LevelJson.Parse(text);
            var boards = new Board[defs.Count];
            var scores = new float[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                boards[i] = defs[i].Board;
                var measured = Difficulty.Measure(boards[i]);
                scores[i] = measured.Value;
            }

            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                for (int i = 0; i < defs.Count; i++)
                {
                    float floor = i >= Difficulty.Window ? scores[i - Difficulty.Window] - 0.7f : 0f;
                    if (defs[i].SuperHard)
                    {
                        if (i > 0 && scores[i - 1] + 1.2f > floor) floor = scores[i - 1] + 1.2f;
                        if (i + 1 < defs.Count && scores[i + 1] + 1.2f > floor) floor = scores[i + 1] + 1.2f;
                    }
                    if (scores[i] >= floor - 0.05f) continue;
                    if (i < 9)
                    {
                        System.Console.Error.WriteLine("early level " + (i + 1) + " is under the floor and was left alone");
                        continue;
                    }
                    float before = scores[i];
                    if (!Lift(defs[i], rows[i] as Dictionary<string, object>, boards[i], floor, scores, i))
                    {
                        System.Console.Error.WriteLine("could not lift level " + (i + 1) + " from " + before.ToString("0.00") + " toward " + floor.ToString("0.00"));
                        continue;
                    }
                    changed = true;
                    System.Console.WriteLine("level " + (i + 1) + " " + before.ToString("0.00") + " -> " + scores[i].ToString("0.00"));
                }
                if (!changed) break;
            }

            var sb = new StringBuilder();
            WriteJson(root, sb, 0);
            sb.Append('\n');
            File.WriteAllText(path, sb.ToString());
            return 0;
        }

        static bool Lift(LevelDef def, Dictionary<string, object> row, Board board, float floor, float[] scores, int index)
        {
            var lanes = row["lanes"] as List<object>;
            var skipped = new HashSet<int>();
            int appends = 0;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (scores[index] >= floor - 0.05f) return true;
                int lane, cell;
                if (TryHide(board, lanes, skipped, out lane, out cell))
                {
                    if (!Accept(board, scores, index))
                    {
                        UndoHide(board, lanes, lane, cell);
                        skipped.Add(lane * 100 + cell);
                    }
                    continue;
                }
                if (def.Number == 11 || appends >= 2) return scores[index] >= floor - 0.05f;
                int[] depths;
                appends++;
                if (!TryAppend(board, lanes, out depths)) return scores[index] >= floor - 0.05f;
                if (!Accept(board, scores, index))
                {
                    UndoAppend(board, lanes, depths);
                    return scores[index] >= floor - 0.05f;
                }
            }
            return scores[index] >= floor - 0.05f;
        }

        static bool Accept(Board board, float[] scores, int index)
        {
            var measured = Difficulty.Measure(board, 200000);
            if (!measured.Solved || measured.Value < scores[index] + 0.4f) return false;
            scores[index] = measured.Value;
            return true;
        }

        static bool TryHide(Board board, List<object> lanes, HashSet<int> skipped, out int lane, out int cell)
        {
            lane = -1;
            cell = -1;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                var tokens = lanes[l] as List<object>;
                for (int c = cells.Count - 1; c >= 1; c--)
                {
                    if (skipped.Contains(l * 100 + c)) continue;
                    var slot = cells[c];
                    if (slot.Empty || slot.Tunnel || slot.Box || slot.Car.Hidden) continue;
                    string token = tokens[c] as string;
                    if (string.IsNullOrEmpty(token) || token.StartsWith("?") || token.StartsWith("tunnel:") || token.StartsWith("box:"))
                        continue;
                    slot.Car.Hidden = true;
                    tokens[c] = "?" + token;
                    lane = l;
                    cell = c;
                    return true;
                }
            }
            return false;
        }

        static void UndoHide(Board board, List<object> lanes, int lane, int cell)
        {
            var tokens = lanes[lane] as List<object>;
            board.Lanes[lane].Cells[cell].Car.Hidden = false;
            string token = tokens[cell] as string;
            if (token != null && token.StartsWith("?")) tokens[cell] = token.Substring(1);
        }

        static bool TryAppend(Board board, List<object> lanes, out int[] depths)
        {
            depths = new int[board.Lanes.Count];
            for (int l = 0; l < depths.Length; l++) depths[l] = board.Lanes[l].Cells.Count;
            var hist = BoardQuery.Histogram(board);
            int color = 0;
            int best = -1;
            for (int i = 0; i < hist.Length; i++)
                if (hist[i] > best) { best = hist[i]; color = i; }
            var safe = new List<int>();
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                bool tunnel = false;
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                    if (cells[c].Tunnel) tunnel = true;
                if (!tunnel) safe.Add(l);
            }
            if (safe.Count == 0) return false;
            string token = "?" + Names[color];
            var paint = new Car((CarColor)color, true);
            for (int n = 0; n < 3; n++)
            {
                int lane = safe[n % safe.Count];
                board.Lanes[lane].Cells.Add(new Slot { Empty = false, Car = paint });
                (lanes[lane] as List<object>).Add(token);
            }
            return true;
        }

        static void UndoAppend(Board board, List<object> lanes, int[] depths)
        {
            for (int l = 0; l < depths.Length; l++)
            {
                var cells = board.Lanes[l].Cells;
                var tokens = lanes[l] as List<object>;
                while (cells.Count > depths[l])
                {
                    cells.RemoveAt(cells.Count - 1);
                    tokens.RemoveAt(tokens.Count - 1);
                }
            }
        }

        static void WriteJson(object value, StringBuilder sb, int indent)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is string)
            {
                sb.Append('"');
                string s = (string)value;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                    else if (c == '\n') sb.Append("\\n");
                    else sb.Append(c);
                }
                sb.Append('"');
                return;
            }
            if (value is long) { sb.Append(((long)value).ToString(CultureInfo.InvariantCulture)); return; }
            if (value is double) { sb.Append(((double)value).ToString(CultureInfo.InvariantCulture)); return; }
            var list = value as List<object>;
            if (list != null)
            {
                sb.Append('[');
                bool flat = list.Count == 0 || list[0] is string || list[0] is long || list[0] is double || list[0] is bool;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (!flat) { sb.Append('\n'); Pad(sb, indent + 1); }
                    else if (i > 0) sb.Append(' ');
                    WriteJson(list[i], sb, indent + 1);
                }
                if (!flat && list.Count > 0) { sb.Append('\n'); Pad(sb, indent); }
                sb.Append(']');
                return;
            }
            var map = value as Dictionary<string, object>;
            if (map != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var pair in map)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('\n');
                    Pad(sb, indent + 1);
                    WriteJson(pair.Key, sb, indent + 1);
                    sb.Append(": ");
                    WriteJson(pair.Value, sb, indent + 1);
                }
                if (!first) { sb.Append('\n'); Pad(sb, indent); }
                sb.Append('}');
                return;
            }
            sb.Append(value.ToString());
        }

        static void Pad(StringBuilder sb, int n)
        {
            for (int i = 0; i < n; i++) sb.Append(' ');
        }
    }
}
