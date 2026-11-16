using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace IC7760Link
{
    // Export / import de toute la configuration de l'interface et des réglages du poste
    public class ConfigForm : Form
    {
        public bool Imported; readonly string radioText; readonly bool connected;
        public ConfigForm(string radioText, bool connected)
        {
            this.radioText = radioText; this.connected = connected;
            Text = "Export / import"; BackColor = T.Bg; ForeColor = T.Text; ClientSize = new Size(380, 150); StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            Label l = new Label(); l.SetBounds(12, 10, 356, 54); Controls.Add(l);
            l.Text = "Exporter : enregistre la configuration du logiciel" + (connected ? " et les réglages du poste" : " (poste non connecté : réglages du poste non inclus)") + " dans un fichier.\nImporter : restaure un fichier (le logiciel redémarre).";
            Button b1 = MkBtn("Exporter…", 12, 80, 112); b1.Click += delegate { Export(); };
            Button b2 = MkBtn("Importer…", 134, 80, 112); b2.Click += delegate { Open(); };
            Button b3 = MkBtn("Annuler", 256, 80, 112); b3.Click += delegate { Close(); };
            CancelButton = b3;
        }
        Button MkBtn(string t, int x, int y, int w)
        {
            Button b = new Button(); b.Text = t; b.SetBounds(x, y, w, 34); b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; Controls.Add(b); return b;
        }
        void Export()
        {
            string txt = Cfg.Dump().Replace("\r\n", "\n");
            txt += "\n# ---------- Réglages du poste " + (connected && radioText.Length > 0 ? "(relevés en direct) ----------\n" + radioText : "----------\n# Poste non connecté : réglages du poste non inclus.\n");
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Configuration IC-7760 Link (*.ini)|*.ini|Tous les fichiers|*.*"; d.FileName = "IC7760Link-config-" + DateTime.Now.ToString("yyyy-MM-dd") + ".ini";
                if (d.ShowDialog(this) == DialogResult.OK) { try { System.IO.File.WriteAllText(d.FileName, txt.Replace("\n", "\r\n"), Encoding.UTF8); MessageBox.Show(this, "Configuration exportée :\n" + d.FileName, "Export"); Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur"); } }
            }
        }
        void Open()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "Configuration IC-7760 Link (*.ini)|*.ini|Tous les fichiers|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string txt; try { txt = System.IO.File.ReadAllText(d.FileName, Encoding.UTF8); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur"); return; }
                if (MessageBox.Show(this, "Remplacer toute la configuration par ce fichier et redémarrer le logiciel ?\nLes réglages du poste (s'il y en a dans le fichier) seront renvoyés au poste dès la connexion.", "Importer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                if (Cfg.Import(txt) == 0) { MessageBox.Show(this, "Aucun réglage valide dans ce fichier (lignes clé=valeur attendues).", "Importer"); return; }
                Imported = true; DialogResult = DialogResult.OK; Close();
            }
        }
    }

    // Moniteur des trames CI-V (utile pour diagnostiquer une liaison réelle)
    public class MonitorForm : Form
    {
        public Func<string> GetPort; public Func<bool> RigBusy; public Action<int, byte> Found; Label lbAddr; byte txDst = 0xFF; readonly Dictionary<byte, int> rxSrc = new Dictionary<byte, int>(); bool scanning;
        readonly ListBox lb = new ListBox(); readonly List<string> pend = new List<string>(); readonly object sync = new object();
        readonly System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); CheckBox cbPause, cbRx; readonly List<string> all = new List<string>();
        public MonitorForm()
        {
            Text = "Monitoring et détection CI-V"; BackColor = T.Bg; ForeColor = T.Text; ClientSize = new Size(760, 420); StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9f);
            Panel top = new Panel(); top.Dock = DockStyle.Top; top.Height = 64; Controls.Add(top);
            lbAddr = new Label(); lbAddr.SetBounds(8, 38, 740, 20); lbAddr.ForeColor = T.Dim; lbAddr.Text = "Adresse CI-V : en attente de trames…"; top.Controls.Add(lbAddr);
            Button det = new Button(); det.Text = "Détecter le poste…"; det.SetBounds(492, 5, 150, 24); det.Click += delegate { Detect(); }; top.Controls.Add(det);
            det.FlatStyle = FlatStyle.Flat; det.BackColor = T.Panel2; det.ForeColor = T.Text; det.FlatAppearance.BorderColor = T.Edge;
            cbPause = new CheckBox(); cbPause.Text = "Pause"; cbPause.SetBounds(8, 8, 70, 20); top.Controls.Add(cbPause);
            cbRx = new CheckBox(); cbRx.Text = "Voir les réponses du poste"; cbRx.Checked = true; cbRx.SetBounds(84, 8, 200, 20); top.Controls.Add(cbRx);
            Button clr = new Button(); clr.Text = "Effacer"; clr.SetBounds(300, 5, 80, 24); clr.Click += delegate { lock (sync) { pend.Clear(); all.Clear(); } lb.Items.Clear(); }; top.Controls.Add(clr);
            Button sv = new Button(); sv.Text = "Enregistrer…"; sv.SetBounds(386, 5, 100, 24); sv.Click += delegate { Save(); }; top.Controls.Add(sv);
            foreach (Button b in new Button[] { clr, sv }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; }
            lb.Dock = DockStyle.Fill; lb.BackColor = Color.FromArgb(8, 10, 14); lb.ForeColor = T.Text; lb.Font = new Font("Consolas", 9f); lb.BorderStyle = BorderStyle.None; lb.DrawMode = DrawMode.OwnerDrawFixed; lb.ItemHeight = 16;
            lb.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                if (e.Index < 0) return; string line = (string)lb.Items[e.Index];
                e.Graphics.FillRectangle(Brushes.Black, e.Bounds);
                using (Brush b = new SolidBrush(line.StartsWith("TX") ? T.Amber : T.Cyan)) e.Graphics.DrawString(line, lb.Font, b, e.Bounds.X, e.Bounds.Y);
            };
            Controls.Add(lb); Controls.SetChildIndex(lb, 0);
            t.Interval = 100; t.Tick += delegate { Flush(); }; t.Start();
            FormClosed += delegate { t.Stop(); };
        }
        public void Add(string hex, bool tx)
        {
            string line = (tx ? "TX  " : "RX  ") + DateTime.Now.ToString("HH:mm:ss.fff") + "  " + hex;
            lock (sync)
            {
                pend.Add(line); if (pend.Count > 4000) pend.RemoveRange(0, 2000);
                try { string[] p = hex.Split(' '); if (p.Length >= 5 && p[0] == "FE" && p[1] == "FE") { byte dst = Convert.ToByte(p[2], 16), src = Convert.ToByte(p[3], 16); if (tx) txDst = dst; } } catch { }
            }
        }
        public void Heard(byte src) { if (src == 0xE0 || src == 0x00) return; lock (sync) { int n; rxSrc.TryGetValue(src, out n); rxSrc[src] = n + 1; } }
        void ShowAddr()
        {
            byte best = 0; int bn = 0; lock (sync) { foreach (KeyValuePair<byte, int> kv in rxSrc) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; } }
            if (scanning) return;
            if (bn == 0) { lbAddr.ForeColor = txDst == 0xFF ? T.Dim : T.Amber; lbAddr.Text = txDst == 0xFF ? "Adresse CI-V : en attente de trames…" : "Aucune réponse du poste (le logiciel écrit à l'adresse " + txDst.ToString("X2") + "h). Essayez « Détecter le poste… »."; return; }
            if (txDst != 0xFF && txDst != best) { lbAddr.ForeColor = T.Amber; lbAddr.Text = "Le poste répond avec l'adresse CI-V " + best.ToString("X2") + "h (" + bn + " trames) mais le logiciel écrit à " + txDst.ToString("X2") + "h."; }
            else { lbAddr.ForeColor = T.Green; lbAddr.Text = "Adresse CI-V du poste détectée : " + best.ToString("X2") + "h (" + bn + " trames reçues)."; }
        }
        // Recherche du poste sur le port COM choisi : écoute et lecture de l'identifiant (commande 19 00, en lecture seule) à plusieurs vitesses
        void Detect()
        {
            if (scanning) return; string port = GetPort != null ? GetPort() : null;
            if (string.IsNullOrEmpty(port) || !port.StartsWith("COM")) { MessageBox.Show(this, "Choisissez d'abord un port COM (pas le poste virtuel) dans la liste de la fenêtre principale.", "Détecter le poste"); return; }
            if (RigBusy != null && RigBusy()) { MessageBox.Show(this, "Le logiciel est connecté au poste : cliquez d'abord sur DÉCONNECTER pour lancer la détection.", "Détecter le poste"); return; }
            scanning = true; lbAddr.ForeColor = T.Cyan; lbAddr.Text = "Détection en cours sur " + port + "…";
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int[] bauds = { 115200, 57600, 38400, 19200, 9600, 4800, 230400 }; string res = null; int fb = 0; byte fa = 0; string lastErr = null;
                foreach (int baud in bauds)
                {
                    SerialLink l = new SerialLink(port, baud); byte got = 0; bool ok = false; string err;
                    Action<byte[]> h = delegate(byte[] f) { if (f.Length >= 6 && f[3] != 0xE0 && f[3] != 0x00) { got = f[3]; ok = true; } };
                    l.Frame += h;
                    BeginInvoke((Action)delegate { lbAddr.Text = "Détection sur " + port + " à " + baud + " bauds…"; });
                    if (!l.Open(out err)) { lastErr = err; l.Close(); continue; }
                    try { for (int i = 0; i < 3 && !ok; i++) { l.Send(Civ.Build(0x00, 0xE0, new byte[] { 0x19, 0x00 })); System.Threading.Thread.Sleep(450); } } catch { }
                    l.Close(); l.Frame -= h;
                    if (ok) { fb = baud; fa = got; break; }
                }
                BeginInvoke((Action)delegate
                {
                    scanning = false;
                    if (fb > 0)
                    {
                        lbAddr.ForeColor = T.Green; lbAddr.Text = "Poste trouvé : adresse CI-V " + fa.ToString("X2") + "h à " + fb + " bauds sur " + port + ".";
                        if (Found != null && MessageBox.Show(this, "Poste trouvé : adresse CI-V " + fa.ToString("X2") + "h, " + fb + " bauds.\nReporter ces valeurs dans la fenêtre principale ?", "Détecter le poste", MessageBoxButtons.YesNo) == DialogResult.Yes) Found(fb, fa);
                    }
                    else
                    {
                        lbAddr.ForeColor = T.Amber; lbAddr.Text = lastErr != null ? "Impossible d'ouvrir " + port + " : " + lastErr : "Aucune réponse sur " + port + " (7 vitesses essayées). Vérifiez le câble, le port COM, que le poste est allumé et le réglage CI-V USB.";
                    }
                });
            });
        }
        void Flush()
        {
            ShowAddr();
            if (cbPause.Checked) return;
            List<string> l; lock (sync) { if (pend.Count == 0) return; l = new List<string>(pend); pend.Clear(); }
            lb.BeginUpdate();
            foreach (string s in l) { all.Add(s); if (!cbRx.Checked && s.StartsWith("RX")) continue; lb.Items.Add(s); }
            while (lb.Items.Count > 1500) lb.Items.RemoveAt(0);
            if (all.Count > 20000) all.RemoveRange(0, 10000);
            lb.TopIndex = Math.Max(0, lb.Items.Count - lb.ClientSize.Height / lb.ItemHeight);
            lb.EndUpdate();
        }
        void Save()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Texte|*.txt"; d.FileName = "civ-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                if (d.ShowDialog() == DialogResult.OK) System.IO.File.WriteAllLines(d.FileName, all.ToArray(), Encoding.UTF8);
            }
        }
    }

    // Accès à tous les réglages du catalogue CI-V (menu SET du poste inclus)
    public class SetForm : Form
    {
        readonly Rig rig; readonly ListView lv = new ListView(); readonly TextBox search = new TextBox(); readonly Label info = new Label();
        readonly NumericUpDown nud = new NumericUpDown(); readonly TextBox hex = new TextBox(); readonly Label cur = new Label(); readonly ComboBox rxSel = new ComboBox();
        readonly System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); CatEntry sel; List<CatEntry> shown = new List<CatEntry>();

        public SetForm(Rig r)
        {
            rig = r;
            Text = "Réglages avancés du poste (catalogue CI-V complet)"; BackColor = T.Bg; ForeColor = T.Text; ClientSize = new Size(1040, 560); StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9f);
            Label ls = new Label(); ls.Text = "Rechercher :"; ls.SetBounds(10, 12, 80, 18); Controls.Add(ls);
            search.SetBounds(92, 9, 300, 24); search.BackColor = T.Panel2; search.ForeColor = T.Text; search.BorderStyle = BorderStyle.FixedSingle; Controls.Add(search); search.TextChanged += delegate { Fill(); };
            lv.SetBounds(10, 40, 640, 510); lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.BackColor = Color.FromArgb(10, 13, 18); lv.ForeColor = T.Text; lv.BorderStyle = BorderStyle.FixedSingle; lv.MultiSelect = false;
            lv.Columns.Add("Cmd", 90); lv.Columns.Add("Fonction", 540); lv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left; Controls.Add(lv);
            lv.SelectedIndexChanged += delegate { Pick(); };
            int x = 666;
            info.SetBounds(x, 40, 360, 150); info.ForeColor = T.Text; Controls.Add(info);
            Label lr = new Label(); lr.Text = "Voie :"; lr.SetBounds(x, 200, 44, 18); Controls.Add(lr);
            rxSel.SetBounds(x + 46, 196, 90, 24); rxSel.DropDownStyle = ComboBoxStyle.DropDownList; rxSel.Items.Add("MAIN"); rxSel.Items.Add("SUB"); rxSel.SelectedIndex = 0; Controls.Add(rxSel);
            cur.SetBounds(x, 232, 360, 22); cur.ForeColor = T.Cyan; cur.Font = new Font("Consolas", 10f, FontStyle.Bold); Controls.Add(cur);
            Label lv2 = new Label(); lv2.Text = "Valeur :"; lv2.SetBounds(x, 266, 60, 18); Controls.Add(lv2);
            nud.SetBounds(x + 64, 262, 140, 24); nud.BackColor = T.Panel2; nud.ForeColor = T.Text; Controls.Add(nud);
            Label lh = new Label(); lh.Text = "Hex brut :"; lh.SetBounds(x, 300, 60, 18); Controls.Add(lh);
            hex.SetBounds(x + 64, 297, 230, 24); hex.BackColor = T.Panel2; hex.ForeColor = T.Text; hex.BorderStyle = BorderStyle.FixedSingle; Controls.Add(hex);
            Button rd = B("Lire", x, 340, 100); rd.Click += delegate { if (sel != null) rig.Read(rxSel.SelectedIndex, sel.Key); };
            Button wr = B("Écrire", x + 110, 340, 100); wr.Click += delegate { Write(); };
            Label note = new Label(); note.SetBounds(x, 384, 360, 110); note.ForeColor = T.Dim; note.Text = "Les écritures modifient le poste réel (menu SET). Pour les entrées sans plage lisible (« voir p.xx »), saisissez les octets de données en hexadécimal (ex. 01 28). Le Moniteur CI-V montre les trames échangées."; Controls.Add(note);
            Fill(); t.Interval = 250; t.Tick += delegate { ShowCur(); }; t.Start(); FormClosed += delegate { t.Stop(); };
        }
        Button B(string text, int x, int y, int w) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 28); b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; Controls.Add(b); return b; }

        void Fill()
        {
            string q = search.Text.Trim().ToLowerInvariant(); shown.Clear(); lv.BeginUpdate(); lv.Items.Clear();
            foreach (CatEntry e in Catalog.All)
            {
                if (!(e.Notes.IndexOf("send/read", StringComparison.OrdinalIgnoreCase) >= 0 || e.Name.IndexOf("Send/read", StringComparison.OrdinalIgnoreCase) >= 0 || e.Name.StartsWith("Read"))) continue;
                string text = (e.Key + " " + e.Name).ToLowerInvariant(); if (q.Length > 0 && text.IndexOf(q) < 0) continue;
                ListViewItem it = new ListViewItem(e.Key); it.SubItems.Add(e.Name); lv.Items.Add(it); shown.Add(e);
            }
            lv.EndUpdate();
        }
        void Pick()
        {
            if (lv.SelectedIndices.Count == 0) return; sel = shown[lv.SelectedIndices[0]];
            info.Text = sel.Key + "\n" + sel.Name + "\n\nFormat : " + sel.Data + (sel.Notes.Length > 0 ? "\n" + sel.Notes : "");
            if (sel.HasRange) { nud.Minimum = sel.Min; nud.Maximum = sel.Max; nud.Value = sel.Min; nud.Enabled = true; } else nud.Enabled = false;
            rig.Read(rxSel.SelectedIndex, sel.Key); ShowCur();
        }
        void ShowCur()
        {
            if (sel == null) return;
            if (sel.HasRange) { int v = rig.Get(rxSel.SelectedIndex, sel.Key); cur.Text = "Valeur actuelle : " + v; if (!nud.Focused && v >= sel.Min && v <= sel.Max) nud.Value = v; }
            else cur.Text = "(format non décodé)";
        }
        void Write()
        {
            if (sel == null) return;
            if (sel.HasRange && string.IsNullOrWhiteSpace(hex.Text)) { rig.Write(rxSel.SelectedIndex, sel.Key, (int)nud.Value); return; }
            List<byte> d = new List<byte>(); foreach (string s in hex.Text.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)) { try { d.Add(Convert.ToByte(s, 16)); } catch { MessageBox.Show(this, "Octet invalide : " + s); return; } }
            byte[] body = Civ.Cat((byte)sel.Cmd, sel.Sub, d.ToArray());
            if (sel.Direct29) body = Civ.Cat((byte)0x29, (byte)rxSel.SelectedIndex, body);
            rig.Raw(body);
        }
    }
}
