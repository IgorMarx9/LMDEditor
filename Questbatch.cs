using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LMDTool
{
    // Counts refer to archives. Details for individual GMDs appear in the log.
    public class QuestBatchResult
    {
        public int Total;
        public int Success;
        public int Skipped;
        public int Failed;
    }

    public delegate void QuestLogCallback(string fileName, QuestLogStatus status, string detail);
    public enum QuestLogStatus { Info, Success, Warning, Failure }

    public static class QuestBatch
    {
        static readonly Regex TextName = new Regex(@"^questData_(\d{7})_jpn$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static QuestBatchResult ExportFolder(string sourceFolder, string destRoot,
            QuestFilter filter, QuestLogCallback log)
        {
            return Run(sourceFolder, destRoot, null, filter, log);
        }

        public static QuestBatchResult ImportFolder(string sourceFolder, string txtRoot,
            string outputRoot, QuestFilter filter, QuestLogCallback log)
        {
            return Run(sourceFolder, txtRoot, outputRoot, filter, log);
        }

        static QuestBatchResult Run(string sourceFolder, string textRoot, string? outputRoot,
            QuestFilter filter, QuestLogCallback log)
        {
            var result = new QuestBatchResult();
            try
            {
                sourceFolder = Path.GetFullPath(sourceFolder);
                textRoot = Path.GetFullPath(textRoot);
                if (!Directory.Exists(sourceFolder)) throw new DirectoryNotFoundException(sourceFolder);
                if (!filter.Valid) throw new InvalidDataException("Invalid star range.");
                if (Within(sourceFolder, textRoot))
                    throw new InvalidDataException("The source must not be inside the text folder.");
                if (outputRoot != null)
                {
                    outputRoot = Path.GetFullPath(outputRoot);
                    if (Within(sourceFolder, outputRoot) || Within(textRoot, outputRoot) || Within(outputRoot, textRoot))
                        throw new InvalidDataException("Source, texts and output must use separate folders.");
                    if (!Directory.Exists(textRoot)) throw new DirectoryNotFoundException(textRoot);
                }
                log("", QuestLogStatus.Info, "TXT/GMD: " + textRoot);
                if (outputRoot != null) log("", QuestLogStatus.Info, "ARC output: " + outputRoot);
                if (!filter.Any)
                {
                    log("", QuestLogStatus.Info, "No quest category selected.");
                    return result;
                }
                string excludedOutput = outputRoot ?? Path.Combine(Path.GetDirectoryName(textRoot)!, "QuestsOutput");
                var allFiles = Discover(sourceFolder, textRoot, excludedOutput, log).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                var files = allFiles.Where(file => IsBundle(file)).ToArray();
                var translations = new Dictionary<string, Translation>(StringComparer.OrdinalIgnoreCase);
                var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                log("", QuestLogStatus.Info, "Found " + files.Length + " quest ARC(s), including subfolders.");
                foreach (string file in files)
                {
                    string relative = Path.GetRelativePath(sourceFolder, file);
                    bool counted = false;
                    try
                    {
                        string archiveDir = SafePath(textRoot, Path.Combine(Path.GetDirectoryName(relative) ?? "", BundleFolder(file)));
                        string stamp = Path.Combine(archiveDir, "source.sha256");
                        // Read only the directory table before loading/hashing a bundled ARC.
                        if (!HasSelectedEntry(file, filter)) continue;
                        result.Total++;
                        counted = true;
                        if (outputRoot != null && !File.Exists(stamp) && !File.Exists(Path.Combine(SafePath(textRoot, relative), "source.sha256")))
                        {
                            result.Skipped++;
                            log(relative, QuestLogStatus.Warning, "No export found. Export this ARC first.");
                            continue;
                        }

                        byte[] original = File.ReadAllBytes(file);
                        var entries = ArcParser.ReadEntries(original);
                        string hash = Convert.ToHexString(SHA256.HashData(original));
                        if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() != hash)
                            throw new InvalidDataException("Source ARC differs from the exported original. Use the original or a new text folder.");
                        string legacyDir = SafePath(textRoot, relative);
                        string legacyStamp = Path.Combine(legacyDir, "source.sha256");
                        if (File.Exists(legacyStamp) && File.ReadAllText(legacyStamp).Trim() != hash)
                            throw new InvalidDataException("Legacy export belongs to a different original ARC.");
                        var pending = new Dictionary<string, Translation>(StringComparer.OrdinalIgnoreCase);
                        var replacements = new Dictionary<int, byte[]>();
                        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        int processed = 0, missing = 0, retained = 0;
                        var duplicateNames = entries.GroupBy(e => e.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                        {
                            var entry = entries[entryIndex];
                            if (!SelectedCategory(entry.Path, file, filter, out string category)) continue;
                            string basePath = SafePath(archiveDir, category + "/" + entry.Path.Replace('\\', '/'));
                            if (duplicateNames.Contains(entry.Path.Replace('\\', '/')))
                                basePath += "__entry" + entryIndex.ToString("D4");
                            if (!usedPaths.Add(basePath)) throw new InvalidDataException("Duplicate text path: " + entry.Path);
                            string txt = basePath + ".txt";
                            if (!File.Exists(txt) && !duplicateNames.Contains(entry.Path.Replace('\\', '/')))
                            {
                                Match parsed = TextName.Match(Leaf(entry.Path));
                                if (QuestNaming.TryParse("q" + parsed.Groups[1].Value, out QuestInfo legacyInfo))
                                {
                                    string oldTxt = SafePath(legacyDir, legacyInfo.OutputSubfolder() + "/" + entry.Path.Replace('\\', '/') + ".txt");
                                    if (File.Exists(oldTxt))
                                    {
                                        if (outputRoot == null)
                                        {
                                            AtomicWrite(txt, File.ReadAllBytes(oldTxt));
                                            if (File.Exists(Path.ChangeExtension(oldTxt, ".gmd")))
                                                AtomicWrite(basePath + ".gmd", File.ReadAllBytes(Path.ChangeExtension(oldTxt, ".gmd")));
                                            AtomicWrite(stamp, System.Text.Encoding.UTF8.GetBytes(hash));
                                        }
                                        else txt = oldTxt;
                                    }
                                }
                            }
                            if (!File.Exists(txt) && category.StartsWith("Especial" + Path.DirectorySeparatorChar) && IsBundle(file) &&
                                Path.GetFileNameWithoutExtension(file).Equals("gq2", StringComparison.OrdinalIgnoreCase) &&
                                !duplicateNames.Contains(entry.Path.Replace('\\', '/')))
                            {
                                int level = int.Parse(Path.GetFileName(category));
                                if (level >= 11 && level <= 14)
                                {
                                    string oldTxt = SafePath(archiveDir, "Especial/G" + (level - 10) + "/" + entry.Path.Replace('\\', '/') + ".txt");
                                    if (File.Exists(oldTxt))
                                    {
                                        if (outputRoot == null)
                                        {
                                            AtomicWrite(txt, File.ReadAllBytes(oldTxt));
                                            if (File.Exists(Path.ChangeExtension(oldTxt, ".gmd")))
                                                AtomicWrite(basePath + ".gmd", File.ReadAllBytes(Path.ChangeExtension(oldTxt, ".gmd")));
                                        }
                                        else txt = oldTxt;
                                    }
                                }
                            }
                            // No decompression or temporary files for texts that will not be processed.
                            if (outputRoot == null && File.Exists(txt)) { retained++; continue; }
                            if (outputRoot != null && !File.Exists(txt)) { missing++; continue; }
                            byte[] raw = ArcParser.ReadEntryData(original, entry);
                            if (raw.Length < 4 || raw[0] != 'G' || raw[1] != 'M' || raw[2] != 'D' || raw[3] != 0)
                                throw new InvalidDataException("Expected GMD: " + entry.Path);
                            string temp = Path.Combine(Path.GetTempPath(), "LMDEditor_" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(temp);
                            try
                            {
                                string input = Path.Combine(temp, "original.gmd");
                                File.WriteAllBytes(input, raw);
                                if (outputRoot == null)
                                {
                                    string generated = Path.Combine(temp, "text.txt");
                                    MHXXGMDParser.ExportToTxt(input, generated);
                                    // Stamp before writing exports so interrupted batches remain tied to the source.
                                    Directory.CreateDirectory(archiveDir);
                                    if (!File.Exists(stamp)) AtomicWrite(stamp, System.Text.Encoding.UTF8.GetBytes(hash));
                                    AtomicWrite(basePath + ".gmd", raw);
                                    AtomicWrite(txt, File.ReadAllBytes(generated));
                                }
                                else
                                {
                                    // Bypass parsing if the exported TXT is unchanged: retain exact original GMD bytes.
                                    string baseline = Path.Combine(temp, "baseline.txt");
                                    MHXXGMDParser.ExportToTxt(input, baseline);
                                    if (File.ReadAllBytes(txt).SequenceEqual(File.ReadAllBytes(baseline)))
                                        replacements.Add(entryIndex, raw);
                                    else
                                    {
                                        string rebuilt = Path.Combine(temp, "translated.gmd");
                                        MHXXGMDParser.ImportFromTxt(input, txt, rebuilt);
                                        // Re-read and verify generated GMD against the edited TXT before packing.
                                        MHXXGMDParser.Verify(rebuilt, txt);
                                        replacements.Add(entryIndex, File.ReadAllBytes(rebuilt));
                                    }
                                }
                                if (outputRoot != null)
                                {
                                    string name = Leaf(entry.Path);
                                    var value = new Translation(txt, raw, replacements[entryIndex]);
                                    if (pending.TryGetValue(name, out Translation? prior) && !prior.Translated.SequenceEqual(value.Translated))
                                    {
                                        ambiguous.Add(name); translations.Remove(name);
                                        log(name, QuestLogStatus.Warning, "Different duplicate GMD contents; bundle occurrences kept separate; standalone propagation blocked.");
                                    }
                                    else pending[name] = value;
                                }
                                processed++;
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidDataException(entry.Path + ": " + ex.Message, ex);
                            }
                            finally { Directory.Delete(temp, true); }
                        }
                        if (processed == 0)
                        {
                            result.Skipped++;
                            log(relative, QuestLogStatus.Warning, "No new files processed; existing TXT kept: " + retained + "; missing TXT: " + missing);
                            continue;
                        }
                        if (outputRoot != null)
                        {
                            byte[] rebuilt = ArcParser.RebuildIndexed(original, entries, replacements);
                            var check = ArcParser.ReadEntries(rebuilt);
                            if (check.Count != entries.Count) throw new InvalidDataException("Rebuilt entry count mismatch.");
                            for (int i = 0; i < check.Count; i++)
                            {
                                if (check[i].Path != entries[i].Path || check[i].EntryId != entries[i].EntryId)
                                    throw new InvalidDataException("Rebuilt metadata mismatch.");
                                if (replacements.TryGetValue(i, out byte[]? expected) &&
                                    !ArcParser.ReadEntryData(rebuilt, check[i]).SequenceEqual(expected))
                                    throw new InvalidDataException("Rebuilt payload mismatch: " + check[i].Path);
                            }
                            AtomicWrite(SafePath(outputRoot, relative), rebuilt);
                            foreach (var item in pending)
                            {
                                if (ambiguous.Contains(item.Key)) continue;
                                if (translations.TryGetValue(item.Key, out Translation? existing) &&
                                    !existing.Translated.SequenceEqual(item.Value.Translated))
                                {
                                    translations.Remove(item.Key);
                                    ambiguous.Add(item.Key);
                                    log(item.Key, QuestLogStatus.Failure, "Conflicting bundle translations; standalone propagation blocked.");
                                }
                                else translations[item.Key] = item.Value;
                            }
                        }
                        result.Success++;
                        log(relative, QuestLogStatus.Success, (outputRoot == null ? "Exported " : "Imported ") + processed +
                            " GMD(s); existing TXT kept: " + retained + "; missing TXT preserved in ARC: " + missing);
                    }
                    catch (Exception ex)
                    {
                        if (!counted) result.Total++;
                        result.Failed++;
                        log(relative, QuestLogStatus.Failure, ex.Message);
                    }
                }
                if (outputRoot != null)
                    ApplyToStandalone(allFiles.Where(file => !IsBundle(file)), sourceFolder, outputRoot, translations, log, result);
                if (files.Length == 0) log("", QuestLogStatus.Warning, "No vq.arc, gq.arc or gq2.arc found. Standalone ARCs are not text sources.");
            }
            catch (Exception ex)
            {
                result.Failed++;
                log("", QuestLogStatus.Failure, ex.Message);
            }
            return result;
        }

        sealed class Translation
        {
            public readonly string Txt;
            public readonly byte[] Original, Translated;
            public Translation(string txt, byte[] original, byte[] translated)
            { Txt = txt; Original = original; Translated = translated; }
        }
        static string Leaf(string path) => path.Replace('\\', '/').Split('/').Last();
        static bool IsBundle(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            return name.Equals("vq", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("gq", StringComparison.OrdinalIgnoreCase) || name.Equals("gq2", StringComparison.OrdinalIgnoreCase);
        }
        static void ApplyToStandalone(IEnumerable<string> files, string sourceRoot, string outputRoot,
            Dictionary<string, Translation> translations, QuestLogCallback log, QuestBatchResult result)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string quest = Path.GetFileNameWithoutExtension(file);
                // Fast filename gate: only read individual quests with an imported bundle TXT.
                string expectedName = "questData_" + quest.Substring(1) + "_jpn";
                if (!translations.TryGetValue(expectedName, out Translation? translation)) continue;
                result.Total++;
                string relative = Path.GetRelativePath(sourceRoot, file);
                try
                {
                    byte[] original = File.ReadAllBytes(file);
                    var entries = ArcParser.ReadEntries(original);
                    var targets = entries.Where(e => Leaf(e.Path).Equals(expectedName, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (targets.Length != 1) throw new InvalidDataException("Expected exactly one matching GMD: " + expectedName);
                    var entry = targets[0];
                    byte[] raw = ArcParser.ReadEntryData(original, entry);
                    byte[] translated;
                    if (raw.SequenceEqual(translation.Original)) translated = translation.Translated;
                    else
                    {
                        // Same name does not guarantee identical headers. Rebuild against this target's GMD.
                        string temp = Path.Combine(Path.GetTempPath(), "QuestSync_" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(temp);
                        try
                        {
                            string input = Path.Combine(temp, "original.gmd"), output = Path.Combine(temp, "new.gmd");
                            File.WriteAllBytes(input, raw);
                            MHXXGMDParser.ImportFromTxt(input, translation.Txt, output);
                            MHXXGMDParser.Verify(output, translation.Txt);
                            translated = File.ReadAllBytes(output);
                        }
                        finally { Directory.Delete(temp, true); }
                    }
                    var replacements = new Dictionary<string, byte[]> { [entry.Path] = translated };
                    byte[] rebuilt = ArcParser.Rebuild(original, entries, replacements);
                    var check = ArcParser.ReadEntries(rebuilt).Single(e => e.Path == entry.Path);
                    if (!ArcParser.ReadEntryData(rebuilt, check).SequenceEqual(translated))
                        throw new InvalidDataException("Standalone verification failed.");
                    AtomicWrite(SafePath(outputRoot, relative), rebuilt);
                    matched.Add(expectedName);
                    result.Success++;
                    log(relative, QuestLogStatus.Success, "Applied shared translation: " + expectedName);
                }
                catch (Exception ex) { result.Failed++; log(relative, QuestLogStatus.Failure, ex.Message); }
            }
            int unmatched = translations.Keys.Count(key => !matched.Contains(key));
            if (unmatched > 0) log("", QuestLogStatus.Warning,
                unmatched + " imported GMD(s) have no successfully updated standalone ARC; check missing files/errors.");
        }

        static bool SelectedCategory(string entryPath, string bundle, QuestFilter filter, out string category)
        {
            category = "";
            string leaf = entryPath.Replace('\\', '/').Split('/').Last();
            Match match = TextName.Match(leaf);
            // Unknown names must not bypass the user's category/star selection.
            if (!match.Success || !QuestNaming.TryParse("q" + match.Groups[1].Value, out QuestInfo info)) return false;
            string name = Path.GetFileNameWithoutExtension(bundle).ToLowerInvariant();
            // Special quests retain their own 01..18 level, even inside gq2.
            if (info.GuildSubtype == 4)
            {
                if (!filter.Special.Regular || info.Stars < filter.Special.Min || info.Stars > filter.Special.Max) return false;
                category = Path.Combine("Especial", info.Stars.ToString());
                return true;
            }
            QuestColumn column = name == "vq" ? filter.Village : name == "gq" ? filter.Hub : filter.Pub;
            int rank = name == "gq2" ? info.Stars - 10 : info.Stars;
            if (rank < column.Min || rank > column.Max) return false;
            bool selected = info.IsProwler ? column.Prowler : column.Regular;
            if (!selected) return false;
            category = Path.Combine(info.IsProwler ? "Gatunos" : info.GuildSubtype == 4 ? "Especial" : "Normal",
                name == "gq2" ? "G" + rank : rank.ToString());
            return true;
        }
        static string BundleFolder(string path) => Path.GetFileNameWithoutExtension(path).ToLowerInvariant() switch
        { "vq" => "Village(vq)", "gq" => "Hub(gq)", "gq2" => "Pub(gq2)", _ => throw new InvalidDataException("Unknown bundle") };

        static bool HasSelectedEntry(string arcPath, QuestFilter filter)
        {
            // Standalone quests are already filtered by filename before opening.
            if (QuestNaming.TryParse(Path.GetFileNameWithoutExtension(arcPath), out _)) return true;
            using var stream = File.OpenRead(arcPath);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 12 || reader.ReadUInt32() != 0x00435241)
                throw new InvalidDataException("Invalid ARC header.");
            reader.ReadUInt16();
            int count = reader.ReadUInt16();
            reader.ReadUInt32();
            if (count == 0 || 12L + count * 80L > stream.Length)
                throw new InvalidDataException("Invalid ARC table.");
            for (int i = 0; i < count; i++)
            {
                stream.Position = 12L + i * 80L;
                byte[] path = reader.ReadBytes(64);
                int end = Array.IndexOf(path, (byte)0);
                string name = System.Text.Encoding.ASCII.GetString(path, 0, end < 0 ? path.Length : end);
                if (SelectedCategory(name, arcPath, filter, out _)) return true;
            }
            return false;
        }

        static IEnumerable<string> Discover(string folder, string texts, string output, QuestLogCallback log)
        {
            if (Within(folder, texts) || Within(folder, output)) yield break;
            string[] files, directories;
            try { files = Directory.GetFiles(folder); directories = Directory.GetDirectories(folder); }
            catch (Exception ex) { log(folder, QuestLogStatus.Warning, ex.Message); yield break; }
            foreach (string file in files)
            {
                if (!Path.GetExtension(file).Equals(".arc", StringComparison.OrdinalIgnoreCase)) continue;
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.Equals("vq", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("gq", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("gq2", StringComparison.OrdinalIgnoreCase) || QuestNaming.TryParse(name, out _)) yield return file;
            }
            foreach (string dir in directories)
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (string file in Discover(dir, texts, output, log)) yield return file;
            }
        }

        static bool Within(string path, string root)
        {
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        static string SafePath(string root, string relative)
        {
            string normalized = relative.Replace('\\', '/');
            var parts = normalized.Split('/');
            if (Path.IsPathRooted(normalized) || parts.Any(p => p.Length == 0 || p == "." || p == ".." ||
                p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.Contains(':') || p.EndsWith('.') || p.EndsWith(' ')))
                throw new InvalidDataException("Unsafe archive path: " + relative);
            string path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
            if (!Within(path, root)) throw new InvalidDataException("Path outside output folder.");
            return path;
        }

        static void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
