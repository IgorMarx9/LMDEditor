using System;
using System.IO;
using System.Linq;

namespace LMDTool
{
    public static class QuestStorage
    {
        // Check the complete tree before moving anything. Never choose between translations.
        public static void Merge(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            Check(source, destination);
            Move(source, destination);
        }
        static void Check(string source, string destination)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked folder cannot be migrated: " + source);
            if (File.Exists(destination)) throw new IOException("Folder conflicts with file: " + destination);
            if (Directory.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked destination: " + destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                CheckFile(file, target);
            }
            foreach (string dir in Directory.GetDirectories(source)) Check(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
        static void CheckFile(string source, string destination)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0 ||
                Directory.Exists(destination) ||
                (File.Exists(destination) && ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0 ||
                 !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))))
                throw new IOException("Conflicting files; keep the desired version before retrying: " + source + " | " + destination);
        }
        public static void MoveFile(string source, string destination)
        {
            if (!File.Exists(source)) return;
            CheckFile(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) File.Delete(source); // Identical contents checked above.
            else File.Move(source, destination);
        }
        static void Move(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) MoveFile(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (string dir in Directory.GetDirectories(source)) Move(dir, Path.Combine(destination, Path.GetFileName(dir)));
            Directory.Delete(source);
        }
        public static void Migrate(string exeDir, bool mh4)
        {
            string oldRoot = mh4 ? Path.Combine(exeDir, "MH4U") : exeDir;
            string newRoot = Path.Combine(exeDir, mh4 ? "MH4G" : "MHXX");
            foreach (string folder in new[] { "QuestsText", "QuestsOutput" })
                Merge(Path.Combine(oldRoot, folder), Path.Combine(newRoot, folder));
            if (mh4 && Directory.Exists(oldRoot) && !Directory.EnumerateFileSystemEntries(oldRoot).Any()) Directory.Delete(oldRoot);
        }
    }
}
