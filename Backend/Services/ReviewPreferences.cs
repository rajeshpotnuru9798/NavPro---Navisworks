using System;
using System.IO;
using System.Web.Script.Serialization;

namespace NavPro.Backend.Services
{
    /// <summary>User-owned local presets. Atomic writes; invalid files are reported, never overwritten on read.</summary>
    public static class ReviewPreferences
    {
        internal static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NavPro", "ReviewPresets");
        private static int Limit(string name) => name.StartsWith("history-", StringComparison.Ordinal) ? 128000000 : 8000000;
        public static T Read<T>(string name) where T : class, new()
        {
            string path = Path.Combine(Root, name + ".json");
            if (!File.Exists(path)) return new T();
            int limit = Limit(name);
            if (new FileInfo(path).Length > limit) throw new InvalidDataException("Local file exceeds its " + (limit / 1000000) + " MB limit: " + path);
            return new JavaScriptSerializer { MaxJsonLength = limit }.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        public static void Write<T>(string name, T value)
        {
            Directory.CreateDirectory(Root);
            string path = Path.Combine(Root, name + ".json"), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                int limit = Limit(name);
                File.WriteAllText(temporary, new JavaScriptSerializer { MaxJsonLength = limit }.Serialize(value));
                if (new FileInfo(temporary).Length > limit) throw new InvalidDataException("Local file exceeds its " + (limit / 1000000) + " MB limit. Existing data was retained.");
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
