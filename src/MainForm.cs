using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace IC7760Link
{
    public class MainForm : Form
    {
        public const string Version = "2.6";
        readonly Rig rig = new Rig();
        readonly ScopeView[] views = new ScopeView[2];
        readonly List<Action> links = new List<Action>();
        readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        int lastVersion = -1, lastFull;
        MonitorForm monitor; SetForm setForm;

        // haut
        FreqDisplay fdMain, fdSub; MeterBar mS0, mS1, mPo, mSwr, mAlc, mComp, mVd, mId;
        DarkCombo cbPort, cbBaud, tbAddr; Btn btnConnect, btnPtt; Label lbStatus, lbClock, lbTx;
        // barre de modes
        readonly Dictionary<int, Btn> modeBtn = new Dictionary<int, Btn>(); readonly Btn[] filBtn = new Btn[4], dataBtn = new Btn[4];
        Btn bSplit, bDual, bRit, bXit, bTuner; DarkCombo cbStep; Knob kRit;
        // scope
        Panel scopeHost; readonly Dictionary<string, Btn> sBtn = new Dictionary<string, Btn>();
        Btn bScMain, bScSub, bScDualV, bScDualH, bAuto, bHold, bPeaks, bPlan, bSpeed, bAvg, bSweep; Label lbRef; DarkCombo cbPal; Knob kLow, kHigh;
        int layoutMode = 0;
        // console
        Btn[] bandBtn; int memCh = 1; Label lbMem; TextBox tbCw; Knob kWidth;
        readonly List<KeyValuePair<string, long>> dummy = new List<KeyValuePair<string, long>>();

        static readonly string[] BandName = { "1.8", "3.5", "5", "7", "10", "14", "18", "21", "24", "28", "50", "WWV" };
        static readonly long[] BandFreq = { 1840000, 3700000, 5357000, 7100000, 10136000, 14200000, 18130000, 21300000, 24950000, 28400000, 50150000, 10000000 };
        static readonly int[] BandMode = { 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 2 };

        int Rx { get { return rig.Sel; } }

        public MainForm()
        {
            Text = "IC-7760 Link " + Version + " — télécommande USB (CI-V)";
            Theme.Load(); Palettes.LoadCfg(); ScopeColors.Load();
            AutoScaleMode = AutoScaleMode.None;   // l'échelle est gérée par la « scène » (voir plus bas)
            try { using (Graphics gd = CreateGraphics()) T.Dpi = gd.DpiX; } catch { }
            BackColor = T.Bg; ForeColor = T.Text; Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(DesignW, DesignH); StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true; KeyPreview = true;
            try { Icon = MakeIcon(); } catch { }

            // l'ordre d'ajout fixe l'empilement des panneaux « Dock »
            BuildScopeArea(); BuildConsole(); BuildScopeBar(); BuildModeBar(); BuildTop();
            Controls.SetChildIndex(scopeHost, 0);
            BuildStage();

            rig.Heard += delegate(byte src) { MonitorForm m = monitor; if (m != null && !m.IsDisposed) m.Heard(src); };
            rig.Log += delegate(string s, bool tx) { MonitorForm m = monitor; if (m != null && !m.IsDisposed) m.Add(s, tx); };
            uiTimer.Interval = 10; uiTimer.Tick += OnTick; uiTimer.Start();
            Load += delegate { RestoreSettings(); ApplyScopeLayout(); };
            FormClosing += OnClosing;
        }

        // ============================================================ partie haute
        void BuildTop()
        {
            Panel top = new Panel(); top.Dock = DockStyle.Top; top.Height = 190; top.BackColor = T.Bg; Controls.Add(top);

            Panel right = new Panel(); right.Dock = DockStyle.Right; right.Width = 330; right.BackColor = T.Bg; top.Controls.Add(right);
            Panel left = new Panel(); left.Dock = DockStyle.Left; left.Width = 920; left.BackColor = T.Bg; top.Controls.Add(left);
            Panel mid = new Panel(); mid.Dock = DockStyle.Fill; mid.BackColor = T.Bg; top.Controls.Add(mid);
            top.Controls.SetChildIndex(mid, 0);

            fdMain = new FreqDisplay(); fdMain.Caption = "MAIN"; fdMain.SetBounds(8, 8, 520, 134); fdMain.Accent = T.Amber;
            fdSub = new FreqDisplay(); fdSub.Caption = "SUB"; fdSub.SetBounds(536, 8, 376, 134); fdSub.Accent = T.Blue;
            fdMain.Activated += delegate { if (rig.Sel != 0) rig.SelectRx(0); }; fdSub.Activated += delegate { if (rig.Sel != 1) rig.SelectRx(1); };
            fdMain.FreqEdited += delegate(long f) { rig.SetFreq(0, f); }; fdSub.FreqEdited += delegate(long f) { rig.SetFreq(1, f); };
            left.Controls.Add(fdMain); left.Controls.Add(fdSub);

            string[] sT = { "1", "5", "9", "+20", "+40", "+60" }; double[] sP = { 15 / 241.0, 67 / 241.0, 120 / 241.0, 160 / 241.0, 201 / 241.0, 1.0 };
            TableLayoutPanel tl = new TableLayoutPanel(); tl.Dock = DockStyle.Fill; tl.ColumnCount = 2; tl.RowCount = 4; tl.BackColor = T.Bg;
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 4; i++) tl.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            mid.Controls.Add(tl);
            mS0 = Meter("S MAIN", 241, 120 / 241.0, sT, sP, delegate(int v) { return Rig.SText(v); }, tl, 0, 0); mS0.OverAt = 120 / 241.0;
            mS1 = Meter("S SUB", 241, 120 / 241.0, sT, sP, delegate(int v) { return Rig.SText(v); }, tl, 0, 1); mS1.OverAt = 120 / 241.0; mS1.Cool = T.Blue;
            mPo = Meter("Po", 212, 0.675, new string[] { "0", "50", "100", "150", "200" }, new double[] { 0, 0.337, 0.675, 0.837, 1 }, delegate(int v) { return Rig.ToWatts(v).ToString("0") + " W"; }, tl, 1, 0);
            mSwr = Meter("SWR", 255, 0.31, new string[] { "1", "1.5", "2", "3", "∞" }, new double[] { 0, 0.19, 0.31, 0.47, 1 }, delegate(int v) { return Rig.ToSwr(v).ToString("0.0"); }, tl, 1, 1);
            mAlc = Meter("ALC", 140, 0.86, new string[] { "0", "ALC" }, new double[] { 0, 0.86 }, delegate(int v) { return (v * 100 / 120) + "%"; }, tl, 2, 0);
            mComp = Meter("COMP", 241, 0.7, new string[] { "0", "15", "30" }, new double[] { 0, 0.54, 1 }, delegate(int v) { return (v * 30 / 241) + " dB"; }, tl, 2, 1);
            mVd = Meter("Vd", 241, 0.9, new string[] { "0", "20", "40", "50" }, new double[] { 0, 0.286, 0.57, 0.80 }, delegate(int v) { return (v * 44 / 151.0).ToString("0.0") + " V"; }, tl, 3, 0);
            mId = Meter("Id", 241, 0.75, new string[] { "0", "5", "10", "15" }, new double[] { 0, 0.32, 0.685, 1 }, delegate(int v) { return (v * 15 / 241.0).ToString("0.0") + " A"; }, tl, 3, 1);

            analog = new AnalogMeter(); analog.Dock = DockStyle.Fill; analog.Visible = false; analog.S[0] = mS0; analog.S[1] = mS1; analog.Po = mPo; analog.Swr = mSwr; analog.Alc = mAlc; analog.Comp = mComp; analog.Vd = mVd; analog.Id = mId;
            mid.Controls.Add(analog); meterTable = tl;
            MouseEventHandler flip = delegate(object so, MouseEventArgs me) { if (me.Button == MouseButtons.Right) SetAnalog(!analog.Visible); };
            tl.MouseUp += flip; analog.MouseUp += flip; foreach (Control mc in tl.Controls) mc.MouseUp += flip;
            analog.MetI = Math.Max(0, Math.Min(5, Cfg.GetInt("met", 0))); analog.MetChanged += delegate { Cfg.SetInt("met", analog.MetI); };
            SetAnalog(Cfg.GetBool("analogmeter", false));
            // liaison
            int y = 6;
            Label l1 = new Label(); l1.Text = "Liaison USB (CI-V)"; l1.ForeColor = T.Amber; l1.Font = new Font("Segoe UI Semibold", 8.5f); l1.SetBounds(6, y, 200, 16); right.Controls.Add(l1);
            cbPort = new DarkCombo(); cbPort.SetBounds(6, y + 18, 318, 24); right.Controls.Add(cbPort);
            cbBaud = new DarkCombo(); cbBaud.SetBounds(6, y + 48, 96, 24); foreach (string b in new string[] { "115200", "57600", "38400", "19200", "9600", "230400", "460800" }) cbBaud.Items.Add(b); cbBaud.SelectedIndex = 0; right.Controls.Add(cbBaud);
            Label l2 = new Label(); l2.Text = "Adresse"; l2.ForeColor = T.Dim; l2.SetBounds(104, y + 52, 58, 18); right.Controls.Add(l2);
            tbAddr = new DarkCombo(); tbAddr.DropDownStyle = ComboBoxStyle.DropDown; tbAddr.Items.Add("94"); tbAddr.Items.Add("B2"); tbAddr.Text = "94"; tbAddr.MaxLength = 2; tbAddr.SetBounds(162, y + 48, 42, 24); right.Controls.Add(tbAddr);
            btnConnect = new Btn(); btnConnect.Text = "CONNECTER"; btnConnect.SetBounds(208, y + 46, 116, 28); btnConnect.Accent = T.Green; right.Controls.Add(btnConnect); btnConnect.Click += delegate { ToggleConnect(); };
            lbStatus = new Label(); lbStatus.SetBounds(6, y + 78, 284, 34); lbStatus.ForeColor = T.Dim; lbStatus.Font = new Font("Segoe UI", 8.5f); right.Controls.Add(lbStatus);
            btnPtt = new Btn(); btnPtt.Text = "ÉMISSION (PTT)"; btnPtt.SetBounds(6, y + 114, 250, 26); btnPtt.Accent = T.Red; right.Controls.Add(btnPtt); btnPtt.Click += delegate { rig.SetPtt(!rig.Ptt); };
            Btn bh = new Btn(); bh.Text = "?"; bh.Led = false; bh.SetBounds(260, y + 114, 30, 26); right.Controls.Add(bh); bh.Click += delegate { ShowHelp(); };
            Btn bo = new Btn(); bo.Text = "\u2699"; bo.Led = false; bo.SetBounds(294, y + 84, 26, 26); right.Controls.Add(bo); bo.Click += delegate { ShowOptions(); };
            Btn bx = new Btn(); bx.Text = "⇅"; bx.Led = false; bx.SetBounds(294, y + 114, 26, 26); right.Controls.Add(bx); bx.Click += delegate { ShowConfig(); };
            lbClock = new Label(); lbClock.SetBounds(214, y, 112, 18); lbClock.ForeColor = T.Cyan; lbClock.TextAlign = ContentAlignment.MiddleRight; lbClock.Font = new Font("Consolas", 9.5f, FontStyle.Bold); right.Controls.Add(lbClock);

            cbPort.Items.Add("Poste virtuel (simulateur IC-7760)");
            foreach (string p in SerialPort.GetPortNames()) cbPort.Items.Add(p);
            cbPort.SelectedIndex = 0;
        }

        AnalogMeter analog; TableLayoutPanel meterTable;
        void SetAnalog(bool on) { analog.Visible = on; meterTable.Visible = !on; Cfg.SetBool("analogmeter", on); }

        MeterBar Meter(string title,  double max, double hot, string[] tt, double[] tp, Func<int, string> fmt, TableLayoutPanel tl, int row, int col)
        {
            MeterBar m = new MeterBar(); m.Title = title; m.Max = max; m.Hot = hot; m.TickText = tt; m.TickPos = tp; m.Fmt = fmt; m.Dock = DockStyle.Fill; m.Margin = new Padding(6, 2, 6, 2);
            tl.Controls.Add(m, col, row); return m;
        }

        // ============================================================ modes / VFO
        void BuildModeBar()
        {
            FlowLayoutPanel f = Flow(); Controls.Add(f);
            int[] mc = { 0, 1, 3, 7, 4, 8, 2, 5, 12, 13 }; string[] mn = { "LSB", "USB", "CW", "CW-R", "RTTY", "RTTY-R", "AM", "FM", "PSK", "PSK-R" };
            for (int i = 0; i < mc.Length; i++) { int code = mc[i]; Btn b = FB(f, mn[i], 56); modeBtn[code] = b; b.Click += delegate { rig.SetMode(Rx, code, rig.Rx[Rx].Data, rig.Rx[Rx].Filter == 0 ? 1 : rig.Rx[Rx].Filter); }; }
            Sep(f);
            for (int i = 1; i <= 3; i++) { int n = i; Btn b = FB(f, "FIL" + i, 50); filBtn[i] = b; b.Click += delegate { rig.SetMode(Rx, rig.Rx[Rx].Mode, rig.Rx[Rx].Data, n); }; }
            Sep(f);
            for (int i = 1; i <= 3; i++) { int n = i; Btn b = FB(f, "DATA" + i, 56); b.Accent = T.Cyan; dataBtn[i] = b; b.Click += delegate { rig.SetMode(Rx, rig.Rx[Rx].Mode, rig.Rx[Rx].Data == n ? 0 : n, rig.Rx[Rx].Filter == 0 ? 1 : rig.Rx[Rx].Filter); }; }
            Sep(f);
            Btn sw = FB(f, "M⇄S", 52); sw.Led = false; sw.Click += delegate { rig.Swap(); };
            Btn eq = FB(f, "M=S", 52); eq.Led = false; eq.Click += delegate { rig.Equalize(); };
            bDual = FB(f, "DUALWATCH", 96); bDual.Click += delegate { rig.SetDual(!rig.Dual); };
            bSplit = FB(f, "SPLIT", 62); bSplit.Click += delegate { rig.SetSplit(!rig.Split); };
            bRit = FB(f, "RIT", 48); bRit.Accent = T.Green; bRit.Click += delegate { rig.SetRit(!rig.RitOn); };
            bXit = FB(f, "XIT", 48); bXit.Accent = T.Green; bXit.Click += delegate { rig.SetXit(!rig.XitOn); };
            Btn rz = FB(f, "RIT 0", 52); rz.Led = false; rz.Click += delegate { rig.SetRitFreq(0); };
            kRit = new Knob(); kRit.Label = "RIT"; kRit.Min = -1000; kRit.Max = 1000; kRit.Center = true; kRit.Default = 0; kRit.Size = new Size(54, 40); kRit.Fmt = delegate(int v) { return v.ToString("+0;-0;0"); };
            kRit.UserChanged += delegate(int v) { rig.SetRitFreq(v); }; f.Controls.Add(kRit);
            Sep(f);
            bTuner = FB(f, "TUNER", 62); bTuner.Click += delegate { rig.Write(0, "1C 01", rig.Get(0, "1C 01") == 0 ? 1 : 0); };
            Btn tu = FB(f, "TUNE", 58); tu.Led = false; tu.Click += delegate { rig.Tune(); };
            Sep(f);
            Label ls = new Label(); ls.Text = "Pas"; ls.ForeColor = T.Dim; ls.AutoSize = true; ls.Margin = new Padding(4, 8, 0, 0); f.Controls.Add(ls);
            cbStep = new DarkCombo(); cbStep.Width = 88; cbStep.Margin = new Padding(2, 4, 4, 0);
            foreach (string s in new string[] { "10 Hz", "100 Hz", "1 kHz", "5 kHz", "9 kHz", "10 kHz", "12,5 kHz", "20 kHz", "25 kHz" }) cbStep.Items.Add(s);
            cbStep.SelectedIndex = 1; cbStep.SelectedIndexChanged += delegate { if (cbStep.Focused) rig.Write(0, "10", cbStep.SelectedIndex); };
            f.Controls.Add(cbStep);
            links.Add(delegate
            {
                RxState s = rig.Rx[Rx];
                foreach (KeyValuePair<int, Btn> kv in modeBtn) kv.Value.Set(kv.Key == s.Mode, null);
                for (int i = 1; i <= 3; i++) { filBtn[i].Set(s.Filter == i, null); dataBtn[i].Set(s.Data == i, null); }
                bDual.Set(rig.Dual, null); bSplit.Set(rig.Split, null); bRit.Set(rig.RitOn, null); bXit.Set(rig.XitOn, null); bTuner.Set(rig.Get(0, "1C 01") != 0, null);
                kRit.SetValue(rig.Rit);
                int st = rig.Get(0, "10"); if (st >= 0 && st < cbStep.Items.Count && !cbStep.DroppedDown && cbStep.SelectedIndex != st) cbStep.SelectedIndex = st;
            });
        }
        FlowLayoutPanel Flow()
        {
            FlowLayoutPanel f = new FlowLayoutPanel(); f.Dock = DockStyle.Top; f.AutoSize = true; f.AutoSizeMode = AutoSizeMode.GrowAndShrink; f.BackColor = T.Bg; f.Padding = new Padding(6, 2, 6, 2); f.WrapContents = true; return f;
        }
        Btn FB(FlowLayoutPanel f, string text, int w) { Btn b = new Btn(); b.Text = text; b.Size = new Size(w, 28); b.Margin = new Padding(2, 3, 2, 3); f.Controls.Add(b); return b; }
        void Sep(FlowLayoutPanel f) { Panel p = new Panel(); p.Size = new Size(10, 28); p.Margin = new Padding(2, 3, 2, 3); f.Controls.Add(p); }

        // ============================================================ barre du scope
        void BuildScopeBar()
        {
            FlowLayoutPanel f = Flow(); f.BackColor = Color.FromArgb(16, 20, 28); Controls.Add(f);
            bScMain = FB(f, "SCOPE MAIN", 92); bScSub = FB(f, "SCOPE SUB", 88); bScDualV = FB(f, "DUAL ↕", 70); bScDualH = FB(f, "DUAL ↔", 70);
            bScMain.Click += delegate { layoutMode = 0; ApplyScopeLayout(); }; bScSub.Click += delegate { layoutMode = 1; ApplyScopeLayout(); };
            bScDualV.Click += delegate { layoutMode = 2; ApplyScopeLayout(); }; bScDualH.Click += delegate { layoutMode = 3; ApplyScopeLayout(); };
            Sep(f);
            string[] mn = { "CENTRE", "FIXE", "SCROLL-C", "SCROLL-F" };
            for (int i = 0; i < 4; i++) { int m = i; Btn b = FB(f, mn[i], i >= 2 ? 82 : 66); sBtn["m" + i] = b; b.Click += delegate { rig.ScopeSetMode(Rx, m); }; }
            Sep(f);
            Btn zo = FB(f, "−", 30); zo.Led = false; zo.Click += delegate { views[Rx].Zoom(-1); };
            for (int i = 0; i < ScopeView.Spans.Length; i++)
            { long sp = ScopeView.Spans[i]; Btn b = FB(f, "±" + (sp / 1000.0).ToString("0.#"), 52); sBtn["s" + sp] = b; b.Click += delegate { rig.ScopeSetSpan(Rx, sp); }; }
            Btn zi = FB(f, "+", 30); zi.Led = false; zi.Click += delegate { views[Rx].Zoom(1); };
            Sep(f);
            Btn rm = FB(f, "RÉF −", 54); rm.Led = false; rm.Click += delegate { rig.ScopeSetRef(Rx, rig.RefTenth[Rx] - 50); };
            lbRef = new Label(); lbRef.Size = new Size(64, 26); lbRef.TextAlign = ContentAlignment.MiddleCenter; lbRef.ForeColor = T.Text; lbRef.Margin = new Padding(0, 4, 0, 0); f.Controls.Add(lbRef);
            Btn rp = FB(f, "RÉF +", 54); rp.Led = false; rp.Click += delegate { rig.ScopeSetRef(Rx, rig.RefTenth[Rx] + 50); };
            bSweep = FB(f, "BALAYAGE", 80); bSweep.Click += delegate { rig.ScopeSetSweep(Rx, (rig.Sweep[Rx] + 1) % 3); };
            bHold = FB(f, "HOLD", 56); bHold.Click += delegate { rig.ScopeSetHold(!rig.ScopeHold); };
            Sep(f);
            Label lp = new Label(); lp.Text = "Cascade"; lp.ForeColor = T.Dim; lp.AutoSize = true; lp.Margin = new Padding(2, 8, 0, 0); f.Controls.Add(lp);
            cbPal = new DarkCombo(); cbPal.Width = 110; cbPal.Margin = new Padding(2, 4, 4, 0); foreach (string s in Palettes.Names) cbPal.Items.Add(s); cbPal.SelectedIndex = 0;
            cbPal.SelectedIndexChanged += delegate { foreach (ScopeView v in views) if (v != null) v.PaletteIdx = cbPal.SelectedIndex; Cfg.SetInt("palette", cbPal.SelectedIndex); }; f.Controls.Add(cbPal);
            bAuto = FB(f, "AUTO", 56); bAuto.Click += delegate { bool v = !views[0].AutoLevel; foreach (ScopeView s in views) { s.AutoLevel = v; } };
            kLow = SmallKnob(f, "Bas", 0, 120, 30); kHigh = SmallKnob(f, "Haut", 40, 160, 130);
            kLow.UserChanged += delegate(int v) { foreach (ScopeView s in views) { s.AutoLevel = false; s.Low = v; } }; kHigh.UserChanged += delegate(int v) { foreach (ScopeView s in views) { s.AutoLevel = false; s.High = v; } };
            bSpeed = FB(f, "VITESSE 1×", 90); bSpeed.Led = false; bSpeed.MouseUp += delegate(object so, MouseEventArgs me) { if (me.Button == MouseButtons.Left) StepSpeed(1); else if (me.Button == MouseButtons.Right) StepSpeed(-1); };
            bAvg = FB(f, "MOY 2", 66); bAvg.Led = false; bAvg.MouseUp += delegate(object so, MouseEventArgs me) { if (me.Button == MouseButtons.Right) StepAvg(1); else if (me.Button == MouseButtons.Left) StepAvg(-1); };
            bPeaks = FB(f, "PICS", 52); bPeaks.MouseUp += delegate(object so, MouseEventArgs me) { if (me.Button == MouseButtons.Left) { bool v = !views[0].PeakHold; foreach (ScopeView s in views) s.PeakHold = v; } else if (me.Button == MouseButtons.Right) PeakMenu(bPeaks); };
            bPlan = FB(f, "PLAN", 56); bPlan.Click += delegate { bool v = !views[0].ShowBandPlan; foreach (ScopeView s in views) s.ShowBandPlan = v; };
            Btn cl = FB(f, "EFFACER", 70); cl.Led = false; cl.Click += delegate { foreach (ScopeView s in views) s.ClearWaterfall(); };
            links.Add(delegate
            {
                bScMain.Set(layoutMode == 0, null); bScSub.Set(layoutMode == 1, null); bScDualV.Set(layoutMode == 2, null); bScDualH.Set(layoutMode == 3, null);
                for (int i = 0; i < 4; i++) sBtn["m" + i].Set(rig.ScopeMode[Rx] == i, null);
                foreach (long sp in ScopeView.Spans) sBtn["s" + sp].Set(rig.HalfSpan[Rx] == sp, null);
                lbRef.Text = (rig.RefTenth[Rx] / 10.0).ToString("+0.0;-0.0;0.0") + " dB";
                string[] sw = { "RAPIDE", "MOYEN", "LENT" }; bSweep.Set(false, "BAL. " + sw[Math.Min(2, rig.Sweep[Rx])]); bSweep.Led = false;
                bHold.Set(rig.ScopeHold, null); bAuto.Set(views[0].AutoLevel, null); bPeaks.Set(views[0].PeakHold, null); bPlan.Set(views[0].ShowBandPlan, null);
                kLow.SetValue((int)views[0].Low); kHigh.SetValue((int)views[0].High);
            });
        }
        Knob SmallKnob(FlowLayoutPanel f, string label, int min, int max, int val)
        { Knob k = new Knob(); k.Label = label; k.Min = min; k.Max = max; k.Value = val; k.Size = new Size(50, 56); k.Margin = new Padding(0, 0, 0, 0); k.Fmt = delegate(int v) { return v.ToString(); }; f.Controls.Add(k); return k; }
        readonly double[] speeds = { 0.1, 0.2, 0.3, 0.5, 0.75, 1, 1.5, 2, 3, 4, 5 }; int speedI = 5; int avgN = 2;
        void StepSpeed(int d) { speedI = (speedI + d + speeds.Length) % speeds.Length; ApplySpeed(); }
        void ApplySpeed() { foreach (ScopeView s in views) s.RowsPerFrame = speeds[speedI]; bSpeed.Text = "VITESSE " + speeds[speedI].ToString("0.##") + "×"; Cfg.SetInt("wfspeed2", speedI); }
        void StepAvg(int d) { avgN = (avgN - 1 + d + 20) % 20 + 1; ApplyAvg(); }
        void ApplyAvg() { foreach (ScopeView s in views) s.AvgFrames = avgN; bAvg.Text = "MOY " + avgN; Cfg.SetInt("avg2", avgN); }
        void PeakMenu(Control anchor)
        {
            ContextMenuStrip cm = new ContextMenuStrip(); ToolStripMenuItem h = new ToolStripMenuItem("Maintien des crêtes"); h.Enabled = false; cm.Items.Add(h);
            for (int s = 0; s <= 10; s++)
            {
                int k = s; ToolStripMenuItem it = new ToolStripMenuItem(s == 0 ? "Normal (comportement d'origine)" : "Maintien " + s + " s en plus"); it.Checked = Math.Abs(views[0].PeakHoldSec - s) < 0.01;
                it.Click += delegate { foreach (ScopeView v in views) v.PeakHoldSec = k; Cfg.SetInt("peakhold", k); }; cm.Items.Add(it);
            }
            cm.Show(Cursor.Position);
        }
        // ============================================================ zone des scopes
        void BuildScopeArea()
        {
            scopeHost = new Panel(); scopeHost.Dock = DockStyle.Fill; scopeHost.BackColor = T.Bg; scopeHost.Padding = new Padding(6, 2, 6, 2); Controls.Add(scopeHost);
            for (int i = 0; i < 2; i++)
            {
                ScopeView v = new ScopeView(); v.Rig = rig; v.RxIndex = i; v.Title = i == 0 ? "MAIN" : "SUB"; views[i] = v; scopeHost.Controls.Add(v);
            }
            scopeHost.Resize += delegate { LayoutScopes(); };
        }
        void ApplyScopeLayout()
        {
            if (layoutMode == 0) { rig.ScopeSetDual(false); rig.ScopeSetRx(0); }
            else if (layoutMode == 1) { rig.ScopeSetDual(false); rig.ScopeSetRx(1); }
            else rig.ScopeSetDual(true);
            LayoutScopes(); Cfg.SetInt("scopelayout", layoutMode);
        }
        void LayoutScopes()
        {
            if (views[0] == null) return;
            Rectangle r = Rectangle.Inflate(scopeHost.ClientRectangle, -scopeHost.Padding.Left, -scopeHost.Padding.Top); r.Height = scopeHost.ClientSize.Height - 4; r.Width = scopeHost.ClientSize.Width - 12;
            views[0].Visible = layoutMode != 1; views[1].Visible = layoutMode != 0;
            switch (layoutMode)
            {
                case 0: views[0].SetBounds(6, 2, r.Width, r.Height); break;
                case 1: views[1].SetBounds(6, 2, r.Width, r.Height); break;
                case 2: { int h = (r.Height - 4) / 2; views[0].SetBounds(6, 2, r.Width, h); views[1].SetBounds(6, 2 + h + 4, r.Width, h); break; }
                default: { int w = (r.Width - 4) / 2; views[0].SetBounds(6, 2, w, r.Height); views[1].SetBounds(6 + w + 4, 2, w, r.Height); break; }
            }
        }

        // ============================================================ console du bas
        void BuildConsole()
        {
            Panel host = new Panel(); host.Dock = DockStyle.Bottom; host.Height = 232; host.BackColor = T.Bg; host.AutoScroll = true; Controls.Add(host);
            int x = 6;
            // ---- bandes + mémoires
            Group gb = G(host, "Bandes · Mémoires", ref x, 296);
            bandBtn = new Btn[BandName.Length];
            for (int i = 0; i < BandName.Length; i++)
            {
                int k = i; Btn b = new Btn(); b.Text = BandName[i]; b.Led = false; b.SetBounds(8 + (i % 4) * 70, 24 + (i / 4) * 34, 66, 30); gb.Controls.Add(b); bandBtn[i] = b; b.Click += delegate { GoBand(k); };
            }
            Label ml = new Label(); ml.Text = "Mémoire"; ml.ForeColor = T.Dim; ml.SetBounds(8, 134, 60, 18); gb.Controls.Add(ml);
            Btn mm = Mini(gb, "◀", 8, 154, 30); mm.Click += delegate { memCh = memCh <= 1 ? 99 : memCh - 1; UpdMem(); rig.Memory(0, memCh); };
            lbMem = new Label(); lbMem.SetBounds(40, 156, 62, 24); lbMem.ForeColor = T.Amber; lbMem.Font = new Font("Consolas", 11f, FontStyle.Bold); lbMem.TextAlign = ContentAlignment.MiddleCenter; gb.Controls.Add(lbMem); UpdMem();
            Btn mp = Mini(gb, "▶", 104, 154, 30); mp.Click += delegate { memCh = memCh >= 99 ? 1 : memCh + 1; UpdMem(); rig.Memory(0, memCh); };
            Btn mr = Mini(gb, "MÉM", 140, 154, 48); mr.Click += delegate { rig.Memory(0, memCh); };
            Btn mw = Mini(gb, "ÉCRIRE", 192, 154, 60); mw.Click += delegate { rig.Memory(0x09, 0); };
            Btn mv = Mini(gb, "M→V", 8, 190, 50); mv.Click += delegate { rig.Memory(0x0A, 0); };
            Btn mc = Mini(gb, "EFFACER", 62, 190, 70); mc.Click += delegate { if (MessageBox.Show(this, "Effacer la mémoire " + memCh + " ?", "Mémoire", MessageBoxButtons.YesNo) == DialogResult.Yes) rig.Memory(0x0B, 0); };
            Btn vf = Mini(gb, "VFO", 136, 190, 50); vf.Click += delegate { rig.Raw((byte)0x07); };
            Btn sc = Mini(gb, "SCAN", 190, 190, 62); sc.Click += delegate { rig.Raw(new byte[] { 0x0E, 0x01 }); }; Btn ss = Mini(gb, "STOP", 256, 190, 34); ss.Click += delegate { rig.Raw(new byte[] { 0x0E, 0x00 }); };

            // ---- récepteur
            Group gr = G(host, "Récepteur (actif : voie MAIN/SUB sélectionnée)", ref x, 384);
            int kx = 6;
            RKnob(gr, ref kx, "AF", "14 01", 0, 255, Pct, false, true); RKnob(gr, ref kx, "RF", "14 02", 0, 255, Pct, false, true); RKnob(gr, ref kx, "SQL", "14 03", 0, 255, Pct, false, true);
            RKnob(gr, ref kx, "NR", "14 06", 0, 255, Pct, false, true); RKnob(gr, ref kx, "NB", "14 12", 0, 255, Pct, false, true); RKnob(gr, ref kx, "NOTCH", "14 0D", 0, 255, PctC, true, true);
            string[] preT = { "PRE OFF", "PRE 1", "PRE 2" }; Cycle(gr, 8, 100, 92, "16 02", preT, true);
            Cycle(gr, 102, 100, 92, "11", new string[] { "ATT OFF", "ATT 6", "ATT 12", "ATT 18", "ATT 24", "ATT 30", "ATT 36", "ATT 45" }, true, new int[] { 0, 6, 12, 18, 24, 30, 36, 45 });
            Cycle(gr, 196, 100, 92, "16 12", new string[] { "?", "AGC FAST", "AGC MID", "AGC SLOW" }, true, new int[] { 1, 1, 2, 3 }, 1);
            Toggle(gr, 290, 100, 92, "16 65", "IP+", true);
            Toggle(gr, 8, 132, 92, "16 22", "NB", true); Toggle(gr, 102, 132, 92, "16 40", "NR", true); Toggle(gr, 196, 132, 92, "16 41", "ANF", true); Toggle(gr, 290, 132, 92, "16 48", "NOTCH", true);
            Cycle(gr, 8, 164, 92, "16 32", new string[] { "APF OFF", "APF WIDE", "APF MID", "APF NAR" }, true);
            Toggle(gr, 102, 164, 92, "16 4F", "TPF", true); Toggle(gr, 196, 164, 92, "16 4E", "DIGI-SEL", true);
            Cycle(gr, 290, 164, 92, "16 56", new string[] { "FIL SHARP", "FIL SOFT" }, true);
            Cycle(gr, 8, 196, 92, "16 57", new string[] { "N-LARGE", "N-MOYEN", "N-ÉTROIT" }, true);
            Toggle(gr, 102, 196, 92, "12 00", "ANT RX I/O", true);

            // ---- filtre
            Group gf = G(host, "Filtre · PBT · CW", ref x, 330);
            kWidth = new Knob(); kWidth.Label = "LARGEUR"; kWidth.Min = 0; kWidth.Max = 40; kWidth.Size = new Size(66, 74); kWidth.Location = new Point(6, 22); kWidth.Accent = T.Amber;
            kWidth.Fmt = delegate(int v) { int w = Widths.FromIndex(rig.Rx[Rx].Mode, v); return w >= 1000 ? (w / 1000.0).ToString("0.0#") + "k" : w + " Hz"; };
            kWidth.UserChanged += delegate(int v) { rig.SetWidth(Rx, Widths.FromIndex(rig.Rx[Rx].Mode, v)); }; gf.Controls.Add(kWidth);
            links.Add(delegate { RxState s = rig.Rx[Rx]; kWidth.Max = s.Mode == 2 ? 49 : 40; kWidth.SetValue(Widths.ToIndex(s.Mode, s.Width)); });
            kx = 72;
            RKnob(gf, ref kx, "PBT 1", "14 07", 0, 255, PctC, true, true); RKnob(gf, ref kx, "PBT 2", "14 08", 0, 255, PctC, true, true);
            RKnob(gf, ref kx, "PITCH", "14 09", 0, 255, delegate(int v) { return (300 + v * 600 / 255) + " Hz"; }, false, false);
            RKnob(gf, ref kx, "APF", "14 05", 0, 255, PctC, true, true);
            int[] pre = { 50, 250, 500, 1000, 1800, 2400, 3000, 3600 }; string[] pn = { "50", "250", "500", "1k", "1.8k", "2.4k", "3k", "3.6k" };
            for (int i = 0; i < pre.Length; i++) { int w = pre[i]; Btn b = Mini(gf, pn[i], 8 + (i % 4) * 80, 104 + (i / 4) * 32, 76); b.Click += delegate { rig.SetWidth(Rx, Math.Max(Widths.Min(rig.Rx[Rx].Mode), Math.Min(Widths.Max(rig.Rx[Rx].Mode), w))); }; }
            Label lw = new Label(); lw.Text = "Préréglages de largeur de filtre IF (Hz)"; lw.ForeColor = T.Dim; lw.Font = new Font("Segoe UI", 7.5f); lw.SetBounds(8, 172, 340, 16); gf.Controls.Add(lw);
            Toggle(gf, 8, 192, 96, "16 50", "VERROU", false); Toggle(gf, 108, 192, 100, "16 5E", "SUIVI M/S", false);

            // ---- émission
            Group gt = G(host, "Émission", ref x, 410);
            kx = 6;
            RKnob(gt, ref kx, "PUISS.", "14 0A", 0, 255, delegate(int v) { return (v * 200 / 255) + " W"; }, false, false); RKnob(gt, ref kx, "MICRO", "14 0B", 0, 255, Pct, false, false); RKnob(gt, ref kx, "COMP", "14 0E", 0, 255, delegate(int v) { return (v * 10 / 255.0).ToString("0.0"); }, false, false);
            RKnob(gt, ref kx, "DRIVE", "14 14", 0, 255, Pct, false, false); RKnob(gt, ref kx, "MONI", "14 15", 0, 255, Pct, false, false); RKnob(gt, ref kx, "VOX", "14 16", 0, 255, Pct, false, false);
            Toggle(gt, 8, 100, 92, "16 44", "COMP", false); Toggle(gt, 102, 100, 92, "16 45", "MONITEUR", false); Toggle(gt, 196, 100, 92, "16 46", "VOX", false);
            Cycle(gt, 290, 100, 116, "16 47", new string[] { "BK-IN OFF", "SEMI BK-IN", "FULL BK-IN" }, false);
            Cycle(gt, 8, 132, 126, "16 58", new string[] { "SSB LARGE", "SSB MOYEN", "SSB ÉTROIT" }, false);
            Toggle(gt, 138, 132, 100, "1C 02", "XFC", false);
            Btn pw = Mini(gt, "100 W", 8, 168, 56); pw.Click += delegate { rig.Write(0, "14 0A", 128); }; Btn p50 = Mini(gt, "50 W", 68, 168, 56); p50.Click += delegate { rig.Write(0, "14 0A", 64); }; Btn p10 = Mini(gt, "10 W", 128, 168, 56); p10.Click += delegate { rig.Write(0, "14 0A", 13); }; Btn pm = Mini(gt, "MAX", 188, 168, 56); pm.Click += delegate { rig.Write(0, "14 0A", 255); };
            Label lp = new Label(); lp.Text = "Préréglages de puissance (0–200 W)"; lp.ForeColor = T.Dim; lp.Font = new Font("Segoe UI", 7.5f); lp.SetBounds(8, 202, 300, 16); gt.Controls.Add(lp);

            // ---- CW
            Group gc = G(host, "CW · Voix", ref x, 400);
            kx = 6; RKnob(gc, ref kx, "VITESSE", "14 0C", 0, 255, delegate(int v) { return (6 + v * 42 / 255) + " WPM"; }, false, false); RKnob(gc, ref kx, "DÉLAI BK", "14 0F", 0, 255, delegate(int v) { return (2.0 + v * 11.0 / 255).ToString("0.0") + " d"; }, false, false);
            tbCw = new TextBox(); tbCw.SetBounds(130, 28, 168, 24); tbCw.BackColor = T.Bg; tbCw.ForeColor = T.Text; tbCw.BorderStyle = BorderStyle.FixedSingle; tbCw.CharacterCasing = CharacterCasing.Upper; gc.Controls.Add(tbCw);
            tbCw.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SendCwText(tbCw.Text); e.SuppressKeyPress = true; } };
            Btn cs = Mini(gc, "ENVOYER", 304, 27, 84); cs.Click += delegate { SendCwText(tbCw.Text); };
            Btn cx = Mini(gc, "STOP CW", 304, 58, 84); cx.Click += delegate { rig.StopCw(); };
            string[] mac = { "CQ CQ DE {C} {C} K", "{C} 599 73", "TU DE {C} K", "QRZ? DE {C}" };
            for (int i = 0; i < 4; i++) { int k = i; Btn b = Mini(gc, "F" + (i + 1), 130 + i * 42, 58, 38); b.Led = false; b.Click += delegate { SendCwText(MacroText(k)); }; b.MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) EditMacro(k); }; macroDefault[i] = mac[i]; }
            Label lc = new Label(); lc.Text = "F1–F4 : messages (clic droit pour modifier) — Entrée envoie le texte"; lc.ForeColor = T.Dim; lc.Font = new Font("Segoe UI", 7.5f); lc.SetBounds(130, 88, 262, 16); gc.Controls.Add(lc);
            Label lv = new Label(); lv.Text = "Mémoires vocales (T1–T8)"; lv.ForeColor = T.Dim; lv.SetBounds(8, 112, 200, 16); gc.Controls.Add(lv);
            for (int i = 1; i <= 8; i++) { int n = i; Btn b = Mini(gc, "T" + i, 8 + ((i - 1) % 4) * 56, 132 + ((i - 1) / 4) * 32, 52); b.Click += delegate { rig.Raw(new byte[] { 0x28, 0x00, (byte)n }); }; }
            Btn vs = Mini(gc, "STOP", 236, 132, 70); vs.Click += delegate { rig.Raw(new byte[] { 0x28, 0x00, 0x00 }); };
            Label ld = new Label(); ld.Text = "Indicatif ({C}) :"; ld.ForeColor = T.Dim; ld.SetBounds(236, 168, 90, 18); gc.Controls.Add(ld);
            TextBox tc = new TextBox(); tc.SetBounds(322, 165, 72, 22); tc.BackColor = T.Bg; tc.ForeColor = T.Text; tc.BorderStyle = BorderStyle.FixedSingle; tc.CharacterCasing = CharacterCasing.Upper; tc.Text = Cfg.Get("call", ""); tc.TextChanged += delegate { Cfg.Set("call", tc.Text); }; gc.Controls.Add(tc);
        }
        readonly string[] macroDefault = new string[4];
        string MacroText(int i) { return Cfg.Get("macro" + i, macroDefault[i]).Replace("{C}", Cfg.Get("call", "").Trim()); }
        void EditMacro(int i) { string v = Prompt.Ask("Message F" + (i + 1) + " ({C} = votre indicatif) :", "Message CW", Cfg.Get("macro" + i, macroDefault[i])); if (v != null) Cfg.Set("macro" + i, v.ToUpperInvariant()); }
        void SendCwText(string t) { if (string.IsNullOrWhiteSpace(t)) return; rig.SendCw(t.ToUpperInvariant()); }

        Group G(Panel host, string title, ref int x, int w)
        { Group g = new Group(title); g.SetBounds(x, 4, w, 222); host.Controls.Add(g); x += w + 6; return g; }
        Btn Mini(Control parent, string text, int x, int y, int w) { Btn b = new Btn(); b.Text = text; b.Led = false; b.SetBounds(x, y, w, 28); parent.Controls.Add(b); return b; }
        static string Pct(int v) { return (v * 100 / 255) + "%"; }
        static string PctC(int v) { int p = (v - 128) * 100 / 128; return p.ToString("+0;-0;0") + "%"; }
        void UpdMem() { lbMem.Text = "M" + memCh.ToString("00"); }

        void RKnob(Control parent, ref int x, string label, string key, int min, int max, Func<int, string> fmt, bool center, bool perRx)
        {
            Knob k = new Knob(); k.Label = label; k.Min = min; k.Max = max; k.Fmt = fmt; k.Center = center; k.Location = new Point(x, 22); k.Size = new Size(62, 74); if (center) k.Default = 128;
            k.UserChanged += delegate(int v) { rig.Write(perRx ? Rx : 0, key, v); };
            parent.Controls.Add(k); links.Add(delegate { k.SetValue(rig.Get(Rx, key)); }); x += 62;
        }
        void Toggle(Control parent, int x, int y, int w, string key, string text, bool perRx)
        {
            Btn b = new Btn(); b.Text = text; b.SetBounds(x, y, w, 28); parent.Controls.Add(b);
            b.Click += delegate { rig.Write(perRx ? Rx : 0, key, rig.Get(Rx, key) != 0 ? 0 : 1); };
            links.Add(delegate { b.Set(rig.Get(Rx, key) != 0, null); });
        }
        void Cycle(Control parent, int x, int y, int w, string key, string[] labels, bool perRx) { Cycle(parent, x, y, w, key, labels, perRx, null, 0); }
        void Cycle(Control parent, int x, int y, int w, string key, string[] labels, bool perRx, int[] values) { Cycle(parent, x, y, w, key, labels, perRx, values, 0); }
        void Cycle(Control parent, int x, int y, int w, string key, string[] labels, bool perRx, int[] values, int firstIdx)
        {
            Btn b = new Btn(); b.SetBounds(x, y, w, 28); b.Text = labels[0]; parent.Controls.Add(b);
            // values : valeur CI-V pour chaque libellé (sinon l'indice) ; firstIdx : premier libellé utilisable
            b.Click += delegate
            {
                int cur = rig.Get(Rx, key); int idx = 0;
                if (values == null) idx = cur; else { for (int i = firstIdx; i < values.Length; i++) if (values[i] == cur) { idx = i; break; } }
                int next = idx + 1; if (next >= labels.Length) next = firstIdx;
                rig.Write(perRx ? Rx : 0, key, values == null ? next : values[next]);
            };
            links.Add(delegate
            {
                int cur = rig.Get(Rx, key); int idx = 0;
                if (values == null) idx = Math.Max(0, Math.Min(labels.Length - 1, cur)); else { for (int i = firstIdx; i < values.Length; i++) if (values[i] == cur) { idx = i; break; } }
                bool on = values == null ? cur != 0 : cur != 0 || (key == "16 12");
                b.Set(on, labels[idx]);
            });
        }

        void GoBand(int i)
        {
            RxState s = rig.Rx[Rx]; int cur = BandOf(s.Freq);
            if (cur >= 0) Cfg.Set("bsr." + cur, s.Freq + "," + s.Mode);
            long f = BandFreq[i]; int m = BandMode[i];
            string v = Cfg.Get("bsr." + i, ""); if (v.Length > 0) { string[] p = v.Split(','); long ff; int mm; if (p.Length == 2 && long.TryParse(p[0], out ff) && int.TryParse(p[1], out mm)) { f = ff; m = mm; } }
            rig.SetMode(Rx, m, 0, Math.Max(1, s.Filter)); rig.SetFreq(Rx, f);
        }
        static int BandOf(long f)
        {
            long[] lo = { 1800000, 3500000, 5250000, 7000000, 10100000, 14000000, 18068000, 21000000, 24890000, 28000000, 50000000 }, hi = { 2000000, 3800000, 5450000, 7200000, 10150000, 14350000, 18168000, 21450000, 24990000, 29700000, 54000000 };
            for (int i = 0; i < lo.Length; i++) if (f >= lo[i] && f <= hi[i]) return i;
            return -1;
        }

        // ============================================================ connexion
        void ToggleConnect()
        {
            if (rig.Link != null) { rig.Disconnect(); views[0].ClearWaterfall(); views[1].ClearWaterfall(); btnConnect.Set(false, "CONNECTER"); return; }
            byte addr = 0x94; try { addr = Convert.ToByte(tbAddr.Text.Trim(), 16); } catch { MessageBox.Show(this, "Adresse CI-V invalide (hexadécimal, ex. 94 ou B2).", "Liaison"); return; }
            ILink link;
            if (cbPort.SelectedIndex == 0) link = new VirtualLink(addr);
            else link = new SerialLink((string)cbPort.SelectedItem, int.Parse((string)cbBaud.SelectedItem));
            string err;
            if (!rig.Connect(link, addr, out err)) { MessageBox.Show(this, "Connexion impossible : " + err, "Liaison", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            btnConnect.Set(true, "DÉCONNECTER"); Cfg.Set("port", (string)cbPort.SelectedItem); Cfg.Set("baud", (string)cbBaud.SelectedItem); Cfg.Set("addr", tbAddr.Text);
            ApplyScopeLayout();
        }

        void ShowMonitor()
        {
            if (monitor == null || monitor.IsDisposed)
            {
                monitor = new MonitorForm(); monitor.GetPort = delegate { return cbPort.Text; }; monitor.RigBusy = delegate { return rig.Link != null; };
                monitor.Found = delegate(int baud, byte addr) { cbBaud.Text = baud.ToString(); tbAddr.Text = addr.ToString("X2"); };
            }
            monitor.Show(); monitor.BringToFront();
        }
        bool importing; int connSeen; OptionsForm optForm;
        void ShowOptions()
        {
            if (optForm == null || optForm.IsDisposed)
            {
                optForm = new OptionsForm(delegate { ShowSet(); }, delegate { ShowMonitor(); });
                optForm.ThemeChanged += delegate(Dictionary<int, Color> map) { foreach (Form fm in Application.OpenForms) Theme.Recolor(fm, map); };
                optForm.Changed += delegate { int cu = Palettes.Names.Length - 1; if (cbPal.SelectedIndex != cu) cbPal.SelectedIndex = cu; foreach (ScopeView v in views) if (v != null) v.Invalidate(); };
            }
            optForm.Show(this); optForm.BringToFront();
        }
        void ShowConfig()
        {
            Cfg.SetInt("w", ClientSize.Width); Cfg.SetInt("h", ClientSize.Height); Cfg.Save();
            using (ConfigForm cf = new ConfigForm(RadioDump(), rig.Connected))
            {
                cf.ShowDialog(this);
                if (cf.Imported) { importing = true; Application.Restart(); Close(); }
            }
        }

        // Réglages du poste relevés en direct (tout ce qui est affiché sur les potentiomètres et boutons) sous forme clé=valeur
        static readonly string[] noExport = { "1C 01", "1C 02" };     // jamais de commande d'accord/émission à la restauration
        string RadioDump()
        {
            if (!rig.Connected) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int r = 0; r < 2; r++)
            {
                RxState s = rig.Rx[r]; string p = "radio.rx" + r + ".";
                sb.AppendLine(p + "freq=" + s.Freq); sb.AppendLine(p + "mode=" + s.Mode); sb.AppendLine(p + "data=" + s.Data); sb.AppendLine(p + "filter=" + s.Filter); sb.AppendLine(p + "width=" + s.Width);
                sb.AppendLine(p + "span=" + rig.HalfSpan[r]); sb.AppendLine(p + "ref=" + rig.RefTenth[r]); sb.AppendLine(p + "scopemode=" + rig.ScopeMode[r]); sb.AppendLine(p + "sweep=" + rig.Sweep[r]);
            }
            sb.AppendLine("radio.split=" + (rig.Split ? 1 : 0)); sb.AppendLine("radio.dual=" + (rig.Dual ? 1 : 0)); sb.AppendLine("radio.rit=" + (rig.RitOn ? 1 : 0)); sb.AppendLine("radio.xit=" + (rig.XitOn ? 1 : 0)); sb.AppendLine("radio.ritfreq=" + rig.Rit);
            List<string> keys = new List<string>(Rig.RxKeys); keys.AddRange(Rig.GlobalKeys);
            foreach (string key in keys)
            {
                if (Array.IndexOf(noExport, key) >= 0) continue; CatEntry e = Catalog.Find(key); if (e == null) continue;
                if (e.Direct29) { for (int r = 0; r < 2; r++) if (rig.Rx[r].V.ContainsKey(key)) sb.AppendLine("radio.rx" + r + "." + key + "=" + rig.Rx[r].V[key]); }
                else if (rig.Rx[0].V.ContainsKey(key)) sb.AppendLine("radio.g." + key + "=" + rig.Rx[0].V[key]);
            }
            return sb.ToString();
        }

        // Renvoie au poste les réglages "radio.*" ; retourne un compte rendu
        string ApplyRadio(Dictionary<string, string> kv)
        {
            if (!rig.Connected) return "Le poste n'est pas connecté : connectez-le d'abord.";
            int n = 0; long L; int I;
            Func<string, bool> has = delegate(string k) { return kv.ContainsKey(k); };
            Func<string, long> num = delegate(string k) { long v; return long.TryParse(kv[k], out v) ? v : 0; };
            int skipped = 0;
            for (int r = 0; r < 2; r++)
            {
                string p = "radio.rx" + r + ".";
                if (has(p + "mode")) { rig.SetMode(r, (int)num(p + "mode"), has(p + "data") ? (int)num(p + "data") : 0, has(p + "filter") ? (int)Math.Max(1, num(p + "filter")) : 1); n++; }
                if (has(p + "width")) { rig.SetWidth(r, (int)num(p + "width")); n++; }
                if (has(p + "freq") && (L = num(p + "freq")) > 0) { rig.SetFreq(r, L); n++; }
                if (has(p + "span") && (L = num(p + "span")) > 0) { rig.ScopeSetSpan(r, L); n++; }
                if (has(p + "ref")) { rig.ScopeSetRef(r, (int)num(p + "ref")); n++; }
                if (has(p + "scopemode")) { rig.ScopeSetMode(r, (int)num(p + "scopemode")); n++; }
                if (has(p + "sweep")) { rig.ScopeSetSweep(r, (int)num(p + "sweep")); n++; }
            }
            if (has("radio.dual")) { rig.SetDual(num("radio.dual") != 0); n++; }
            if (has("radio.split")) { rig.SetSplit(num("radio.split") != 0); n++; }
            if (has("radio.rit")) { rig.SetRit(num("radio.rit") != 0); n++; }
            if (has("radio.xit")) { rig.SetXit(num("radio.xit") != 0); n++; }
            if (has("radio.ritfreq")) { rig.SetRitFreq((int)num("radio.ritfreq")); n++; }
            List<string> keys = new List<string>(Rig.RxKeys); keys.AddRange(Rig.GlobalKeys);
            foreach (string key in keys)
            {
                if (Array.IndexOf(noExport, key) >= 0) continue; CatEntry e = Catalog.Find(key); if (e == null) continue;
                if (e.Direct29) { for (int r = 0; r < 2; r++) { string k = "radio.rx" + r + "." + key; if (has(k) && int.TryParse(kv[k], out I)) { rig.Write(r, key, I); n++; } } }
                else { string k = "radio.g." + key; if (has(k) && int.TryParse(kv[k], out I)) { rig.Write(0, key, I); n++; } }
            }
            if (n == 0) return "Aucun réglage du poste trouvé dans ce texte (lignes radio.…=…).";
            return n + " réglages envoyés au poste (fréquences, modes, filtres, potentiomètres, boutons, scope).\nLe poste met quelques secondes à les prendre en compte.";
        }
        void ShowSet() { if (setForm == null || setForm.IsDisposed) setForm = new SetForm(rig); setForm.Show(); setForm.BringToFront(); }
        void ShowHelp()
        {
            MessageBox.Show(this,
                "SOURIS SUR LA CASCADE / LE SPECTRE\n" +
                "• Clic ou glisser : accorde la fréquence (Maj = pas de 10 Hz)\n" +
                "• Molette : fréquence par pas (Maj = 1 kHz)\n" +
                "• Ctrl + molette : zoom (span)\n" +
                "• Clic sur une étiquette de signal : accord direct\n" +
                "• Clic droit : palette, niveau auto, moyennage, plan de bandes, image\n\n" +
                "AFFICHEUR DE FRÉQUENCE\n" +
                "• Clic en haut/bas d'un chiffre ou molette dessus : +/− sur ce chiffre\n" +
                "• Double-clic : saisie directe (14074 = kHz ; 14.074 = MHz)\n\n" +
                "POTENTIOMÈTRES : glisser verticalement, molette, double-clic = valeur par défaut (Maj = réglage fin)\n\n" +
                "CLAVIER : ← → pas de fréquence, ↑ ↓ 1 kHz, Échap arrête la CW, F1–F4 messages CW\n\n" +
                "LIAISON : branchez le câble USB, choisissez le port COM « Standard » de l'IC-7760 (115200 bauds, adresse 94)." +
                "Sans poste, le « Poste virtuel » simule un IC-7760 complet.",
                "Aide — IC-7760 Link " + Version, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ============================================================ boucle d'affichage
        void OnTick(object s, EventArgs e)
        {
            // flux du scope : rendu dès l'arrivée des trames
            ScopeFrame f; int n = 0;
            while (rig.ScopeQueue.TryDequeue(out f) && n++ < 40) { ScopeView v = views[f.Rx & 1]; if (v != null) v.Push(f); }
            if (rig.Connected) { if (connSeen == 0) connSeen = Environment.TickCount | 1; } else connSeen = 0;
            if (connSeen != 0 && Environment.TickCount - connSeen > 6000 && Cfg.GetBool("radio.pending", false)) { Cfg.Set("radio.pending", "0"); ApplyRadio(Cfg.WithPrefix("radio.")); Cfg.RemovePrefix("radio."); }
            foreach (ScopeView sv in views) if (sv != null) sv.Animate();
            int tick = Environment.TickCount;
            bool full = tick - lastFull > 200; int ver = rig.Version;
            if (ver != lastVersion || full)
            {
                lastVersion = ver; if (full) lastFull = tick;
                Refresh2(full);
            }
        }

        void Refresh2(bool full)
        {
            RxState m = rig.Rx[0], sb = rig.Rx[1];
            string sw = "";
            fdMain.SetFreq(m.Freq); fdSub.SetFreq(sb.Freq);
            fdMain.SetInfo(Widths.ModeName(m.Mode) + (m.Data > 0 ? "-D" + m.Data : ""), Info(0), rig.Sel == 0, rig.Ptt && rig.Sel == 0);
            fdSub.SetInfo(Widths.ModeName(sb.Mode) + (sb.Data > 0 ? "-D" + sb.Data : ""), Info(1), rig.Sel == 1, rig.Ptt && rig.Sel == 1);
            mS0.SetValue(m.Get("15 02")); mS1.SetValue(sb.Get("15 02"));
            mPo.SetValue(m.Get("15 11")); mSwr.SetValue(m.Get("15 12")); mAlc.SetValue(m.Get("15 13")); mComp.SetValue(m.Get("15 14")); mVd.SetValue(m.Get("15 15")); mId.SetValue(m.Get("15 16"));
            if (analog.Visible) { analog.TxIdx = rig.Ptt ? rig.Sel : -1; for (int q = 0; q < 2; q++) { RxState rs = rig.Rx[q]; analog.Bw[q] = rs.Width >= 1000 ? (rs.Width / 1000.0).ToString("0.#") + "k" : rs.Width.ToString(); analog.Mode[q] = Widths.ModeName(rs.Mode); analog.Fil[q] = "FIL" + Math.Max(1, rs.Filter); } analog.Invalidate(); }
            foreach (Action a in links) a();
            btnPtt.Set(rig.Ptt, rig.Ptt ? "ÉMISSION !" : "ÉMISSION (PTT)");
            lbStatus.Text = rig.Status + (rig.Connected ? "   " + rig.FramesPerSec + " trames/s" : ""); lbStatus.ForeColor = rig.Connected ? T.Green : (rig.Link != null ? T.Amber : T.Dim);
            if (rig.Link != null && !rig.Connected && Environment.TickCount - connStart > 3000) lbStatus.Text = "Aucune réponse du poste : vérifiez le port COM, la vitesse (115200) et l'adresse CI-V (94).";
            if (rig.Link == null) btnConnect.Set(false, "CONNECTER");
            lbClock.Text = DateTime.UtcNow.ToString("HH:mm:ss") + " UTC";
            Text = "IC-7760 Link " + Version + (rig.Ptt ? "  ●  ÉMISSION" : "") + " — " + (rig.Link != null ? rig.Link.Name : "hors ligne");
            BackColor = rig.Ptt ? Color.FromArgb(40, 8, 8) : T.Bg;
        }
        int connStart = Environment.TickCount;
        string Info(int rx)
        {
            RxState s = rig.Rx[rx]; string w = s.Width >= 1000 ? (s.Width / 1000.0).ToString("0.0#") + " kHz" : s.Width + " Hz";
            string[] agc = { "", "AGC-F", "AGC-M", "AGC-S" }; int a = s.Get("16 12"); int att = s.Get("11"); int pre = s.Get("16 02");
            return "FIL" + Math.Max(1, s.Filter) + "  " + w + "  " + (a >= 1 && a <= 3 ? agc[a] : "") + "  " + (att > 0 ? "ATT" + att : "") + (pre > 0 ? "  P" + pre : "") + (s.Get("16 22") != 0 ? "  NB" : "") + (s.Get("16 40") != 0 ? "  NR" : "");
        }

        // ============================================================ clavier
        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (ActiveControl is TextBox || ActiveControl is ComboBox) return base.ProcessCmdKey(ref msg, key);
            long step = views[Rx].StepHz(); long f = rig.Rx[Rx].Freq;
            switch (key)
            {
                case Keys.Left: rig.SetFreq(Rx, f - step); return true;
                case Keys.Right: rig.SetFreq(Rx, f + step); return true;
                case Keys.Down: rig.SetFreq(Rx, f - 1000); return true;
                case Keys.Up: rig.SetFreq(Rx, f + 1000); return true;
                case Keys.Escape: rig.StopCw(); return true;
                case Keys.F1: SendCwText(MacroText(0)); return true;
                case Keys.F2: SendCwText(MacroText(1)); return true;
                case Keys.F3: SendCwText(MacroText(2)); return true;
                case Keys.F4: SendCwText(MacroText(3)); return true;
            }
            return base.ProcessCmdKey(ref msg, key);
        }

        // ============================================================ réglages mémorisés
        void RestoreSettings()
        {
            string p = Cfg.Get("port", ""); for (int i = 0; i < cbPort.Items.Count; i++) if ((string)cbPort.Items[i] == p) cbPort.SelectedIndex = i;
            string b = Cfg.Get("baud", "115200"); for (int i = 0; i < cbBaud.Items.Count; i++) if ((string)cbBaud.Items[i] == b) cbBaud.SelectedIndex = i;
            tbAddr.Text = Cfg.Get("addr", "94");
            int pal = Cfg.GetInt("palette", 0); if (pal >= 0 && pal < cbPal.Items.Count) cbPal.SelectedIndex = pal;
            speedI = Cfg.GetInt("wfspeed2", 5); if (speedI < 0 || speedI >= speeds.Length) speedI = 5; ApplySpeed();
            avgN = Cfg.GetInt("avg2", 2); if (avgN < 1 || avgN > 20) avgN = 2; ApplyAvg();
            foreach (ScopeView v in views) v.PeakHoldSec = Cfg.GetInt("peakhold", 3);
            layoutMode = Cfg.GetInt("scopelayout", 0);
            int w = Cfg.GetInt("w", 0); if (w < 900) w = (int)Math.Round(DesignW * T.Dpi / 96f);
            Size nc = new Size(Width - ClientSize.Width, Height - ClientSize.Height); MinimumSize = new Size(nc.Width + 900, nc.Height + (int)Math.Round(900 / Ratio));
            ClientSize = FitClient(w); CenterToScreen(); LayoutStage();
            connStart = Environment.TickCount;
            if (Cfg.GetBool("autoconnect", false)) ToggleConnect();
        }
        // ============================================================ échelle de la fenêtre
        // Toute l'interface vit dans une « scène » de proportions fixes (1480 x 940). Quand la fenêtre change de taille,
        // la scène est agrandie ou réduite d'un bloc (positions, tailles, polices) : rien n'est tronqué, aucun ascenseur.
        // Le rapport largeur/hauteur est imposé pendant le redimensionnement (WM_SIZING) ; en plein écran, des bandes noires.
        const int DesignW = 1860, DesignH = 980; const double Ratio = DesignW / (double)DesignH;
        class Snap { public Rectangle B; public DockStyle Dock; public string FName; public float FPt; public FontStyle FSt; public Padding Mg, Pd; public int ItemH; }
        Panel stage; readonly Dictionary<Control, Snap> snaps = new Dictionary<Control, Snap>(); bool stageReady;

        void BuildStage()
        {
            stage = new Panel(); stage.BackColor = T.Bg; stage.Bounds = new Rectangle(0, 0, DesignW, DesignH);
            Control[] arr = new Control[Controls.Count]; for (int i = 0; i < arr.Length; i++) arr[i] = Controls[i];
            SuspendLayout(); foreach (Control c in arr) stage.Controls.Add(c);   // même ordre = même empilement des panneaux « Dock »
            Controls.Add(stage); stage.PerformLayout();
            Snapshot(stage);
            stageReady = true; ResumeLayout(false);
            Resize += delegate { LayoutStage(); };
        }
        void Snapshot(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                Snap s = new Snap(); s.B = c.Bounds; s.Dock = c.Dock; s.FName = c.Font.Name; s.FPt = c.Font.SizeInPoints * 96f / T.Dpi; s.FSt = c.Font.Style; s.Mg = c.Margin; s.Pd = c.Padding;
                ComboBox cb = c as ComboBox; if (cb != null) s.ItemH = cb.ItemHeight;
                snaps[c] = s;
                Panel p = c as Panel; if (p != null) p.AutoScroll = false;
                if (!(c is ScopeView)) Snapshot(c);
            }
        }
        static int R(int v, float s) { return (int)Math.Round(v * s); }
        static Padding R(Padding p, float s) { return new Padding(R(p.Left, s), R(p.Top, s), R(p.Right, s), R(p.Bottom, s)); }
        void ApplyScale(Control parent, float s)
        {
            foreach (Control c in parent.Controls)
            {
                Snap n; if (!snaps.TryGetValue(c, out n)) continue;
                c.SuspendLayout();
                c.Font = T.Fn(n.FName, n.FPt, n.FSt);
                ComboBox cb = c as ComboBox; if (cb != null && n.ItemH > 0) cb.ItemHeight = Math.Max(8, R(n.ItemH, s));
                c.Margin = R(n.Mg, s); c.Padding = R(n.Pd, s);
                switch (n.Dock)
                {
                    case DockStyle.None: c.Bounds = new Rectangle(R(n.B.X, s), R(n.B.Y, s), R(n.B.Width, s), R(n.B.Height, s)); break;
                    case DockStyle.Top: case DockStyle.Bottom: c.Height = R(n.B.Height, s); break;
                    case DockStyle.Left: case DockStyle.Right: c.Width = R(n.B.Width, s); break;
                }
                if (!(c is ScopeView)) ApplyScale(c, s);
                c.ResumeLayout(false);
            }
        }
        void LayoutStage()
        {
            if (!stageReady || WindowState == FormWindowState.Minimized) return;
            int cw = ClientSize.Width, ch = ClientSize.Height; if (cw < 50 || ch < 50) return;
            float s = Math.Min(cw / (float)DesignW, ch / (float)DesignH);
            int sw = (int)Math.Round(DesignW * s), sh = (int)Math.Round(DesignH * s);
            if (sw > cw) sw = cw; if (sh > ch) sh = ch;
            stage.SuspendLayout();
            if (Math.Abs(s - T.Scale) > 0.001f) { T.Scale = s; ApplyScale(stage, s); }
            stage.Bounds = new Rectangle((cw - sw) / 2, (ch - sh) / 2, sw, sh);
            stage.ResumeLayout(true);
            foreach (ScopeView v in views) if (v != null) v.Invalidate();
            Invalidate(true);
        }
        // largeur de client conforme au rapport, ajustée à l'écran de travail
        Size FitClient(int w)
        {
            Rectangle wa = Screen.FromControl(this).WorkingArea; Size nc = new Size(Width - ClientSize.Width, Height - ClientSize.Height);
            int maxW = wa.Width - nc.Width, maxH = wa.Height - nc.Height; if (w > maxW) w = maxW; if ((int)(w / Ratio) > maxH) w = (int)(maxH * Ratio);
            if (w < 900) w = 900; return new Size(w, (int)Math.Round(w / Ratio));
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0214 && stageReady)   // WM_SIZING : conserve le rapport de la fenêtre
            {
                RECT r = (RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(RECT)); int edge = m.WParam.ToInt32();
                int ncw = Width - ClientSize.Width, nch = Height - ClientSize.Height;
                int cw = r.Right - r.Left - ncw, ch = r.Bottom - r.Top - nch;
                if (edge == 3 || edge == 6) cw = (int)Math.Round(ch * Ratio); else ch = (int)Math.Round(cw / Ratio);
                if (cw < 900) { cw = 900; ch = (int)Math.Round(cw / Ratio); }
                if (edge == 1 || edge == 4 || edge == 7) r.Left = r.Right - (cw + ncw); else r.Right = r.Left + cw + ncw;
                if (edge == 3 || edge == 4 || edge == 5) r.Top = r.Bottom - (ch + nch); else r.Bottom = r.Top + ch + nch;
                System.Runtime.InteropServices.Marshal.StructureToPtr(r, m.LParam, false);
                m.Result = (IntPtr)1; return;
            }
            base.WndProc(ref m);
        }

        void OnClosing(object s, FormClosingEventArgs e)
        {
            try
            {
                if (rig.Ptt) rig.SetPtt(false);
                if (!importing) { Cfg.SetInt("w", ClientSize.Width); Cfg.SetInt("h", ClientSize.Height); Cfg.SetBool("autoconnect", rig.Link != null && cbPort.SelectedIndex > 0); Cfg.Save(); }
                System.Threading.Thread.Sleep(60); rig.Disconnect();
            }
            catch { }
        }

        // Mode test : capture d'écran de la fenêtre après un délai puis fermeture (--shot=fichier.png[,ms[,actions]])
        public void ShotAndExit(string path, int ms, string actions)
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = Math.Max(500, ms);
            if (actions != null && actions.Length > 0) { System.Windows.Forms.Timer a = new System.Windows.Forms.Timer(); a.Interval = Math.Max(300, ms / 2); a.Tick += delegate { a.Stop(); TestActions(actions); }; a.Start(); }
            t.Tick += delegate
            {
                t.Stop();
                try { ShotForms(path); using (Bitmap b = new Bitmap(Width, Height)) { DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); b.Save(path, System.Drawing.Imaging.ImageFormat.Png); } } catch (Exception ex) { System.IO.File.WriteAllText(path + ".err", ex.ToString()); }
                Close();
            };
            t.Start();
        }
        void TestActions(string actions)
        {
            foreach (string a in actions.Split('+'))
            {
                switch (a)
                {
                    case "dual": layoutMode = 2; ApplyScopeLayout(); break;
                    case "small": ClientSize = new Size(900, 474); break;
                    case "big": ClientSize = new Size(1900, 1001); break;
                    case "wide": WindowState = FormWindowState.Maximized; break;
                    case "analog": SetAnalog(true); break;
                    case "theme": { Color[] tb = Theme.Snapshot(); Theme.Set(0, Color.FromArgb(40, 10, 10)); Theme.Set(1, Color.FromArgb(60, 20, 20)); Theme.Set(11, Color.LimeGreen); Theme.Set(12, Color.Magenta); Theme.Set(4, Color.Yellow); foreach (Form fm in Application.OpenForms) Theme.Recolor(fm, Theme.Map(tb, Theme.Snapshot())); } break;
                    case "optshot": { OptionsForm of = new OptionsForm(null, null); of.Show(this); Application.DoEvents(); using (Bitmap bm = new Bitmap(of.ClientSize.Width, of.ClientSize.Height)) { of.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save("opt.png"); } using (ColorPickForm cp = new ColorPickForm(Color.OrangeRed, "x")) { cp.Show(this); Application.DoEvents(); using (Bitmap bm = new Bitmap(cp.ClientSize.Width, cp.ClientSize.Height)) { cp.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save("pick.png"); } } using (ConfigForm cf = new ConfigForm("", false)) { cf.Show(this); Application.DoEvents(); using (Bitmap bm = new Bitmap(cf.ClientSize.Width, cf.ClientSize.Height)) { cf.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save("cfg.png"); } } } break;
                    case "radio": { string dd = RadioDump(); int bf = rig.Get(0, "14 01"); rig.Write(0, "14 01", (bf + 40) % 256); string msg = ApplyRadio(Cfg.Parse(dd)); System.IO.File.WriteAllText("radiodump.txt", dd + "# " + msg + " avant=" + bf + " apres=" + rig.Get(0, "14 01") + "\r\n"); } break;
                    case "sub": rig.SelectRx(1); break;
                    case "tune": rig.SetFreq(0, 14205000); break;
                    case "cw": rig.SetMode(0, 3, 0, 1); break;
                    case "tx": rig.SetPtt(true); break;
                    case "zoom": rig.ScopeSetSpan(0, 5000); break;
                    case "fixed": rig.ScopeSetMode(0, 1); break;
                    case "ft8": rig.SetFreq(0, 14074000); rig.ScopeSetSpan(0, 5000); break;
                    case "sideh": layoutMode = 3; ApplyScopeLayout(); break;
                    case "forms": ShowTestForms(); break;
                }
            }
        }
        // icône dessinée par le programme : une mini cascade colorée
        static Icon MakeIcon()
        {
            using (Bitmap b = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(10, 14, 22)); int[] lut = Palettes.Build(0, 0.9); Random r = new Random(5);
                    for (int y = 0; y < 20; y++) for (int x = 0; x < 32; x++)
                    {
                        double v = 40 + r.Next(25); double d1 = Math.Abs(x - 10 - y * 0.15); double d2 = Math.Abs(x - 22); if (d1 < 2) v += 170 - d1 * 40; if (d2 < 1.5 && (y / 3) % 2 == 0) v += 150;
                        b.SetPixel(x, y + 12, Color.FromArgb(lut[(int)Math.Min(255, v)]));
                    }
                    using (Pen p = new Pen(Color.FromArgb(255, 200, 60), 2f)) g.DrawLines(p, new Point[] { new Point(1, 10), new Point(6, 9), new Point(9, 3), new Point(12, 9), new Point(18, 8), new Point(22, 2), new Point(25, 9), new Point(31, 10) });
                }
                return Icon.FromHandle(b.GetHicon());
            }
        }
        public void ShowTestForms() { ShowSet(); ShowMonitor(); }
        public void ShotForms(string path)
        {
            if (setForm != null) using (Bitmap b = new Bitmap(setForm.Width, setForm.Height)) { setForm.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); b.Save(path + ".set.png", System.Drawing.Imaging.ImageFormat.Png); }
            if (monitor != null) using (Bitmap b = new Bitmap(monitor.Width, monitor.Height)) { monitor.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); b.Save(path + ".mon.png", System.Drawing.Imaging.ImageFormat.Png); }
        }
        public void AutoStart(string how) { if (how == "virtual") { cbPort.SelectedIndex = 0; ToggleConnect(); } }
    }
}
