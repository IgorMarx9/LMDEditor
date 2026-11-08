using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LMDTool
{
    public static class MH4QuestBatch
    {
        const uint QuestType = 0x1BBFD18E;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static bool Within(string path, string root) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)
            .Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
            Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        static string SafePath(string root, string relative)
        {
            string[] parts = relative.Replace('\\', '/').Split('/');
            if (parts.Any(p => p.Length == 0 || p == "." || p == ".." || p.IndexOfAny("<>:\"|?*".ToCharArray()) >= 0 || p.Any(char.IsControl)))
                throw new InvalidDataException("Unsafe quest path: " + relative);
            string path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
            if (!Within(path, root)) throw new InvalidDataException("Quest path outside destination.");
            return path;
        }
        static IEnumerable<string> Discover(string dir, string texts, string output)
        {
            if (Within(dir, texts) || Within(dir, output)) yield break;
            foreach (string file in Directory.EnumerateFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (Regex.IsMatch(Path.GetFileName(file), @"^quest\d+\.arc$", RegexOptions.IgnoreCase)) yield return file;
            foreach (string child in Directory.EnumerateDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    foreach (string file in Discover(child, texts, output)) yield return file;
        }
        static void AtomicWrite(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(tmp, data); File.Move(tmp, path, true); }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        // Escape separators and underscores unambiguously; retain the archive entry index.
        static string EncodeName(string value)
        {
            var result = new StringBuilder();
            foreach (byte b in Encoding.UTF8.GetBytes(value.Replace('\\', '/')))
                if (b == '/') result.Append('~');
                else if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') ||
                         (b >= '0' && b <= '9') || b == '-' || b == '.') result.Append((char)b);
                else result.Append('%').Append(b.ToString("X2"));
            return result.ToString();
        }
        public static string TextName(string archive, string entry, int index, string language) =>
            EncodeName(archive) + "__" + EncodeName(entry) + "__" + index.ToString("D4") + "__" + language + ".txt";

        public static QuestBatchResult Run(string source, string texts, string? output,
            bool[] languages, QuestLogCallback log)
        {
            var result = new QuestBatchResult();
            if (languages.Length != 5 || !languages.Any(x => x))
            { log("", QuestLogStatus.Failure, Localization.T("mh4NoLanguage")); result.Failed++; return result; }
            source = Path.GetFullPath(source); texts = Path.GetFullPath(texts);
            string outputPath = Path.GetFullPath(output ?? Path.Combine(Path.GetDirectoryName(texts)!, "QuestsOutput"));
            if (Within(source, texts) || Within(source, outputPath) || Within(texts, outputPath) || Within(outputPath, texts))
            { log("", QuestLogStatus.Failure, "Source, text and output folders must be separate."); result.Failed++; return result; }
            string[] files;
            try { files = Discover(source, texts, outputPath).ToArray(); }
            catch (Exception ex) { log("", QuestLogStatus.Failure, ex.Message); result.Failed++; return result; }
            result.Total = files.Length;
            log("", QuestLogStatus.Info, Localization.T("logFilesFound") + files.Length);
            if (files.Length == 0) log("", QuestLogStatus.Warning, Localization.T("mh4NoArchives"));
            foreach (string file in files)
            {
                string relative = Path.GetRelativePath(source, file);
                try
                {
                    byte[] original = File.ReadAllBytes(file);
                    if (original.Length < 12 || original[4] != 0x13 || original[5] != 0)
                        throw new InvalidDataException("Expected MH4U ARC version 0x13.");
                    var entries = ArcParser.ReadEntries(original);
                    string dir = SafePath(texts, Path.ChangeExtension(relative, null)!);
                    string stamp = Path.Combine(dir, "source.sha256");
                    string hash = Convert.ToHexString(SHA256.HashData(original));
                    if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() != hash)
                        throw new InvalidDataException("Source ARC differs from exported original.");
                    if (!File.Exists(stamp))
                    {
                        if (output != null || (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any()))
                            throw new InvalidDataException("Export this source ARC to an empty text folder first (missing source.sha256).");
                        AtomicWrite(stamp, Utf8.GetBytes(hash));
                    }
                    var replacements = new Dictionary<int, byte[]>();
                    int processed = 0, kept = 0, missing = 0;
                    var counts = entries.GroupBy(e => e.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        if (entry.EntryId != QuestType) continue;
                        string name = entry.Path.Replace('\\', '/');
                        if (counts[name] > 1) name += "__entry" + i.ToString("D4");
                        string questDir = SafePath(dir, name);
                        var translated = new Dictionary<int, string[]>();
                        if (output != null)
                        {
                            for (int l = 0; l < 5; l++)
                            {
                                if (!languages[l]) continue;
                                string txt = Path.Combine(texts, TextName(relative, entry.Path, i, MH4QuestParser.Languages[l]));
                                QuestStorage.MoveFile(Path.Combine(questDir, MH4QuestParser.Languages[l] + ".txt"), txt);
                                if (File.Exists(txt)) translated.Add(l, MH4QuestParser.ParseTxt(File.ReadAllText(txt, Utf8)));
                                else missing++;
                            }
                            if (translated.Count == 0) continue;
                        }
                        byte[] raw = ArcParser.ReadEntryData(original, entry);
                        MH4QuestParser.Analyze(raw);
                        if (output == null)
                        {
                            string rawPath = questDir + ".1BBFD18E";
                            if (!File.Exists(rawPath)) AtomicWrite(rawPath, raw);
                            for (int l = 0; l < 5; l++)
                            {
                                if (!languages[l]) continue;
                                string txt = Path.Combine(texts, TextName(relative, entry.Path, i, MH4QuestParser.Languages[l]));
                                QuestStorage.MoveFile(Path.Combine(questDir, MH4QuestParser.Languages[l] + ".txt"), txt);
                                if (File.Exists(txt))
                                {
                                    string existing = File.ReadAllText(txt, Utf8);
                                    if (MH4QuestParser.IsLegacyTxt(existing))
                                    {
                                        string natural = MH4QuestParser.FormatTxt(MH4QuestParser.ParseTxt(existing));
                                        string backup = txt + ".v1.bak";
                                        if (!File.Exists(backup)) AtomicWrite(backup, File.ReadAllBytes(txt));
                                        else if (!File.ReadAllBytes(backup).SequenceEqual(File.ReadAllBytes(txt)))
                                            throw new IOException("Different legacy backup already exists: " + backup);
                                        AtomicWrite(txt, Utf8.GetBytes(natural));
                                    }
                                    kept++; continue;
                                }
                                AtomicWrite(txt, Utf8.GetBytes(MH4QuestParser.Export(raw, l)));
                            }
                        }
                        else
                        {
                            byte[] rebuilt = MH4QuestParser.Import(raw, translated);
                            if (!raw.SequenceEqual(rebuilt)) replacements.Add(i, rebuilt);
                        }
                        processed++;
                    }
                    if (output != null)
                    {
                        if (processed == 0)
                        { result.Skipped++; log(relative, QuestLogStatus.Warning, Localization.T("mh4NoTexts")); continue; }
                        byte[] rebuilt = ArcParser.RebuildIndexed(original, entries, replacements);
                        ArcParser.ValidatePreservedMetadata(original, rebuilt);
                        var check = ArcParser.ReadEntries(rebuilt);
                        if (check.Count != entries.Count) throw new InvalidDataException("ARC entry count changed.");
                        foreach (var pair in replacements)
                            if (!ArcParser.ReadEntryData(rebuilt, check[pair.Key]).SequenceEqual(pair.Value))
                                throw new InvalidDataException("Rebuilt quest ARC validation failed.");
                        AtomicWrite(SafePath(outputPath, relative), rebuilt);
                    }
                    result.Success++;
                    log(relative, QuestLogStatus.Success, string.Format(Localization.T("mh4QuestDone"), processed, kept, missing));
                }
                catch (Exception ex) { result.Failed++; log(relative, QuestLogStatus.Failure, ex.Message); }
            }
            return result;
        }
    }
}
