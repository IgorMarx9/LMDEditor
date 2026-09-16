using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace LMDTool
{
    public static class MH3UArcWorkflow
    {
        public static void Run(string source, string texts, string? output, bool verifyOnly = false)
        {
            byte[] original = File.ReadAllBytes(source);
            var entries = ArcParser.ReadEntries(original);
            if (original[4] != 0x10 || original[5] != 0) throw new InvalidDataException("Expected MH3U ARC version 0x10.");
            var replacements = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int found = 0;
            foreach (var entry in entries)
            {
                if (entry.EntryId != 0x242BB29A) continue;
                string relative = entry.Path.Replace('\\', '/');
                if (relative.Split('/').Any(p => p.Length == 0 || p == "." || p == ".." || p.Contains(':') ||
                    p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) throw new InvalidDataException("Invalid internal path.");
                string txt = Path.Combine(texts, relative.Replace('/', Path.DirectorySeparatorChar) + ".txt");
                if (!paths.Add(txt)) throw new InvalidDataException("Duplicate internal path.");
                found++;
                if ((verifyOnly || output != null) && !File.Exists(txt)) continue;
                if (!verifyOnly && output == null && File.Exists(txt)) continue;
                string temp = Path.Combine(Path.GetTempPath(), "MH3U_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                try
                {
                    string input = Path.Combine(temp, "original.gmd");
                    File.WriteAllBytes(input, ArcParser.ReadEntryData(original, entry));
                    if (verifyOnly) GMDParser.Verify(input, txt);
                    else if (output == null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(txt)!);
                        string generated = Path.Combine(temp, "export.txt");
                        GMDParser.ExportToTxt(input, generated);
                        File.Copy(input, Path.ChangeExtension(txt, ".gmd"), true);
                        File.Move(generated, txt);
                    }
                    else
                    {
                        string rebuilt = Path.Combine(temp, "translated.gmd");
                        GMDParser.ImportFromTxt(input, txt, rebuilt);
                        replacements.Add(entry.Path, File.ReadAllBytes(rebuilt));
                    }
                }
                finally { Directory.Delete(temp, true); }
            }
            if (found == 0) throw new InvalidDataException("No supported GMD entries in ARC.");
            if (output != null)
            {
                if (replacements.Count == 0) throw new InvalidDataException("No matching translated TXT found.");
                byte[] rebuilt = ArcParser.Rebuild(original, entries, replacements);
                var check = ArcParser.ReadEntries(rebuilt);
                foreach (var entry in check)
                    if (replacements.TryGetValue(entry.Path, out var expected) && !ArcParser.ReadEntryData(rebuilt, entry).SequenceEqual(expected))
                        throw new InvalidDataException("ARC rebuild verification failed.");
                string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllBytes(temporary, rebuilt); File.Move(temporary, output, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
    }
}
