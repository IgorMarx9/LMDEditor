using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LMDTool
{
    // ARC variant validated against the two supplied vq archives.
    // Payloads use zlib even when the high size byte is zero.
    // Preserve unknown metadata instead of deriving its meaning from its value.
    public static class ArcParser
    {
        const int HeaderSize = 12;
        const int EntrySize = 80;
        const int PathFieldSize = 64;

        static readonly Encoding Ascii = Encoding.ASCII;

        // ============================================================
        // PUBLIC TYPES
        // ============================================================

        public class ArcEntry
        {
            public string Path = "";
            public uint EntryId;
            public int CompressedSize;
            public byte CompressionFlag;
            public int DecompressedSize;
            public int DataOffset;
        }

        // ============================================================
        // READING
        // ============================================================

        public static List<ArcEntry> ReadEntries(byte[] data)
        {
            if (data == null || data.Length < HeaderSize)
                throw new Exception("ARC file is too small.");

            if (data[0] != 0x41 || data[1] != 0x52 || data[2] != 0x43 || data[3] != 0x00)
                throw new Exception("Invalid ARC magic.");

            int entryCount = ReadUInt16LE(data, 0x06);

            if (entryCount <= 0)
                throw new Exception("Invalid ARC entry count.");

            int tableEnd = HeaderSize + entryCount * EntrySize;

            if (tableEnd > data.Length)
                throw new Exception("ARC entry table runs past end of file.");

            var entries = new List<ArcEntry>(entryCount);

            for (int i = 0; i < entryCount; i++)
            {
                int off = HeaderSize + i * EntrySize;

                string path = ReadFixedAsciiString(data, off, PathFieldSize);

                uint entryId = ReadUInt32LE(data, off + 0x40);
                int compressedSize = (int)ReadUInt32LE(data, off + 0x44);

                uint sizeField = ReadUInt32LE(data, off + 0x48);
                byte flag = (byte)((sizeField >> 24) & 0xFF);
                int decompressedSize = (int)(sizeField & 0x00FFFFFF);

                int dataOffset = (int)ReadUInt32LE(data, off + 0x4C);

                if (dataOffset < tableEnd || compressedSize < 0 ||
                    (long)dataOffset + compressedSize > data.Length)
                {
                    throw new Exception(
                        "Entry '" + path + "' has an invalid data range."
                    );
                }

                entries.Add(new ArcEntry()
                {
                    Path = path,
                    EntryId = entryId,
                    CompressedSize = compressedSize,
                    CompressionFlag = flag,
                    DecompressedSize = decompressedSize,
                    DataOffset = dataOffset,
                });
            }

            return entries;
        }

        public static byte[] ReadEntryData(byte[] arcData, ArcEntry entry)
        {
            if (arcData == null || entry == null)
                throw new ArgumentNullException(arcData == null ? nameof(arcData) : nameof(entry));
            if (entry.DataOffset < 0 || entry.CompressedSize < 0 ||
                (long)entry.DataOffset + entry.CompressedSize > arcData.Length)
                throw new InvalidDataException("Invalid ARC entry range.");
            byte[] compressed = new byte[entry.CompressedSize];
            Buffer.BlockCopy(arcData, entry.DataOffset, compressed, 0, compressed.Length);
            // Both 0x00 and 0x20 high bytes accompany zlib in the supplied files.
            // Other compression formats are deliberately unsupported here.
            byte[] raw = ZlibDecompress(compressed, entry.DecompressedSize);
            if (raw.Length != entry.DecompressedSize)
                throw new InvalidDataException("Decompressed size mismatch: " + entry.Path);
            return raw;
        }

        /*
            Convenience helper: finds the single quest-text GMD entry
            inside a quest .arc (path containing "questData" and ending
            in "_jpn"), if any. Returns null if the .arc has no such
            entry (e.g. it is not a quest file, or has no translatable
            text).
        */
        public static ArcEntry? FindQuestTextEntry(List<ArcEntry> entries)
        {
            foreach (var entry in entries)
            {
                string normalized = entry.Path.Replace('/', '\\');

                if (normalized.IndexOf("questData", StringComparison.OrdinalIgnoreCase) >= 0
                    && normalized.EndsWith("_jpn", StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        // ============================================================
        // WRITING / REBUILDING
        // ============================================================

        /*
            Rebuilds an .arc file, replacing the raw (decompressed) bytes
            of one or more entries while preserving every other entry
            byte-for-byte. Entries not present in 'replacements' keep
            their original compressed payload unchanged.

            'replacements' keys are matched against ArcEntry.Path
            (case-insensitive, with '/' and '\' treated as equivalent).
        */
        public static byte[] Rebuild(
            byte[] originalArcData,
            List<ArcEntry> entries,
            Dictionary<string, byte[]> replacements)
        {
            var mapped = new Dictionary<int, byte[]>();
            foreach (var item in replacements)
            {
                int index = -1;
                for (int i = 0; i < entries.Count; i++)
                    if (string.Equals(NormalizePath(entries[i].Path), NormalizePath(item.Key), StringComparison.OrdinalIgnoreCase))
                    {
                        if (index >= 0) throw new InvalidDataException("Use indexed replacements for duplicate path: " + item.Key);
                        index = i;
                    }
                if (index < 0) throw new InvalidDataException("Replacement path not found: " + item.Key);
                if (!mapped.TryAdd(index, item.Value)) throw new InvalidDataException("Duplicate replacement key.");
            }
            return RebuildIndexed(originalArcData, entries, mapped);
        }

        public static byte[] RebuildIndexed(byte[] originalArcData, List<ArcEntry> entries, Dictionary<int, byte[]> replacements)
        {
            if (entries == null || replacements == null)
                throw new ArgumentNullException(entries == null ? nameof(entries) : nameof(replacements));

            // Re-read authoritative metadata; reject stale or reordered caller entries.
            var originals = ReadEntries(originalArcData);
            if (entries.Count != originals.Count)
                throw new InvalidDataException("ARC entry count changed.");
            for (int i = 0; i < originals.Count; i++)
            {
                var a = entries[i]; var b = originals[i];
                if (a == null || a.Path != b.Path || a.EntryId != b.EntryId ||
                    a.DataOffset != b.DataOffset || a.CompressedSize != b.CompressedSize ||
                    a.DecompressedSize != b.DecompressedSize || a.CompressionFlag != b.CompressionFlag)
                    throw new InvalidDataException("ARC entries do not match the source archive.");
            }

            var requested = new Dictionary<int, byte[]>();
            foreach (var kv in replacements)
            {
                if (kv.Key < 0 || kv.Key >= originals.Count || kv.Value == null || kv.Value.Length > 0x00FFFFFF)
                    throw new InvalidDataException("Invalid indexed ARC replacement.");
                requested.Add(kv.Key, kv.Value);
            }
            if (requested.Count == 0)
                return (byte[])originalArcData.Clone();

            var payloads = new Dictionary<int, byte[]>();
            foreach (var kv in requested)
            {
                byte[] before = ReadEntryData(originalArcData, originals[kv.Key]);
                bool equal = before.Length == kv.Value.Length;
                for (int j = 0; equal && j < before.Length; j++)
                    equal = before[j] == kv.Value[j];
                if (!equal)
                    payloads.Add(kv.Key, ZlibCompress(kv.Value));
            }
            if (payloads.Count == 0)
                return (byte[])originalArcData.Clone();

            // Keep physical order, prefix, gaps, table bytes and trailing bytes.
            // Offsets and sizes are the only patched fields.
            var order = new List<int>();
            for (int i = 0; i < originals.Count; i++) order.Add(i);
            order.Sort((a, b) => originals[a].DataOffset.CompareTo(originals[b].DataOffset));
            var newOffsets = new int[originals.Count];
            int sourceCursor = 0;
            int previousOffset = -1, previousSize = -1;
            using (var stream = new MemoryStream())
            {
                foreach (int index in order)
                {
                    var entry = originals[index];
                    if (entry.DataOffset < sourceCursor)
                    {
                        if (entry.DataOffset != previousOffset || entry.CompressedSize != previousSize)
                            throw new InvalidDataException("Partially overlapping ARC payloads are unsupported.");
                        // Exact aliases can be serialized separately when one occurrence changes.
                    }
                    else stream.Write(originalArcData, sourceCursor, entry.DataOffset - sourceCursor);
                    previousOffset = entry.DataOffset;
                    previousSize = entry.CompressedSize;
                    newOffsets[index] = checked((int)stream.Position);
                    if (payloads.TryGetValue(index, out byte[]? changed))
                        stream.Write(changed, 0, changed.Length);
                    else
                        stream.Write(originalArcData, entry.DataOffset, entry.CompressedSize);
                    sourceCursor = checked(entry.DataOffset + entry.CompressedSize);
                }
                stream.Write(originalArcData, sourceCursor, originalArcData.Length - sourceCursor);
                byte[] output = stream.ToArray();
                for (int i = 0; i < originals.Count; i++)
                {
                    int off = HeaderSize + i * EntrySize;
                    WriteUInt32LE(output, off + 0x4C, (uint)newOffsets[i]);
                    if (payloads.TryGetValue(i, out byte[]? changed))
                    {
                        WriteUInt32LE(output, off + 0x44, (uint)changed.Length);
                        uint oldField = ReadUInt32LE(originalArcData, off + 0x48);
                        WriteUInt32LE(output, off + 0x48,
                            (oldField & 0xFF000000u) | (uint)requested[i].Length);
                    }
                }
                return output;
            }
        }

        // ============================================================
        // ZLIB HELPERS
        // ============================================================

        static byte[] ZlibDecompress(byte[] compressed, int expectedSize)
        {
            // .NET's ZLibStream understands the raw zlib format directly
            // (2-byte zlib header + deflate stream + 4-byte Adler32),
            // matching the "78 9C..." streams observed in the file.
            using (var input = new MemoryStream(compressed))
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(expectedSize > 0 ? expectedSize : 256))
            {
                zlib.CopyTo(output);
                return output.ToArray();
            }
        }

        static byte[] ZlibCompress(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    zlib.Write(raw, 0, raw.Length);
                }

                return output.ToArray();
            }
        }

        // ============================================================
        // BINARY HELPERS
        // ============================================================

        static string ReadFixedAsciiString(byte[] data, int offset, int fieldSize)
        {
            int end = offset;
            int limit = offset + fieldSize;

            while (end < limit && data[end] != 0x00)
                end++;

            return Ascii.GetString(data, offset, end - offset);
        }

        static void WriteFixedAsciiString(byte[] data, int offset, int fieldSize, string value)
        {
            byte[] bytes = Ascii.GetBytes(value ?? "");

            if (bytes.Length >= fieldSize)
                throw new Exception("Path '" + value + "' does not fit in the ARC entry's path field.");

            Buffer.BlockCopy(bytes, 0, data, offset, bytes.Length);
            // Remaining bytes in the field are already zero in a fresh
            // output array.
        }

        static string NormalizePath(string path)
        {
            return (path ?? "").Replace('/', '\\');
        }

        static int RoundUpTo(int value, int alignment)
        {
            int remainder = value % alignment;

            if (remainder == 0)
                return value;

            return value + (alignment - remainder);
        }

        static ushort ReadUInt16LE(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        static void WriteUInt16LE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(data[offset]
                 | (data[offset + 1] << 8)
                 | (data[offset + 2] << 16)
                 | (data[offset + 3] << 24));
        }

        static void WriteUInt32LE(byte[] data, int offset, uint value)
        {
            data[offset + 0] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}