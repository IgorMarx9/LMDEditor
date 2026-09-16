using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace LMDTool
{
    /*
        Minimal persistent settings, stored as a simple "key=value" text
        file next to the executable (config.txt). Deliberately avoids any
        JSON/XML dependency - this only ever needs to remember a couple of
        folder paths between runs.
    */
    public static class AppConfig
    {
        const string FileName = "config.txt";

        static Dictionary<string, string> values = new Dictionary<string, string>();
        static bool loaded = false;

        static string ConfigPath
        {
            get
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(exeDir, FileName);
            }
        }

        public static string? Get(string key)
        {
            EnsureLoaded();
            return values.TryGetValue(key, out string? value) ? value : null;
        }

        public static void Set(string key, string value)
        {
            EnsureLoaded();
            values[key] = value;
            Save();
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;

            try
            {
                if (!File.Exists(ConfigPath))
                    return;

                foreach (string line in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    string trimmed = line.Trim();

                    if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                        continue;

                    int eq = trimmed.IndexOf('=');

                    if (eq <= 0)
                        continue;

                    string key = trimmed.Substring(0, eq).Trim();
                    string value = trimmed.Substring(eq + 1).Trim();

                    values[key] = value;
                }
            }
            catch
            {
                // A corrupt or unreadable config file should never crash
                // the app - just fall back to no remembered settings.
            }
        }

        static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                foreach (var kvp in values)
                    sb.AppendLine(kvp.Key + "=" + kvp.Value);

                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Best-effort persistence; if we can't write the config
                // file (e.g. read-only folder), the app should keep
                // working for the current session anyway.
            }
        }
    }
}