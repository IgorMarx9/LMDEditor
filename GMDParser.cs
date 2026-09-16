using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LMDTool
{
    // MH3U GMD v0x00010201: 32-byte header, filename+NUL,
    // labelCount * 8 bytes, name pool, text pool. No marker scanning.
    public static class GMDParser
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly Regex Entry = new Regex(@"^\[(\d+)\][ \t](.*)$");
        sealed class Layout
        {
            public int Start;
            public int Count;
            public string[] Texts = Array.Empty<string>();
        }
        static uint U(byte[] b, int p) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p, 4));
        static Layout Analyze(byte[] b)
        {
            if (b.Length < 32 || U(b, 0) != 0x00444D47 || U(b, 4) != 0x00010201)
                throw new InvalidDataException("Unsupported MH3U GMD signature/version.");
            long names = U(b, 12), count = U(b, 16), nameSize = U(b, 20), textSize = U(b, 24), fileSize = U(b, 28);
            long table = 32 + fileSize + 1;
            long pool = table + names * 8;
            long start = pool + nameSize;
            if (table > b.Length || start > b.Length || start + textSize != b.Length || count > textSize ||
                b[(int)table - 1] != 0)
                throw new InvalidDataException("Invalid GMD section sizes.");
            Utf8.GetString(b, 32, (int)fileSize);
            ReadPool(b, (int)pool, (int)start, checked((int)names));
            for (long i = 0; i < names; i++)
                if (U(b, checked((int)(table + i * 8))) >= count)
                    throw new InvalidDataException("Invalid label text index.");
            return new Layout { Start = (int)start, Count = (int)count,
                Texts = ReadPool(b, (int)start, b.Length, (int)count) };
        }
        static string[] ReadPool(byte[] b, int start, int end, int count)
        {
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                int stop = start;
                while (stop < end && b[stop] != 0) stop++;
                if (stop == end) throw new InvalidDataException("Missing GMD string terminator.");
                result[i] = Utf8.GetString(b, start, stop - start);
                start = stop + 1;
            }
            if (start != end) throw new InvalidDataException("Unexpected bytes after GMD pool.");
            return result;
        }
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\r", "\\r")
            .Replace("\n", "\\n").Replace("\t", "\\t");
        static string Unescape(string value)
        {
            var b = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\') { b.Append(value[i]); continue; }
                if (++i >= value.Length) throw new InvalidDataException("Incomplete TXT escape.");
                b.Append(value[i] switch { '\\' => '\\', 'r' => '\r', 'n' => '\n', 't' => '\t',
                    _ => throw new InvalidDataException("Unknown TXT escape.") });
            }
            return b.ToString();
        }
        const string Format = "# LMDEditor MH3U GMD v2; escapes: \\\\, \\r, \\n, \\t";
        static string[] Load(string path, Layout layout)
        {
            string[] lines = File.ReadAllLines(path, Utf8);
            bool escaped = lines.Length > 0 && lines[0] == Format;
            var list = new List<string>();
            for (int i = escaped ? 1 : 0; i < lines.Length; i++)
            {
                Match m = Entry.Match(lines[i]);
                if (m.Success)
                {
                    if (!int.TryParse(m.Groups[1].Value, out int index) || index != list.Count)
                        throw new InvalidDataException("TXT indices must be sequential from [0000].");
                    list.Add(escaped ? Unescape(m.Groups[2].Value) : m.Groups[2].Value);
                }
                else if (!escaped && list.Count > 0) list[^1] += "\r\n" + lines[i];
                else throw new InvalidDataException("Invalid TXT line " + (i + 1));
            }
            if (list.Count != layout.Count)
                throw new InvalidDataException("Expected " + layout.Count + " texts; found " + list.Count +
                    ". Re-export with this version: old exports may contain TMSG labels.");
            if (list.Any(t => t.Contains('\0'))) throw new InvalidDataException("NUL is forbidden in TXT text.");
            return list.ToArray();
        }
        public static void ExportToTxt(string file, string outTxt)
        {
            Layout layout = Analyze(File.ReadAllBytes(file));
            using var writer = new StreamWriter(outTxt, false, Utf8);
            for (int i = 0; i < layout.Count; i++)
            {
                // Plain text: physical line breaks/tabs, literal backslashes, no escape header.
                string text = layout.Texts[i].Replace("\r\n", "\n").Replace("\r", "\n");
                if (text.Split('\n').Skip(1).Any(line => Entry.IsMatch(line)))
                    throw new InvalidDataException("Text continuation resembles a numbered entry; cannot export unambiguously.");
                writer.WriteLine($"[{i:0000}] {text.Replace("\n", Environment.NewLine)}");
            }
        }
        public static void Verify(string file, string txt) => Load(txt, Analyze(File.ReadAllBytes(file)));
        public static void ImportFromTxt(string originalFile, string txtFile, string outFile)
        {
            byte[] original = File.ReadAllBytes(originalFile);
            Layout layout = Analyze(original);
            string[] texts = Load(txtFile, layout);
            byte[] output;
            if (texts.SequenceEqual(layout.Texts)) output = original;
            else
            {
                using var stream = new MemoryStream();
                stream.Write(original, 0, layout.Start);
                foreach (string text in texts) { stream.Write(Utf8.GetBytes(text)); stream.WriteByte(0); }
                output = stream.ToArray();
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(24, 4), (uint)(output.Length - layout.Start));
                if (!Analyze(output).Texts.SequenceEqual(texts)) throw new InvalidDataException("GMD rebuild verification failed.");
            }
            File.WriteAllBytes(outFile, output);
        }
    }
}
