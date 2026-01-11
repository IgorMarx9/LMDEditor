using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class LMDEntry
{
    public uint Offset;
    public uint Length;
    public uint Length2;
    public string Text = "";
}

public static class LMDParser
{
    const int EntrySize = 12;
    static readonly byte[] RED_OPCODE = { 0x00, 0x00, 0x00, 0x00 };

    // =========================
    // PUBLIC API
    // =========================

    public static void ExportToTxt(string lmdPath, string txtPath)
    {
        byte[] data = File.ReadAllBytes(lmdPath);

        long tableOffset = FindTableOffset(data, out int entryCount);
        if (tableOffset < 0)
            throw new Exception("String table not found.");

        var entries = ReadEntries(data, tableOffset, entryCount);

        using StreamWriter sw = new StreamWriter(txtPath, false, Encoding.UTF8);

        for (int i = 0; i < entries.Count; i++)
            sw.WriteLine($"[{i:D4}] {entries[i].Text}");
    }

    public static void ImportFromTxt(string originalLmd, string txtPath, string outLmd)
    {
        byte[] original = File.ReadAllBytes(originalLmd);

        long tableOffset = FindTableOffset(original, out int entryCount);
        if (tableOffset < 0)
            throw new Exception("String table not found.");

        var originalEntries = ReadRawEntries(original, tableOffset, entryCount);
        var newStrings = ReadTxt(txtPath);

        if (newStrings.Count != entryCount)
            throw new Exception($"TXT line count ({newStrings.Count}) does not match expected ({entryCount}).");

        using var fs = new FileStream(outLmd, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        bw.Write(original, 0, (int)tableOffset);

        long newTablePos = fs.Position;
        bw.Write(new byte[entryCount * EntrySize]);

        List<LMDEntry> newEntries = new();

        for (int i = 0; i < entryCount; i++)
        {
            long pos = fs.Position;
            byte[] block = BuildBinaryBlock(newStrings[i]);
            bw.Write(block);

            newEntries.Add(new LMDEntry
            {
                Offset = (uint)pos,
                Length = (uint)newStrings[i].Length,
                Length2 = (uint)newStrings[i].Length,
                Text = newStrings[i]
            });
        }

        fs.Seek(newTablePos, SeekOrigin.Begin);

        foreach (var e in newEntries)
        {
            bw.Write(e.Offset);
            bw.Write(e.Length);
            bw.Write(e.Length2);
        }

        long oldTextEnd = FindTextBlockEnd(originalEntries);
        if (oldTextEnd < original.Length)
        {
            fs.Seek(0, SeekOrigin.End);
            bw.Write(original, (int)oldTextEnd, original.Length - (int)oldTextEnd);
        }
    }

    public static void Verify(string originalLmd, string txtPath)
    {
        byte[] original = File.ReadAllBytes(originalLmd);

        long tableOffset = FindTableOffset(original, out int entryCount);
        if (tableOffset < 0)
            throw new Exception("String table not found.");

        var newStrings = ReadTxt(txtPath);

        if (newStrings.Count != entryCount)
            throw new Exception($"TXT line count ({newStrings.Count}) does not match expected ({entryCount}).");
    }

    // =========================
    // CORE
    // =========================

    static List<LMDEntry> ReadEntries(byte[] data, long tableOffset, int count)
    {
        var raw = ReadRawEntries(data, tableOffset, count);

        for (int i = 0; i < raw.Count; i++)
        {
            int start = (int)raw[i].Offset;
            int end = (i + 1 < raw.Count) ? (int)raw[i + 1].Offset : data.Length;
            raw[i].Text = ReadScriptBlock(data, start, end);
        }

        return raw;
    }

    static List<LMDEntry> ReadRawEntries(byte[] data, long tableOffset, int count)
    {
        List<LMDEntry> list = new();

        for (int i = 0; i < count; i++)
        {
            int pos = (int)tableOffset + i * EntrySize;

            uint off = BitConverter.ToUInt32(data, pos);
            uint len = BitConverter.ToUInt32(data, pos + 4);
            uint len2 = BitConverter.ToUInt32(data, pos + 8);

            list.Add(new LMDEntry { Offset = off, Length = len, Length2 = len2 });
        }

        return list;
    }

    static string ReadScriptBlock(byte[] data, int start, int end)
    {
        StringBuilder sb = new();
        bool red = false;
        int i = start;

        while (i + 1 < end)
        {
            if (data[i] == 0x00 && data[i + 1] == 0x00)
            {
                bool onlyZero = true;
                for (int k = i; k < end; k++)
                {
                    if (data[k] != 0x00) { onlyZero = false; break; }
                }
                if (onlyZero) break;
                i += 2;
                continue;
            }

            if (i + 3 < end &&
                data[i] == 0x00 && data[i + 1] == 0x00 &&
                data[i + 2] == 0x00 && data[i + 3] == 0x00)
            {
                if (i + 5 < end && !(data[i + 4] == 0x00 && data[i + 5] == 0x00))
                {
                    sb.Append(red ? "</RED>" : "<RED>");
                    red = !red;
                }
                i += 4;
                continue;
            }

            sb.Append(Encoding.Unicode.GetString(data, i, 2));
            i += 2;
        }

        if (red) sb.Append("</RED>");
        return sb.ToString();
    }

    static byte[] BuildBinaryBlock(string txt)
    {
        List<byte> buf = new();
        int i = 0;

        while (i < txt.Length)
        {
            if (txt.Substring(i).StartsWith("<RED>")) { buf.AddRange(RED_OPCODE); i += 5; continue; }
            if (txt.Substring(i).StartsWith("</RED>")) { buf.AddRange(RED_OPCODE); i += 6; continue; }

            buf.AddRange(Encoding.Unicode.GetBytes(txt[i].ToString()));
            i++;
        }

        buf.Add(0x00);
        buf.Add(0x00);
        return buf.ToArray();
    }

    // =========================
    // AUTO DETECT
    // =========================

    static long FindTableOffset(byte[] data, out int entryCount)
    {
        for (int i = 0; i < data.Length - 0x1000; i += 4)
            if (TryReadTable(data, i, out entryCount) && entryCount > 10)
                return i;

        entryCount = 0;
        return -1;
    }

    static bool TryReadTable(byte[] data, int start, out int count)
    {
        count = 0;
        uint lastOff = 0;

        for (int i = 0; i < 10000; i++)
        {
            int p = start + i * EntrySize;
            if (p + EntrySize >= data.Length) break;

            uint off = BitConverter.ToUInt32(data, p);
            uint len1 = BitConverter.ToUInt32(data, p + 4);
            uint len2 = BitConverter.ToUInt32(data, p + 8);

            if (off <= lastOff || off >= data.Length || off % 2 != 0 || len1 != len2)
                break;

            lastOff = off;
            count++;
        }

        return count > 0;
    }

    static long FindTextBlockEnd(List<LMDEntry> entries)
    {
        uint max = 0;
        foreach (var e in entries)
            if (e.Offset > max) max = e.Offset;
        return max;
    }

    // =========================
    // TXT
    // =========================

    static List<string> ReadTxt(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        List<string> list = new();
        StringBuilder current = null;

        foreach (var raw in lines)
        {
            var m = Regex.Match(raw, @"^\[(\d+)\]\s?(.*)$");

            if (m.Success)
            {
                if (current != null) list.Add(current.ToString());
                current = new StringBuilder();
                current.Append(m.Groups[2].Value);
            }
            else if (current != null)
            {
                current.Append("\n");
                current.Append(raw);
            }
        }

        if (current != null) list.Add(current.ToString());
        return list;
    }
}
