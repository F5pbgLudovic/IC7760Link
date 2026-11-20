using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace IC7760Link
{
    public static class Locks { public static Action<Control> Changed; }

    public static class T
    {
        // échelle de la fenêtre (1 = taille de conception) et résolution de l'écran
        public static float Scale = 1f, Dpi = 96f;
        static readonly Dictionary<string, Font> fc = new Dictionary<string, Font>();
        public static Font Fn(string name, float pt) { return Fn(name, pt, FontStyle.Regular); }
        public static Font Fn(string name, float pt, FontStyle st)
        {
            string k = name + "|" + pt + "|" + (int)st + "|" + Scale.ToString("0.000"); Font f;
            lock (fc)
            {
                if (!fc.TryGetValue(k, out f)) { if (fc.Count > 300) fc.Clear(); f = new Font(name, Math.Max(4f, pt * Scale * 96f / Dpi), st); fc[k] = f; }
            }
            return f;
        }
        public static Color Bg = Color.FromArgb(12, 15, 21), Panel = Color.FromArgb(22, 27, 36), Panel2 = Color.FromArgb(30, 37, 49), Edge = Color.FromArgb(56, 68, 86);
        public static Color Text = Color.FromArgb(222, 230, 238), Dim = Color.FromArgb(128, 145, 163), Amber = Color.FromArgb(255, 178, 20), Cyan = Color.FromArgb(40, 205, 235);
        public static Color Lock = Color.FromArgb(235, 30, 30), Red = Color.FromArgb(255, 66, 58), Green = Color.FromArgb(70, 224, 112), Blue = Color.FromArgb(70, 150, 255);
        public static Color FreqMain = Color.FromArgb(255, 226, 150), FreqSub = Color.FromArgb(160, 205, 255), FreqBg = Color.FromArgb(10, 13, 18);
        public static Color Mix(Color a, Color b, double t) { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath(); float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
    }

    // ------------------------------------------------------------------ bouton à voyant
    public class Btn : Control
    {
        public bool Checked; public bool Led = true; public Color Accent = T.Amber; public bool Flat;
        bool hot, down; Font f { get { return T.Fn("Segoe UI Semibold", 8.5f); } }
        public Btn() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Size = new Size(58, 28); Cursor = Cursors.Hand; }
        public void Set(bool on, string text) { if (on != Checked || (text != null && text != Text)) { Checked = on; if (text != null) Text = text; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        bool locked; public bool Slaved; public bool LockOnRight = true; public bool Locked { get { return locked; } set { if (locked != value) { locked = value; Invalidate(); if (Locks.Changed != null) Locks.Changed(this); } } } public bool IsLocked { get { return locked || Slaved; } }
        MouseButtons lastBtn; protected override void OnMouseDown(MouseEventArgs e) { lastBtn = e.Button; if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; if (e.Button == MouseButtons.Right && LockOnRight) Locked = !Locked; Invalidate(); base.OnMouseUp(e); }
        protected override void OnClick(EventArgs e) { if (lastBtn == MouseButtons.Right && LockOnRight) return; if (IsLocked) return; base.OnClick(e); }
        protected override void OnMouseClick(MouseEventArgs e) { if (e.Button == MouseButtons.Right && LockOnRight) return; if (IsLocked && e.Button == MouseButtons.Left) return; base.OnMouseClick(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : T.Panel);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            Color top = Checked ? Color.FromArgb(58, 66, 82) : Color.FromArgb(46, 55, 70), bot = Checked ? Color.FromArgb(34, 40, 52) : Color.FromArgb(28, 34, 45);
            if (hot) { top = ControlPaint.Light(top, 0.15f); bot = ControlPaint.Light(bot, 0.1f); }
            if (down) { Color t = top; top = bot; bot = t; }
            if (!Enabled) { top = Color.FromArgb(20, 24, 31); bot = top; }
            using (GraphicsPath p = T.Round(r, 5))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, top, bot, 90f)) g.FillPath(b, p);
                using (Pen pen = new Pen(IsLocked ? T.Lock : (Checked ? Accent : T.Edge), IsLocked ? 2f : (Checked ? 1.4f : 1f))) g.DrawPath(pen, p);
            }
            float tx = 0;
            if (Led)
            {
                float d = 6; float lx = 8, ly = Height / 2f - d / 2;
                using (Brush b = new SolidBrush(IsLocked ? T.Lock : (Checked ? Accent : Color.FromArgb(60, 68, 80)))) g.FillEllipse(b, lx, ly, d, d);
                if (Checked) using (Brush b = new SolidBrush(Color.FromArgb(60, Accent))) g.FillEllipse(b, lx - 3, ly - 3, d + 6, d + 6);
                tx = 8;
            }
            StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap;
            using (Brush b = new SolidBrush(!Enabled ? Color.FromArgb(62, 70, 82) : (IsLocked ? T.Lock : (Checked ? T.Text : Color.FromArgb(190, 202, 216)))))
                g.DrawString(Text, f, b, new RectangleF(tx, 0, Width - tx, Height), sf);
        }
    }

    // ------------------------------------------------------------------ potentiomètre
    public class Knob : Control
    {
        public int Min = 0, Max = 255, Value, Default = -1; public string Label = ""; public Func<int, string> Fmt; public Color Accent = T.Cyan;
        public bool Center; public bool Dragging; public int Step = 4;
        public event Action<int> UserChanged;
        int dragY, dragV; Font fl { get { return T.Fn("Segoe UI", 7.5f); } } Font fv { get { return T.Fn("Segoe UI Semibold", 8f); } }
        public Knob() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Size = new Size(62, 74); Cursor = Cursors.SizeNS; }
        public void SetValue(int v) { if (Dragging) return; if (v < Min) v = Min; if (v > Max) v = Max; if (v != Value) { Value = v; Invalidate(); } }
        void User(int v) { if (v < Min) v = Min; if (v > Max) v = Max; if (v > Limit) v = Limit; if (v == Value) return; Value = v; Invalidate(); if (UserChanged != null) UserChanged(v); }
        bool locked; readonly List<Btn> linked = new List<Btn>(); public int Limit = int.MaxValue;
        public bool Locked { get { return locked; } set { if (locked != value) { locked = value; foreach (Btn b in linked) { b.Slaved = value; b.Invalidate(); } Invalidate(); if (Locks.Changed != null) Locks.Changed(this); } } }
        public void Link(Btn b) { linked.Add(b); b.Slaved = locked; }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Right) { Locked = !Locked; } else if (e.Button == MouseButtons.Left && !Locked) { Dragging = true; dragY = e.Y; dragV = Value; Capture = true; } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Dragging) { double sens = (Control.ModifierKeys & Keys.Shift) != 0 ? 0.25 : 1.0; User(dragV + (int)((dragY - e.Y) * sens * (Max - Min) / 150.0)); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { Dragging = false; Capture = false; base.OnMouseUp(e); }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { if (Locked) return; if (Default >= 0) User(Default); else if (Center) User((Min + Max) / 2); base.OnMouseDoubleClick(e); }
        public static int WheelPct = 2;
        protected override void OnMouseWheel(MouseEventArgs e) { if (Locked) return; int s = Math.Max(1, (int)Math.Round((Max - Min) * WheelPct / 100.0)); if ((Control.ModifierKeys & Keys.Shift) != 0) s = Math.Max(1, s / 3); s *= Math.Max(1, Math.Abs(e.Delta) / 120); User(Value + (e.Delta > 0 ? s : -s)); base.OnMouseWheel(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : T.Panel);
            float k = T.Scale * 96f / T.Dpi * T.Dpi / 96f; float d = Math.Max(10f, Math.Min(Width - 14 * k, Height - 28 * k)); float cx = Width / 2f, cy = 8 * k + d / 2f;
            RectangleF r = new RectangleF(cx - d / 2, cy - d / 2, d, d);
            using (Brush b = new SolidBrush(Color.FromArgb(18, 22, 30))) g.FillEllipse(b, r.X + 3 * k, r.Y + 3 * k, d - 6 * k, d - 6 * k);
            RectangleF ar = new RectangleF(r.X + 1.5f * k, r.Y + 1.5f * k, d - 3 * k, d - 3 * k);
            using (Pen p = new Pen(Color.FromArgb(52, 62, 78), 4f * k)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, ar, 135, 270); }
            double t = Max > Min ? (Value - Min) / (double)(Max - Min) : 0;
            using (Pen p = new Pen(Locked ? T.Lock : Accent, 4f * k))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                if (Center) { float a0 = 270, sw = (float)((t - 0.5) * 270); if (Math.Abs(sw) > 0.5) g.DrawArc(p, ar, a0, sw); }
                else if (t > 0.002) g.DrawArc(p, ar, 135, (float)(270 * t));
            }
            double ang = (135 + 270 * t) * Math.PI / 180.0; float rr = d / 2 - 7 * k;
            using (Pen p = new Pen(T.Text, 2f * k)) { p.EndCap = LineCap.Round; g.DrawLine(p, cx + (float)Math.Cos(ang) * (rr * 0.3f), cy + (float)Math.Sin(ang) * (rr * 0.3f), cx + (float)Math.Cos(ang) * rr, cy + (float)Math.Sin(ang) * rr); }
            StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center;
            string vs = Fmt != null ? Fmt(Value) : Value.ToString();
            using (Brush b = new SolidBrush(Locked ? T.Lock : T.Text)) g.DrawString(vs, fv, b, new RectangleF(0, Height - 28 * k, Width, 14 * k), sf);
            using (Brush b = new SolidBrush(T.Dim)) g.DrawString(Label, fl, b, new RectangleF(0, Height - 15 * k, Width, 14 * k), sf);
        }
    }

    // ------------------------------------------------------------------ bargraphe
    public class MeterBar : Control
    {
        public static int HoldMs = 900; public double Max = 241, Hot = 0.5; public string Title = ""; public string[] TickText; public double[] TickPos; public Func<int, string> Fmt;
        public Color Cool = T.Green, Warm = T.Amber, Over = T.Red; public double OverAt = 2;
        double val, peak, avg; int peakTick;
        public double AvgFrac { get { return avg / Max; } } public double PeakFrac { get { return peak / Max; } } public int AvgRaw { get { return (int)Math.Round(avg); } }
        Font f { get { return T.Fn("Segoe UI", 7f); } } Font ft { get { return T.Fn("Segoe UI Semibold", 8f); } }
        public MeterBar() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Size = new Size(240, 30); }
        public void SetValue(int v)
        {
            double nv = Math.Max(0, Math.Min(Max, v)); int tick = Environment.TickCount;
            avg += (nv - avg) * 0.35; if (nv >= peak) { peak = nv; peakTick = tick; } else if (tick - peakTick > HoldMs) peak = Math.Max(nv, peak - Max * 0.02);
            if (Math.Abs(nv - val) > 0.01 || peak != nv) { val = nv; Invalidate(); }
            lastRaw = v;
        }
        int lastRaw; public int Raw { get { return lastRaw; } } public double Frac { get { return val / Max; } }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.Clear(Parent != null ? Parent.BackColor : T.Panel);
            int tw = 56; Rectangle bar = new Rectangle(52, 3, Width - 52 - tw - 4, 12);
            using (Brush b = new SolidBrush(T.Dim)) g.DrawString(Title, ft, b, 0, 1);
            using (Brush b = new SolidBrush(Color.FromArgb(8, 10, 14))) g.FillRectangle(b, bar);
            int seg = 4, gap = 1; int n = bar.Width / (seg + gap);
            double frac = val / Max; int lit = (int)Math.Round(frac * n);
            for (int i = 0; i < n; i++)
            {
                double p = (i + 0.5) / n; Color c = p > OverAt ? Over : (p > Hot ? Warm : Cool);
                if (i >= lit) c = Color.FromArgb(34, c);
                using (Brush b = new SolidBrush(c)) g.FillRectangle(b, bar.X + i * (seg + gap), bar.Y + 1, seg, bar.Height - 2);
            }
            int px = bar.X + (int)(peak / Max * (bar.Width - 2)); using (Pen p = new Pen(Color.FromArgb(220, 255, 255, 255))) g.DrawLine(p, px, bar.Y, px, bar.Bottom);
            if (TickText != null)
                for (int i = 0; i < TickText.Length; i++)
                {
                    int x = bar.X + (int)(TickPos[i] * (bar.Width - 1));
                    using (Pen p = new Pen(T.Edge)) g.DrawLine(p, x, bar.Bottom, x, bar.Bottom + 2);
                    SizeF sz = g.MeasureString(TickText[i], f);
                    using (Brush b = new SolidBrush(TickPos[i] > Hot ? Color.FromArgb(220, 120, 100) : T.Dim)) g.DrawString(TickText[i], f, b, x - sz.Width / 2 + 1, bar.Bottom + 1);
                }
            StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Far;
            using (Brush b = new SolidBrush(T.Text)) g.DrawString(Fmt != null ? Fmt(lastRaw) : lastRaw.ToString(), ft, b, new RectangleF(Width - tw - 2, 1, tw, 14), sf);
        }
    }

    // ------------------------------------------------------------------ vumetres a aiguille (style ecran de l IC-7760, format 4/3)
    public class AnalogMeter : Control
    {
        public MeterBar[] S = new MeterBar[2]; public MeterBar Po, Swr, Alc, Comp, Vd, Id;
        public int TxIdx = -1; public string[] Bw = { "", "" }, Mode = { "", "" }, Fil = { "", "" };
        public int MetI; public event Action MetChanged;
        static readonly string[] metName = { "Po", "SWR", "ALC", "COMP", "Vd", "Id" };
        const double TH = 26 * Math.PI / 180;
        static readonly Font f40 = new Font("Segoe UI", 38f, FontStyle.Regular, GraphicsUnit.Pixel), f40b = new Font("Segoe UI Semibold", 38f, FontStyle.Regular, GraphicsUnit.Pixel);
        static readonly Font f30 = new Font("Segoe UI", 30f, FontStyle.Regular, GraphicsUnit.Pixel), f70 = new Font("Segoe UI Semibold", 74f, FontStyle.Regular, GraphicsUnit.Pixel), f56 = new Font("Segoe UI Semibold", 52f, FontStyle.Regular, GraphicsUnit.Pixel);
        public AnalogMeter() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); BackColor = Color.Black; }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { MetI = (MetI + 1) % 6; if (MetChanged != null) MetChanged(); Invalidate(); } base.OnMouseDown(e); }
        MeterBar Met { get { MeterBar[] a = { Po, Swr, Alc, Comp, Vd, Id }; return a[MetI]; } }
        const float CX = 500, CY = 1230;
        static PointF P(float r, double a) { return new PointF(CX + (float)(r * Math.Sin(a)), CY - (float)(r * Math.Cos(a))); }
        static void Txt(Graphics g, string s, Font f, Color c, float x, float y, bool centre)
        {
            SizeF sz = g.MeasureString(s, f); using (Brush b = new SolidBrush(c)) g.DrawString(s, f, b, centre ? x - sz.Width / 2 : x, centre ? y - sz.Height / 2 : y);
        }
        // un anneau gradue : labels vers l exterieur (outside) ou vers l interieur
        static void Scale(Graphics g, MeterBar m, float r, bool outside, Color norm, Color hot, double hotF)
        {
            if (m == null) return;
            float a0 = (float)(-90 - TH * 180 / Math.PI), sw = (float)(2 * TH * 180 / Math.PI); RectangleF rc = new RectangleF(CX - r, CY - r, 2 * r, 2 * r);
            using (Pen p = new Pen(norm, 7)) g.DrawArc(p, rc, a0, (float)(sw * Math.Min(1, hotF)));
            if (hotF < 1) using (Pen p = new Pen(hot, 11)) g.DrawArc(p, rc, a0 + (float)(sw * hotF), (float)(sw * (1 - hotF)));
            if (m.TickPos == null) return;
            double prev = 0; int dir = outside ? 1 : -1;
            for (int i = 0; i <= m.TickPos.Length; i++)
            {
                double cur = i < m.TickPos.Length ? m.TickPos[i] : 1.0;
                for (int k = 1; k < 5; k++)    // petites graduations entre deux reperes
                {
                    double f = prev + (cur - prev) * k / 5.0;
                    double a = -TH + 2 * TH * f; PointF p1 = P(r, a), p2 = P(r + dir * 16, a);
                    using (Pen p = new Pen(f > hotF ? hot : norm, 3)) g.DrawLine(p, p1, p2);
                }
                prev = cur;
            }
            for (int i = 0; i < m.TickPos.Length; i++)
            {
                double f = m.TickPos[i]; double a = -TH + 2 * TH * f; Color c = f > hotF ? hot : norm;
                PointF p1 = P(r, a), p2 = P(r + dir * 30, a);
                using (Pen p = new Pen(c, 6)) g.DrawLine(p, p1, p2);
                if (m.TickText != null) { PointF pt = P(r + dir * 56, a); Txt(g, m.TickText[i], f30, c, pt.X, pt.Y, true); }
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.Clear(Color.Black);
            float H = Height - 2, gap = 6, w = (Width - gap) / 2f;
            float x0 = 0, y0 = (Height - H) / 2f;
            for (int i = 0; i < 2; i++)
            {
                GraphicsState st = g.Save(); g.TranslateTransform(x0 + i * (w + gap), y0); g.ScaleTransform(w / 1000f, H / 750f);
                g.SetClip(new RectangleF(0, 0, 1000, 750));
                DrawOne(g, i); g.Restore(st);
            }
        }
        void DrawOne(Graphics g, int idx)
        {
            Color white = Color.FromArgb(240, 240, 240), red = Color.FromArgb(240, 50, 40), cyan = Color.FromArgb(40, 175, 255), gray = Color.FromArgb(110, 118, 118);
            using (Brush b = new SolidBrush(Color.Black)) g.FillRectangle(b, 0, 0, 1000, 750);
            bool tx = TxIdx == idx; MeterBar sm = S[idx], cur = tx ? Met : sm;
            // en-tete
            Txt(g, "ANT 1", f40, white, 24, 14, false);
            RoundBox(g, 250, 18, 300, 56, gray, Color.FromArgb(20, 20, 20)); Txt(g, "BW", f40b, white, 262, 20, false); Txt(g, Bw[idx], f40, white, 350, 20, false);
            RoundBox(g, 24, 100, 300, 56, gray, Color.FromArgb(20, 20, 20)); Txt(g, "MET", f40b, white, 36, 102, false); Txt(g, metName[MetI], f40, white, 200, 102, false);
            Txt(g, idx == 0 ? "MAIN" : "SUB", f40b, idx == 0 ? T.Amber : T.Blue, 840, 14, false);
            // echelles
            double hotS = sm != null ? sm.OverAt : 0.5;
            Scale(g, sm, 990, true, white, red, hotS);
            Scale(g, Id, 905, true, white, white, 2);
            Scale(g, Po, 905, false, white, white, 2);
            Scale(g, Swr, 815, false, white, white, 2);
            Scale(g, Comp, 735, true, cyan, cyan, 2);
            if (Alc != null) using (Pen p = new Pen(red, 10)) g.DrawArc(p, new RectangleF(CX - 700, CY - 700, 1400, 1400), (float)(-90 - TH * 180 / Math.PI), (float)(2 * TH * 180 / Math.PI * 0.5));
            Txt(g, "ALC", f30, red, CX - 150, CY - 640, true);
            Scale(g, Vd, 640, false, white, white, 2);
            // legendes a gauche et unites a droite
            string[] tag = { "S", "Id", "Po", "SWR", "COMP" }; float[] rr = { 990, 905, 905, 815, 735 }; Color[] tc = { white, white, white, white, cyan };
            for (int i = 0; i < 5; i++) { PointF p = P(rr[i], -TH); Txt(g, tag[i], f40b, tc[i], Math.Max(6, p.X - 70 - (i >= 3 ? 30 : 0)), p.Y - 18 + (i == 1 ? -26 : 0) + (i == 2 ? 30 : 0), false); }
            string[] un = { "A", "W", "dB", "V" }; float[] ur = { 905 + 56, 905 - 56, 735 + 56, 640 - 56 };
            for (int i = 0; i < 4; i++) { PointF p = P(ur[i], TH + 0.05); Txt(g, un[i], f30, i == 2 ? cyan : white, p.X, p.Y, true); }
            // aiguille
            double f = cur != null ? Math.Max(0, Math.Min(1, tx ? cur.AvgFrac : cur.Frac)) : 0; double a = -TH + 2 * TH * f;
            if (tx && cur != null) { double pk = Math.Max(0, Math.Min(1, cur.PeakFrac)); double pa = -TH + 2 * TH * pk; if (pk > f + 0.01) using (Pen p = new Pen(red, 9)) g.DrawLine(p, P(640, pa), P(1010, pa)); }
            using (Pen p = new Pen(Color.White, 15)) g.DrawLine(p, P(640, a), P(1010, a));
            // bas : TX, valeur, mode, filtre
            if (tx) { using (Pen p = new Pen(red, 5)) g.DrawRectangle(p, 22, 640, 150, 80); Txt(g, "TX", f56, red, 97, 680, true); }
            string val = cur != null && cur.Fmt != null ? cur.Fmt(tx ? cur.AvgRaw : cur.Raw) : "";
            Txt(g, val.Replace(" ", ""), f56, white, 190, 646, false);
            RoundBox(g, 560, 650, 190, 70, Color.FromArgb(60, 140, 235), Color.FromArgb(40, 110, 205)); Txt(g, Mode[idx], f56, white, 655, 686, true);
            RoundBox(g, 765, 650, 190, 70, gray, Color.FromArgb(40, 44, 44)); Txt(g, Fil[idx], f56, white, 860, 686, true);
            using (Pen p = new Pen(Color.FromArgb(80, 80, 90), 4)) g.DrawRectangle(p, 2, 2, 996, 746);
        }
        static void RoundBox(Graphics g, float x, float y, float w, float h, Color edge, Color fill)
        {
            using (GraphicsPath gp = new GraphicsPath())
            {
                float r = 14; gp.AddArc(x, y, r * 2, r * 2, 180, 90); gp.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90); gp.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90); gp.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90); gp.CloseFigure();
                using (Brush b = new SolidBrush(fill)) g.FillPath(b, gp); using (Pen p = new Pen(edge, 4)) g.DrawPath(p, gp);
            }
        }
    }

    // ------------------------------------------------------------------ afficheur de fréquence
    public class FreqDisplay : Control
    {
        public long Freq; public string ModeText = ""; public string Info = ""; public string Caption = "MAIN"; public bool Selected, Tx, Rit; public Color Accent = T.Amber;
        public event Action<long> FreqEdited; public event Action Activated;
        int hover = -1; static readonly long[] Place = { 10000000, 1000000, 100000, 10000, 1000, 100, 10, 1 };
        Rectangle[] cell = new Rectangle[8]; Font fd; Font fs { get { return T.Fn("Segoe UI Semibold", 9f); } } Font ft { get { return T.Fn("Segoe UI Semibold", 10f); } }
        public FreqDisplay() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Size = new Size(430, 96); }
        public void SetFreq(long f) { if (f != Freq) { Freq = f; Invalidate(); } }
        public void SetInfo(string mode, string info, bool sel, bool tx) { if (mode != ModeText || info != Info || sel != Selected || tx != Tx) { ModeText = mode; Info = info; Selected = sel; Tx = tx; Invalidate(); } }
        void Calc(Graphics g)
        {
            float fsz = Math.Max(14, Math.Min(Height * 0.60f, (Width - 24) / 5.1f));
            if (fd == null || Math.Abs(fd.Size - fsz) > 0.5f) { if (fd != null) fd.Dispose(); fd = new Font("Consolas", fsz, FontStyle.Bold, GraphicsUnit.Pixel); }
            float dw = fsz * 0.56f, dotw = fsz * 0.30f; float total = dw * 8 + dotw * 2; float x = Width - total - 12;
            if (x < 6) x = 6;
            float y = Height * 0.24f; float h = Height * 0.70f; int k = 0;
            for (int i = 0; i < 8; i++)
            {
                cell[i] = new Rectangle((int)x, (int)y, (int)dw, (int)h); x += dw; k++;
                if (i == 1 || i == 4) x += dotw;
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.SmoothingMode = SmoothingMode.AntiAlias;
            Calc(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            Color border = Tx ? T.Red : (Selected ? Accent : T.Edge);
            using (GraphicsPath p = T.Round(r, 8))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, T.FreqBg, T.Mix(T.FreqBg, Color.White, 0.06), 90f)) g.FillPath(b, p);
                using (Pen pen = new Pen(border, Selected || Tx ? 2f : 1f)) g.DrawPath(pen, p);
            }
            using (Brush b = new SolidBrush(Selected ? Accent : T.Dim)) g.DrawString(Caption, ft, b, 10, 6);
            using (Brush b = new SolidBrush(Tx ? T.Red : T.Cyan)) g.DrawString(ModeText, ft, b, 10, 26);
            using (Brush b = new SolidBrush(T.Dim)) g.DrawString(Info, fs, b, 10, Height - 22);
            string s = Freq.ToString().PadLeft(8);
            Color base0 = Caption == "SUB" ? T.FreqSub : T.FreqMain;
            Color on = Tx ? Color.FromArgb(255, 110, 100) : (Selected ? base0 : T.Mix(base0, T.FreqBg, 0.38));
            bool lead = true;
            for (int i = 0; i < 8; i++)
            {
                char c = s[i]; if (c != ' ' && c != '0') lead = false; else if (c == '0' && i >= 2 && lead) lead = false;
                bool dim = c == ' ' || (c == '0' && lead);
                if (i == hover) using (Brush hb = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillRectangle(hb, cell[i]);
                if (!dim) using (Brush b = new SolidBrush(dim ? Color.FromArgb(50, 60, 74) : on))
                { StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; g.DrawString(c == ' ' ? "8" : c.ToString(), fd, b, new RectangleF(cell[i].X - 4, cell[i].Y, cell[i].Width + 8, cell[i].Height), sf); }
                if (i == 1 || i == 4)
                { int dx = cell[i].Right + (int)(fd.Size * 0.12f); using (Brush b = new SolidBrush(on)) g.FillRectangle(b, dx, cell[i].Bottom - (int)(fd.Size * 0.14f), Math.Max(3, (int)(fd.Size * 0.08f)), Math.Max(3, (int)(fd.Size * 0.08f))); }
            }
            using (Brush b = new SolidBrush(T.Dim)) g.DrawString("Hz", fs, b, Width - 24, 6);
        }
        int Hit(Point p) { for (int i = 0; i < 8; i++) { Rectangle c = cell[i]; c.Inflate(2, 0); if (c.Contains(p)) return i; } return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != hover) { hover = h; Invalidate(); } Cursor = h >= 0 ? Cursors.SizeNS : Cursors.Default; base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Activated != null) Activated();
            int h = Hit(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left) Bump(h, e.Y < cell[h].Top + cell[h].Height / 2 ? 1 : -1);
            base.OnMouseDown(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int h = Hit(e.Location); if (h < 0) { h = 7; } // hors chiffre : pas de 1 kHz par défaut
            if (Hit(e.Location) < 0) h = 4;
            Bump(h, e.Delta > 0 ? 1 : -1); base.OnMouseWheel(e);
        }
        void Bump(int i, int dir)
        {
            long nf = Freq + dir * Place[i]; if (nf < 30000) nf = 30000; if (nf > 59999999) nf = 59999999;
            if (nf != Freq) { Freq = nf; Invalidate(); if (FreqEdited != null) FreqEdited(nf); }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e); if (Hit(e.Location) >= 0 && false) return;
            string v = Prompt.Ask("Fréquence (en kHz, ou MHz avec un point) :", Caption, (Freq / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            if (v == null) return;
            double d; v = v.Trim().Replace(',', '.').Replace(" ", "");
            if (!double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return;
            long hz = d < 100 ? (long)Math.Round(d * 1e6) : (long)Math.Round(d * 1e3);
            if (hz >= 30000 && hz <= 59999999 && FreqEdited != null) { Freq = hz; Invalidate(); FreqEdited(hz); }
        }
    }

    // ------------------------------------------------------------------ boîtes de dialogue simples
    public static class Prompt
    {
        public static string Ask(string text, string title, string def)
        {
            using (Form f = new Form())
            {
                f.Text = title; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterParent; f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
                f.BackColor = T.Panel; f.ForeColor = T.Text; f.ClientSize = new Size(380, 110); f.Font = new Font("Segoe UI", 9f);
                Label l = new Label(); l.Text = text; l.SetBounds(12, 10, 356, 20);
                TextBox tb = new TextBox(); tb.Text = def; tb.SetBounds(12, 34, 356, 24); tb.BackColor = T.Bg; tb.ForeColor = T.Text; tb.BorderStyle = BorderStyle.FixedSingle;
                Button ok = new Button(); ok.Text = "OK"; ok.DialogResult = DialogResult.OK; ok.SetBounds(200, 70, 80, 28); Button ca = new Button(); ca.Text = "Annuler"; ca.DialogResult = DialogResult.Cancel; ca.SetBounds(288, 70, 80, 28);
                foreach (Button b in new Button[] { ok, ca }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = T.Panel2; b.ForeColor = T.Text; b.FlatAppearance.BorderColor = T.Edge; }
                f.Controls.AddRange(new Control[] { l, tb, ok, ca }); f.AcceptButton = ok; f.CancelButton = ca;
                tb.SelectAll();
                return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
            }
        }
    }

    // liste déroulante sombre
    public class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed; DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat;
            BackColor = Color.FromArgb(30, 37, 49); ForeColor = T.Text; Font = new Font("Segoe UI", 8.5f); ItemHeight = 20;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (Brush b = new SolidBrush(sel ? Color.FromArgb(58, 78, 110) : Color.FromArgb(30, 37, 49))) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index >= 0) using (Brush b = new SolidBrush(T.Text)) e.Graphics.DrawString(GetItemText(Items[e.Index]), Font, b, e.Bounds.X + 3, e.Bounds.Y + 2);
        }
    }

    // groupe titré
    public class Group : Panel
    {
        public string Title = ""; Font f { get { return T.Fn("Segoe UI Semibold", 8f); } }
        public Group(string title) { Title = title; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = T.Panel; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : T.Bg);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (GraphicsPath p = T.Round(r, 7)) { using (Brush b = new SolidBrush(T.Panel)) g.FillPath(b, p); using (Pen pen = new Pen(T.Edge)) g.DrawPath(pen, p); }
            using (Brush b = new SolidBrush(T.Amber)) g.DrawString(Title.ToUpperInvariant(), f, b, 9, 4);
        }
    }
}
