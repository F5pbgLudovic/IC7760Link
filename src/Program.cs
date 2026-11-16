using System;
using System.IO;
using System.Windows.Forms;

namespace IC7760Link
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Cfg.Load();
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { Log(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log(e.ExceptionObject as Exception); };
            foreach (string a in args) if (a.StartsWith("--selftest=")) { Environment.Exit(SelfTest.Run(a.Substring(11))); return; }
            foreach (string a in args) if (a.StartsWith("--diag")) { Environment.Exit(Diag(a)); return; }
            MainForm f = new MainForm();
            foreach (string a in args)
            {
                if (a == "--virtual") f.Shown += delegate { f.AutoStart("virtual"); };
                else if (a.StartsWith("--shot="))
                {
                    string[] p = a.Substring(7).Split(','); string path = p[0]; int ms = p.Length > 1 ? int.Parse(p[1]) : 8000; string act = p.Length > 2 ? p[2] : "";
                    f.Shown += delegate { f.AutoStart("virtual"); f.ShotAndExit(path, ms, act); };
                }
            }
            Application.Run(f);
        }
        // Diagnostic : IC7760Link.exe --diag=COM5[,115200[,B2]] enregistre 12 s de trames CI-V dans diagnostic-ci-v.txt
        static int Diag(string arg)
        {
            string[] p = arg.Contains("=") ? arg.Substring(arg.IndexOf('=') + 1).Split(',') : new string[0];
            string port = p.Length > 0 ? p[0] : ""; int baud = 115200; byte addr = 0x94;
            if (p.Length > 1) int.TryParse(p[1], out baud); if (p.Length > 2) try { addr = Convert.ToByte(p[2], 16); } catch { }
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostic-ci-v.txt");
            System.Text.StringBuilder sb = new System.Text.StringBuilder(); object lk = new object(); int n = 0;
            sb.AppendLine("IC-7760 Link " + MainForm.Version + " - diagnostic " + DateTime.Now + " - " + port + " " + baud + " bauds, adresse " + addr.ToString("X2"));
            Rig rig = new Rig(); rig.TraceScopeMax = 40;
            rig.Log += delegate(string s, bool tx) { lock (lk) { if (n++ < 6000) sb.AppendLine((tx ? "TX " : "RX ") + s); } };
            try
            {
                string err; ILink link = new SerialLink(port, baud);
                if (!rig.Connect(link, addr, out err)) sb.AppendLine("CONNEXION IMPOSSIBLE : " + err);
                else
                {
                    System.Threading.Thread.Sleep(12000);
                    sb.AppendLine("--- fin : connecté=" + rig.Connected + " MAIN=" + rig.Rx[0].Freq + " mode=" + rig.Rx[0].Mode + " largeur=" + rig.Rx[0].Width + " | trames scope brutes=" + rig.ScopeRaw + " assemblées=" + (rig.ScopeAlive ? "oui" : "non") + " adresse finale=" + rig.Addr.ToString("X2") + (rig.AddrAdopted ? " (adoptée)" : ""));
                    rig.Disconnect();
                }
            }
            catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex); }
            lock (lk) File.WriteAllText(file, sb.ToString());
            return 0;
        }
        static void Log(Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Cfg.Dir ?? ".", "crash.log"), DateTime.Now + "\r\n" + ex + "\r\n\r\n"); } catch { }
        }
    }
}
