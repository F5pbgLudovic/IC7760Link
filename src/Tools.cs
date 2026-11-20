using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Media;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace IC7760Link
{
    // Textes : français seulement (comme le reste de l'interface)
    static class Lang
    {
        public static string T(string fr, string en) { return fr; }
        public static string F(string fr, string en, params object[] a) { return string.Format(fr, a); }
    }

    // Journal d'événements (data\evenements.log, 100 Ko maximum) : TUNE, ROS, micro, mise à jour
    static class CatLog
    {
        static readonly object lk = new object();
        public static void Event(string cat, string msg)
        {
            try
            {
                lock (lk)
                {
                    string p = Path.Combine(Cfg.Dir ?? ".", "evenements.log");
                    if (File.Exists(p) && new FileInfo(p).Length > 100000) File.Delete(p);
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + cat + "] " + msg + "\r\n", Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    // Étalonnage des vumètres Po et ROS : points « lecture du poste (0-255) = valeur réelle » interpolés ; réglages cal.po / cal.swr
    static class Calib
    {
        public static event Action Changed;
        public static readonly double[][] DefPo = { new double[] { 0, 143, 212 }, new double[] { 0, 100, 200 } };
        public static readonly double[][] DefSwr = { new double[] { 0, 48, 80, 120, 255 }, new double[] { 1.0, 1.5, 2.0, 3.0, 10.0 } };
        static readonly Dictionary<string, KeyValuePair<string, double[][]>> cache = new Dictionary<string, KeyValuePair<string, double[][]>>();
        public static double[][] Pts(string kind)
        {
            string s = Cfg.Get("cal." + kind, ""); KeyValuePair<string, double[][]> c;
            if (cache.TryGetValue(kind, out c) && c.Key == s) return c.Value;
            double[][] r = Parse(kind, s); cache[kind] = new KeyValuePair<string, double[][]>(s, r); return r;
        }
        static double[][] Parse(string kind, string s)
        {
            double[][] def = kind == "po" ? DefPo : DefSwr;
            if (s.Length == 0) return def;
            List<double[]> l = new List<double[]>();
            foreach (string p in s.Split(';')) { string[] a = p.Split(':'); double x, y; if (a.Length == 2 && double.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && double.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) l.Add(new double[] { x, y }); }
            if (l.Count < 2) return def;
            l.Sort(delegate(double[] a, double[] b) { return a[0].CompareTo(b[0]); });
            double[] xs = new double[l.Count], ys = new double[l.Count]; for (int i = 0; i < l.Count; i++) { xs[i] = l[i][0]; ys[i] = l[i][1]; }
            return new double[][] { xs, ys };
        }
        public static string Text(string kind) { double[][] p = Pts(kind); StringBuilder sb = new StringBuilder(); for (int i = 0; i < p[0].Length; i++) sb.AppendLine(p[0][i].ToString("0.##", CultureInfo.InvariantCulture) + " = " + p[1][i].ToString("0.##", CultureInfo.InvariantCulture)); return sb.ToString(); }
        // texte « lecture = valeur » (une ligne par point) -> réglage ; false si moins de 2 points valides
        public static bool SetText(string kind, string text)
        {
            List<string> l = new List<string>();
            foreach (string line in text.Split('\n')) { string[] a = line.Trim().Replace(',', '.').Split('='); double x, y; if (a.Length == 2 && double.TryParse(a[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) && double.TryParse(a[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)) l.Add(x.ToString(CultureInfo.InvariantCulture) + ":" + y.ToString(CultureInfo.InvariantCulture)); }
            if (l.Count < 2) return false;
            Cfg.Set("cal." + kind, string.Join(";", l.ToArray())); Cfg.Save(); Fire(); return true;
        }
        public static void Reset(string kind) { Cfg.Set("cal." + kind, ""); Cfg.Save(); Fire(); }
        public static void Fire() { Action a = Changed; if (a != null) a(); }
        public static double Interp(string kind, double raw)
        {
            double[][] p = Pts(kind); double[] x = p[0], y = p[1];
            if (raw <= x[0]) return y[0]; if (raw >= x[x.Length - 1]) return y[y.Length - 1];
            for (int i = 1; i < x.Length; i++) if (raw <= x[i]) return y[i - 1] + (y[i] - y[i - 1]) * (raw - x[i - 1]) / Math.Max(1e-9, x[i] - x[i - 1]);
            return y[y.Length - 1];
        }
        // lecture du poste correspondant à une valeur réelle (pour placer les graduations)
        public static double RawOf(string kind, double val)
        {
            double[][] p = Pts(kind); double[] x = p[0], y = p[1];
            if (val <= y[0]) return x[0]; if (val >= y[y.Length - 1]) return x[x.Length - 1];
            for (int i = 1; i < y.Length; i++) if (val <= y[i]) return x[i - 1] + (x[i] - x[i - 1]) * (val - y[i - 1]) / Math.Max(1e-9, y[i] - y[i - 1]);
            return x[x.Length - 1];
        }
    }

    // Micro du PC vers le poste : le micro choisi est envoyé en continu sur la carte son USB du poste (WASAPI partagé).
    static class MicRoute
    {
        static RigMonitor mon;
        public static string MicId { get { return Cfg.Get("micro.id", ""); } }
        public static string MicName { get { return Cfg.Get("micro.nom", ""); } }
        public static string CardId { get { return Cfg.Get("micro.carte", ""); } }
        public static string CardName { get { return Cfg.Get("micro.cartenom", ""); } }
        public static bool On { get { return Cfg.GetBool("micro.on", false); } }
        public static void Update()
        {
            bool want = On && MicId.Length > 0 && CardId.Length > 0;
            if (mon != null && (!want || mon.CaptureId != MicId || mon.OutputId != CardId)) Stop();
            if (want && mon == null)
            {
                RigMonitor r = new RigMonitor(MicId, CardId, false, true, "Micro vers le poste");
                r.Problem += delegate(string msg) { if (msg != null) CatLog.Event("micro", "carte son indisponible : " + msg); };
                mon = r;
            }
        }
        public static void Stop() { if (mon == null) return; RigMonitor r = mon; mon = null; r.Stop(); CatLog.Event("micro", "arrêté"); }
    }

    // Bouton TONE : tonalité de 1500 Hz sur la carte son du poste, PTT par CAT (ou VOX du poste), 5 s à 1 min. Réglages : Outils > TUNE / micro.
    static class TuneMic
    {
        public static event Action Changed;
        static TuneTone tone; static Rig rig; static bool owned; static DateTime until; static System.Windows.Forms.Timer tm;
        static bool? forceVox; static bool safe; static bool pwrOn; static int pwrOrig;
        public static int Level { get { return Math.Max(1, Math.Min(100, Cfg.GetInt("tune.niveau", 15))); } }
        public static int Seconds { get { return Math.Max(5, Math.Min(60, Cfg.GetInt("tune.duree", 10))); } }
        public static bool Vox { get { return forceVox.HasValue ? forceVox.Value : Cfg.GetBool("tune.vox", true); } }
        public static bool Active { get { return tone != null; } }
        public static bool SafeActive { get { return tone != null && pwrOn; } }
        public static int Left { get { return Math.Max(0, (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds)); } }
        static void Fire() { Action a = Changed; if (a != null) a(); }
        // Test de la puissance de sécurité : TONE avec la puissance réduite au % réglé (Alerte ROS), puissance d'origine remise à la fin
        public static void StartSafe(Rig r, bool vox) { if (tone != null) { Stop("TONE arrêté"); return; } safe = true; forceVox = vox; Start(r); if (tone == null) { safe = false; forceVox = null; } }
        public static void Reapply() { try { if (tone != null && pwrOn && rig != null && rig.Link != null) rig.Write(0, "14 0A", RosGuard.Reduced(pwrOrig)); } catch { } }
        public static void Toggle(Rig r) { if (tone != null) { Stop("TONE arrêté"); return; } Start(r); }

        static void Start(Rig r)
        {
            if (r == null) return;
            if (Cfg.Get("micro.carte", "").Length == 0) { MessageBox.Show("TONE : choisissez d'abord la carte son USB du poste (Outils ⚑ > Micro / TUNE).", "TONE"); return; }
            if (!Vox && r.Link == null) { MessageBox.Show("TONE : connectez d'abord le poste (ou choisissez « VOX du poste » dans Outils > TUNE).", "TONE"); return; }
            rig = r; owned = false; pwrOn = false;
            if (safe)
            {
                if (r.Link == null || !r.Connected) { MessageBox.Show("Test impossible : le poste n'est pas connecté.", "TONE"); return; }
                int o = r.Get(0, "14 0A"); pwrOrig = o; pwrOn = true; r.Write(0, "14 0A", RosGuard.Reduced(o)); CatLog.Event("tune", "Test sécurité : puissance " + o + " -> " + RosGuard.Reduced(o));
            }
            if (!Vox) { owned = !r.Ptt; if (owned) r.SetPtt(true); }
            tone = new TuneTone(MicRoute.CardId, 1500, Level / 100f); tone.Start();
            until = DateTime.UtcNow.AddSeconds(Seconds);
            if (tm == null) { tm = new System.Windows.Forms.Timer(); tm.Interval = 200; tm.Tick += delegate { Tick(); }; }
            tm.Start(); CatLog.Event("tune", "TONE : émission " + Seconds + " s, niveau " + Level + " %, " + (Vox ? "VOX du poste" : "PTT par CAT")); Fire();
        }
        static void Tick()
        {
            TuneTone t = tone; if (t == null) { tm.Stop(); return; }
            if (t.Error != null) { Stop("TONE : carte son indisponible (" + t.Error + ")"); return; }
            if (!Vox && rig.Link == null) { Stop("TONE arrêté (poste déconnecté)"); return; }
            if (DateTime.UtcNow >= until) { Stop("TONE terminé"); return; }
            Fire();
        }
        public static void Stop(string why)
        {
            TuneTone t = tone; if (t == null) return;
            tone = null; if (tm != null) tm.Stop();
            try { t.Stop(); } catch { }
            try { if (owned && rig != null && rig.Link != null) rig.SetPtt(false); } catch { }
            try { if (pwrOn && rig != null && rig.Link != null) rig.Write(0, "14 0A", pwrOrig); } catch { }
            pwrOn = false; safe = false; forceVox = null; owned = false; if (why != null) CatLog.Event("tune", why); Fire();
        }
    }

    // Alerte ROS : en émission, si le ROS dépasse le seuil (après un délai), bips continus jusqu'à l'arrêt par l'opérateur ; option : chute de puissance.
    static class RosGuard
    {
        public static bool On { get { return Cfg.GetBool("ros.on", true); } }
        public static double Threshold { get { return Math.Max(1.1, Math.Min(20, Cfg.GetInt("ros.seuil", 200) / 100.0)); } }
        public static int DelayMs { get { return Math.Max(0, Math.Min(10000, Cfg.GetInt("ros.delai", 300))); } }
        public static int Freq { get { return Math.Max(200, Math.Min(8000, Cfg.GetInt("ros.freq", 2000))); } }
        public static int Volume { get { return Math.Max(1, Math.Min(100, Cfg.GetInt("ros.vol", 100))); } }
        public static int IntervalMs { get { return Math.Max(50, Math.Min(2000, Cfg.GetInt("ros.pause", 50))); } }
        public static bool Drop { get { return Cfg.GetBool("ros.chute", true); } }
        public static int DropPct { get { return Math.Max(0, Math.Min(100, Cfg.GetInt("ros.pct", 0))); } }
        public static int ClearMs { get { return Math.Max(500, Math.Min(30000, Cfg.GetInt("ros.eteint", 1500))); } }
        public static bool KeepReduced { get { return Cfg.GetBool("ros.garder", false); } }
        public static bool Alarming { get { return alarm; } }
        public static string Notice;
        public static bool Silent;                    // essais automatiques : ni bips ni fenêtre

        // ROS actuel (lecture 0-255 du poste -> table étalonnée), -1 si pas de lecture
        public static double Ratio(Rig r) { if (r == null || r.Link == null) return -1; return Rig.ToSwr(r.Rx[0].Get("15 12")); }
        // 0 % = puissance minimale du poste, 100 % = puissance réglée, linéaire entre les deux
        public static int Reduced(int v) { return (int)Math.Round(Math.Max(0, v) * DropPct / 100.0); }
        public static bool ShouldTrigger(double ratio, ref int since, int nowMs, double threshold, int delayMs)
        {
            if (ratio < threshold) { since = 0; return false; }
            if (since == 0) since = nowMs == 0 ? 1 : nowMs;
            return unchecked(nowMs - since) >= delayMs;
        }
        public static bool ShouldClear(double ratio, ref int since, int nowMs, double threshold, int holdMs)
        {
            if (ratio < 0 || ratio >= threshold) { since = 0; return false; }
            if (since == 0) since = nowMs == 0 ? 1 : nowMs;
            return unchecked(nowMs - since) >= holdMs;
        }
        public static byte[] Wav(int hz, int vol, int ms)
        {
            const int sr = 44100; int n = sr * ms / 1000; MemoryStream m = new MemoryStream(); BinaryWriter w = new BinaryWriter(m);
            w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n * 2); w.Write(new char[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(n * 2); double amp = 32767.0 * vol / 100.0; int fade = sr / 300;
            for (int i = 0; i < n; i++) { double e = Math.Min(1.0, Math.Min(i, n - 1 - i) / (double)fade); w.Write((short)Math.Round(Math.Sin(2 * Math.PI * hz * i / sr) * amp * e)); }
            w.Flush(); return m.ToArray();
        }
        static volatile bool alarm; static Thread th; static System.Windows.Forms.Timer tm; static Func<Rig> getRig; static int since, clearSince;
        static Rig alarmRig; static bool dropped; static int dropOrig; static Form alertForm; static Label alertLbl; static Button btnRest;
        public static string AlarmText { get { double r = Ratio(alarmRig); return "ALERTE ROS" + (r > 0 ? "  " + r.ToString("0.0") : ""); } }
        static void BeepLoop(int count)
        {
            byte[] wav = Wav(Freq, Volume, 120); int pause = IntervalMs;
            using (SoundPlayer p = new SoundPlayer(new MemoryStream(wav)))
                for (int i = 0; (count == 0 ? alarm : i < count); i++) { try { p.PlaySync(); } catch { } Thread.Sleep(pause); }
        }
        public static void TestBeep() { Thread t = new Thread(delegate() { BeepLoop(4); }); t.IsBackground = true; t.Start(); }
        public static void Test(Rig r) { if (!alarm) Trigger(r, Threshold + 1); }
        public static void Start(Func<Rig> g)
        {
            getRig = g; if (tm != null) return; tm = new System.Windows.Forms.Timer(); tm.Interval = 100; tm.Tick += delegate { try { Tick(Environment.TickCount); } catch { } }; tm.Start();
        }
        public static void Tick(int now)
        {
            if (alarm)
            {
                if (alertLbl != null) alertLbl.Text = Msg(alarmRig, Ratio(alarmRig));
                double rc = Ratio(alarmRig); bool idle = alarmRig == null || !alarmRig.Ptt;
                if (ShouldClear(idle && (rc < 0 || rc >= Threshold) ? 1.0 : rc, ref clearSince, now, Threshold, ClearMs))
                {
                    bool keep = KeepReduced;
                    string w = "Alerte ROS terminée : " + (idle && (rc < 0 || rc >= Threshold) ? "l'émission est terminée" : "le ROS est revenu à " + rc.ToString("0.0")) + (dropped ? (keep ? ". Puissance laissée réduite (" + dropOrig + " avant), comme demandé dans les paramètres." : ". Puissance rétablie.") : ".");
                    CatLog.Event("ros", w); Notice = w; Stop(!keep);
                }
                return;
            }
            Rig r = getRig == null ? null : getRig();
            if (!On || r == null) return;
            if (r.Link == null || !r.Ptt) { since = 0; return; }
            double ro = Ratio(r); if (ro < 0) { since = 0; return; }
            if (ShouldTrigger(ro, ref since, now, Threshold, DelayMs)) Trigger(r, ro);
        }
        static string Msg(Rig r, double ro) { return "ROS " + (ro < 0 ? "?" : ro.ToString("0.0")) + " — seuil " + Threshold.ToString("0.0") + (dropped ? "\nPuissance réduite à " + DropPct + " % (était " + dropOrig + "/255)." : ""); }
        static void Trigger(Rig r, double ro)
        {
            alarm = true; clearSince = 0; alarmRig = r; dropped = false; dropOrig = 0;
            if (Drop && r.Link != null) { try { int o = r.Get(0, "14 0A"); r.Write(0, "14 0A", Reduced(o)); dropped = true; dropOrig = o; } catch { } }
            CatLog.Event("ros", "ALERTE ROS " + ro.ToString("0.0") + " (seuil " + Threshold.ToString("0.0") + ")" + (dropped ? " : puissance " + dropOrig + " -> " + Reduced(dropOrig) : ""));
            if (Silent) return;
            th = new Thread(delegate() { BeepLoop(0); }); th.IsBackground = true; th.Start();
            ShowForm(r, ro);
        }
        static void ShowForm(Rig r, double ro)
        {
            Form f = new Form(); alertForm = f; f.Text = "ALERTE ROS"; f.TopMost = true; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.ControlBox = false; f.MaximizeBox = false; f.MinimizeBox = false; f.StartPosition = FormStartPosition.CenterScreen;
            f.BackColor = Color.FromArgb(150, 0, 0); f.ForeColor = Color.White; f.ClientSize = new Size(520, 190); f.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            alertLbl = new Label(); alertLbl.SetBounds(12, 12, 496, 90); alertLbl.Font = new Font("Segoe UI", 14f, FontStyle.Bold); alertLbl.Text = Msg(r, ro); f.Controls.Add(alertLbl);
            Button b = new Button(); b.Text = "ARRÊTER L'ALERTE"; b.SetBounds(12, 120, 240, 54); b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.White; b.ForeColor = Color.Black; b.Click += delegate { Stop(false); }; f.Controls.Add(b);
            btnRest = new Button(); btnRest.Text = "Arrêter et rétablir la puissance"; btnRest.SetBounds(262, 120, 246, 54); btnRest.FlatStyle = FlatStyle.Flat; btnRest.BackColor = Color.FromArgb(230, 230, 230); btnRest.ForeColor = Color.Black; btnRest.Enabled = dropped; btnRest.Click += delegate { Stop(true); }; f.Controls.Add(btnRest);
            f.FormClosing += delegate(object s, FormClosingEventArgs e) { if (alarm && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; };
            f.Show();
        }
        // Le % de chute vient d'être modifié : pris en compte tout de suite (alerte en cours ou test TONE de sécurité), envois regroupés
        static System.Windows.Forms.Timer reTm;
        public static void ReapplyDrop()
        {
            if (reTm == null) { reTm = new System.Windows.Forms.Timer(); reTm.Interval = 150; reTm.Tick += delegate { reTm.Stop(); ReapplyNow(); }; }
            reTm.Stop(); reTm.Start();
        }
        public static void ReapplyNow()
        {
            if (alarm && dropped && alarmRig != null && alarmRig.Link != null) { try { alarmRig.Write(0, "14 0A", Reduced(dropOrig)); } catch { } CatLog.Event("ros", "puissance de sécurité modifiée : " + DropPct + " %"); }
            TuneMic.Reapply();
        }
        public static void Stop(bool restore)
        {
            bool was = alarm; alarm = false;
            if (restore && dropped && alarmRig != null && alarmRig.Link != null) { try { alarmRig.Write(0, "14 0A", dropOrig); } catch { } }
            if (was) CatLog.Event("ros", "alerte ROS arrêtée" + (restore ? " (puissance rétablie)" : ""));
            dropped = false; alarmRig = null;
            Form f = alertForm; alertForm = null; alertLbl = null; btnRest = null; if (f != null) { try { f.Close(); f.Dispose(); } catch { } }
        }
    }

    // Mise à jour depuis la dernière version publiée sur GitHub : seul l'exe est remplacé, le dossier data reste en place ; l'ancien exe est conservé.
    static class Updater
    {
        public static bool Disabled;                 // essais automatiques : aucune vérification
        public static string ApiOverride;            // essais : adresse (file:// ou http) à la place de GitHub
        public static bool AutoAccept;               // essais : accepte sans fenêtre
        public const string Repo = "F5pbgLudovic/IC7760Link";
        public static bool AutoOn { get { return Cfg.GetBool("maj.auto", false); } }
        public class Info { public string Tag = "", Version = "", Url = "", Notes = ""; public long Size; }
        static string Api { get { string o = ApiOverride ?? Environment.GetEnvironmentVariable("IC7760_UPDATE_API"); return !string.IsNullOrEmpty(o) ? o : "https://api.github.com/repos/" + Repo + "/releases/latest"; } }
        static WebClient Web()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | ServicePointManager.SecurityProtocol; } catch { }
            WebClient w = new WebClient(); w.Encoding = Encoding.UTF8; w.Headers[HttpRequestHeader.UserAgent] = "IC7760Link-updater"; w.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json"; return w;
        }
        public static int Compare(string a, string b)
        {
            int[] x = Parts(a), y = Parts(b);
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++) { int p = i < x.Length ? x[i] : 0, q = i < y.Length ? y[i] : 0; if (p != q) return p < q ? -1 : 1; }
            return 0;
        }
        static int[] Parts(string s)
        {
            List<int> l = new List<int>(); foreach (string p in (s ?? "").TrimStart('v', 'V', ' ').Split('.')) { Match m = Regex.Match(p, @"^\d+"); int n; l.Add(m.Success && int.TryParse(m.Value, out n) ? n : 0); } return l.ToArray();
        }
        public static Info Check(out string err)
        {
            err = null;
            try { string json; using (WebClient w = Web()) json = w.DownloadString(Api); return Parse(json, out err); }
            catch (Exception ex) { err = "Vérification impossible : " + ex.Message; return null; }
        }
        public static Info Parse(string json, out string err)
        {
            err = null; Info i = new Info();
            Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\""); if (!t.Success) { err = "Réponse inattendue (pas de numéro de version)."; return null; }
            i.Tag = t.Groups[1].Value; i.Version = i.Tag.TrimStart('v', 'V');
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"")) { string u = m.Groups[1].Value.Replace("\\/", "/"); if (u.EndsWith("/IC7760Link.exe", StringComparison.OrdinalIgnoreCase)) { i.Url = u; break; } }
            Match s = Regex.Match(json, "\"size\"\\s*:\\s*(\\d+)[^{}]*\"browser_download_url\"\\s*:\\s*\"[^\"]*IC7760Link\\.exe\""); if (s.Success) long.TryParse(s.Groups[1].Value, out i.Size);
            Match b = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (b.Success) { string n = b.Groups[1].Value.Replace("\\r", "").Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\/", "/").Replace("\\\\", "\\"); i.Notes = n.Length > 900 ? n.Substring(0, 900) + "…" : n; }
            if (i.Url.Length == 0) { err = "La version " + i.Version + " ne contient pas IC7760Link.exe."; return null; }
            return i;
        }
        public static bool IsNewer(Info i) { return i != null && Compare(i.Version, MainForm.Version) > 0; }
        public static string Download(Info i, string dir, Action<int> progress, out string err)
        {
            err = null; string dst = Path.Combine(dir, "IC7760Link.maj");
            try
            {
                using (WebClient w = Web())
                {
                    ManualResetEvent done = new ManualResetEvent(false); Exception fail = null;
                    if (progress != null) w.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e) { progress(e.ProgressPercentage); };
                    w.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e) { fail = e.Error; done.Set(); };
                    w.DownloadFileAsync(new Uri(i.Url), dst); while (!done.WaitOne(40)) Application.DoEvents();
                    if (fail != null) throw fail;
                }
                FileInfo f = new FileInfo(dst); byte[] head = new byte[2]; using (FileStream fs = File.OpenRead(dst)) fs.Read(head, 0, 2);
                if (f.Length < 100000 || head[0] != 'M' || head[1] != 'Z') { try { File.Delete(dst); } catch { } err = "Le fichier téléchargé n'est pas un programme valide."; return null; }
                if (i.Size > 0 && f.Length != i.Size) { try { File.Delete(dst); } catch { } err = "Téléchargement incomplet (" + f.Length + " / " + i.Size + " octets)."; return null; }
                return dst;
            }
            catch (Exception ex) { try { if (File.Exists(dst)) File.Delete(dst); } catch { } err = "Téléchargement impossible : " + ex.Message; return null; }
        }
        public static string BackupData(string dir)
        {
            string b = Path.Combine(dir, "sauvegarde-avant-MAJ-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss")); Directory.CreateDirectory(b);
            foreach (string f in Directory.GetFiles(dir, "*.ini")) File.Copy(f, Path.Combine(b, Path.GetFileName(f)), true);
            return b;
        }
        public static string BuildScript(int pid, string exe, bool deleteSettings, string settings)
        {
            StringBuilder sb = new StringBuilder(); sb.AppendLine("@echo off"); sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine(":w"); sb.AppendLine("tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul && (ping -n 2 127.0.0.1 >nul & goto w)");
            if (deleteSettings) sb.AppendLine("del /f /q \"" + settings + "\" 2>nul");
            sb.AppendLine("start \"\" \"" + exe + "\""); sb.AppendLine("(goto) 2>nul & del \"%~f0\"");
            return sb.ToString();
        }
        // L'ancien exe est renommé IC7760Link-<version>.exe (permis même ouvert), le nouveau prend sa place ; un script attend la fin du programme puis le relance.
        public static bool Apply(string exe, string newFile, string oldVersion, bool deleteSettings, out string backup, out string err)
        {
            err = null; backup = null; string dir = Path.GetDirectoryName(exe);
            try
            {
                string bk = Path.Combine(dir, "IC7760Link-" + oldVersion + ".exe"); if (File.Exists(bk)) bk = Path.Combine(dir, "IC7760Link-" + oldVersion + "-" + DateTime.Now.ToString("HHmmss") + ".exe");
                File.Move(exe, bk);
                try { File.Move(newFile, exe); } catch (Exception ex) { try { File.Move(bk, exe); } catch { } err = "Remplacement impossible : " + ex.Message; return false; }
                backup = bk;
                string cmd = Path.Combine(Path.GetTempPath(), "ic7760link-maj.cmd"); File.WriteAllText(cmd, BuildScript(Process.GetCurrentProcess().Id, exe, deleteSettings, Path.Combine(Cfg.Dir ?? dir, "settings.ini")), new UTF8Encoding(false));
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\""); psi.CreateNoWindow = true; psi.UseShellExecute = false; psi.WindowStyle = ProcessWindowStyle.Hidden; Process.Start(psi);
                return true;
            }
            catch (Exception ex) { err = "Mise à jour impossible : " + ex.Message + "\n(le dossier du programme est-il protégé en écriture ?)"; return false; }
        }
        public static void CheckAtStart(MainForm f)
        {
            if (Disabled || !AutoOn) return;
            Thread t = new Thread(delegate()
            {
                string err; Info i = Check(out err);
                if (i == null || !IsNewer(i) || Cfg.Get("maj.ignore", "") == i.Version) return;
                try { f.BeginInvoke((Action)delegate { Propose(f, i, true); }); } catch { }
            }); t.IsBackground = true; t.Start();
        }
        public static void Propose(MainForm owner, Info i, bool auto)
        {
            bool keepSet = true, ok;
            if (AutoAccept) ok = true;
            else using (UpdateDlg d = new UpdateDlg(i, auto)) { DialogResult r = d.ShowDialog(owner); ok = r == DialogResult.OK; if (r == DialogResult.Ignore) { Cfg.Set("maj.ignore", i.Version); Cfg.Save(); } keepSet = d.KeepSettings; }
            if (!ok) return;
            if (!owner.CanQuitForUpdate()) return;
            string dir = Cfg.Dir ?? ".";
            try { Cfg.Save(); BackupData(dir); }
            catch (Exception ex) { MessageBox.Show(owner, "Sauvegarde impossible : " + ex.Message + "\nMise à jour annulée.", "Mise à jour"); return; }
            string err; string exe = Application.ExecutablePath; string tmp;
            using (ProgressForm pf = new ProgressForm("Téléchargement de la version " + i.Version + "…"))
            {
                pf.Show(owner); Application.DoEvents();
                tmp = Download(i, Path.GetDirectoryName(exe), delegate(int p) { pf.Set(p); Application.DoEvents(); }, out err);
            }
            if (tmp == null) { MessageBox.Show(owner, err, "Mise à jour"); return; }
            string bkExe; if (!Apply(exe, tmp, MainForm.Version, !keepSet, out bkExe, out err)) { MessageBox.Show(owner, err, "Mise à jour"); try { File.Delete(tmp); } catch { } return; }
            CatLog.Event("maj", "mise à jour " + MainForm.Version + " -> " + i.Version + " (ancien programme : " + bkExe + ")");
            owner.QuitForUpdate();
        }
    }

    class ProgressForm : Form
    {
        readonly ProgressBar pb = new ProgressBar();
        public ProgressForm(string text)
        {
            Text = "Mise à jour"; FormBorderStyle = FormBorderStyle.FixedDialog; ControlBox = false; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(380, 80); BackColor = T.Bg; ForeColor = T.Text; Font = new Font("Segoe UI", 9f);
            Label l = new Label(); l.Text = text; l.SetBounds(12, 10, 356, 20); Controls.Add(l); pb.SetBounds(12, 40, 356, 22); Controls.Add(pb);
        }
        public void Set(int p) { pb.Value = Math.Max(0, Math.Min(100, p)); }
    }

    class UpdateDlg : Form
    {
        readonly CheckBox cbSet = new CheckBox();
        public bool KeepSettings { get { return cbSet.Checked; } }
        public UpdateDlg(Updater.Info i, bool auto)
        {
            Text = "Mise à jour disponible"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; BackColor = T.Panel; ForeColor = T.Text; Font = new Font("Segoe UI", 9f); ClientSize = new Size(560, 360);
            Label h = new Label(); h.Text = "Version " + i.Version + " disponible (vous avez la " + MainForm.Version + ")"; h.Font = new Font("Segoe UI Semibold", 11f); h.ForeColor = T.Amber; h.SetBounds(12, 10, 536, 24); Controls.Add(h);
            TextBox n = new TextBox(); n.Multiline = true; n.ReadOnly = true; n.ScrollBars = ScrollBars.Vertical; n.Text = i.Notes.Length > 0 ? i.Notes.Replace("\n", "\r\n") : "(pas de notes de version)"; n.SetBounds(12, 40, 536, 130); n.BackColor = T.Bg; n.ForeColor = T.Text; Controls.Add(n);
            Label w = new Label(); w.SetBounds(12, 180, 536, 60); w.Text = "Seul le programme est remplacé. Vos réglages (dossier data) restent en place ; l'ancien programme est gardé sous le nom IC7760Link-" + MainForm.Version + ".exe. Une copie de sécurité des réglages est faite avant. Le logiciel redémarre tout seul."; Controls.Add(w);
            cbSet.Text = "Conserver mes réglages (couleurs, port, TONE, ROS, étalonnage…)"; cbSet.Checked = true; cbSet.SetBounds(16, 250, 530, 24); Controls.Add(cbSet);
            Label u = new Label(); u.SetBounds(34, 276, 510, 36); u.ForeColor = T.Dim; u.Text = "Décochez pour repartir des valeurs d'origine : la copie de sécurité permet de retrouver l'ancien fichier."; Controls.Add(u);
            Button ok = Mk("Mettre à jour", 270, 316, 140); ok.DialogResult = DialogResult.OK; ok.BackColor = Color.FromArgb(20, 110, 40); AcceptButton = ok;
            Button no = Mk("Plus tard", 418, 316, 130); no.DialogResult = DialogResult.Cancel; CancelButton = no;
            if (auto) { Button ig = Mk("Ignorer cette version", 16, 316, 180); ig.DialogResult = DialogResult.Ignore; }
        }
        Button Mk(string t, int x, int y, int w) { Button b = new Button(); b.Text = t; b.SetBounds(x, y, w, 32); b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; Controls.Add(b); return b; }
    }
}
