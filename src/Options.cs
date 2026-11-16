using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace IC7760Link
{
    // Choix d'une couleur : palette de 64 couleurs + réglage RVB
    public class ColorPickForm : Form
    {
        public Color Result; readonly Panel preview = new Panel(); readonly NumericUpDown nr = new NumericUpDown(), ng = new NumericUpDown(), nb = new NumericUpDown();
        readonly TrackBar tr = new TrackBar(), tg = new TrackBar(), tb = new TrackBar(); bool busy; readonly TextBox hex = new TextBox();
        public static Color[] Grid64()
        {
            Color[] c = new Color[64];
            for (int i = 0; i < 8; i++) { int v = i * 255 / 7; c[i] = Color.FromArgb(v, v, v); }
            for (int row = 1; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    double h = col * 45.0, l = 0.12 + (row - 1) * 0.13; c[row * 8 + col] = FromHsl(h, 1.0, l);
                }
            return c;
        }
        static Color FromHsl(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = l - c / 2; double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; } else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }
        public ColorPickForm(Color start, string title)
        {
            Text = title; BackColor = T.Bg; ForeColor = T.Text; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(560, 300); StartPosition = FormStartPosition.CenterParent; Result = start;
            Label l1 = new Label(); l1.Text = "Palette de 64 couleurs"; l1.SetBounds(10, 8, 240, 18); Controls.Add(l1);
            Color[] g64 = Grid64();
            for (int i = 0; i < 64; i++)
            {
                Panel p = new Panel(); p.BackColor = g64[i]; p.SetBounds(10 + (i % 8) * 30, 30 + (i / 8) * 30, 28, 28); p.Cursor = Cursors.Hand; Color c = g64[i];
                p.Click += delegate { SetColor(c); }; Controls.Add(p);
            }
            Label l2 = new Label(); l2.Text = "Réglage RVB"; l2.SetBounds(270, 8, 200, 18); Controls.Add(l2);
            string[] nm = { "R", "V", "B" }; TrackBar[] ts = { tr, tg, tb }; NumericUpDown[] ns = { nr, ng, nb };
            for (int i = 0; i < 3; i++)
            {
                Label l = new Label(); l.Text = nm[i]; l.SetBounds(270, 38 + i * 44, 16, 22); Controls.Add(l);
                TrackBar t = ts[i]; t.Minimum = 0; t.Maximum = 255; t.TickStyle = TickStyle.None; t.SetBounds(288, 32 + i * 44, 190, 30); Controls.Add(t);
                NumericUpDown n = ns[i]; n.Minimum = 0; n.Maximum = 255; n.SetBounds(484, 36 + i * 44, 56, 24); n.BackColor = T.Panel2; n.ForeColor = T.Text; Controls.Add(n);
                int k = i;
                t.ValueChanged += delegate { if (!busy) FromBars(); }; n.ValueChanged += delegate { if (!busy) { busy = true; ts[k].Value = (int)ns[k].Value; busy = false; FromBars(); } };
            }
            Label lh = new Label(); lh.Text = "Hexa #"; lh.SetBounds(270, 174, 50, 22); Controls.Add(lh);
            hex.SetBounds(322, 171, 80, 24); hex.BackColor = T.Panel2; hex.ForeColor = T.Text; hex.BorderStyle = BorderStyle.FixedSingle; hex.MaxLength = 6;
            hex.TextChanged += delegate { if (busy || hex.Text.Length != 6) return; try { int v = Convert.ToInt32(hex.Text, 16); SetColor(Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255)); } catch { } }; Controls.Add(hex);
            Label lp = new Label(); lp.Text = "Aperçu"; lp.SetBounds(270, 206, 60, 18); Controls.Add(lp);
            preview.SetBounds(330, 202, 210, 34); preview.BorderStyle = BorderStyle.FixedSingle; Controls.Add(preview);
            Button ok = new Button(); ok.Text = "OK"; ok.SetBounds(360, 256, 85, 30); ok.DialogResult = DialogResult.OK; Controls.Add(ok);
            Button ca = new Button(); ca.Text = "Annuler"; ca.SetBounds(455, 256, 85, 30); ca.DialogResult = DialogResult.Cancel; Controls.Add(ca);
            foreach (Button b in new Button[] { ok, ca }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; }
            AcceptButton = ok; CancelButton = ca;
            SetColor(start);
        }
        void FromBars() { SetColor(Color.FromArgb(tr.Value, tg.Value, tb.Value)); }
        void SetColor(Color c)
        {
            if (busy) return; busy = true; Result = Color.FromArgb(255, c);
            tr.Value = c.R; tg.Value = c.G; tb.Value = c.B; nr.Value = c.R; ng.Value = c.G; nb.Value = c.B; hex.Text = c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); preview.BackColor = Result; busy = false;
        }
    }

    // Couleurs de l'interface (reglables dans Options)
    public static class Theme
    {
        public static readonly string[] Names = { "Arrière-plan général", "Panneaux", "Boutons et champs", "Bordures", "Texte (avant-plan)", "Texte atténué", "Accent (titres, MAIN, voyants)", "Cyan (mode, infos)", "Rouge (émission, alertes)", "Vert (connexion, OK)", "Bleu (SUB)", "Chiffres fréquence MAIN", "Chiffres fréquence SUB", "Fond des afficheurs de fréquence" };
        static readonly Color[] def;
        static Theme() { def = Snapshot(); }
        public static Color Get(int i)
        {
            switch (i) { case 0: return T.Bg; case 1: return T.Panel; case 2: return T.Panel2; case 3: return T.Edge; case 4: return T.Text; case 5: return T.Dim; case 6: return T.Amber; case 7: return T.Cyan; case 8: return T.Red; case 9: return T.Green; case 10: return T.Blue; case 11: return T.FreqMain; case 12: return T.FreqSub; default: return T.FreqBg; }
        }
        public static void Set(int i, Color c)
        {
            c = Color.FromArgb(255, c);
            switch (i) { case 0: T.Bg = c; break; case 1: T.Panel = c; break; case 2: T.Panel2 = c; break; case 3: T.Edge = c; break; case 4: T.Text = c; break; case 5: T.Dim = c; break; case 6: T.Amber = c; break; case 7: T.Cyan = c; break; case 8: T.Red = c; break; case 9: T.Green = c; break; case 10: T.Blue = c; break; case 11: T.FreqMain = c; break; case 12: T.FreqSub = c; break; default: T.FreqBg = c; break; }
        }
        public static Color Default(int i) { return def[i]; }
        public static Color[] Snapshot() { Color[] a = new Color[Names.Length]; for (int i = 0; i < a.Length; i++) a[i] = Get(i); return a; }
        public static void Load() { for (int i = 0; i < Names.Length; i++) { int v = Cfg.GetInt("theme." + i, 0); if (v != 0) Set(i, Color.FromArgb(v)); } }
        public static void Save() { for (int i = 0; i < Names.Length; i++) Cfg.SetInt("theme." + i, Get(i).ToArgb()); }
        // correspondance ancienne couleur -> nouvelle couleur entre deux etats
        public static Dictionary<int, Color> Map(Color[] before, Color[] after)
        {
            Dictionary<int, Color> m = new Dictionary<int, Color>(); for (int i = 0; i < before.Length; i++) if (before[i].ToArgb() != after[i].ToArgb() && !m.ContainsKey(before[i].ToArgb())) m[before[i].ToArgb()] = after[i]; return m;
        }
        static Color M(Dictionary<int, Color> m, Color c) { Color r; return m.TryGetValue(c.ToArgb(), out r) ? r : c; }
        public static void Recolor(Control c, Dictionary<int, Color> m)
        {
            if (c.BackColor.A == 255) { Color nb = M(m, c.BackColor); if (nb.ToArgb() != c.BackColor.ToArgb()) c.BackColor = nb; }
            { Color nf = M(m, c.ForeColor); if (nf.ToArgb() != c.ForeColor.ToArgb()) c.ForeColor = nf; }
            Btn b = c as Btn; if (b != null) b.Accent = M(m, b.Accent);
            Knob k = c as Knob; if (k != null) k.Accent = M(m, k.Accent);
            MeterBar mb = c as MeterBar; if (mb != null) { mb.Cool = M(m, mb.Cool); mb.Warm = M(m, mb.Warm); mb.Over = M(m, mb.Over); }
            FreqDisplay fd = c as FreqDisplay; if (fd != null) fd.Accent = M(m, fd.Accent);
            foreach (Control ch in c.Controls) Recolor(ch, m);
            c.Invalidate();
        }
    }

    // Fenêtre d'options (menu à gauche, page à droite)
    public class OptionsForm : Form
    {
        public event Action Changed; public event Action<Dictionary<int, Color>> ThemeChanged;
        readonly Panel page = new Panel(); readonly List<Panel> swatches = new List<Panel>(); Panel gradient;
        readonly Action openSet, openMon;
        public OptionsForm(Action openSet, Action openMon)
        {
            this.openSet = openSet; this.openMon = openMon;
            Text = "Options"; BackColor = T.Bg; ForeColor = T.Text; Font = new Font("Segoe UI", 9f); ClientSize = new Size(860, 470); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ListBox menu = new ListBox(); menu.SetBounds(0, 0, 190, 470); menu.BackColor = Color.FromArgb(14, 18, 26); menu.ForeColor = T.Text; menu.BorderStyle = BorderStyle.None; menu.Font = new Font("Segoe UI Semibold", 10f); menu.ItemHeight = 28; menu.IntegralHeight = false;
            menu.Items.Add("  Couleurs du waterfall"); menu.Items.Add("  Couleurs de l'interface"); menu.Items.Add("  Réglages avancés du poste"); menu.Items.Add("  Monitoring et détection CI-V"); menu.SelectedIndex = 0; Controls.Add(menu);
            menu.SelectedIndexChanged += delegate
            {
                switch (menu.SelectedIndex)
                {
                    case 1: BuildThemePage(); break;
                    case 2: BuildActionPage("Réglages avancés du poste", "Liste complète des réglages internes de l'IC-7760 (menus SET, affichage, CW, modulation, etc.) : recherche par mot-clé, lecture de la valeur actuelle dans le poste et modification.\nÀ utiliser avec précaution : chaque changement est envoyé directement au poste.", "Ouvrir les réglages avancés", openSet); break;
                    case 3: BuildActionPage("Monitoring et détection CI-V", "Journal en direct des trames CI-V échangées avec le poste, affichage de l'adresse CI-V détectée sur la liaison et recherche automatique de l'adresse et de la vitesse du poste.\nUtile quand le poste ne se connecte pas sans qu'on sache pourquoi.", "Ouvrir le monitoring CI-V", openMon); break;
                    default: BuildColorsPage(); break;
                }
            };
            page.SetBounds(200, 0, 660, 470); Controls.Add(page);
            BuildColorsPage();
        }
        void Raise() { Palettes.Changed(); ScopeColors.Save(); gradient.Invalidate(); if (Changed != null) Changed(); }
        Color Ask(Color c, string title) { using (ColorPickForm f = new ColorPickForm(c, title)) return f.ShowDialog(this) == DialogResult.OK ? f.Result : c; }
        void BuildColorsPage()
        {
            page.Controls.Clear(); swatches.Clear();
            Label h = new Label(); h.Text = "Couleurs du waterfall"; h.Font = new Font("Segoe UI Semibold", 12f); h.ForeColor = T.Amber; h.SetBounds(6, 8, 400, 26); page.Controls.Add(h);
            Label h1 = new Label(); h1.Text = "Dégradé de la cascade : 10 couleurs, du niveau le plus faible (fond) au signal le plus fort. Cliquez sur une case pour la modifier."; h1.SetBounds(6, 38, 640, 36); page.Controls.Add(h1);
            gradient = new Panel(); gradient.SetBounds(6, 82, 640, 46); gradient.Paint += delegate(object s, PaintEventArgs e)
            {
                int[] lut = Palettes.Build(Palettes.Names.Length - 1, 1.0);
                for (int x = 0; x < gradient.Width; x++) using (Pen p = new Pen(Color.FromArgb(lut[x * 255 / (gradient.Width - 1)]))) e.Graphics.DrawLine(p, x, 0, x, gradient.Height);
            }; page.Controls.Add(gradient);
            for (int i = 0; i < Palettes.NCustom; i++)
            {
                int k = i; Panel p = new Panel(); p.SetBounds(6 + i * 64, 136, 58, 40); p.BackColor = Palettes.Custom[i]; p.BorderStyle = BorderStyle.FixedSingle; p.Cursor = Cursors.Hand; swatches.Add(p);
                p.Click += delegate { Palettes.Custom[k] = Ask(Palettes.Custom[k], "Couleur " + (k + 1) + " / " + Palettes.NCustom); p.BackColor = Palettes.Custom[k]; Raise(); };
                page.Controls.Add(p);
                Label l = new Label(); l.Text = k == 0 ? "fond" : (k == Palettes.NCustom - 1 ? "fort" : (k * 100 / (Palettes.NCustom - 1)) + "%"); l.ForeColor = T.Dim; l.TextAlign = ContentAlignment.TopCenter; l.SetBounds(6 + i * 64, 178, 58, 18); page.Controls.Add(l);
            }
            Label lp = new Label(); lp.Text = "Partir d'une palette existante :"; lp.SetBounds(6, 210, 200, 20); page.Controls.Add(lp);
            ComboBox cb = new ComboBox(); cb.DropDownStyle = ComboBoxStyle.DropDownList; cb.SetBounds(210, 206, 150, 24); for (int i = 0; i < Palettes.Names.Length - 1; i++) cb.Items.Add(Palettes.Names[i]); cb.SelectedIndex = 0; page.Controls.Add(cb);
            Button bl = Mk("Charger", 370, 204, 90); bl.Click += delegate { Palettes.CopyFrom(cb.SelectedIndex); RefreshSwatches(); Raise(); };
            Label h2 = new Label(); h2.Text = "Autres couleurs de la cascade et du spectre"; h2.Font = new Font("Segoe UI Semibold", 10f); h2.ForeColor = T.Amber; h2.SetBounds(6, 254, 500, 22); page.Controls.Add(h2);
            string[] names = { "Curseur MAIN", "Curseur SUB", "Courbe du spectre", "Crêtes (maintien)", "Bande passante" };
            for (int i = 0; i < names.Length; i++)
            {
                int k = i; Label l = new Label(); l.Text = names[i]; l.SetBounds(6 + (i % 3) * 215, 286 + (i / 3) * 56, 150, 18); page.Controls.Add(l);
                Panel p = new Panel(); p.SetBounds(6 + (i % 3) * 215, 306 + (i / 3) * 56, 100, 28); p.BorderStyle = BorderStyle.FixedSingle; p.Cursor = Cursors.Hand; p.BackColor = GetCol(k); page.Controls.Add(p);
                p.Click += delegate { SetCol(k, Ask(GetCol(k), names[k])); p.BackColor = GetCol(k); Raise(); };
            }
            Button rs = Mk("Rétablir les couleurs par défaut", 6, 420, 230); rs.Click += delegate
            {
                Palettes.CopyFrom(0); ScopeColors.CursorMain = Color.FromArgb(255, 70, 60); ScopeColors.CursorSub = Color.FromArgb(90, 170, 255); ScopeColors.Line = Color.FromArgb(240, 245, 255); ScopeColors.Peak = Color.FromArgb(255, 200, 60); ScopeColors.Pass = Color.FromArgb(255, 210, 60);
                BuildColorsPage(); Raise();
            };
            Button cl = Mk("Fermer", 556, 420, 90); cl.Click += delegate { Close(); };
        }
        void BuildActionPage(string title, string text, string button, Action open)
        {
            page.Controls.Clear();
            Label h = new Label(); h.Text = title; h.Font = new Font("Segoe UI Semibold", 12f); h.ForeColor = T.Amber; h.SetBounds(6, 8, 640, 26); page.Controls.Add(h);
            Label d = new Label(); d.Text = text; d.SetBounds(6, 46, 640, 90); page.Controls.Add(d);
            Button b = Mk(button, 6, 150, 280); b.Height = 38; b.Click += delegate { if (open != null) open(); };
        }
        void BuildThemePage()
        {
            page.Controls.Clear();
            Label h = new Label(); h.Text = "Couleurs de l'interface"; h.Font = new Font("Segoe UI Semibold", 12f); h.ForeColor = T.Amber; h.SetBounds(6, 8, 400, 26); page.Controls.Add(h);
            Label h1 = new Label(); h1.Text = "Cliquez sur une couleur pour la modifier (palette 64 couleurs ou RVB). Le changement est immédiat et mémorisé."; h1.SetBounds(6, 38, 640, 20); page.Controls.Add(h1);
            for (int i = 0; i < Theme.Names.Length; i++)
            {
                int k = i; int col = i / 7, row = i % 7; int x = 6 + col * 320, y = 74 + row * 44;
                Panel p = new Panel(); p.SetBounds(x, y, 70, 30); p.BorderStyle = BorderStyle.FixedSingle; p.Cursor = Cursors.Hand; p.BackColor = Theme.Get(k); page.Controls.Add(p);
                Label l = new Label(); l.Text = Theme.Names[k]; l.SetBounds(x + 78, y + 5, 235, 20); page.Controls.Add(l);
                p.Click += delegate
                {
                    Color[] before = Theme.Snapshot(); Color c = Ask(Theme.Get(k), Theme.Names[k]);
                    if (c.ToArgb() == before[k].ToArgb()) return;
                    Theme.Set(k, c); Theme.Save(); p.BackColor = c; if (ThemeChanged != null) ThemeChanged(Theme.Map(before, Theme.Snapshot()));
                };
            }
            Button rs = Mk("Rétablir les couleurs par défaut", 6, 420, 230); rs.Click += delegate
            {
                Color[] before = Theme.Snapshot(); for (int i = 0; i < Theme.Names.Length; i++) Theme.Set(i, Theme.Default(i)); Theme.Save();
                if (ThemeChanged != null) ThemeChanged(Theme.Map(before, Theme.Snapshot())); BuildThemePage();
            };
            Button cl = Mk("Fermer", 556, 420, 90); cl.Click += delegate { Close(); };
        }
        static Color GetCol(int k) { switch (k) { case 0: return ScopeColors.CursorMain; case 1: return ScopeColors.CursorSub; case 2: return ScopeColors.Line; case 3: return ScopeColors.Peak; default: return ScopeColors.Pass; } }
        static void SetCol(int k, Color c) { switch (k) { case 0: ScopeColors.CursorMain = c; break; case 1: ScopeColors.CursorSub = c; break; case 2: ScopeColors.Line = c; break; case 3: ScopeColors.Peak = c; break; default: ScopeColors.Pass = c; break; } }
        void RefreshSwatches() { for (int i = 0; i < swatches.Count; i++) swatches[i].BackColor = Palettes.Custom[i]; }
        Button Mk(string t, int x, int y, int w) { Button b = new Button(); b.Text = t; b.SetBounds(x, y, w, 30); b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; page.Controls.Add(b); return b; }
    }
}
