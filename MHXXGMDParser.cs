using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LMDTool
{
    public static class MHXXGMDParser
    {
        const int HeaderSize = 0x28;
        const int ExpectedVersion = 0x00010302;

        static readonly UTF8Encoding Utf8Strict = new UTF8Encoding(false, true);
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // ============================================================
        // PUBLIC API
        // ============================================================

        public static void ExportToTxt(string gmdPath, string txtPath)
        {
            byte[] data = File.ReadAllBytes(gmdPath);
            GmdLayout layout = AnalyzeLayout(data);

            using (StreamWriter writer = new StreamWriter(txtPath, false, Utf8NoBom))
            {

                for (int i = 0; i < layout.TextCount; i++)
                {
                    string name = i < layout.Names.Count ? layout.Names[i] : "";
                    string text = i < layout.Texts.Count ? layout.Texts[i] : "";

                    string id = string.IsNullOrEmpty(name)
                        ? i.ToString("D4")
                        : name;
                    writer.Write("[" + EscapeNameForTxt(id) + "]\t");
                    writer.Write(PrepareTextForTxt(text));
                    writer.WriteLine();
                }
            }
        }

        public static void ImportFromTxt(string originalGmdPath, string txtPath, string outputGmdPath)
        {
            byte[] original = File.ReadAllBytes(originalGmdPath);
            GmdLayout layout = AnalyzeLayout(original);

            TxtParseResult txt = ReadTxt(txtPath);
            TxtVerifyResult verify = VerifyTxtEntries(layout, txt);

            if (!verify.Success)
                throw new Exception(verify.ToString());

            string[] newTexts = verify.GetOrderedTexts(layout.TextCount);
            byte[] newTextPool = BuildNullTerminatedUtf8Pool(newTexts);

            /*
                Everything before TextPoolStart is preserved byte-for-byte
                - header
                - filename
                - unknown tables (hash/entry table + hash bucket table)
                - string name pool

                Only final text pool is rebuilt.
            */

            byte[] output = new byte[layout.TextPoolStart + newTextPool.Length];

            Buffer.BlockCopy(original, 0, output, 0, layout.TextPoolStart);
            Buffer.BlockCopy(newTextPool, 0, output, layout.TextPoolStart, newTextPool.Length);

            // Header offset 0x20 stores the text pool size.
            WriteInt32LE(output, 0x20, newTextPool.Length);

            File.WriteAllBytes(outputGmdPath, output);
        }

        public static void Verify(string gmdPath, string txtPath)
        {
            byte[] data = File.ReadAllBytes(gmdPath);
            GmdLayout layout = AnalyzeLayout(data);

            TxtParseResult txt = ReadTxt(txtPath);
            TxtVerifyResult result = VerifyTxtEntries(layout, txt);

            if (!result.Success)
                throw new Exception(result.ToString());
        }

        public static TxtVerifyResult VerifyTxt(string gmdPath, string txtPath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(gmdPath);
                GmdLayout layout = AnalyzeLayout(data);

                TxtParseResult txt = ReadTxt(txtPath);
                return VerifyTxtEntries(layout, txt);
            }
            catch (Exception ex)
            {
                TxtVerifyResult result = new TxtVerifyResult();
                result.Errors.Add("GMD error: " + ex.Message);
                return result;
            }
        }

        public static void DebugLayout(string gmdPath)
        {
            byte[] data = File.ReadAllBytes(gmdPath);
            GmdLayout layout = AnalyzeLayout(data);

            Console.WriteLine("FileName:       " + layout.FileName);
            Console.WriteLine("FileSize:       0x" + layout.FileSize.ToString("X"));
            Console.WriteLine("NameCount:      " + layout.NameCount);
            Console.WriteLine("TextCount:      " + layout.TextCount);
            Console.WriteLine("NamePoolSize:   0x" + layout.NamePoolSize.ToString("X"));
            Console.WriteLine("TextPoolSize:   0x" + layout.TextPoolSize.ToString("X"));
            Console.WriteLine("NamePoolStart:  0x" + layout.NamePoolStart.ToString("X"));
            Console.WriteLine("NamePoolEnd:    0x" + layout.NamePoolEnd.ToString("X"));
            Console.WriteLine("TextPoolStart:  0x" + layout.TextPoolStart.ToString("X"));
            Console.WriteLine("TextPoolEnd:    0x" + layout.TextPoolEnd.ToString("X"));
            Console.WriteLine();

            int preview = Math.Min(layout.TextCount, 30);

            for (int i = 0; i < preview; i++)
            {
                string name = i < layout.Names.Count ? layout.Names[i] : "";
                string text = i < layout.Texts.Count ? layout.Texts[i] : "";

                Console.WriteLine(
                    "[" + i.ToString("D4") + "] " +
                    "name='" + name + "' " +
                    "text='" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "'"
                );
            }
        }

        // ============================================================
        // GMD ANALYSIS
        // ============================================================

        static GmdLayout AnalyzeLayout(byte[] data)
        {
            if (data == null || data.Length < HeaderSize)
                throw new Exception("File is too small.");

            if (data[0] != 0x47 || data[1] != 0x4D || data[2] != 0x44 || data[3] != 0x00)
                throw new Exception("Invalid GMD magic.");

            int version = ReadInt32LE(data, 0x04);

            if (version != ExpectedVersion)
                throw new Exception("Unsupported GMD version: 0x" + version.ToString("X8"));

            int nameCount = ReadInt32LE(data, 0x14);
            int textCount = ReadInt32LE(data, 0x18);
            int namePoolSize = ReadInt32LE(data, 0x1C);
            int textPoolSize = ReadInt32LE(data, 0x20);
            int fileNameLength = ReadInt32LE(data, 0x24);

            /*
                MHXX has two valid GMD layouts that share the same header:

                - "Named" files (UI/menu text, e.g. buttonName, CommonMsg,
                  GC_background): nameCount > 0 and namePoolSize > 0. These
                  have a hash/entry table + hash bucket table + name pool
                  in front of the text pool.

                - "Data-only" files (e.g. activityData, amuletData,
                  catSkillData, questData_*): nameCount == 0 and
                  namePoolSize == 0. There is no hash table, no bucket
                  table, and no name pool at all - the text pool starts
                  immediately after the filename.

                Both fields must agree on which layout this file uses;
                a mismatch (one zero, the other not) means the header is
                actually corrupt or unrecognized, so that case still
                throws.
            */

            bool hasNames = nameCount > 0 || namePoolSize > 0;

            if (hasNames)
            {
                if (nameCount <= 0)
                    throw new Exception("Invalid name count.");

                if (namePoolSize <= 0)
                    throw new Exception("Invalid name pool size.");
            }

            if (textCount <= 0)
                throw new Exception("Invalid text count.");

            if (textPoolSize <= 0)
                throw new Exception("Invalid text pool size.");

            if (fileNameLength < 0 || HeaderSize + fileNameLength >= data.Length)
                throw new Exception("Invalid filename length.");

            string fileName = Utf8Strict.GetString(data, HeaderSize, fileNameLength);

            int fileNameTerminator = HeaderSize + fileNameLength;

            if (data[fileNameTerminator] != 0x00)
                throw new Exception("Filename is not null-terminated.");

            /*
                MHXX strategy:

                - Text pool is the last block.
                - Name pool is immediately before the text pool.
                - Both pools are sequential UTF-8 strings separated by 00.
                - Between the filename and the name pool sits a hash/entry
                  table (272 * 20 bytes: index, 8-byte hash, name-pool
                  offset, extra field) followed by a hash bucket lookup
                  table (272 * 4 bytes, using 0xFFFFFFFF as an empty-slot
                  sentinel). Neither table is needed to read or rewrite the
                  text, so it is treated as an opaque block and preserved
                  byte-for-byte during import.
            */

            int textPoolEnd = data.Length;
            int textPoolStart;
            int namePoolEnd;
            int namePoolStart;

            if (hasNames)
            {
                // Named files retain the existing layout calculation.
                textPoolStart = textPoolEnd - textPoolSize;
                namePoolEnd = textPoolStart;
                namePoolStart = namePoolEnd - namePoolSize;

                if (textPoolStart <= HeaderSize || textPoolStart >= data.Length)
                    throw new Exception("Invalid text pool location. File bytes: " +
                        data.Length + "; declared text pool bytes: " + textPoolSize + ".");

                if (namePoolStart <= fileNameTerminator || namePoolStart >= namePoolEnd)
                    throw new Exception("Invalid name pool location.");
            }
            else
            {
                // Nameless files have no intervening tables. Some edited GMDs
                // retain an obsolete pool size; derive the start structurally.
                // ReadStringPool below must still validate every declared entry,
                // UTF-8 and NUL termination, and reject extra nonzero data.
                textPoolStart = fileNameTerminator + 1;
                namePoolStart = namePoolEnd = textPoolStart;
                textPoolSize = textPoolEnd - textPoolStart;
            }

            GmdLayout layout = new GmdLayout();

            layout.FileSize = data.Length;
            layout.FileName = fileName;

            layout.NameCount = nameCount;
            layout.TextCount = textCount;

            layout.NamePoolSize = namePoolSize;
            layout.TextPoolSize = textPoolSize;

            layout.NamePoolStart = namePoolStart;
            layout.NamePoolEnd = namePoolEnd;

            layout.TextPoolStart = textPoolStart;
            layout.TextPoolEnd = textPoolEnd;

            layout.Names = ReadStringPool(data, namePoolStart, namePoolEnd, nameCount, "name");
            layout.Texts = ReadStringPool(data, textPoolStart, textPoolEnd, textCount, "text");

            return layout;
        }

        static List<string> ReadStringPool(
            byte[] data,
            int start,
            int end,
            int expectedCount,
            string poolName
        )
        {
            List<string> result = new List<string>();

            int pos = start;

            for (int i = 0; i < expectedCount; i++)
            {
                if (pos >= end)
                {
                    throw new Exception(
                        poolName + " pool ended before entry [" + i.ToString("D4") + "]."
                    );
                }

                string value = ReadNullTerminatedUtf8(data, pos, end, out int nextPos);
                result.Add(value);

                pos = nextPos;
            }

            if (pos < end)
            {
                bool onlyZeroPadding = true;

                for (int i = pos; i < end; i++)
                {
                    if (data[i] != 0x00)
                    {
                        onlyZeroPadding = false;
                        break;
                    }
                }

                if (!onlyZeroPadding)
                {
                    throw new Exception(
                        poolName + " pool has non-zero bytes after the expected string count."
                    );
                }
            }

            return result;
        }

        static string ReadNullTerminatedUtf8(byte[] data, int start, int limit, out int nextPos)
        {
            if (start < 0 || start >= limit || limit > data.Length)
                throw new Exception("Invalid string range.");

            int end = start;

            while (end < limit && data[end] != 0x00)
                end++;

            if (end >= limit)
                throw new Exception("Missing null terminator.");

            string value = Utf8Strict.GetString(data, start, end - start);

            nextPos = end + 1;
            return value;
        }

        // ============================================================
        // TXT PARSER
        // ============================================================

        static TxtParseResult ReadTxt(string path)
        {
            string[] lines = File.ReadAllLines(path, Utf8Strict);

            TxtParseResult result = new TxtParseResult();

            string currentId = "";
            int currentLine = -1;
            StringBuilder currentText = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (TryParseEntryLine(line,out string id,out string text)){
                    FlushTxtEntry(result,currentId,currentLine,currentText);
                    currentLine = i + 1;
                    currentId = id;
                    currentText = new StringBuilder();
                    currentText.Append(text);
                }
                else
                {
                    if (currentText == null)
                    {
                        string trimmed = line.TrimStart();

                        if (trimmed.Length == 0)
                            continue;

                        if (trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                            continue;

                        result.Errors.Add("Line " + (i + 1) + ": content before first entry.");
                    }
                    else
                    {
                        /*
                            Physical TXT line breaks are converted back into a
                            REAL CR+LF (0x0D 0x0A) inside the string, matching
                            how MHXX actually stores in-game line breaks (this
                            was verified directly against the GMD hex dump:
                            line breaks inside text are physical CR/LF bytes,
                            not a literal "\r\n" text command).
                        */
                        currentText.Append('\r');
                        currentText.Append('\n');
                        currentText.Append(line);
                    }
                }
            }

            FlushTxtEntry(result,currentId,currentLine,currentText);
            return result;
        }

        static bool TryParseEntryLine(
    string line,
    out string id,
    out string text
)
        {
            id = "";
            text = "";

            if (string.IsNullOrEmpty(line))
                return false;

            if (line[0] != '[')
                return false;

            int close = line.IndexOf(']');

            if (close <= 1)
                return false;

            id = UnescapeNameFromTxt(
                line.Substring(1, close - 1)
            );

            string rest = line.Substring(close + 1);

            if (rest.StartsWith("\t"))
                rest = rest.Substring(1);
            else if (rest.StartsWith(" "))
                rest = rest.Substring(1);

            text = rest;

            return true;
        }

        static void FlushTxtEntry(
    TxtParseResult result,
    string id,
    int lineNumber,
    StringBuilder text
)
        {
            if (text == null)
                return;

            TxtEntry entry = new TxtEntry();

            entry.Id = id ?? "";
            entry.LineNumber = lineNumber;
            entry.Text = UnescapeTextFromTxt(text.ToString());

            result.Entries.Add(entry);
        }

        static TxtVerifyResult VerifyTxtEntries(GmdLayout layout, TxtParseResult txt)
        {
            TxtVerifyResult result = new TxtVerifyResult();

            result.ExpectedCount = layout.TextCount;
            result.ActualCount = txt.Entries.Count;

            foreach (string error in txt.Errors)
                result.Errors.Add(error);

            if (txt.Entries.Count != layout.TextCount)
            {
                result.Errors.Add(
                    "String count mismatch. GMD: " +
                    layout.TextCount +
                    ", TXT: " +
                    txt.Entries.Count +
                    "."
                );
            }

            for (int i = 0; i < layout.TextCount; i++)
            {
                if (i >= txt.Entries.Count)
                    break;

                TxtEntry entry = txt.Entries[i];

                string expectedName =
                    i < layout.Names.Count
                    ? layout.Names[i]
                    : i.ToString("D4");

                if (entry.Id != expectedName)
                {
                    result.Errors.Add(
                        "Entry " + (i + 1) +
                        " expected '" + expectedName +
                        "' but found '" + entry.Id + "'."
                    );
                }

                if (entry.Text.IndexOf('\0') >= 0)
                {
                    result.Errors.Add(
                        "Entry '" + entry.Id +
                        "' contains NUL character."
                    );
                }

                result.Texts[i] = entry.Text;
            }

            return result;
        }

        // ============================================================
        // TXT EXPORT FORMATTING
        // ============================================================

        static string EscapeNameForTxt(string text)
        {
            if (text == null)
                return "";

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case ' ':
                        if (i == 0 || i == text.Length - 1)
                            sb.Append("\\s");
                        else
                            sb.Append(' ');
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        static string PrepareTextForTxt(string text)
        {
            if (text == null)
                return "";

            /*
                MHXX stores in-game line breaks as a REAL physical CR+LF
                (0x0D 0x0A) inside the UTF-8 string itself. For TXT editing,
                normalize that to a single \n so the rest of this method has
                one consistent line-break marker to work with before
                emitting it as a real physical line break in the output
                file (handled by the '\n' case below).
            */
            text = text.Replace("\r\n", "\n");
            text = text.Replace("\r", "\n");

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;

                    case '\n':
                        sb.Append(Environment.NewLine);
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case ' ':
                        if (i == 0 || i == text.Length - 1)
                            sb.Append("\\s");
                        else
                            sb.Append(' ');
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        // ============================================================
        // TXT IMPORT UNESCAPING
        // ============================================================

        static string UnescapeNameFromTxt(string text)
        {
            if (text == null)
                return "";

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c != '\\' || i + 1 >= text.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char n = text[++i];

                switch (n)
                {
                    case '\\':
                        sb.Append('\\');
                        break;

                    case 't':
                        sb.Append('\t');
                        break;

                    case 's':
                        sb.Append(' ');
                        break;

                    default:
                        sb.Append('\\');
                        sb.Append(n);
                        break;
                }
            }

            return sb.ToString();
        }

        static string UnescapeTextFromTxt(string text)
        {
            if (text == null)
                return "";

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c != '\\' || i + 1 >= text.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char n = text[++i];

                switch (n)
                {
                    case '\\':
                        sb.Append('\\');
                        break;

                    case 't':
                        sb.Append('\t');
                        break;

                    case 's':
                        sb.Append(' ');
                        break;

                    case '0':
                        sb.Append('\0');
                        break;

                    default:
                        sb.Append('\\');
                        sb.Append(n);
                        break;
                }
            }

            return sb.ToString();
        }

        // ============================================================
        // REBUILD
        // ============================================================

        static byte[] BuildNullTerminatedUtf8Pool(string[] values)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                for (int i = 0; i < values.Length; i++)
                {
                    string value = values[i] ?? "";

                    if (value.IndexOf('\0') >= 0)
                        throw new Exception("Entry [" + i.ToString("D4") + "] contains NUL character.");

                    /*
                        Normalize any text-mode \r\n (Environment.NewLine on
                        Windows) back to a plain \n, then expand every \n
                        into the physical CR+LF that MHXX expects for
                        in-game line breaks. This mirrors PrepareTextForTxt
                        and keeps the round-trip symmetric regardless of
                        which newline style ended up in the in-memory string.
                    */
                    string normalized = value.Replace("\r\n", "\n").Replace("\n", "\r\n");

                    byte[] bytes = Utf8Strict.GetBytes(normalized);

                    ms.Write(bytes, 0, bytes.Length);
                    ms.WriteByte(0x00);
                }

                return ms.ToArray();
            }
        }

        // ============================================================
        // BINARY HELPERS
        // ============================================================

        static int ReadInt32LE(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new Exception("Unexpected end of file.");

            return data[offset]
                 | (data[offset + 1] << 8)
                 | (data[offset + 2] << 16)
                 | (data[offset + 3] << 24);
        }

        static void WriteInt32LE(byte[] data, int offset, int value)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new Exception("Unexpected end of file.");

            data[offset + 0] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        // ============================================================
        // TYPES
        // ============================================================

        class GmdLayout
        {
            public int FileSize;
            public string FileName = "";

            public int NameCount;
            public int TextCount;

            public int NamePoolSize;
            public int TextPoolSize;

            public int NamePoolStart;
            public int NamePoolEnd;

            public int TextPoolStart;
            public int TextPoolEnd;

            public List<string> Names = new List<string>();
            public List<string> Texts = new List<string>();
        }

        class TxtEntry
        {
            public string Id = "";
            public int LineNumber;
            public string Text = "";
        }

        class TxtParseResult
        {
            public List<TxtEntry> Entries = new List<TxtEntry>();
            public List<string> Errors = new List<string>();
        }

        public class TxtVerifyResult
        {
            public int ExpectedCount;
            public int ActualCount;

            public Dictionary<int, string> Texts = new Dictionary<int, string>();
            public List<string> Errors = new List<string>();

            public bool Success
            {
                get { return Errors.Count == 0; }
            }

            public string[] GetOrderedTexts(int count)
            {
                string[] result = new string[count];

                for (int i = 0; i < count; i++)
                    result[i] = Texts.ContainsKey(i) ? Texts[i] : "";

                return result;
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Expected entries: " + ExpectedCount);
                sb.AppendLine("Found entries: " + ActualCount);

                if (Errors.Count == 0)
                {
                    sb.AppendLine("TXT verification OK.");
                }
                else
                {
                    sb.AppendLine("TXT verification failed:");

                    foreach (string error in Errors)
                        sb.AppendLine("- " + error);
                }

                return sb.ToString();
            }
        }
    }
}