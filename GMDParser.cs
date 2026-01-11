using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace LMDTool
{
    public static class GMDParser
    {
        // =========================================================
        // PUBLIC
        // =========================================================

        public static void ExportToTxt(string file, string outTxt)
        {
            byte[] data = File.ReadAllBytes(file);

            int start = FindFirstStringOffset(data);
            if (start < 0)
                throw new Exception("GMD header not found.");

            var strings = ReadStrings(data, start);

            using StreamWriter sw = new StreamWriter(outTxt, false, Encoding.UTF8);

            for (int i = 0; i < strings.Count; i++)
                sw.WriteLine($"[{i:0000}] {strings[i]}");
        }

        public static void ImportFromTxt(string originalFile, string txtFile, string outFile)
        {
            throw new Exception("GMD import is not implemented yet. (Export + research phase)");
        }

        public static void Verify(string file, string txt)
        {
            byte[] data = File.ReadAllBytes(file);

            int start = FindFirstStringOffset(data);
            if (start < 0)
                throw new Exception("GMD header not found.");

            var binStrings = ReadStrings(data, start);
            var txtStrings = LoadTxt(txt);

            if (binStrings.Count != txtStrings.Count)
                throw new Exception("String count mismatch.");

            for (int i = 0; i < binStrings.Count; i++)
            {
                string a = Normalize(binStrings[i]);
                string b = Normalize(txtStrings[i]);

                if (a != b)
                    throw new Exception($"Mismatch at [{i:0000}]");
            }
        }


        // =========================================================
        // CORE
        // =========================================================

        static int FindFirstStringOffset(byte[] data)
        {
            // procura "GMD\0"
            for (int i = 0; i < data.Length - 4; i++)
            {
                if (data[i] == 0x47 && data[i + 1] == 0x4D &&
                    data[i + 2] == 0x44 && data[i + 3] == 0x00)
                {
                    // pula header + bloco binário
                    for (int j = i + 4; j < data.Length - 4; j++)
                    {
                        if (IsPrintable(data[j]) && data[j + 1] == 0x00)
                            return j;
                    }
                }
            }
            return -1;
        }

        static List<string> ReadStrings(byte[] data, int start)
        {
            List<string> list = new List<string>();
            List<byte> buffer = new List<byte>();

            for (int i = start; i < data.Length; i++)
            {
                byte b = data[i];

                if (b == 0x00)
                {
                    if (buffer.Count > 1)
                    {
                        string s = Encoding.ASCII.GetString(buffer.ToArray());

                        if (IsValidString(s))
                            list.Add(s);
                    }

                    buffer.Clear();
                }
                else
                {
                    buffer.Add(b);
                }
            }

            return list;
        }

        static bool IsPrintable(byte b)
        {
            return (b >= 0x20 && b <= 0x7E);
        }
        static string Normalize(string s)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in s)
            {
                if (!char.IsControl(c) || c == '\n')
                    sb.Append(c);
            }

            return sb.ToString().Trim();
        }


        static bool IsValidString(string s)
        {
            if (s.Length < 2) return false;

            int letters = 0;
            foreach (char c in s)
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '.' || c == '-' || c == '?')
                    letters++;

            return letters > s.Length / 2;
        }

        // =========================================================
        // TXT
        // =========================================================

        static List<string> LoadTxt(string path)
        {
            var list = new List<string>();

            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("[")) continue;
                int p = line.IndexOf("] ");
                if (p < 0) continue;

                list.Add(line[(p + 2)..]);
            }

            return list;
        }
    }
}
