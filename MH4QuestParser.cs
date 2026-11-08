using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LMDTool
{
    // v005 layout observed in 207 quest00 entries and the supplied loose quest.
    public static class MH4QuestParser
    {
        public static readonly string[] Languages = { "eng", "fre", "spa", "ger", "ita" };
        static readonly Encoding Utf16 = new UnicodeEncoding(false, false, true);
        const string NaturalHeader = "# LMDEditor MH4U quest v2";
        const string TxtHeader = "# LMDEditor MH4U quest v1; escapes: \\\\, \\n, \\r, \\t";
        public sealed class Field
        {
            public int PointerPosition;
            public int Offset;
            public int End;
            public string Text = "";
        }
        static int Read(byte[] data, int at)
        {
            if (at < 0 || at > data.Length - 4) throw new InvalidDataException("Quest pointer outside file.");
            uint n = (uint)(data[at] | data[at + 1] << 8 | data[at + 2] << 16 | data[at + 3] << 24);
            if (n > int.MaxValue) throw new InvalidDataException("Quest pointer too large.");
            return (int)n;
        }
        static void Write(byte[] data, int at, int n)
        { for (int i = 0; i < 4; i++) data[at + i] = (byte)(n >> (i * 8)); }

        public static Field[][] Analyze(byte[] data)
        {
            if (data.Length < 0x100 || Read(data, 0) != 0xA0 ||
                Encoding.ASCII.GetString(data, 4, 4) != "v005")
                throw new InvalidDataException("Unsupported MH4U quest header (expected 0xA0 / v005).");
            int root = Read(data, 0xBC);
            if (root < 0xF0 || root % 4 != 0 || root > data.Length - 20)
                throw new InvalidDataException("Invalid quest language table.");
            var tables = new int[5];
            for (int l = 0; l < 5; l++)
            {
                tables[l] = Read(data, root + l * 4);
                if (tables[l] < 0xF0 || tables[l] % 4 != 0 || tables[l] > data.Length - 28)
                    throw new InvalidDataException("Invalid quest field table.");
            }
            var ranges = tables.Select(t => (Start: t, End: t + 28)).ToList();
            ranges.Add((root, root + 20));
            for (int i = 0; i < ranges.Count; i++)
                for (int j = i + 1; j < ranges.Count; j++)
                    if (ranges[i].Start < ranges[j].End && ranges[j].Start < ranges[i].End)
                        throw new InvalidDataException("Overlapping quest tables.");
            var result = new Field[5][];
            for (int l = 0; l < 5; l++)
            {
                result[l] = new Field[7];
                for (int f = 0; f < 7; f++)
                {
                    int pos = tables[l] + f * 4, offset = Read(data, pos), end = offset;
                    if (offset < 0xF0 || offset % 2 != 0 || offset > data.Length - 2)
                        throw new InvalidDataException("Invalid quest text offset.");
                    while (end <= data.Length - 2 && (data[end] != 0 || data[end + 1] != 0)) end += 2;
                    if (end > data.Length - 2) throw new InvalidDataException("Unterminated quest text.");
                    if (ranges.Any(r => offset < r.End && r.Start < end + 2))
                        throw new InvalidDataException("Quest text overlaps pointer table.");
                    result[l][f] = new Field { PointerPosition = pos, Offset = offset, End = end + 2,
                        Text = Utf16.GetString(data, offset, end - offset) };
                }
            }
            return result;
        }
        static string Unescape(string value)
        {
            var b = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\') { b.Append(value[i]); continue; }
                if (++i >= value.Length) throw new InvalidDataException("Incomplete TXT escape.");
                b.Append(value[i] switch { '\\' => '\\', 'n' => '\n', 'r' => '\r', 't' => '\t',
                    _ => throw new InvalidDataException("Unknown TXT escape.") });
            }
            if (b.ToString().Contains('\0')) throw new InvalidDataException("NUL is not allowed in quest text.");
            return b.ToString();
        }
        public static string Export(byte[] data, int language)
        {
            if (language < 0 || language >= 5) throw new ArgumentOutOfRangeException(nameof(language));
            return FormatTxt(Analyze(data)[language].Select(f => f.Text).ToArray());
        }
        public static bool IsLegacyTxt(string text) => text.TrimStart('\uFEFF').StartsWith(TxtHeader, StringComparison.Ordinal);
        public static string FormatTxt(string[] fields)
        {
            if (fields.Length != 7) throw new InvalidDataException("Expected seven quest fields.");
            var lines = fields.Select((value, i) =>
            {
                value = value.Replace("\r\n", "\n").Replace("\r", "\n");
                if (value.Contains('\0') || Regex.IsMatch(value, @"(?m)^\[\d{4}\]"))
                    throw new InvalidDataException("Quest text contains NUL or a reserved index at line start.");
                return "[" + i.ToString("D4") + "] " + value;
            });
            return NaturalHeader + "\n" + string.Join("\n", lines) + "\n";
        }
        public static string[] ParseTxt(string text)
        {
            text = text.TrimStart('\uFEFF').Replace("\r\n", "\n");
            if (IsLegacyTxt(text)) return ParseLegacy(text);
            if (!text.StartsWith(NaturalHeader + "\n", StringComparison.Ordinal) || text.Contains('\0'))
                throw new InvalidDataException("Expected MH4U quest v2 header and seven indexed fields.");
            string body = text.Substring(NaturalHeader.Length + 1);
            // One final newline belongs to the document; additional ones belong to field 0006.
            if (body.EndsWith("\n", StringComparison.Ordinal)) body = body.Substring(0, body.Length - 1);
            var markers = Regex.Matches(body, @"(?m)^\[(\d{4})\]");
            if (markers.Count != 7 || markers[0].Index != 0)
                throw new InvalidDataException("Expected seven ordered quest indices.");
            var result = new string[7];
            for (int i = 0; i < 7; i++)
            {
                var marker = markers[i];
                int start = marker.Index + marker.Length;
                if (marker.Groups[1].Value != i.ToString("D4") || start >= body.Length || body[start] != ' ')
                    throw new InvalidDataException("Missing or reordered quest text index.");
                start++;
                int end = i == 6 ? body.Length : markers[i + 1].Index - 1;
                result[i] = body.Substring(start, end - start);
            }
            return result;
        }
        static string[] ParseLegacy(string text)
        {
            string[] lines = text.TrimStart('\uFEFF').Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 8 || lines[0] != TxtHeader || lines.Skip(8).Any(s => s.Length != 0))
                throw new InvalidDataException("Expected MH4U quest TXT header and seven indexed lines.");
            var result = new string[7];
            for (int i = 0; i < 7; i++)
            {
                string prefix = "[" + i.ToString("D4") + "] ";
                if (!lines[i + 1].StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidDataException("Missing or reordered quest text index: " + prefix);
                result[i] = Unescape(lines[i + 1].Substring(prefix.Length));
            }
            return result;
        }
        public static byte[] Import(byte[] original, IReadOnlyDictionary<int, string[]> translations)
        {
            var fields = Analyze(original);
            using var buffer = new MemoryStream();
            buffer.Write(original, 0, original.Length);
            var patches = new List<(int Position, int Offset)>();
            foreach (var language in translations.OrderBy(x => x.Key))
            {
                if (language.Key < 0 || language.Key >= 5 || language.Value.Length != 7)
                    throw new InvalidDataException("Invalid quest language or field count.");
                for (int f = 0; f < 7; f++)
                {
                    string value = language.Value[f];
                    if (value.Contains('\0')) throw new InvalidDataException("NUL is not allowed in quest text.");
                    if (value == fields[language.Key][f].Text) continue;
                    // Append instead of moving unknown gameplay sections or stale text copies.
                    while (buffer.Length % 4 != 0) buffer.WriteByte(0);
                    int at = checked((int)buffer.Length);
                    byte[] bytes = Utf16.GetBytes(value);
                    buffer.Write(bytes, 0, bytes.Length); buffer.WriteByte(0); buffer.WriteByte(0);
                    patches.Add((fields[language.Key][f].PointerPosition, at));
                }
            }
            if (patches.Count == 0) return (byte[])original.Clone();
            while (buffer.Length % 4 != 0) buffer.WriteByte(0);
            byte[] output = buffer.ToArray();
            foreach (var patch in patches) Write(output, patch.Position, patch.Offset);
            var check = Analyze(output);
            for (int l = 0; l < 5; l++)
                for (int f = 0; f < 7; f++)
                    if (check[l][f].Text != (translations.TryGetValue(l, out var values) ? values[f] : fields[l][f].Text))
                        throw new InvalidDataException("Quest text verification failed.");
            return output;
        }
    }
}
