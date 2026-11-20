using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace IC7760Link
{
    // Fenêtre « Outils » (bouton ⚑) : micro du PC / TONE, alerte ROS, étalonnage des vumètres, mise à jour
    public class ToolsForm : Form
    {
        readonly Rig rig; readonly Func<string, int> rawOf; readonly MainForm main;
        readonly TabControl tabs = new TabControl();
        readonly Dictionary<string, Control> ctl = new Dictionary<string, Control>();
        ComboBox cbMic, cbCard;
        TextBox tbPo, tbSwr;
        List<AudioEndpoints.Endpoint> eps = new List<AudioEndpoints.Endpoint>();

        public int TabCount { get { return tabs.TabPages.Count; } } public void SelectTab(int i) { tabs.SelectedIndex = i; }
        public ToolsForm(Rig rig, MainForm main, Func<string, int> rawOf)
        {
            this.rig = rig; this.main = main; this.rawOf = rawOf;
            Text = "Outils — micro, TONE, alerte ROS, étalonnage, mise à jour"; BackColor = T.Bg; ForeColor = T.Text; Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(640, 520);
            tabs.SetBounds(8, 8, 624, 470); Controls.Add(tabs);
            BuildMic(Page("Micro / TONE")); BuildRos(Page("Alerte ROS")); BuildCal(Page("Étalonnage")); BuildUpdate(Page("Mise à jour"));
            Button ok = new Button(); ok.Text = "Fermer"; ok.SetBounds(530, 484, 100, 28); Flat(ok); ok.Click += delegate { SaveAll(); Close(); }; Controls.Add(ok);
            FormClosing += delegate { SaveAll(); };
        }
        TabPage Page(string t) { TabPage p = new TabPage(t); p.BackColor = T.Panel; p.ForeColor = T.Text; tabs.TabPages.Add(p); return p; }
        static void Flat(Button b) { b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; }
        Label Lb(Control p, string t, int x, int y, int w, int h) { Label l = new Label(); l.Text = t; l.SetBounds(x, y, w, h); p.Controls.Add(l); return l; }
        Button Bt(Control p, string t, int x, int y, int w) { Button b = new Button(); b.Text = t; b.SetBounds(x, y, w, 28); Flat(b); p.Controls.Add(b); return b; }
        // champ numérique relié à un réglage : valeur affichée = réglage / div
        NumericUpDown Num(Control p, string label, string key, int x, int y, decimal min, decimal max, int def, int div, int dec, string unit)
        {
            Lb(p, label, x, y + 3, 250, 20); NumericUpDown n = new NumericUpDown(); n.SetBounds(x + 255, y, 80, 24); n.Minimum = min; n.Maximum = max; n.DecimalPlaces = dec; n.Increment = dec > 0 ? 0.1m : 1;
            n.Value = Math.Max(min, Math.Min(max, (decimal)Cfg.GetInt(key, def) / div)); n.BackColor = T.Panel2; n.ForeColor = T.Text; p.Controls.Add(n); Lb(p, unit, x + 340, y + 3, 240, 20);
            n.Tag = new object[] { key, div }; ctl[key] = n; return n;
        }
        CheckBox Chk(Control p, string label, string key, int x, int y, bool def)
        {
            CheckBox c = new CheckBox(); c.Text = label; c.SetBounds(x, y, 580, 22); c.Checked = Cfg.GetBool(key, def); c.Tag = new object[] { key, 0 }; p.Controls.Add(c); ctl[key] = c; return c;
        }
        void SaveAll()
        {
            foreach (KeyValuePair<string, Control> kv in ctl)
            {
                NumericUpDown n = kv.Value as NumericUpDown; CheckBox c = kv.Value as CheckBox;
                if (n != null) Cfg.SetInt(kv.Key, (int)Math.Round(n.Value * (int)((object[])n.Tag)[1])); else if (c != null) Cfg.SetBool(kv.Key, c.Checked);
            }
            if (cbMic != null && cbMic.SelectedIndex >= 0 && cbMic.SelectedIndex < micIds.Count) { Cfg.Set("micro.id", micIds[cbMic.SelectedIndex]); Cfg.Set("micro.nom", cbMic.Text); }
            if (cbCard != null && cbCard.SelectedIndex >= 0 && cbCard.SelectedIndex < cardIds.Count) { Cfg.Set("micro.carte", cardIds[cbCard.SelectedIndex]); Cfg.Set("micro.cartenom", cbCard.Text); }
            Cfg.Save(); MeterBar.HoldMs = Math.Max(300, Cfg.GetInt("meter.hold", 900)); MicRoute.Update();
        }
        readonly List<string> micIds = new List<string>(), cardIds = new List<string>();

        // ------------------------------------------------------------- micro / TONE
        void BuildMic(TabPage p)
        {
            Lb(p, "Micro du PC envoyé en continu vers la carte son USB du poste (rien n'est rejoué sur le PC). La carte choisie sert aussi à la tonalité du bouton TONE.", 10, 8, 590, 36);
            Lb(p, "Micro du PC", 10, 52, 120, 20); cbMic = new ComboBox(); cbMic.SetBounds(130, 48, 470, 24); cbMic.DropDownStyle = ComboBoxStyle.DropDownList; p.Controls.Add(cbMic);
            Lb(p, "Carte son du poste", 10, 84, 120, 20); cbCard = new ComboBox(); cbCard.SetBounds(130, 80, 470, 24); cbCard.DropDownStyle = ComboBoxStyle.DropDownList; p.Controls.Add(cbCard);
            try
            {
                foreach (AudioEndpoints.Endpoint e in AudioEndpoints.All())
                {
                    string nm = e.FullName ?? e.Name ?? e.Id;
                    if (e.Capture) { micIds.Add(e.Id); cbMic.Items.Add(nm); if (e.Id == MicRoute.MicId) cbMic.SelectedIndex = cbMic.Items.Count - 1; }
                    else { cardIds.Add(e.Id); cbCard.Items.Add(nm); if (e.Id == MicRoute.CardId) cbCard.SelectedIndex = cbCard.Items.Count - 1; }
                }
            }
            catch (Exception ex) { Lb(p, "Lecture des cartes son impossible : " + ex.Message, 10, 110, 590, 20); }
            Chk(p, "Activer « micro du PC vers le poste »", "micro.on", 130, 112, false);
            Lb(p, "Bouton TONE (à côté de PTT) : tonalité de 1500 Hz + PTT, pour régler le coupleur ou une charge", 10, 150, 590, 20).ForeColor = T.Amber;
            Num(p, "Niveau de la tonalité", "tune.niveau", 10, 178, 1, 100, 15, 1, 0, "%  (de la pleine échelle de la carte)");
            Num(p, "Durée maximale", "tune.duree", 10, 208, 5, 60, 10, 1, 0, "secondes");
            Chk(p, "Émettre avec le VOX du poste (sinon PTT par CI-V)", "tune.vox", 10, 240, true);
            Lb(p, "Réglez le poste : source du modulateur USB (MOD) en mode données, niveau « Data MOD » prudent ; essayez sur charge fictive.", 10, 272, 590, 36).ForeColor = T.Dim;
            Num(p, "Maintien du max des vumètres", "meter.hold", 10, 318, 300, 10000, 900, 1, 0, "ms");
        }

        // ------------------------------------------------------------- alerte ROS
        void BuildRos(TabPage p)
        {
            Chk(p, "Alerte ROS active : bips continus si le ROS dépasse le seuil en émission", "ros.on", 10, 10, true);
            Num(p, "Seuil du ROS", "ros.seuil", 10, 42, 1.1m, 20, 200, 100, 1, "(2.0 = ROS 2:1)");
            Num(p, "Délai avant l'alerte", "ros.delai", 10, 72, 0, 10000, 300, 1, 0, "ms");
            Num(p, "Fréquence des bips", "ros.freq", 10, 102, 200, 8000, 2000, 1, 0, "Hz");
            Num(p, "Volume des bips", "ros.vol", 10, 132, 1, 100, 100, 1, 0, "%");
            Num(p, "Pause entre les bips", "ros.pause", 10, 162, 50, 2000, 50, 1, 0, "ms");
            Chk(p, "Faire chuter la puissance pendant l'alerte", "ros.chute", 10, 196, true);
            NumericUpDown pct = Num(p, "Puissance de sécurité", "ros.pct", 10, 224, 0, 100, 0, 1, 0, "% de la puissance réglée");
            pct.ValueChanged += delegate { Cfg.SetInt("ros.pct", (int)pct.Value); RosGuard.ReapplyDrop(); };
            Num(p, "Alerte éteinte après ROS bas pendant", "ros.eteint", 10, 254, 500, 30000, 1500, 1, 0, "ms");
            Chk(p, "Laisser la puissance réduite à la fin de l'alerte", "ros.garder", 10, 286, false);
            Button b1 = Bt(p, "Essayer les bips", 10, 322, 150); b1.Click += delegate { SaveAll(); RosGuard.TestBeep(); };
            Button b2 = Bt(p, "Essayer l'alerte", 170, 322, 150); b2.Click += delegate { SaveAll(); RosGuard.Test(rig); };
            Button b3 = Bt(p, "Test TONE puissance réduite", 330, 322, 200); b3.Click += delegate { SaveAll(); TuneMic.StartSafe(rig, Cfg.GetBool("tune.vox", true)); };
            Lb(p, "Le test TONE émet 1500 Hz avec la puissance de sécurité (charge fictive conseillée) ; la puissance d'origine est remise à la fin.", 10, 360, 590, 36).ForeColor = T.Dim;
        }

        // ------------------------------------------------------------- étalonnage
        void BuildCal(TabPage p)
        {
            Lb(p, "Une ligne par point : « lecture du poste (0 à 255) = valeur réelle ». Les valeurs intermédiaires sont interpolées ; les graduations des vumètres suivent. Pour relever un point, émettez (charge fictive) et cliquez « Ajouter le point lu ».", 10, 8, 590, 52);
            Lb(p, "Puissance Po (W)", 10, 66, 280, 18).ForeColor = T.Amber; Lb(p, "ROS", 320, 66, 280, 18).ForeColor = T.Amber;
            tbPo = Box(p, 10, 88, Calib.Text("po")); tbSwr = Box(p, 320, 88, Calib.Text("swr"));
            Button a1 = Bt(p, "Ajouter le point lu", 10, 262, 140); a1.Click += delegate { AddPoint("po", tbPo, "Puissance réelle mesurée (W) pour la lecture actuelle :"); };
            Button r1 = Bt(p, "Valeurs d'origine", 156, 262, 134); r1.Click += delegate { Calib.Reset("po"); tbPo.Text = Calib.Text("po").Replace("\n", "\r\n"); };
            Button a2 = Bt(p, "Ajouter le point lu", 320, 262, 140); a2.Click += delegate { AddPoint("swr", tbSwr, "ROS réel pour la lecture actuelle :"); };
            Button r2 = Bt(p, "Valeurs d'origine", 466, 262, 134); r2.Click += delegate { Calib.Reset("swr"); tbSwr.Text = Calib.Text("swr").Replace("\n", "\r\n"); };
            Button ap = Bt(p, "Appliquer l'étalonnage", 10, 306, 200); ap.Click += delegate
            {
                bool o1 = Calib.SetText("po", tbPo.Text), o2 = Calib.SetText("swr", tbSwr.Text);
                if (!o1 || !o2) MessageBox.Show(this, "Il faut au moins 2 lignes valides (lecture = valeur) pour chaque courbe.", "Étalonnage");
            };
        }
        TextBox Box(Control p, int x, int y, string text)
        {
            TextBox t = new TextBox(); t.Multiline = true; t.AcceptsReturn = true; t.ScrollBars = ScrollBars.Vertical; t.SetBounds(x, y, 280, 166); t.BackColor = T.Panel2; t.ForeColor = T.Text; t.Font = new Font("Consolas", 10f); t.Text = text.Replace("\n", "\r\n"); p.Controls.Add(t); return t;
        }
        void AddPoint(string kind, TextBox tb, string ask)
        {
            int raw = rawOf(kind); if (raw <= 0) { MessageBox.Show(this, "Aucune lecture : émettez (charge fictive) puis cliquez pendant l'émission.", "Étalonnage"); return; }
            string v = Prompt.Ask(ask + "\n(lecture du poste : " + raw + ")", "Étalonnage", ""); if (v == null) return;
            double x; if (!double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)) return;
            tb.Text = tb.Text.TrimEnd() + "\r\n" + raw + " = " + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n";
        }

        // ------------------------------------------------------------- mise à jour
        void BuildUpdate(TabPage p)
        {
            Lb(p, "Version installée : " + MainForm.Version + ". Seul le programme est remplacé ; vos réglages (dossier data) restent en place, une copie de sécurité est faite avant.", 10, 10, 590, 40);
            Chk(p, "Vérifier au démarrage s'il existe une version plus récente", "maj.auto", 10, 56, false);
            Label res = Lb(p, "", 10, 130, 590, 60);
            Button b = Bt(p, "Rechercher maintenant", 10, 90, 200);
            b.Click += delegate
            {
                SaveAll(); b.Enabled = false; res.Text = "Recherche…"; Application.DoEvents();
                string err; Updater.Info i = Updater.Check(out err); b.Enabled = true;
                if (i == null) { res.Text = err; return; }
                if (!Updater.IsNewer(i)) { res.Text = "Vous avez la dernière version (" + MainForm.Version + ")."; return; }
                res.Text = "Version " + i.Version + " disponible."; Updater.Propose(main, i, false);
            };
        }
    }
}
