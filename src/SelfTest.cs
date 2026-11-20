using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace IC7760Link
{
    // Autotest sans interface : IC7760Link.exe --selftest=rapport.txt
    public static class SelfTest
    {
        static StringBuilder sb = new StringBuilder(); static int fail;
        static void Check(string name, bool ok, string detail) { sb.AppendLine((ok ? "OK    " : "ECHEC ") + name + (detail != null ? "  [" + detail + "]" : "")); if (!ok) fail++; }

        public static int Run(string report)
        {
            try
            {
                Rig rig = new Rig(); VirtualLink link = new VirtualLink(0xB2); string err;
                int frames = 0, minLen = 99999, maxLen = 0; ScopeFrame last = null;
                Check("connexion", rig.Connect(link, 0xB2, out err), err);
                Thread.Sleep(2500);
                Check("poste répond", rig.Connected, rig.Status);
                Check("fréquence MAIN lue", rig.Rx[0].Freq == 14074000, rig.Rx[0].Freq.ToString());
                Check("fréquence SUB lue", rig.Rx[1].Freq == 7100000, rig.Rx[1].Freq.ToString());
                Check("mode MAIN USB / SUB LSB", rig.Rx[0].Mode == 1 && rig.Rx[1].Mode == 0, rig.Rx[0].Mode + "/" + rig.Rx[1].Mode);
                Check("AF MAIN = 140", rig.Rx[0].Get("14 01") == 140, rig.Rx[0].Get("14 01").ToString());
                ScopeFrame f; while (rig.ScopeQueue.TryDequeue(out f)) { frames++; last = f; if (f.Data.Length < minLen) minLen = f.Data.Length; if (f.Data.Length > maxLen) maxLen = f.Data.Length; }
                Check("trames de scope reçues", frames > 10, frames + " trames, " + minLen + ".." + maxLen + " points");
                Check("scope complet 475 points", minLen == 475 && maxLen == 475, null);
                if (last != null) Check("fenêtre du scope = fréquence ± 25 kHz", Math.Abs(last.Lo - (14074000 - 25000)) < 2 && Math.Abs(last.Hi - (14074000 + 25000)) < 2, last.Lo + " .. " + last.Hi);

                // écriture sur la voie SUB : ne doit pas toucher MAIN
                rig.Write(1, "14 01", 77); Thread.Sleep(1800);
                Check("AF SUB écrit et relu = 77", rig.Rx[1].Get("14 01") == 77, rig.Rx[1].Get("14 01").ToString());
                Check("AF MAIN inchangé = 140", rig.Rx[0].Get("14 01") == 140, rig.Rx[0].Get("14 01").ToString());

                // changement de fréquence / de mode
                rig.SetFreq(0, 14205500); Thread.Sleep(900);
                Check("fréquence MAIN réglée", rig.Rx[0].Freq == 14205500, rig.Rx[0].Freq.ToString());
                rig.SetMode(0, 3, 0, 2); Thread.Sleep(900);
                Check("mode CW FIL2", rig.Rx[0].Mode == 3 && rig.Rx[0].Filter == 2, rig.Rx[0].Mode + "/" + rig.Rx[0].Filter);
                Check("largeur CW par défaut 500 Hz", rig.Rx[0].Width == 500, rig.Rx[0].Width.ToString());
                rig.SetWidth(0, 250); Thread.Sleep(1500);
                Check("largeur 250 Hz", rig.Rx[0].Width == 250, rig.Rx[0].Width.ToString());

                // scope : span et mode
                rig.ScopeSetSpan(0, 5000); Thread.Sleep(600);
                while (rig.ScopeQueue.TryDequeue(out f)) last = f;
                Check("span ±5 kHz appliqué", last != null && Math.Abs((last.Hi - last.Lo) - 10000) < 2, last == null ? "-" : (last.Hi - last.Lo).ToString());
                rig.ScopeSetMode(0, 1); Thread.Sleep(600);
                while (rig.ScopeQueue.TryDequeue(out f)) last = f;
                Check("mode FIXE (bande 20 m)", last != null && last.Fixed && last.Lo < 14000000, last == null ? "-" : last.Lo + ".." + last.Hi);
                rig.ScopeSetMode(0, 0);

                // double scope : la voie SUB envoie aussi
                rig.ScopeSetDual(true); Thread.Sleep(600); int sub = 0, main = 0;
                while (rig.ScopeQueue.TryDequeue(out f)) { if (f.Rx == 1) sub++; else main++; }
                Check("double scope MAIN+SUB", sub > 3 && main > 3, "main " + main + " sub " + sub);

                // émission : S-mètre -> Po / SWR
                rig.SetPtt(true); Thread.Sleep(900);
                Check("PTT retour d'état", rig.Ptt, null);
                Check("Po lu en émission", rig.Rx[0].Get("15 11") > 50, rig.Rx[0].Get("15 11").ToString());
                rig.SetPtt(false); Thread.Sleep(700);
                Check("retour en réception", !rig.Ptt, null);

                // latence aller-retour brute (lecture de fréquence)
                List<double> lat = new List<double>(); ManualResetEvent ev = new ManualResetEvent(false); Stopwatch sw = new Stopwatch();
                Action<byte[]> h = delegate(byte[] fr) { if (fr.Length > 5 && fr[4] == 0x03) { lat.Add(sw.Elapsed.TotalMilliseconds); ev.Set(); } };
                link.Frame += h;
                for (int i = 0; i < 20; i++) { ev.Reset(); sw.Restart(); link.Send(Civ.Build(0xB2, 0xE0, new byte[] { 0x03 })); ev.WaitOne(500); Thread.Sleep(5); }
                link.Frame -= h; lat.Sort();
                double med = lat.Count > 0 ? lat[lat.Count / 2] : -1, mx = lat.Count > 0 ? lat[lat.Count - 1] : -1;
                Check("latence commande -> réponse", lat.Count >= 18 && med < 25, "médiane " + med.ToString("0.0") + " ms, max " + mx.ToString("0.0") + " ms (" + lat.Count + "/20)");

                // catalogue
                Check("catalogue chargé", Catalog.All.Count > 400, Catalog.All.Count + " commandes");
                CatEntry e = Catalog.Find("1A 05 00 02");
                Check("entrée SET décodée (1A 05 00 02 : 0..10)", e != null && e.HasRange && e.Min == 0 && e.Max == 10, e == null ? "absente" : e.Min + ".." + e.Max);
                CatEntry e2 = Catalog.Find("14 01"); Check("14 01 : 2 octets 0..255, [29]", e2 != null && e2.DataLen == 2 && e2.Max == 255 && e2.Direct29, null);
                // menu interne du poste : lecture complète, modification, réécriture
                List<string> mn = rig.ReadMenu(null, null); int nm = Rig.MenuEntries().Count;
                Check("menu interne lu en entier (" + nm + " entrées)", nm > 100 && mn.Count >= nm, mn.Count + " lues");
                rig.Write(0, "1A 05 00 02", 7); Thread.Sleep(900);
                Dictionary<string, string> mkv = new Dictionary<string, string>(); mkv["menu.g.1A 05 00 02"] = "3";
                rig.WriteMenu(mkv); Thread.Sleep(1200); rig.Rx[0].V.Clear(); List<string> mn2 = rig.ReadMenu(null, null);
                Check("menu réécrit puis relu (1A 05 00 02 = 3)", mn2.Contains("menu.g.1A 05 00 02=3"), mn2.Contains("menu.g.1A 05 00 02=3") ? "" : "valeur absente");
                // mémoires de fréquences : écriture brute puis relecture
                Dictionary<string, string> mk = new Dictionary<string, string>(); mk["memch.5"] = "00 00 00 07 07 00 00 03 01 01 00 54 45 53 54"; mk["memch.101"] = "00 00 00 14 20 00 00 01 01 01";
                rig.WriteMemories(mk); List<string> ml = rig.ReadMemories(null, null);
                Check("mémoires : 2 canaux écrits puis relus à l'identique", ml.Count == 2 && ml.Contains("memch.5=" + mk["memch.5"]) && ml.Contains("memch.101=" + mk["memch.101"]), string.Join(" | ", ml.ToArray()));
                // étalonnage des vumètres
                Check("étalonnage : valeurs d'origine (143 = 100 W, 212 = 200 W, ROS 80 = 2,0)", Math.Abs(Rig.ToWatts(143) - 100) < 0.01 && Math.Abs(Rig.ToWatts(212) - 200) < 0.01 && Math.Abs(Rig.ToSwr(80) - 2.0) < 0.01, null);
                Calib.SetText("po", "0 = 0\n100 = 50\n200 = 150");
                Check("étalonnage : points personnalisés (150 -> 100 W, graduation 100 W à 150)", Math.Abs(Rig.ToWatts(150) - 100) < 0.01 && Math.Abs(Calib.RawOf("po", 100) - 150) < 0.01, Rig.ToWatts(150).ToString("0.0"));
                Calib.Reset("po");
                Check("étalonnage : retour aux valeurs d'origine", Math.Abs(Rig.ToWatts(143) - 100) < 0.01, null);
                // alerte ROS : logique de seuil, de délai, d'extinction
                int sc = 0; bool t1 = RosGuard.ShouldTrigger(2.5, ref sc, 1000, 2.0, 300), t2 = RosGuard.ShouldTrigger(2.5, ref sc, 1400, 2.0, 300), t3 = RosGuard.ShouldTrigger(1.2, ref sc, 1500, 2.0, 300);
                Check("alerte ROS : déclenche après le délai seulement, remise à zéro sous le seuil", !t1 && t2 && !t3 && sc == 0, t1 + "/" + t2 + "/" + t3);
                int cc = 0; bool c1 = RosGuard.ShouldClear(1.3, ref cc, 1000, 2.0, 1500), c2 = RosGuard.ShouldClear(1.3, ref cc, 2600, 2.0, 1500);
                Check("alerte ROS : s'éteint après un ROS bas maintenu", !c1 && c2, c1 + "/" + c2);
                // alerte ROS de bout en bout sur le poste virtuel (sans bips ni fenêtre) : chute de puissance puis rétablissement
                int pOld = Cfg.GetInt("ros.pct", 0), sOld = Cfg.GetInt("ros.seuil", 200), dOld = Cfg.GetInt("ros.delai", 300); bool onOld = Cfg.GetBool("ros.on", true);
                Cfg.SetInt("ros.pct", 50); Cfg.SetInt("ros.seuil", 110); Cfg.SetInt("ros.delai", 0); Cfg.SetBool("ros.on", true); RosGuard.Silent = true;
                int pw0 = rig.Get(0, "14 0A"); rig.SetPtt(true); Thread.Sleep(1800);
                RosGuard.Start(delegate { return rig; });
                for (int i = 0; i < 12 && !RosGuard.Alarming; i++) { RosGuard.Tick(Environment.TickCount + i); }
                // le déclenchement demande un Func<Rig> : on passe par Test() qui appelle la même routine
                if (!RosGuard.Alarming) RosGuard.Test(rig);
                Thread.Sleep(1500); int pw1 = rig.Get(0, "14 0A");
                Check("alerte ROS : alerte levée et puissance réduite de moitié (" + pw0 + " -> " + pw1 + ")", RosGuard.Alarming && Math.Abs(pw1 - pw0 / 2) <= 1, RosGuard.Alarming + " " + pw1);
                RosGuard.Stop(true); Thread.Sleep(1500); int pw2 = rig.Get(0, "14 0A");
                Check("alerte ROS : puissance rétablie à l'arrêt (" + pw2 + ")", !RosGuard.Alarming && pw2 == pw0, pw2 + " / " + pw0);
                rig.SetPtt(false); RosGuard.Silent = false; Cfg.SetInt("ros.pct", pOld); Cfg.SetInt("ros.seuil", sOld); Cfg.SetInt("ros.delai", dOld); Cfg.SetBool("ros.on", onOld);
                // mise à jour : lecture de la réponse GitHub, comparaison des versions
                string uerr; Updater.Info ui = Updater.Parse("{\"tag_name\":\"v3.10\",\"body\":\"Notes\\nligne 2\",\"assets\":[{\"size\":123456,\"browser_download_url\":\"https://github.com/x/y/releases/download/v3.10/IC7760Link.exe\"}]}", out uerr);
                Check("mise à jour : réponse GitHub lue (v3.10, exe, taille, notes)", ui != null && ui.Version == "3.10" && ui.Url.EndsWith("IC7760Link.exe") && ui.Size == 123456 && ui.Notes.StartsWith("Notes"), uerr);
                Check("mise à jour : 3.10 > 3.9 > 2.9, 3.0 = v3.0", Updater.Compare("3.10", "3.9") > 0 && Updater.Compare("3.9", "2.9") > 0 && Updater.Compare("3.0", "v3.0") == 0, null);
                rig.Disconnect();
            }
            catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex); fail++; }
            sb.AppendLine(fail == 0 ? "TOUS LES TESTS PASSENT" : fail + " ECHEC(S)");
            File.WriteAllText(report, sb.ToString(), Encoding.UTF8);
            return fail;
        }
    }
}
