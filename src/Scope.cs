using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IC7760Link
{
    public static class Palettes
    {
        public static readonly string[] Names = { "IC-7760", "Thermique", "Océan", "Viridis", "Ambre", "Matrix", "Arc-en-ciel", "Gris", "PersonnalisÃ©e" };
        static readonly double[][][] Stops = {
            // pos, r, g, b
            new double[][]{ new double[]{0,0,0,12}, new double[]{0.14,0,10,70}, new double[]{0.30,0,50,160}, new double[]{0.46,0,170,210}, new double[]{0.60,30,210,90}, new double[]{0.74,235,230,40}, new double[]{0.88,255,120,10}, new double[]{1,255,255,255} },
            new double[][]{ new double[]{0,0,0,0}, new double[]{0.2,50,0,90}, new double[]{0.4,170,0,70}, new double[]{0.6,250,70,0}, new double[]{0.8,255,200,20}, new double[]{1,255,255,230} },
            new double[][]{ new double[]{0,0,4,20}, new double[]{0.3,0,40,110}, new double[]{0.55,0,130,200}, new double[]{0.8,120,220,240}, new double[]{1,255,255,255} },
            new double[][]{ new double[]{0,68,1,84}, new double[]{0.25,59,82,139}, new double[]{0.5,33,145,140}, new double[]{0.75,94,201,98}, new double[]{1,253,231,37} },
            new double[][]{ new double[]{0,0,0,0}, new double[]{0.35,90,40,0}, new double[]{0.65,230,140,0}, new double[]{0.88,255,215,90}, new double[]{1,255,255,240} },
            new double[][]{ new double[]{0,0,0,0}, new double[]{0.4,0,70,10}, new double[]{0.7,40,220,40}, new double[]{0.9,180,255,150}, new double[]{1,255,255,255} },
            new double[][]{ new double[]{0,0,0,40}, new double[]{0.2,0,0,230}, new double[]{0.4,0,220,230}, new double[]{0.55,0,230,0}, new double[]{0.75,240,240,0}, new double[]{0.9,240,0,0}, new double[]{1,255,255,255} },
            new double[][]{ new double[]{0,0,0,0}, new double[]{1,255,255,255} } };

        public static int[] Build(int idx, double gamma)
        {
            double[][] st = idx >= Stops.Length ? CustomStops() : Stops[idx];
            int[] lut = new int[256];
            for (int i = 0; i < 256; i++)
            {
                double t = Math.Pow(i / 255.0, gamma);
                int k = 0; while (k < st.Length - 2 && t > st[k + 1][0]) k++;
                double a = st[k][0], b = st[k + 1][0]; double u = b > a ? (t - a) / (b - a) : 0; if (u < 0) u = 0; if (u > 1) u = 1;
                int r = (int)(st[k][1] + (st[k + 1][1] - st[k][1]) * u);
                int g = (int)(st[k][2] + (st[k + 1][2] - st[k][2]) * u);
                int bl = (int)(st[k][3] + (st[k + 1][3] - st[k][3]) * u);
                lut[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
            }
            return lut;
        }
        // palette personnalisee : 10 couleurs reparties de 0 (fond) a 1 (signal le plus fort)
        public const int NCustom = 10; public static int Version;
        public static Color[] Custom = DefaultCustom();
        static Color[] DefaultCustom() { Color[] c = new Color[NCustom]; int[] l = Build(0, 1.0); for (int i = 0; i < NCustom; i++) c[i] = Color.FromArgb(l[i * 255 / (NCustom - 1)]); return c; }
        public static void CopyFrom(int idx) { int[] l = Build(idx, 1.0); for (int i = 0; i < NCustom; i++) Custom[i] = Color.FromArgb(255, Color.FromArgb(l[i * 255 / (NCustom - 1)])); Changed(); }
        static double[][] CustomStops() { double[][] s = new double[NCustom][]; for (int i = 0; i < NCustom; i++) s[i] = new double[] { i / (double)(NCustom - 1), Custom[i].R, Custom[i].G, Custom[i].B }; return s; }
        public static void Changed() { Version++; StringBuilder sb = new StringBuilder(); for (int i = 0; i < NCustom; i++) { if (i > 0) sb.Append('|'); sb.Append(Custom[i].R + "," + Custom[i].G + "," + Custom[i].B); } Cfg.Set("wfstops", sb.ToString()); }
        public static void LoadCfg()
        {
            string v = Cfg.Get("wfstops", ""); if (v.Length == 0) return; string[] p = v.Split('|'); if (p.Length != NCustom) return;
            try { for (int i = 0; i < NCustom; i++) { string[] c = p[i].Split(','); Custom[i] = Color.FromArgb(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2])); } } catch { }
        }
        public static Color Top(int idx) { int[] l = Build(idx, 1.0); return Color.FromArgb(l[200]); }
    }

    // couleurs reglables du scope (curseurs, courbe, cretes, bande passante)
    public static class ScopeColors
    {
        public static Color CursorMain = Color.FromArgb(255, 70, 60), CursorSub = Color.FromArgb(90, 170, 255), Line = Color.FromArgb(240, 245, 255), Peak = Color.FromArgb(255, 200, 60), Pass = Color.FromArgb(255, 210, 60);
        public static void Load()
        {
            CursorMain = Get("col.cur0", CursorMain); CursorSub = Get("col.cur1", CursorSub); Line = Get("col.line", Line); Peak = Get("col.peak", Peak); Pass = Get("col.pass", Pass);
        }
        static Color Get(string k, Color d) { int v = Cfg.GetInt(k, d.ToArgb()); return Color.FromArgb(255, Color.FromArgb(v)); }
        public static void Save() { Cfg.SetInt("col.cur0", CursorMain.ToArgb()); Cfg.SetInt("col.cur1", CursorSub.ToArgb()); Cfg.SetInt("col.line", Line.ToArgb()); Cfg.SetInt("col.peak", Peak.ToArgb()); Cfg.SetInt("col.pass", Pass.ToArgb()); }
    }

    public static class BandPlan
    {
        // kHz : début, fin, type (0 CW, 1 numérique, 2 phonie, 3 balises) — plan IARU région 1, indicatif
        public static readonly int[][] Seg = {
            new int[]{1810,1838,0}, new int[]{1838,1840,1}, new int[]{1840,2000,2},
            new int[]{3500,3570,0}, new int[]{3570,3600,1}, new int[]{3600,3800,2},
            new int[]{5352,5367,2},
            new int[]{7000,7040,0}, new int[]{7040,7060,1}, new int[]{7060,7200,2},
            new int[]{10100,10130,0}, new int[]{10130,10150,1},
            new int[]{14000,14070,0}, new int[]{14070,14099,1}, new int[]{14099,14101,3}, new int[]{14101,14350,2},
            new int[]{18068,18095,0}, new int[]{18095,18109,1}, new int[]{18109,18111,3}, new int[]{18111,18168,2},
            new int[]{21000,21070,0}, new int[]{21070,21149,1}, new int[]{21149,21151,3}, new int[]{21151,21450,2},
            new int[]{24890,24915,0}, new int[]{24915,24931,1}, new int[]{24931,24990,2},
            new int[]{28000,28070,0}, new int[]{28070,28190,1}, new int[]{28190,28225,3}, new int[]{28225,29700,2},
            new int[]{50000,50100,0}, new int[]{50100,50300,2}, new int[]{50300,50500,1}, new int[]{50500,52000,2} };
        public static readonly Color[] Col = { Color.FromArgb(230, 200, 60), Color.FromArgb(60, 200, 120), Color.FromArgb(70, 140, 235), Color.FromArgb(235, 90, 90) };
        public static readonly string[] Nom = { "CW", "Numérique", "Phonie", "Balises" };
    }

    public class ScopeView : Control
    {
        sealed class Wf { public int W, H; public int[] Px; public GCHandle Hd; public Bitmap Bmp; }
        public static readonly long[] Spans = { 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000 };

        public Rig Rig; public int RxIndex;
        public int PaletteIdx = 0; public double Gamma = 0.85;
        public bool AutoLevel = true; public double Low = 30, High = 130;
        public int AvgFrames = 2; public bool PeakHold = true; public bool ShowBandPlan = true; public bool ShowPeaks = true;
        public double PeakHoldSec = 3; public bool FillSpectrum = true; public double RowsPerFrame = 1.0; public bool Smooth = true;
        public double SplitRatio = 0.36; public bool IsSub { get { return RxIndex == 1; } }
        public string Title = "MAIN";

        // données
        float[] avg = new float[0], peak = new float[0]; long[] peakAt = new long[0]; int nPts;
        double winLo, winHi; bool haveWin; bool winFixed; bool oor;
        int[] lut; int lutVer; int lutKey = -1; double lutGamma = -1;
        Wf wf, wf2; double wfLo, wfHi; float[] rowPrev = new float[0], rowAcc = new float[0]; int rowAccN; double rowFrac;
        long[] rowTime = new long[0];
        int lastFrameTick; public int Fps; int fpsN; int fpsTick;
        float[] rowBuf = new float[0]; PointF[] pts = new PointF[0];
        double autoLow = 30, autoHigh = 130;
        static readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        int lastRows; long lastRowMs, lastAnim, rowSeq; float wfOff; double rowInterval = 66; float[] dispAvg = new float[0];
        // interaction
        Point mouse = new Point(-1, -1); bool mouseIn, dragging, dragSplit;
        Font fSmall { get { return T.Fn("Segoe UI", 8f); } } Font fMed { get { return T.Fn("Segoe UI Semibold", 9f); } } Font fBig { get { return T.Fn("Segoe UI Semibold", 11f); } }
        Rectangle rSpec, rAxis, rWf; List<Rectangle> peakHit = new List<Rectangle>(); List<long> peakF = new List<long>();
        string status = "";

        public ScopeView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(6, 8, 12); ForeColor = Color.FromArgb(200, 210, 220);
            Cursor = Cursors.Cross; TabStop = false;
            ContextMenuStrip = BuildMenu();
        }

        protected override void Dispose(bool d) { if (d) { Free(wf); Free(wf2); } base.Dispose(d); }

        // ------------------------------------------------------------ cascade : mémoire
        Wf Make(int w, int h)
        {
            Wf o = new Wf(); o.W = w; o.H = h; o.Px = new int[w * h];
            int blk = unchecked((int)0xFF000000); for (int i = 0; i < o.Px.Length; i++) o.Px[i] = blk;
            o.Hd = GCHandle.Alloc(o.Px, GCHandleType.Pinned);
            o.Bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, o.Hd.AddrOfPinnedObject());
            return o;
        }
        void Free(Wf o) { if (o == null) return; o.Bmp.Dispose(); o.Hd.Free(); }

        void LayoutAll()
        {
            int w = Math.Max(40, ClientSize.Width), h = Math.Max(60, ClientSize.Height);
            int axisH = (int)Math.Round(24 * T.Scale);
            int specH = (int)((h - axisH) * SplitRatio); if (specH < 50) specH = 50; if (specH > h - axisH - 40) specH = h - axisH - 40;
            rSpec = new Rectangle(0, 0, w, specH);
            rAxis = new Rectangle(0, specH, w, axisH);
            rWf = new Rectangle(0, specH + axisH, w, Math.Max(20, h - specH - axisH));
            if (wf == null || wf.W != rWf.Width || wf.H != rWf.Height)
            {
                Wf n = Make(rWf.Width, rWf.Height);
                if (wf != null)
                {   // conserve l'historique en le redimensionnant
                    using (Graphics g = Graphics.FromImage(n.Bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(wf.Bmp, 0, 0, n.W, n.H); }
                    Free(wf);
                }
                wf = n; rowTime = new long[n.H];
            }
        }

        public void ClearWaterfall() { if (wf == null) return; int blk = unchecked((int)0xFF000000); for (int i = 0; i < wf.Px.Length; i++) wf.Px[i] = blk; Array.Clear(rowTime, 0, rowTime.Length); Invalidate(); }

        // ------------------------------------------------------------ entrée des données
        // Appelé depuis le thread d'interface pour chaque trame de scope
        public void Push(ScopeFrame f)
        {
            if (f.Data == null || f.Data.Length < 8) return;
            LayoutAll();
            int n = f.Data.Length;
            if (n != nPts) { nPts = n; avg = new float[n]; peak = new float[n]; peakAt = new long[n]; for (int i = 0; i < n; i++) { avg[i] = f.Data[i]; peak[i] = f.Data[i]; } dispAvg = (float[])avg.Clone(); }
            else
            {
                float a = AvgFrames <= 1 ? 1f : 1f / AvgFrames;
                for (int i = 0; i < n; i++)
                {
                    float v = f.Data[i]; avg[i] += (v - avg[i]) * a;
                    if (avg[i] >= peak[i]) { peak[i] = avg[i]; peakAt[i] = sw.ElapsedMilliseconds; }
                    else if (sw.ElapsedMilliseconds - peakAt[i] > PeakHoldSec * 1000) { float pk = peak[i] - 0.35f; if (pk <= avg[i]) { pk = avg[i]; peakAt[i] = sw.ElapsedMilliseconds; } peak[i] = pk; }
                }
            }
            double lo = f.Lo, hi = f.Hi; if (hi <= lo) return;
            // recalage de la cascade si la fenêtre de fréquence a changé
            if (haveWin && wf != null && (Math.Abs(lo - wfLo) / (wfHi - wfLo) * wf.W >= 0.5 || Math.Abs((hi - lo) - (wfHi - wfLo)) / (wfHi - wfLo) * wf.W >= 0.5)) Reproject(lo, hi);
            else if (!haveWin) { wfLo = lo; wfHi = hi; }
            winLo = lo; winHi = hi; haveWin = true; winFixed = f.Fixed; oor = f.Oor;
            if (wfLo == 0 && wfHi == 0) { wfLo = lo; wfHi = hi; }
            int tick = Environment.TickCount; if (lastFrameTick != 0 && tick - lastFrameTick > 0) { } lastFrameTick = tick;
            fpsN++; if (tick - fpsTick >= 1000) { Fps = fpsN; fpsN = 0; fpsTick = tick; }
            if (AutoLevel) UpdateAutoLevel(); else { specLo += (Math.Max(0, Low - 14) - specLo) * 0.1; specHi += (Math.Min(160, High + 22) - specHi) * 0.1; }
            AddRows(f);
            Invalidate();
        }

        // appelé à chaque tick de l interface : défilement et courbe fluides entre deux trames du poste
        public void Animate()
        {
            if (!Smooth || wf == null || nPts < 8 || !Visible) return;
            long now = sw.ElapsedMilliseconds; long dt = now - lastAnim; if (dt < 15) return; lastAnim = now;
            if (dispAvg.Length != avg.Length) dispAvg = (float[])avg.Clone();
            float k = 1f - (float)Math.Exp(-dt / 55.0); float mx = 0;
            for (int i = 0; i < avg.Length; i++) { float d = avg[i] - dispAvg[i]; dispAvg[i] += d * k; float ad = d < 0 ? -d : d; if (ad > mx) mx = ad; }
            bool sliding = lastRows > 0 && now - lastRowMs < rowInterval + 20;
            if (sliding || mx > 0.15f) Invalidate();
        }

        void UpdateAutoLevel()
        {
            int[] hist = new int[161]; int n = avg.Length;
            for (int i = 0; i < n; i++) { int v = (int)avg[i]; if (v < 0) v = 0; if (v > 160) v = 160; hist[v]++; }
            int c = 0, lo = 0, hi = 160; int t1 = n * 25 / 100, t2 = n * 997 / 1000;
            bool gotLo = false;
            for (int v = 0; v <= 160; v++) { c += hist[v]; if (!gotLo && c >= t1) { lo = v; gotLo = true; } if (c >= t2) { hi = v; break; } }
            double tl = lo - 3, th = Math.Max(lo + 45, hi + 6);
            autoLow += (tl - autoLow) * 0.04; autoHigh += (th - autoHigh) * 0.04;
            double sl = Math.Max(0, autoLow - 14), sh = Math.Min(160, autoHigh + 22); specLo += (sl - specLo) * 0.05; specHi += (sh - specHi) * 0.05;
            Low = autoLow; High = autoHigh;
        }

        void Reproject(double lo, double hi)
        {
            if (wf == null) return;
            if (wf2 == null || wf2.W != wf.W || wf2.H != wf.H) { Free(wf2); wf2 = Make(wf.W, wf.H); }
            double ow = wfHi - wfLo, nw = hi - lo;
            float x0 = (float)((wfLo - lo) / nw * wf.W), ww = (float)(ow / nw * wf.W);
            using (Graphics g = Graphics.FromImage(wf2.Bmp))
            {
                g.Clear(Color.Black);
                g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.CompositingMode = CompositingMode.SourceCopy; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(wf.Bmp, new RectangleF(x0, 0, ww, wf.H), new RectangleF(0, 0, wf.W, wf.H), GraphicsUnit.Pixel);
            }
            Wf t = wf; wf = wf2; wf2 = t; wfLo = lo; wfHi = hi;
        }

        void EnsureLut()
        {
            if (lut == null || lutKey != PaletteIdx || lutGamma != Gamma || lutVer != Palettes.Version) { lut = Palettes.Build(PaletteIdx, Gamma); lutKey = PaletteIdx; lutGamma = Gamma; lutVer = Palettes.Version; }
        }

        // interpolation cubique (Catmull-Rom) des points reçus vers la largeur d'affichage
        void Resample(byte[] src, float[] dst, int W)
        {
            int n = src.Length;
            if (W >= n)
            {
                float k = (n - 1) / (float)(W - 1);
                for (int x = 0; x < W; x++)
                {
                    float p = x * k; int i = (int)p; float t = p - i;
                    float p0 = src[i > 0 ? i - 1 : 0], p1 = src[i], p2 = src[i + 1 < n ? i + 1 : n - 1], p3 = src[i + 2 < n ? i + 2 : n - 1];
                    float v = 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);
                    dst[x] = v < 0 ? 0 : (v > 160 ? 160 : v);
                }
            }
            else
            {   // plus de points que de pixels : on garde le maximum pour ne perdre aucun signal étroit
                float k = n / (float)W;
                for (int x = 0; x < W; x++)
                {
                    int a = (int)(x * k), b = (int)((x + 1) * k); if (b <= a) b = a + 1; if (b > n) b = n;
                    float m = 0; for (int i = a; i < b; i++) if (src[i] > m) m = src[i]; dst[x] = m;
                }
            }
        }
        void ResampleF(float[] src, float[] dst, int W)
        {
            int n = src.Length; float k = (n - 1) / (float)(W - 1);
            for (int x = 0; x < W; x++)
            {
                float p = x * k; int i = (int)p; float t = p - i;
                float p0 = src[i > 0 ? i - 1 : 0], p1 = src[i], p2 = src[i + 1 < n ? i + 1 : n - 1], p3 = src[i + 2 < n ? i + 2 : n - 1];
                float v = 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);
                dst[x] = v < 0 ? 0 : (v > 160 ? 160 : v);
            }
        }

        void AddRows(ScopeFrame f)
        {
            if (wf == null) return;
            int W = wf.W, H = wf.H;
            if (rowBuf.Length != W) { rowBuf = new float[W]; rowPrev = new float[W]; rowAcc = new float[W]; rowAccN = 0; rowFrac = 0; Array.Clear(rowPrev, 0, W); }
            Resample(f.Data, rowBuf, W);
            EnsureLut();
            bool first = rowPrev[0] == 0 && rowPrev[W / 2] == 0 && rowPrev[W - 1] == 0;
            if (first) Array.Copy(rowBuf, rowPrev, W);
            rowFrac += RowsPerFrame; int rows = (int)rowFrac; rowFrac -= rows;
            // intégration quand la cascade défile plus lentement que le flux de trames
            for (int x = 0; x < W; x++) rowAcc[x] += rowBuf[x]; rowAccN++;
            if (rows <= 0) { return; }
            if (rows > H) rows = H;
            long nowMs = sw.ElapsedMilliseconds; if (lastRowMs > 0) { double d = nowMs - lastRowMs; if (d > 5 && d < 1000) rowInterval += (d - rowInterval) * 0.2; }
            lastRowMs = nowMs; lastRows = rows; rowSeq += rows;
            Array.Copy(wf.Px, 0, wf.Px, W * rows, W * (H - rows));
            Array.Copy(rowTime, 0, rowTime, rows, H - rows);
            double inv = 255.0 / Math.Max(10, High - Low); double low = Low;
            long stamp = Environment.TickCount;
            for (int r = 0; r < rows; r++)
            {
                // la ligne la plus récente est tout en haut ; les lignes intermédiaires sont interpolées dans le temps
                float u = (rows - r) / (float)rows;           // r=0 → 1.0 (actuel)
                int y = r; int o = y * W;
                bool integ = rowAccN > 1 && rows == 1;
                for (int x = 0; x < W; x++)
                {
                    float v = integ ? rowAcc[x] / rowAccN : rowPrev[x] + (rowBuf[x] - rowPrev[x]) * u;
                    int ix = (int)((v - low) * inv); if (ix < 0) ix = 0; else if (ix > 255) ix = 255;
                    wf.Px[o + x] = lut[ix];
                }
                rowTime[y] = stamp;
            }
            Array.Copy(rowBuf, rowPrev, W);
            Array.Clear(rowAcc, 0, W); rowAccN = 0;
        }

        // ------------------------------------------------------------ géométrie
        double XToF(int x) { double lo = WLo, hi = WHi; return lo + (hi - lo) * x / (double)Math.Max(1, ClientSize.Width - 1); }
        float FToX(double f) { return (float)((f - WLo) / (WHi - WLo) * (ClientSize.Width - 1)); }
        double WLo { get { if (haveWin) return winLo; return Rig != null ? Rig.Rx[RxIndex].Freq - Rig.HalfSpan[RxIndex] : 0; } }
        double WHi { get { if (haveWin) return winHi; return Rig != null ? Rig.Rx[RxIndex].Freq + Rig.HalfSpan[RxIndex] : 1; } }

        // largeur et position de la bande passante autour de la porteuse
        void Passband(out double lo, out double hi)
        {
            RxState s = Rig.Rx[RxIndex]; double f = CurFreq, w = s.Width; if (w < 50) w = 2400;
            switch (s.Mode)
            {
                case 1: lo = f; hi = f + w; break;
                case 0: lo = f - w; hi = f; break;
                case 3: lo = f + Pitch() - w / 2; hi = f + Pitch() + w / 2; break;
                case 7: lo = f - Pitch() - w / 2; hi = f - Pitch() + w / 2; break;
                case 4: case 12: lo = f + 1275 - w / 2 + 400; hi = lo + w; break;
                default: lo = f - w / 2; hi = f + w / 2; break;
            }
        }
        // frequence autour de laquelle la cascade est centree : en mode centre, le curseur reste strictement au milieu
        double CurFreq { get { if (haveWin && !winFixed && Rig.ScopeMode[RxIndex] == 0) return (winLo + winHi) / 2; return Rig.Rx[RxIndex].Freq; } }
        double Pitch() { return 300 + Rig.Get(0, "14 09") * 600.0 / 255.0; }

        static string NiceKhz(double hz) { return (hz / 1000.0).ToString("0.###"); }

        // ------------------------------------------------------------ dessin
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; LayoutAll();
            g.Clear(BackColor);
            if (Rig == null) return;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawSpectrum(g);
            DrawAxis(g);
            g.SmoothingMode = SmoothingMode.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.CompositingMode = CompositingMode.SourceCopy; g.PixelOffsetMode = PixelOffsetMode.Half;
            float off = 0;
            if (Smooth && lastRows > 0) { double tt = (sw.ElapsedMilliseconds - lastRowMs) / Math.Max(20.0, rowInterval); if (tt < 1) off = (float)(-(1 - tt) * lastRows); }
            wfOff = off < -0.02f ? off : 0f;
            if (off > -0.02f) g.DrawImageUnscaled(wf.Bmp, rWf.X, rWf.Y);
            else
            {
                GraphicsState gs = g.Save(); g.SetClip(rWf); g.InterpolationMode = InterpolationMode.Bilinear;
                using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    PointF[] dp = { new PointF(rWf.X, rWf.Y + off), new PointF(rWf.X + wf.W, rWf.Y + off), new PointF(rWf.X, rWf.Y + off + wf.H) };
                    g.DrawImage(wf.Bmp, dp, new RectangleF(0, 0, wf.W, wf.H), GraphicsUnit.Pixel, ia);
                }
                g.Restore(gs);
            }
            g.CompositingMode = CompositingMode.SourceOver; g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawWfOverlay(g);
            DrawHover(g);
            using (Pen p = new Pen(Color.FromArgb(60, 70, 85))) { g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1); }
        }

        void DrawSpectrum(Graphics g)
        {
            Rectangle r = rSpec; int W = r.Width;
            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(12, 16, 24), Color.FromArgb(4, 6, 10), 90f)) g.FillRectangle(bg, r);
            // grille
            double lo = WLo, hi = WHi;
            double major = NiceStep((hi - lo) / 8.0);
            using (Pen gp = new Pen(Color.FromArgb(34, 44, 58)))
            {
                for (double f = Math.Ceiling(lo / major) * major; f <= hi; f += major) { float x = FToX(f); g.DrawLine(gp, x, r.Top, x, r.Bottom); }
                for (int d = (int)(Math.Ceiling(specLo / 20) * 20); d <= specHi; d += 20) { float y = Y(d); g.DrawLine(gp, 0, y, W, y); }
            }
            // niveaux dB (échelle relative au niveau de référence)
            int refT = Rig.RefTenth[RxIndex];
            using (Brush tb = new SolidBrush(Color.FromArgb(120, 150, 170, 190)))
                for (int d = (int)(Math.Ceiling(specLo / 20) * 20); d <= specHi - 8; d += 20) { double db = refT / 10.0 - (160 - d) / 2.0; g.DrawString(db.ToString("0") + " dB", fSmall, tb, 3, Y(d) - 14); }
            if (nPts < 8) { using (Brush b = new SolidBrush(Color.FromArgb(150, 160, 175))) g.DrawString(Rig.Connected ? "En attente des données du scope…" : "Non connecté", fBig, b, 14, r.Top + 14); return; }
            if (ShowBandPlan) DrawBandPlan(g);
            // bande passante dans le spectre
            double plo, phi; Passband(out plo, out phi);
            float x1 = FToX(plo), x2 = FToX(phi);
            using (Brush pb = new SolidBrush(Color.FromArgb(38, ScopeColors.Pass))) g.FillRectangle(pb, Math.Min(x1, x2), r.Top, Math.Max(2, Math.Abs(x2 - x1)), r.Height);
            // courbe
            if (rowBuf.Length != W) { }
            if (pts.Length != W + 2) pts = new PointF[W + 2];
            if (rowBuf.Length < 2) return;
            float[] disp = new float[W]; ResampleF(Smooth && dispAvg.Length == avg.Length ? dispAvg : avg, disp, W);
            for (int x = 0; x < W; x++) pts[x + 1] = new PointF(x, Y(disp[x]));
            pts[0] = new PointF(0, r.Bottom); pts[W + 1] = new PointF(W - 1, r.Bottom);
            Color top = Palettes.Top(PaletteIdx);
            float[] pk = null;
            if (PeakHold)
            {
                pk = new float[W]; ResampleF(peak, pk, W);
                PointF[] pp = new PointF[W]; for (int x = 0; x < W; x++) pp[x] = new PointF(x, Y(pk[x]));
                using (Pen p = new Pen(Color.FromArgb(120, ScopeColors.Peak), 1f)) g.DrawLines(p, pp);
            }
            if (FillSpectrum)
                using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(170, top), Color.FromArgb(25, 20, 60, 160), 90f)) g.FillPolygon(lb, pts);
            PointF[] line = new PointF[W]; Array.Copy(pts, 1, line, 0, W);
            using (Pen lp = new Pen(Color.FromArgb(235, ScopeColors.Line), 1.4f)) { lp.LineJoin = LineJoin.Round; g.DrawLines(lp, line); }
            if (ShowPeaks) DrawPeaks(g, pk != null ? pk : disp, W);
            // marqueurs
            DrawMarkers(g, r, true);
            // légende
            string sp = winFixed ? "FIXE" : "±" + NiceKhz((hi - lo) / 2) + " kHz";
            string head = Title + "   " + sp + "   Réf " + (refT / 10.0).ToString("+0.0;-0.0;0") + " dB   " + (oor ? "HORS PLAGE  " : "") + Fps + " img/s";
            using (Brush b = new SolidBrush(Color.FromArgb(190, 210, 225, 240))) g.DrawString(head, fMed, b, W - 10 - g.MeasureString(head, fMed).Width, 4);
        }

        double specLo = 0, specHi = 160;
        float Y(double raw) { double t = (raw - specLo) / (specHi - specLo); if (t < 0) t = 0; if (t > 1) t = 1; return (float)(rSpec.Bottom - 2 - t * (rSpec.Height - 6)); }

        static double NiceStep(double raw)
        {
            double[] b = { 1, 2, 2.5, 5 }; double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            for (int i = 0; i < b.Length; i++) if (b[i] * mag >= raw) return b[i] * mag;
            return 10 * mag;
        }

        void DrawAxis(Graphics g)
        {
            Rectangle r = rAxis; double lo = WLo, hi = WHi;
            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(30, 38, 50), Color.FromArgb(16, 20, 28), 90f)) g.FillRectangle(bg, r);
            double major = NiceStep((hi - lo) / 9.0), minor = major / 10; if ((minor / (hi - lo)) * rAxis.Width < 4) minor = major / 5;
            using (Pen tp = new Pen(Color.FromArgb(110, 130, 150)))
            using (Brush tb = new SolidBrush(Color.FromArgb(215, 225, 235)))
            {
                for (double f = Math.Ceiling(lo / minor) * minor; f <= hi; f += minor)
                { float x = FToX(f); double q = f / major; bool mj = Math.Abs(q - Math.Round(q)) < 1e-6; bool md = !mj && Math.Abs(q * 2 - Math.Round(q * 2)) < 1e-6; g.DrawLine(tp, x, r.Top, x, r.Top + (mj ? 8 : (md ? 5 : 3))); }
                for (double f = Math.Ceiling(lo / major) * major; f <= hi; f += major)
                {
                    float x = FToX(f); string s = FormatAxis(f, major);
                    SizeF sz = g.MeasureString(s, fSmall); float tx = x - sz.Width / 2; if (tx < 1) tx = 1; if (tx + sz.Width > r.Width - 1) tx = r.Width - sz.Width - 1;
                    g.DrawString(s, fSmall, tb, tx, r.Top + 7);
                }
            }
            DrawMarkers(g, r, false);
        }
        static string FormatAxis(double f, double step)
        {
            double mhz = f / 1e6;
            if (step >= 100000) return mhz.ToString("0.0##");
            if (step >= 1000) return mhz.ToString("0.000");
            return mhz.ToString("0.0000");
        }

        void DrawBandPlan(Graphics g)
        {
            double lo = WLo, hi = WHi;
            for (int i = 0; i < BandPlan.Seg.Length; i++)
            {
                int[] s = BandPlan.Seg[i]; double a = s[0] * 1000.0, b = s[1] * 1000.0; if (b < lo || a > hi) continue;
                float x1 = Math.Max(0, FToX(a)), x2 = Math.Min(ClientSize.Width, FToX(b));
                using (Brush br = new SolidBrush(Color.FromArgb(200, BandPlan.Col[s[2]]))) g.FillRectangle(br, x1, rSpec.Bottom - 5, Math.Max(1, x2 - x1), 4);
            }
        }

        void DrawMarkers(Graphics g, Rectangle r, bool spec)
        {
            RxState s = Rig.Rx[RxIndex];
            double plo, phi; Passband(out plo, out phi);
            float xc = FToX(CurFreq);
            // porteuse / curseur de réception
            using (Pen p = new Pen(Color.FromArgb(235, IsSub ? ScopeColors.CursorSub : ScopeColors.CursorMain), 1.6f)) g.DrawLine(p, xc, r.Top, xc, r.Bottom);
            if (!spec) { using (Brush b = new SolidBrush(IsSub ? ScopeColors.CursorSub : ScopeColors.CursorMain)) g.FillPolygon(b, new PointF[] { new PointF(xc - 5, r.Top), new PointF(xc + 5, r.Top), new PointF(xc, r.Top + 6) }); }
            // RIT
            if (Rig.RitOn && RxIndex == Rig.Sel) { float xr = FToX(s.Freq + Rig.Rit); using (Pen p = new Pen(Color.FromArgb(200, 80, 255, 120), 1.2f) { DashStyle = DashStyle.Dash }) g.DrawLine(p, xr, r.Top, xr, r.Bottom); }
            // fréquence d'émission en split
            if (Rig.Split && RxIndex == 0)
            {
                float xs = FToX(Rig.Rx[1].Freq);
                if (xs > 0 && xs < ClientSize.Width) { using (Pen p = new Pen(Color.FromArgb(230, 255, 140, 0), 1.4f) { DashStyle = DashStyle.Dash }) g.DrawLine(p, xs, r.Top, xs, r.Bottom); if (spec) using (Brush b = new SolidBrush(Color.FromArgb(255, 150, 0))) g.DrawString("TX", fMed, b, xs + 3, r.Top + 2); }
            }
        }

        void DrawPeaks(Graphics g, float[] disp, int W)
        {
            peakHit.Clear(); peakF.Clear();
            List<int> cand = new List<int>();
            double thr = Low + (High - Low) * 0.42;
            for (int x = 3; x < W - 3; x++) { float v = disp[x]; if (v > thr && v >= disp[x - 1] && v > disp[x + 1] && v >= disp[x - 2] && v >= disp[x + 2] && v >= disp[x - 3] && v >= disp[x + 3]) cand.Add(x); }
            cand.Sort(delegate(int a, int b) { return disp[b].CompareTo(disp[a]); });
            List<int> keep = new List<int>();
            foreach (int x in cand) { bool ok = true; foreach (int k in keep) if (Math.Abs(k - x) < 62) { ok = false; break; } if (ok) keep.Add(x); if (keep.Count >= 8) break; }
            using (Brush tb = new SolidBrush(Color.FromArgb(225, 255, 255, 200)))
            using (Brush bb = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                foreach (int x in keep)
                {
                    double f = XToF(x); string s = (f / 1000.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    SizeF sz = g.MeasureString(s, fSmall); float y = Y(disp[x]) - 16; if (y < rSpec.Top + 2) y = rSpec.Top + 2;
                    RectangleF rc = new RectangleF(Math.Max(2, Math.Min(W - sz.Width - 2, x - sz.Width / 2)), y, sz.Width, sz.Height - 2);
                    g.FillRectangle(bb, rc); g.DrawString(s, fSmall, tb, rc.X, rc.Y - 1);
                    peakHit.Add(Rectangle.Round(rc)); peakF.Add((long)f);
                }
        }

        void DrawWfOverlay(Graphics g)
        {
            Rectangle r = rWf; if (nPts < 8) return;
            double plo, phi; Passband(out plo, out phi);
            float x1 = FToX(plo), x2 = FToX(phi);
            using (Brush pb = new SolidBrush(Color.FromArgb(34, ScopeColors.Pass))) g.FillRectangle(pb, Math.Min(x1, x2), r.Top, Math.Max(2, Math.Abs(x2 - x1)), r.Height);
            using (Pen p = new Pen(Color.FromArgb(110, 255, 255, 255), 1f)) { g.DrawLine(p, x1, r.Top, x1, r.Bottom); g.DrawLine(p, x2, r.Top, x2, r.Bottom); }
            DrawMarkers(g, r, false);
            // heure UTC qui descend avec la cascade, un repere toutes les 4 lignes de texte
            int lh = (int)Math.Ceiling(fSmall.GetHeight()); int spc = Math.Max(8, lh * 4);
            DateTime nowUtc = DateTime.UtcNow; int nowTick = Environment.TickCount;
            using (Brush tb = new SolidBrush(Color.FromArgb(170, 200, 215, 230))) using (Pen tp = new Pen(Color.FromArgb(80, 200, 215, 230)))
            {
                for (int y = 0; y < rowTime.Length && y < r.Height; y++)
                {
                    if ((rowSeq - y) % spc != 0) continue; long tt = rowTime[y]; if (tt == 0) continue;
                    float yy = r.Top + y + wfOff; if (yy < r.Top || yy > r.Bottom - lh / 2f) continue;
                    string txt = nowUtc.AddMilliseconds(-(nowTick - tt)).ToString("HH:mm:ss");
                    SizeF tsz = g.MeasureString(txt, fSmall); using (Brush bb = new SolidBrush(Color.FromArgb(150, 4, 6, 10))) g.FillRectangle(bb, 7, yy - lh / 2f, tsz.Width, lh); g.DrawLine(tp, 0, yy, 6, yy); g.DrawString(txt, fSmall, tb, 8, yy - lh / 2f);
                }
            }
        }
        void DrawHover(Graphics g)
        {
            if (!mouseIn || mouse.X < 0 || nPts < 8) return;
            if (mouse.Y >= rAxis.Top && mouse.Y < rAxis.Bottom) return;
            double f = XToF(mouse.X);
            using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1f) { DashStyle = DashStyle.Dot }) g.DrawLine(p, mouse.X, 0, mouse.X, ClientSize.Height);
            string s = FreqText((long)f);
            if (mouse.Y < rSpec.Bottom)
            {
                int i = (int)((f - WLo) / (WHi - WLo) * (nPts - 1)); i = Math.Max(0, Math.Min(nPts - 1, i));
                double db = Rig.RefTenth[RxIndex] / 10.0 - (160 - avg[i]) / 2.0; s += "   " + db.ToString("0") + " dB";
            }
            string bp = BandInfo((long)f); if (bp != null) s += "   [" + bp + "]";
            SizeF sz = g.MeasureString(s, fMed);
            float tx = mouse.X + 14; if (tx + sz.Width + 6 > ClientSize.Width) tx = mouse.X - sz.Width - 18;
            float ty = Math.Max(4, mouse.Y - 28);
            using (Brush bb = new SolidBrush(Color.FromArgb(210, 14, 18, 26))) g.FillRectangle(bb, tx - 3, ty - 1, sz.Width + 8, sz.Height + 2);
            using (Brush tb = new SolidBrush(Color.FromArgb(255, 255, 240, 150))) g.DrawString(s, fMed, tb, tx, ty);
        }
        public static string FreqText(long hz)
        {
            long mhz = hz / 1000000, k = (hz / 1000) % 1000, h = hz % 1000;
            return mhz + "." + k.ToString("000") + "." + h.ToString("000");
        }
        static string BandInfo(long hz)
        {
            long k = hz / 1000;
            for (int i = 0; i < BandPlan.Seg.Length; i++) { int[] s = BandPlan.Seg[i]; if (k >= s[0] && k < s[1]) return BandPlan.Nom[s[2]]; }
            return null;
        }

        // ------------------------------------------------------------ souris
        long SnapFreq(double f, bool fine)
        {
            long step = fine ? 10 : 100; RxState s = Rig.Rx[RxIndex];
            long hz = (long)(Math.Round(f / step) * step);
            if (s.Mode == 3) hz -= (long)Pitch(); else if (s.Mode == 7) hz += (long)Pitch();
            else if (s.Mode == 0) hz = hz;                      // LSB : la porteuse est à droite de la bande
            return hz;
        }
        void TuneAt(int x, bool fine)
        {
            if (Rig == null || nPts < 8) return;
            long hz = SnapFreq(XToF(x), fine);
            if (RxIndex != Rig.Sel) Rig.SelectRx(RxIndex);
            Rig.SetFreq(RxIndex, hz);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            if (Rig == null) return;
            if (e.Button == MouseButtons.Left)
            {
                if (Math.Abs(e.Y - rAxis.Top - rAxis.Height / 2) < 14 && e.Y >= rAxis.Top - 4 && e.Y < rAxis.Bottom + 3 && Control.ModifierKeys == Keys.Alt) { dragSplit = true; return; }
                for (int i = 0; i < peakHit.Count; i++) if (peakHit[i].Contains(e.Location)) { if (RxIndex != Rig.Sel) Rig.SelectRx(RxIndex); long f = peakF[i]; Rig.SetFreq(RxIndex, SnapFreq(f, false)); return; }
                if (e.Y >= rAxis.Top && e.Y < rAxis.Bottom) { dragSplit = true; return; }
                dragging = true; TuneAt(e.X, (Control.ModifierKeys & Keys.Shift) != 0);
            }
            else if (e.Button == MouseButtons.Middle) { Rig.SetFreq(RxIndex, Rig.Rx[RxIndex].Freq); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); mouse = e.Location; mouseIn = true;
            if (dragging) TuneAt(e.X, (Control.ModifierKeys & Keys.Shift) != 0);
            else if (dragSplit) { SplitRatio = Math.Max(0.12, Math.Min(0.8, e.Y / (double)ClientSize.Height)); Invalidate(); }
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; dragSplit = false; }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); mouseIn = false; Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e); if (Rig == null) return;
            int dir = e.Delta > 0 ? 1 : -1;
            if ((Control.ModifierKeys & Keys.Control) != 0) { Zoom(dir); return; }
            long step = (Control.ModifierKeys & Keys.Shift) != 0 ? 1000 : StepHz();
            if (RxIndex != Rig.Sel) Rig.SelectRx(RxIndex);
            Rig.SetFreq(RxIndex, Rig.Rx[RxIndex].Freq + dir * step);
        }
        public long StepHz()
        {
            switch (Rig.Get(0, "10")) { case 0: return 10; case 1: return 100; case 2: return 1000; case 3: return 5000; case 4: return 9000; case 5: return 10000; case 6: return 12500; case 7: return 20000; case 8: return 25000; }
            return 100;
        }
        public void Zoom(int dir)    // dir>0 : zoom avant (span plus étroit)
        {
            long h = Rig.HalfSpan[RxIndex]; int i = 0; for (; i < Spans.Length - 1; i++) if (Spans[i] >= h) break;
            i = Math.Max(0, Math.Min(Spans.Length - 1, i - dir));
            Rig.ScopeSetSpan(RxIndex, Spans[i]);
        }

        // ------------------------------------------------------------ menu contextuel
        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            ToolStripMenuItem pal = new ToolStripMenuItem("Palette de la cascade");
            for (int i = 0; i < Palettes.Names.Length; i++) { int k = i; ToolStripMenuItem it = new ToolStripMenuItem(Palettes.Names[i]); it.Click += delegate { PaletteIdx = k; Invalidate(); }; pal.DropDownItems.Add(it); }
            m.Items.Add(pal);
            m.Items.Add(Toggle("Defilement fluide", delegate(bool v) { Smooth = v; }, delegate { return Smooth; }));
            m.Items.Add(Toggle("Niveau automatique", delegate(bool v) { AutoLevel = v; }, delegate { return AutoLevel; }));
            m.Items.Add(Toggle("Maintien des crêtes", delegate(bool v) { PeakHold = v; }, delegate { return PeakHold; }));
            m.Items.Add(Toggle("Plan de bandes", delegate(bool v) { ShowBandPlan = v; }, delegate { return ShowBandPlan; }));
            m.Items.Add(Toggle("Fréquences des signaux", delegate(bool v) { ShowPeaks = v; }, delegate { return ShowPeaks; }));
            m.Items.Add(Toggle("Remplissage du spectre", delegate(bool v) { FillSpectrum = v; }, delegate { return FillSpectrum; }));
            ToolStripMenuItem av = new ToolStripMenuItem("Moyennage du spectre");
            string[] an = { "Aucun", "2 trames", "3 trames", "4 trames", "6 trames" }; int[] av2 = { 1, 2, 3, 4, 6 };
            for (int i = 0; i < an.Length; i++) { int k = av2[i]; ToolStripMenuItem it = new ToolStripMenuItem(an[i]); it.Click += delegate { AvgFrames = k; }; av.DropDownItems.Add(it); }
            m.Items.Add(av);
            ToolStripMenuItem gm = new ToolStripMenuItem("Contraste (gamma)");
            double[] gv = { 0.6, 0.75, 0.85, 1.0, 1.25, 1.6 }; string[] gn = { "Très doux", "Doux", "Normal", "Linéaire", "Contrasté", "Très contrasté" };
            for (int i = 0; i < gv.Length; i++) { double k = gv[i]; ToolStripMenuItem it = new ToolStripMenuItem(gn[i]); it.Click += delegate { Gamma = k; }; gm.DropDownItems.Add(it); }
            m.Items.Add(gm);
            m.Items.Add("Effacer la cascade", null, delegate { ClearWaterfall(); });
            m.Items.Add("Enregistrer l'image…", null, delegate { SaveImage(); });
            m.Opening += delegate { foreach (KeyValuePair<ToolStripMenuItem, Getter> kv in chk) kv.Key.Checked = kv.Value(); };
            return m;
        }
        delegate bool Getter(); delegate void Setter(bool v);
        readonly List<KeyValuePair<ToolStripMenuItem, Getter>> chk = new List<KeyValuePair<ToolStripMenuItem, Getter>>();
        ToolStripMenuItem Toggle(string text, Setter set, Getter get)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text); it.CheckOnClick = true; it.Checked = get();
            it.Click += delegate { set(it.Checked); };
            chk.Add(new KeyValuePair<ToolStripMenuItem, Getter>(it, get));
            return it;
        }
        public void SaveImage()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "PNG|*.png"; d.FileName = "IC7760-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
                if (d.ShowDialog() != DialogResult.OK) return;
                using (Bitmap b = new Bitmap(Width, Height)) { DrawToBitmap(b, new Rectangle(0, 0, Width, Height)); b.Save(d.FileName, ImageFormat.Png); }
            }
        }
    }
}
