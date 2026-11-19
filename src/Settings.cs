using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IC7760Link
{
    // Réglages mémorisés à côté de l'exe (logiciel portable) ; sinon dans %APPDATA%
    public static class Cfg
    {
        static readonly Dictionary<string, string> d = new Dictionary<string, string>();
        static string path;
        public static string Dir;

        public static void Load()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            Dir = Path.Combine(exeDir, "data");
            try { Directory.CreateDirectory(Dir); string t = Path.Combine(Dir, "w.tmp"); File.WriteAllText(t, "x"); File.Delete(t); }
            catch
            {
                Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IC7760Link");
                try { Directory.CreateDirectory(Dir); } catch { }
            }
            path = Path.Combine(Dir, "settings.ini");
            try
            {
                if (File.Exists(path))
                    foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                    { int i = line.IndexOf('='); if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1); }
            }
            catch { }
        }
        public static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                List<string> k = new List<string>(d.Keys); k.Sort();
                foreach (string s in k) sb.AppendLine(s + "=" + d[s]);
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
        // nature d'une clé : 0 = application, 1 = réglages affichés du poste (radio.), 2 = menu interne du poste (menu.), 3 = mémoires de fréquences du poste (memch.)
        public static int Kind(string k) { return k.StartsWith("radio.") ? 1 : k.StartsWith("menu.") ? 2 : k.StartsWith("memch.") ? 3 : 0; }
        // texte de configuration du logiciel (les reglages du poste sont releves a part, en direct)
        public static string Dump() { StringBuilder sb = new StringBuilder(); List<string> k = new List<string>(d.Keys); k.Sort(); foreach (string s in k) if (Kind(s) == 0) sb.AppendLine(s + "=" + d[s]); return sb.ToString(); }
        // import partiel : sel[0] = application, [1] = réglages affichés du poste, [2] = menu interne, [3] = mémoires de fréquences
        public static int ImportParts(string text, bool[] sel)
        {
            Dictionary<string, string> n = Parse(text); int c = 0;
            foreach (string s in new List<string>(d.Keys)) if (sel[Kind(s)]) d.Remove(s);
            bool[] has = new bool[4];
            foreach (KeyValuePair<string, string> kv in n) { int k = Kind(kv.Key); if (!sel[k]) continue; d[kv.Key] = kv.Value; c++; has[k] = true; }
            if (has[1]) d["radio.pending"] = "1"; if (has[2]) d["menu.pending"] = "1"; if (has[3]) d["memch.pending"] = "1";
            Save(); return c;
        }        public static Dictionary<string, string> Parse(string text)
        {
            Dictionary<string, string> n = new Dictionary<string, string>();
            foreach (string raw in text.Replace("\r", "").Split('\n')) { string line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue; int i = line.IndexOf('='); if (i > 0) n[line.Substring(0, i).Trim()] = line.Substring(i + 1); }
            return n;
        }
        public static Dictionary<string, string> WithPrefix(string p) { Dictionary<string, string> r = new Dictionary<string, string>(); foreach (KeyValuePair<string, string> kv in d) if (kv.Key.StartsWith(p)) r[kv.Key] = kv.Value; return r; }
        public static void RemovePrefix(string p) { List<string> k = new List<string>(d.Keys); foreach (string s in k) if (s.StartsWith(p)) d.Remove(s); Save(); }
        // remplace toute la configuration par le texte donne ; si des reglages du poste y figurent, ils seront renvoyes au poste a la connexion
        public static int Import(string text)
        {
            Dictionary<string, string> n = Parse(text);
            if (n.Count == 0) return 0;
            d.Clear(); bool radio = false; foreach (KeyValuePair<string, string> kv in n) { d[kv.Key] = kv.Value; if (kv.Key.StartsWith("radio.")) radio = true; }
            if (radio) d["radio.pending"] = "1"; Save(); return n.Count;
        }
        public static string Get(string k, string def) { string v; return d.TryGetValue(k, out v) ? v : def; }
        public static void Set(string k, string v) { d[k] = v; }
        public static int GetInt(string k, int def) { int v; return int.TryParse(Get(k, ""), out v) ? v : def; }
        public static void SetInt(string k, int v) { d[k] = v.ToString(); }
        public static double GetDbl(string k, double def) { double v; return double.TryParse(Get(k, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
        public static void SetDbl(string k, double v) { d[k] = v.ToString(CultureInfo.InvariantCulture); }
        public static bool GetBool(string k, bool def) { return GetInt(k, def ? 1 : 0) != 0; }
        public static void SetBool(string k, bool v) { SetInt(k, v ? 1 : 0); }
    }
}
