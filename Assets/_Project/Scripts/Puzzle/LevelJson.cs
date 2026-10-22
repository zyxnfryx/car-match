using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CarMatch
{
    public sealed class LevelDef
    {
        public int Number;
        public string Id;
        public string Title;
        public string Blurb;
        // Every 10th level: a tougher spike with a red 'Super Hard' badge; pays Economy.SuperHardPay.
        public bool SuperHard;
        public Board Board;
        public LevelHooks Hooks = new LevelHooks();
    }

    // Levels are JSON so the same file feeds the player, the solver, and the tests.
    public static class LevelJson
    {
        public static List<LevelDef> Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new System.FormatException("Level JSON is empty.");
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("levels"))
                throw new System.FormatException("Level JSON needs a levels array.");
            var rows = root["levels"] as List<object>;
            if (rows == null)
                throw new System.FormatException("levels must be an array.");
            var defs = new List<LevelDef>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i] as Dictionary<string, object>;
                if (row == null)
                    throw new System.FormatException("Level " + (i + 1) + " is not an object.");
                var def = new LevelDef
                {
                    Number = i + 1,
                    Id = Str(row, "id", "level-" + (i + 1)),
                    Title = Str(row, "title", "Level " + (i + 1)),
                    Blurb = Str(row, "blurb", ""),
                    SuperHard = Str(row, "tier", "") == "superhard",
                    Board = Build(row, i + 1),
                    Hooks = LevelHooks.Read(row)
                };
                HiddenReveal.Sweep(def.Board);
                defs.Add(def);
            }
            return defs;
        }

        static Board Build(Dictionary<string, object> row, int number)
        {
            var lanes = row.ContainsKey("lanes") ? row["lanes"] as List<object> : null;
            if (lanes == null || lanes.Count == 0)
                throw new System.FormatException("Level " + number + " has no lanes.");
            var board = new Board();
            for (int l = 0; l < lanes.Count; l++)
            {
                var cells = lanes[l] as List<object>;
                if (cells == null)
                    throw new System.FormatException("Level " + number + " lane " + l + " is not an array.");
                var lane = new Lane();
                for (int c = 0; c < cells.Count; c++)
                    lane.Cells.Add(ParseCell(AsString(cells[c]), number));
                board.Lanes.Add(lane);
            }
            if (row.ContainsKey("sides") && row["sides"] != null)
            {
                var sides = row["sides"] as List<object>;
                if (sides == null)
                    throw new System.FormatException("Level " + number + " sides must be an array.");
                for (int s = 0; s < sides.Count; s++)
                {
                    var sideRow = sides[s] as Dictionary<string, object>;
                    if (sideRow == null)
                        throw new System.FormatException("Level " + number + " side " + s + " is not an object.");
                    var side = new SideLot
                    {
                        Lane = AsInt(sideRow["lane"]),
                        Cell = AsInt(sideRow["at"])
                    };
                    var cars = sideRow["cars"] as List<object>;
                    if (cars == null || cars.Count == 0)
                        throw new System.FormatException("Level " + number + " side " + s + " has no cars.");
                    for (int c = 0; c < cars.Count; c++)
                        side.Queue.Add(ParseCar(AsString(cars[c]), number));
                    if ((uint)side.Lane >= (uint)board.Lanes.Count ||
                        (uint)side.Cell >= (uint)board.Lanes[side.Lane].Cells.Count)
                        throw new System.FormatException("Level " + number + " side " + s + " is off the lane.");
                    board.Sides.Add(side);
                }
            }
            if (row.ContainsKey("garages") && row["garages"] != null)
            {
                var garages = row["garages"] as List<object>;
                if (garages == null)
                    throw new System.FormatException("Level " + number + " garages must be an array.");
                for (int gi = 0; gi < garages.Count; gi++)
                {
                    var gr = garages[gi] as Dictionary<string, object>;
                    if (gr == null)
                        throw new System.FormatException("Level " + number + " garage " + gi + " is not an object.");
                    var g = new Garage { Lane = AsInt(gr["lane"]) };
                    var cells = gr["cells"] as List<object>;
                    var cars = gr["cars"] as List<object>;
                    if (cells == null || cells.Count == 0 || cars == null || cars.Count == 0)
                        throw new System.FormatException("Level " + number + " garage " + gi + " needs cells and cars.");
                    for (int c = 0; c < cells.Count; c++) g.Cells.Add(AsInt(cells[c]));
                    for (int c = 0; c < cars.Count; c++) g.Queue.Add(ParseCar(AsString(cars[c]), number));
                    if ((uint)g.Lane >= (uint)board.Lanes.Count)
                        throw new System.FormatException("Level " + number + " garage " + gi + " is off the lot.");
                    var laneCells = board.Lanes[g.Lane].Cells;
                    for (int c = 0; c < g.Cells.Count; c++)
                        if ((uint)g.Cells[c] >= (uint)laneCells.Count || laneCells[g.Cells[c]].Tunnel)
                            throw new System.FormatException("Level " + number + " garage " + gi + " sits off its lane or on a tunnel.");
                    board.Garages.Add(g);
                }
            }
            return board;
        }

        static Slot ParseCell(string token, int number)
        {
            token = token == null ? "" : token.Trim();
            if (token.Length == 0 || token == "-")
                return new Slot { Empty = true };
            if (token.StartsWith("box:"))
                return new Slot { Empty = false, Box = true, Car = ParseCar(token.Substring(4), number) };
            if (token.StartsWith("tunnel:"))
            {
                var body = token.Substring("tunnel:".Length);
                var parts = body.Split(',');
                var slot = new Slot
                {
                    Empty = false,
                    Tunnel = true,
                    Queue = new List<Car>(parts.Length)
                };
                for (int i = 0; i < parts.Length; i++)
                {
                    var piece = parts[i].Trim();
                    if (piece.Length == 0)
                        throw new System.FormatException("Level " + number + " has an empty tunnel car.");
                    slot.Queue.Add(ParseCar(piece, number));
                }
                if (slot.Queue.Count == 0)
                    throw new System.FormatException("Level " + number + " has an empty tunnel.");
                return slot;
            }
            return new Slot { Empty = false, Car = ParseCar(token, number) };
        }

        static Car ParseCar(string token, int number)
        {
            token = token.Trim();
            bool hidden = token.StartsWith("?");
            if (hidden) token = token.Substring(1);
            CarColor color;
            if (!Palette.TryParse(token, out color))
                throw new System.FormatException("Level " + number + " has unknown color '" + token + "'.");
            return new Car(color, hidden);
        }

        static string Str(Dictionary<string, object> row, string key, string fallback)
        {
            if (!row.ContainsKey(key) || row[key] == null) return fallback;
            return AsString(row[key]);
        }

        static string AsString(object value)
        {
            return value as string ?? (value == null ? "" : value.ToString());
        }

        static int AsInt(object value)
        {
            if (value is long) return (int)(long)value;
            if (value is int) return (int)value;
            if (value is double) return (int)(double)value;
            long parsed;
            if (value is string && long.TryParse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return (int)parsed;
            throw new System.FormatException("Expected an integer.");
        }
    }

    sealed class MiniJson
    {
        readonly string _s;
        int _i;

        public static object Parse(string json)
        {
            var parser = new MiniJson(json);
            var value = parser.ReadValue();
            parser.Skip();
            if (parser._i != parser._s.Length)
                throw new System.FormatException("Trailing data in JSON at " + parser._i + ".");
            return value;
        }

        MiniJson(string json)
        {
            _s = json ?? "";
        }

        object ReadValue()
        {
            Skip();
            if (_i >= _s.Length) throw new System.FormatException("Unexpected end of JSON.");
            char c = _s[_i];
            if (c == '{') return ReadObject();
            if (c == '[') return ReadArray();
            if (c == '"') return ReadString();
            if (c == 't') return ReadLiteral("true", true);
            if (c == 'f') return ReadLiteral("false", false);
            if (c == 'n') return ReadLiteral("null", null);
            return ReadNumber();
        }

        Dictionary<string, object> ReadObject()
        {
            Expect('{');
            var map = new Dictionary<string, object>();
            Skip();
            if (Peek('}'))
            {
                _i++;
                return map;
            }
            while (true)
            {
                Skip();
                string key = ReadString();
                Skip();
                Expect(':');
                map[key] = ReadValue();
                Skip();
                if (Peek('}'))
                {
                    _i++;
                    return map;
                }
                Expect(',');
            }
        }

        List<object> ReadArray()
        {
            Expect('[');
            var list = new List<object>();
            Skip();
            if (Peek(']'))
            {
                _i++;
                return list;
            }
            while (true)
            {
                list.Add(ReadValue());
                Skip();
                if (Peek(']'))
                {
                    _i++;
                    return list;
                }
                Expect(',');
            }
        }

        string ReadString()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (_i < _s.Length)
            {
                char c = _s[_i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (_i >= _s.Length) break;
                char e = _s[_i++];
                if (e == '"' || e == '\\' || e == '/') sb.Append(e);
                else if (e == 'b') sb.Append('\b');
                else if (e == 'f') sb.Append('\f');
                else if (e == 'n') sb.Append('\n');
                else if (e == 'r') sb.Append('\r');
                else if (e == 't') sb.Append('\t');
                else if (e == 'u')
                {
                    if (_i + 4 > _s.Length) throw new System.FormatException("Bad unicode escape.");
                    int code = int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    sb.Append((char)code);
                    _i += 4;
                }
                else throw new System.FormatException("Bad escape \\" + e);
            }
            throw new System.FormatException("Unclosed string.");
        }

        object ReadNumber()
        {
            int start = _i;
            if (Peek('-')) _i++;
            while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
            bool frac = false;
            if (Peek('.'))
            {
                frac = true;
                _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
            }
            if (Peek('e') || Peek('E'))
            {
                frac = true;
                _i++;
                if (Peek('+') || Peek('-')) _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
            }
            string text = _s.Substring(start, _i - start);
            if (!frac)
            {
                long whole;
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out whole))
                    throw new System.FormatException("Bad number " + text);
                return whole;
            }
            double real;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out real))
                throw new System.FormatException("Bad number " + text);
            return real;
        }

        object ReadLiteral(string word, object value)
        {
            if (_i + word.Length > _s.Length || _s.Substring(_i, word.Length) != word)
                throw new System.FormatException("Expected " + word);
            _i += word.Length;
            return value;
        }

        void Skip()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }

        bool Peek(char c)
        {
            return _i < _s.Length && _s[_i] == c;
        }

        void Expect(char c)
        {
            Skip();
            if (!Peek(c))
                throw new System.FormatException("Expected '" + c + "' at " + _i + ".");
            _i++;
        }
    }
}
