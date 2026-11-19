using System;
using System.Collections.Generic;
using System.Threading;

namespace IC7760Link
{
    // Largeurs de filtre IF (commande 1A 03, index 0000-0049) : hypothèse alignée sur la famille IC-7xxx
    public static class Widths
    {
        public static int FromIndex(int mode, int i)
        {
            if (mode == 2) return 200 * (i + 1);                  // AM : 200 Hz..10 kHz
            if (mode == 5) return 15000;                          // FM
            if (i < 10) return 50 * (i + 1);                      // 50..500 Hz
            return 600 + 100 * (i - 10);                          // 600..3600 Hz
        }
        public static int ToIndex(int mode, int hz)
        {
            if (mode == 2) { int i = hz / 200 - 1; return i < 0 ? 0 : (i > 49 ? 49 : i); }
            if (hz <= 500) { int i = hz / 50 - 1; return i < 0 ? 0 : (i > 9 ? 9 : i); }
            int j = 10 + (hz - 600) / 100; return j < 10 ? 10 : (j > 40 ? 40 : j);
        }
        public static int Max(int mode) { return mode == 2 ? 10000 : 3600; }
        public static int Min(int mode) { return mode == 2 ? 200 : 50; }
        public static int DefaultFor(int mode)
        {
            switch (mode) { case 3: case 7: return 500; case 4: case 8: return 500; case 2: return 6000; case 5: return 15000; case 12: case 13: return 500; default: return 2400; }
        }
        public static string ModeName(int m)
        {
            switch (m) { case 0: return "LSB"; case 1: return "USB"; case 2: return "AM"; case 3: return "CW"; case 4: return "RTTY"; case 5: return "FM"; case 7: return "CW-R"; case 8: return "RTTY-R"; case 12: return "PSK"; case 13: return "PSK-R"; }
            return "?";
        }
    }

    public struct Station { public long F; public int Kind; public float Level; public float Phase; public float Rate; public int Bw; }

    // Un IC-7760 qui n'existe pas : mêmes trames CI-V, spectre vivant (SSB, CW, FT8, AM, RTTY, bruit, QRM)
    public class VirtualRadio
    {
        public event Action<byte[]> Output;
        readonly byte addr;
        readonly Queue<byte[]> inbox = new Queue<byte[]>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        Thread th; volatile bool run;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        readonly Random rnd = new Random(7760);

        // état
        bool powered = true;
        int sel = 0; bool dualwatch = true; bool split = false;
        long[] freq = { 14074000, 7100000 };
        int[] mode = { 1, 0 };            // USB, LSB
        int[] dmode = { 0, 0 };
        int[] filt = { 1, 1 };
        int[] width = { 2400, 2400 };
        int ritHz = 0; bool ritOn = false, xitOn = false;
        bool ptt = false; long tuneUntil = 0, cwUntil = 0; int tuner = 0;
        readonly Dictionary<string, int> vals = new Dictionary<string, int>();
        readonly Dictionary<string, byte[]> rawVals = new Dictionary<string, byte[]>();
        float[] sm = new float[2];

        // scope
        bool scopeOn = true, waveOut = true; int scopeRx = 0; bool scopeDual = false;
        int[] smode = { 0, 0 }; long[] halfSpan = { 25000, 25000 }; int[] refTenth = { 0, 0 };
        int[] sweep = { 1, 1 }; int[] edgeNo = { 1, 1 }; bool hold = false;
        long nextScope = 0;
        public const int ScopePoints = 475;
        readonly List<Station> stations = new List<Station>();
        readonly float[] noiseTab = new float[4096];

        static readonly long[][] Bands = {
            new long[]{1800000,2000000},new long[]{3500000,3800000},new long[]{5351500,5366500},new long[]{7000000,7200000},
            new long[]{10100000,10150000},new long[]{14000000,14350000},new long[]{18068000,18168000},new long[]{21000000,21450000},
            new long[]{24890000,24990000},new long[]{28000000,29700000},new long[]{50000000,52000000}};

        public VirtualRadio(byte address)
        {
            addr = address;
            for (int i = 0; i < noiseTab.Length; i++) noiseTab[i] = (float)rnd.NextDouble();
            Defaults(); BuildWorld();
        }

        void Defaults()
        {
            foreach (CatEntry e in Catalog.All)
            {
                if (e.Cmd == 0x1A && e.SubText.StartsWith("05") && e.HasRange) vals["0:" + e.Key] = e.Min;
            }
            Set("14 01", 140, true); Set("14 02", 255, true); Set("14 03", 0, true); Set("14 06", 128, true); Set("14 12", 100, true);
            Set("14 07", 128, true); Set("14 08", 128, true); Set("14 0D", 128, true); Set("14 05", 128, true); Set("14 13", 128, true);
            Set("14 09", 128, false); Set("14 0A", 200, false); Set("14 0B", 120, false); Set("14 0C", 100, false); Set("14 0E", 90, false);
            Set("14 0F", 60, false); Set("14 14", 255, false); Set("14 15", 80, false); Set("14 16", 128, false); Set("14 17", 128, false); Set("14 19", 200, false);
            Set("16 02", 1, true); Set("16 12", 2, true); Set("16 56", 0, true); Set("16 57", 1, true); Set("16 47", 0, false);
            Set("10", 2, false); Set("11", 0, true);
        }
        void Set(string key, int v, bool perRx) { if (perRx) { vals["0:" + key] = v; vals["1:" + key] = v; } else vals["g:" + key] = v; }

        void BuildWorld()
        {
            Random r = new Random(2026);
            // balises et signaux fixes
            AddSt(14100000, 3, 118, 0.9f, 40); AddSt(28200000, 3, 105, 1.2f, 40); AddSt(50313000, 4, 96, 0, 3000);
            // FT8 (slots de 15 s) sur les fréquences usuelles
            long[] ft8 = { 1840000, 3573000, 5357000, 7074000, 10136000, 14074000, 18100000, 21074000, 24915000, 28074000, 50313000 };
            foreach (long f in ft8)
                for (int k = 0; k < 18; k++) AddSt(f + 200 + r.Next(2600), 4, 60 + r.Next(55), (float)r.NextDouble() * 15f, 50);
            // CW / SSB dans chaque bande amateur
            foreach (long[] b in Bands)
            {
                long span = b[1] - b[0];
                int n = (int)Math.Min(40, span / 18000) + 6;
                for (int k = 0; k < n; k++)
                {
                    long f = b[0] + (long)(r.NextDouble() * span);
                    double frac = (double)(f - b[0]) / span;
                    int kind = frac < 0.25 ? 2 : (frac < 0.45 ? 5 : 1);        // CW, RTTY, SSB
                    AddSt(f, kind, 45 + r.Next(80), (float)r.NextDouble() * 20f, kind == 1 ? 2700 : (kind == 2 ? 120 : 250));
                }
            }
            // radiodiffusion AM
            long[] bc = { 5985000, 6005000, 6095000, 7260000, 7330000, 9410000, 9590000, 9775000, 11860000, 11990000, 13650000, 15150000, 15420000, 17540000, 17730000, 21470000 };
            foreach (long f in bc) AddSt(f, 6, 90 + r.Next(40), (float)r.NextDouble() * 6f, 9000);
            for (long f = 540000; f < 1700000; f += 9000) if (r.NextDouble() < 0.7) AddSt(f, 6, 70 + r.Next(60), (float)r.NextDouble() * 6f, 9000);
            // un QRM large et mobile
            AddSt(14212000, 7, 80, 0.3f, 7000);
        }
        void AddSt(long f, int kind, int lvl, float rate, int bw)
        { Station s = new Station(); s.F = f; s.Kind = kind; s.Level = lvl; s.Phase = (float)rnd.NextDouble() * 100f; s.Rate = rate; s.Bw = bw; stations.Add(s); }

        // ---------------------------------------------------------- fil d'exécution
        public void Start()
        {
            if (run) return;
            run = true; th = new Thread(Loop); th.IsBackground = true; th.Name = "VirtualRadio"; th.Start();
        }
        public void Stop() { run = false; wake.Set(); }
        public void Receive(byte[] f) { lock (inbox) inbox.Enqueue(f); wake.Set(); }

        void Loop()
        {
            while (run)
            {
                wake.WaitOne(8);
                byte[] f;
                while (true) { lock (inbox) { f = inbox.Count > 0 ? inbox.Dequeue() : null; } if (f == null) break; try { Process(f); } catch { } }
                long now = clock.ElapsedMilliseconds;
                if (ptt && tuneUntil > 0 && now > tuneUntil) { tuneUntil = 0; ptt = false; tuner = 1; Emit(new byte[] { 0x1C, 0x00, 0x00 }); }
                if (ptt && cwUntil > 0 && now > cwUntil) { cwUntil = 0; ptt = false; }
                if (now >= nextScope)
                {
                    nextScope = now + (sweep[0] == 0 ? 25 : (sweep[0] == 1 ? 40 : 70));
                    if (powered && scopeOn && waveOut) { if (scopeDual) { SendScope(0); SendScope(1); } else SendScope(scopeRx); }
                }
            }
        }

        void Emit(byte[] payload) { Emit(payload, Civ.Controller); }
        void Emit(byte[] payload, byte to)
        {
            Action<byte[]> o = Output; if (o != null) o(Civ.Build(to, addr, payload));
        }
        void Ok() { Emit(new byte[] { 0xFB }); }
        void Ng() { Emit(new byte[] { 0xFA }); }

        // ---------------------------------------------------------- traitement des commandes
        void Process(byte[] f)
        {
            if (f.Length < 6) return;
            if (f[2] != addr && f[2] != 0x00) return;
            if (f[3] != Civ.Controller && f[3] != 0xE1) { }
            byte[] p = new byte[f.Length - 5]; Array.Copy(f, 4, p, 0, p.Length);
            if (!powered) { if (p[0] == 0x18 && p.Length > 1 && p[1] == 0x01) { powered = true; Ok(); } return; }
            int rx = sel; bool pref = false;
            if (p[0] == 0x29 && p.Length >= 3) { rx = p[1] & 1; pref = true; byte[] q = new byte[p.Length - 2]; Array.Copy(p, 2, q, 0, q.Length); p = q; }
            Dispatch(p, rx, pref);
        }

        void Reply(bool pref, int rx, params object[] parts)
        {
            byte[] b = Civ.Cat(parts);
            if (pref) b = Civ.Cat((byte)0x29, (byte)rx, b);
            Emit(b);
        }

        void Dispatch(byte[] p, int rx, bool pref)
        {
            int cmd = p[0]; int n = p.Length;
            switch (cmd)
            {
                case 0x03: Reply(pref, rx, (byte)3, Civ.FreqBytes(freq[rx])); return;
                case 0x04: Reply(pref, rx, (byte)4, (byte)mode[rx], (byte)filt[rx]); return;
                case 0x05: if (n >= 6) { freq[rx] = Civ.Freq(p, 1); Ok(); Transceive(rx); } else Ng(); return;
                case 0x00: if (n >= 6) { freq[rx] = Civ.Freq(p, 1); Transceive(rx); } return;
                case 0x06: case 0x01:
                    if (n >= 2) { mode[rx] = p[1]; if (n >= 3) filt[rx] = p[2]; width[rx] = Widths.DefaultFor(mode[rx]); if (cmd == 0x06) Ok(); Transceive(rx); } else Ng(); return;
                case 0x25:
                    if (n == 2) { int r = p[1] & 1; Emit(Civ.Cat((byte)0x25, (byte)r, Civ.FreqBytes(freq[r]))); }
                    else if (n >= 7) { freq[p[1] & 1] = Civ.Freq(p, 2); Ok(); Transceive(p[1] & 1); } else Ng(); return;
                case 0x26:
                    if (n == 2) { int r = p[1] & 1; Emit(new byte[] { 0x26, (byte)r, (byte)mode[r], (byte)dmode[r], (byte)filt[r] }); }
                    else if (n >= 3) { int r = p[1] & 1; mode[r] = p[2]; if (n >= 4) dmode[r] = p[3]; if (n >= 5) filt[r] = p[4]; width[r] = Widths.DefaultFor(mode[r]); Ok(); Transceive(r); } else Ng(); return;
                case 0x07: Cmd07(p); return;
                case 0x0F:
                    if (n == 1) Emit(new byte[] { 0x0F, (byte)(split ? 1 : 0) }); else { split = p[1] == 1; Ok(); } return;
                case 0x08: case 0x09: case 0x0A: case 0x0B: case 0x0E: Ok(); return;
                case 0x18: if (n > 1 && p[1] == 0) { powered = false; Ok(); } else Ok(); return;
                case 0x19: Emit(new byte[] { 0x19, 0x00, addr }); return;
                case 0x17: { cwUntil = clock.ElapsedMilliseconds + (n - 1) * 90 + 200; ptt = true; Ok(); return; }
                case 0x15: Meters(p, rx, pref); return;
                case 0x1C: Cmd1C(p); return;
                case 0x1A:
                    if (n >= 4 && p[1] == 0x00)
                    {
                        int ch = Civ.Bcd(p[2]) * 100 + Civ.Bcd(p[3]);
                        if (n == 4) { byte[] m; if (mems.TryGetValue(ch, out m)) Emit(m); else Emit(new byte[] { 0x1A, 0x00, p[2], p[3], 0xFF }); }
                        else { mems[ch] = (byte[])p.Clone(); Ok(); }
                        return;
                    }
                    if (n >= 2 && p[1] == 0x03) { Width(rx, pref, p); return; } break;
                case 0x21: Cmd21(p); return;
                case 0x27: Cmd27(p); return;
            }
            Generic(p, rx, pref);
        }

        void Transceive(int rx)
        {
            if (rx != sel) return;
            Emit(Civ.Cat((byte)0x00, Civ.FreqBytes(freq[rx])), 0x00);
            Emit(new byte[] { 0x01, (byte)mode[rx], (byte)filt[rx] }, 0x00);
        }

        readonly Dictionary<int, byte[]> mems = new Dictionary<int, byte[]>();
        void Cmd07(byte[] p)
        {
            if (p.Length == 1) { Ok(); return; }
            switch (p[1])
            {
                case 0xB0: { long f = freq[0]; freq[0] = freq[1]; freq[1] = f; int m = mode[0]; mode[0] = mode[1]; mode[1] = m; Ok(); return; }
                case 0xB1: freq[1] = freq[0]; mode[1] = mode[0]; Ok(); return;
                case 0xC0: dualwatch = false; Ok(); return;
                case 0xC1: dualwatch = true; Ok(); return;
                case 0xC2: if (p.Length == 2) Emit(new byte[] { 0x07, 0xC2, (byte)(dualwatch ? 1 : 0) }); else { dualwatch = p[2] == 1; Ok(); } return;
                case 0xD0: sel = 0; Ok(); return;
                case 0xD1: sel = 1; Ok(); return;
                case 0xD2: if (p.Length == 2) Emit(new byte[] { 0x07, 0xD2, (byte)sel }); else { sel = p[2] & 1; Ok(); } return;
            }
            Ok();
        }

        void Cmd1C(byte[] p)
        {
            if (p.Length < 2) { Ng(); return; }
            switch (p[1])
            {
                case 0x00: if (p.Length == 2) Emit(new byte[] { 0x1C, 0x00, (byte)(ptt ? 1 : 0) }); else { ptt = p[2] == 1; Ok(); } return;
                case 0x01:
                    if (p.Length == 2) Emit(new byte[] { 0x1C, 0x01, (byte)tuner });
                    else { tuner = p[2]; if (tuner == 2) { ptt = true; tuneUntil = clock.ElapsedMilliseconds + 2500; Emit(new byte[] { 0x1C, 0x00, 0x01 }); } Ok(); }
                    return;
            }
            Generic(p, sel, false);
        }

        void Cmd21(byte[] p)
        {
            if (p.Length < 2) { Ng(); return; }
            switch (p[1])
            {
                case 0x00:
                    if (p.Length == 2)
                    {
                        int a = Math.Abs(ritHz);
                        Emit(new byte[] { 0x21, 0x00, Civ.ToBcd((a / 10) % 10 * 10 + a % 10), Civ.ToBcd((a / 1000) * 10 + (a / 100) % 10), (byte)(ritHz < 0 ? 1 : 0) });
                    }
                    else { int a = Civ.Bcd(p[3]) * 100 + Civ.Bcd(p[2]); ritHz = p[4] == 1 ? -a : a; Ok(); }
                    return;
                case 0x01: if (p.Length == 2) Emit(new byte[] { 0x21, 0x01, (byte)(ritOn ? 1 : 0) }); else { ritOn = p[2] == 1; Ok(); } return;
                case 0x02: if (p.Length == 2) Emit(new byte[] { 0x21, 0x02, (byte)(xitOn ? 1 : 0) }); else { xitOn = p[2] == 1; Ok(); } return;
            }
            Ng();
        }

        void Width(int rx, bool pref, byte[] p)
        {
            if (p.Length == 2) Reply(pref, rx, (byte)0x1A, (byte)0x03, Civ.ToBcd2(Widths.ToIndex(mode[rx], width[rx])));
            else { width[rx] = Widths.FromIndex(mode[rx], Civ.Bcd2(p[2], p[3])); Ok(); }
        }

        void Meters(byte[] p, int rx, bool pref)
        {
            if (p.Length < 2) { Ng(); return; }
            int v = 0; double t = clock.ElapsedMilliseconds / 1000.0;
            switch (p[1])
            {
                case 0x01: Reply(pref, rx, (byte)0x15, (byte)0x01, (byte)(sm[rx] > 20 ? 1 : 0)); return;
                case 0x02: v = (int)sm[rx]; break;
                case 0x05: Reply(pref, rx, (byte)0x15, (byte)0x05, (byte)1); return;
                case 0x07: Reply(pref, rx, (byte)0x15, (byte)0x07, (byte)(sm[rx] > 235 ? 1 : 0)); return;
                case 0x11: v = ptt ? (int)(vals["g:14 0A"] * 212 / 255.0 * (0.97 + 0.03 * Math.Sin(t * 9))) : 0; break;
                case 0x12: v = ptt ? (int)(18 + 14 * (1 + Math.Sin(t * 0.7)) + (tuner == 2 ? 40 : 0)) : 0; break;
                case 0x13: v = ptt ? 40 + (int)(25 * Math.Abs(Math.Sin(t * 5.3))) : 0; break;
                case 0x14: v = ptt && vals.ContainsKey("g:16 44") && vals["g:16 44"] == 1 ? 40 + (int)(60 * Math.Abs(Math.Sin(t * 4.1))) : 0; break;
                case 0x15: v = 151 + (ptt ? -8 : 0); break;
                case 0x16: v = ptt ? 120 + (int)(vals["g:14 0A"] / 2.5) : 22; break;
                default: Ng(); return;
            }
            Reply(pref, rx, (byte)0x15, p[1], Civ.ToBcd2(Math.Max(0, Math.Min(255, v))));
        }

        // lecture/écriture générique d'après le catalogue
        void Generic(byte[] p, int rx, bool pref)
        {
            CatEntry e = null; int subLen = 0;
            for (int sl = Math.Min(3, p.Length - 1); sl >= 0; sl--)
            {
                string key = p[0].ToString("X2");
                for (int i = 0; i < sl; i++) key += " " + p[1 + i].ToString("X2");
                e = Catalog.Find(key); if (e != null) { subLen = sl; break; }
            }
            if (e == null) { Ng(); return; }
            int dlen = p.Length - 1 - subLen;
            string store = (e.Direct29 ? rx.ToString() : "g") + ":" + e.Key;
            if (dlen <= 0)
            {
                byte[] raw; int v;
                if (rawVals.TryGetValue(store, out raw)) { Reply(pref, rx, SubFrame(p, subLen), raw); return; }
                if (!vals.TryGetValue(store, out v)) v = e.HasRange ? e.Min : 0;
                byte[] d = e.DataLen == 2 ? Civ.ToBcd2(v) : new byte[] { Civ.ToBcd(v) };
                Reply(pref, rx, SubFrame(p, subLen), d);
            }
            else
            {
                if (e.Raw || !e.HasRange) { byte[] d = new byte[dlen]; Array.Copy(p, 1 + subLen, d, 0, dlen); rawVals[store] = d; if (dlen == 1) vals[store] = Civ.Bcd(d[0]); }
                else vals[store] = e.DataLen == 2 ? Civ.Bcd2(p[1 + subLen], p[2 + subLen]) : Civ.Bcd(p[1 + subLen]);
                Ok();
            }
        }
        static byte[] SubFrame(byte[] p, int subLen) { byte[] b = new byte[1 + subLen]; Array.Copy(p, 0, b, 0, b.Length); return b; }

        // ---------------------------------------------------------- scope
        void Cmd27(byte[] p)
        {
            if (p.Length < 2) { Ng(); return; }
            int s = p[1];
            int rx = p.Length > 2 ? (p[2] & 1) : 0;
            switch (s)
            {
                case 0x10: if (p.Length == 2) Emit(new byte[] { 0x27, 0x10, (byte)(scopeOn ? 1 : 0) }); else { scopeOn = p[2] == 1; Ok(); } return;
                case 0x11: if (p.Length == 2) Emit(new byte[] { 0x27, 0x11, (byte)(waveOut ? 1 : 0) }); else { waveOut = p[2] == 1; Ok(); } return;
                case 0x12: if (p.Length == 2) Emit(new byte[] { 0x27, 0x12, (byte)scopeRx }); else { scopeRx = p[2] & 1; Ok(); } return;
                case 0x13: if (p.Length == 2) Emit(new byte[] { 0x27, 0x13, (byte)(scopeDual ? 1 : 0) }); else { scopeDual = p[2] == 1; Ok(); } return;
                case 0x14: if (p.Length == 3) Emit(new byte[] { 0x27, 0x14, (byte)rx, (byte)smode[rx] }); else { smode[rx] = p[3] & 3; Ok(); } return;
                case 0x15:
                    if (p.Length == 3) Emit(Civ.Cat((byte)0x27, (byte)0x15, (byte)rx, Civ.FreqBytes(halfSpan[rx])));
                    else { halfSpan[rx] = Civ.Freq(p, 3); Ok(); } return;
                case 0x16: if (p.Length == 3) Emit(new byte[] { 0x27, 0x16, (byte)rx, (byte)edgeNo[rx] }); else { edgeNo[rx] = p[3]; Ok(); } return;
                case 0x17: if (p.Length == 3) Emit(new byte[] { 0x27, 0x17, (byte)rx, (byte)(hold ? 1 : 0) }); else { hold = p[3] == 1; Ok(); } return;
                case 0x19:
                    if (p.Length == 3) { int a = Math.Abs(refTenth[rx]); Emit(new byte[] { 0x27, 0x19, (byte)rx, Civ.ToBcd(a / 100), Civ.ToBcd(a % 100), (byte)(refTenth[rx] < 0 ? 1 : 0) }); }
                    else { int a = Civ.Bcd(p[3]) * 100 + Civ.Bcd(p[4]); refTenth[rx] = p[5] == 1 ? -a : a; Ok(); } return;
                case 0x1A: if (p.Length == 3) Emit(new byte[] { 0x27, 0x1A, (byte)rx, (byte)sweep[rx] }); else { sweep[rx] = p[3] > 2 ? 2 : p[3]; Ok(); } return;
            }
            Generic(p, sel, false);
        }

        void EdgesFor(int rx, out long lo, out long hi)
        {
            long f = freq[rx];
            foreach (long[] b in Bands) if (f >= b[0] && f <= b[1]) { lo = b[0] - 10000; hi = b[1] + 10000; return; }
            lo = f - 100000; hi = f + 100000;
        }

        void SendScope(int rx)
        {
            int N = ScopePoints; int mdl = smode[rx];
            long lo, hi, f1, f2;
            if (mdl == 1 || mdl == 3) { EdgesFor(rx, out lo, out hi); f1 = lo; f2 = hi; }
            else { lo = freq[rx] - halfSpan[rx]; hi = freq[rx] + halfSpan[rx]; f1 = freq[rx]; f2 = halfSpan[rx]; }
            if (lo < 0) lo = 0;
            double t = clock.ElapsedMilliseconds / 1000.0;
            byte[] pts = new byte[N];
            long cur = Math.Min(Math.Max(0, freq[rx]), 60000000);
            // stations proches
            List<Station> near = new List<Station>();
            for (int i = 0; i < stations.Count; i++) { Station s = stations[i]; if (s.F + s.Bw + 2000 >= lo && s.F - 12000 <= hi) near.Add(s); }
            double att = Val(rx, "11") * 2.0; int pre = Val(rx, "16 02"); double gain = pre * 8 - att;
            double refAdj = -refTenth[rx] / 10.0 * 2.0;
            double step = (double)(hi - lo) / (N - 1);
            int seed = (int)(t * 25);
            for (int i = 0; i < N; i++)
            {
                double f = lo + step * i;
                double pw = 0;                              // puissance linéaire arbitraire
                double nz = noiseTab[(i * 7 + seed * 13) & 4095];
                double floor = 24 + (f > 14e6 ? 0 : 5) + nz * 7 + 3 * Math.Sin(f / 400000.0);
                double v = floor;
                for (int k = 0; k < near.Count; k++) { double c = Contribution(near[k], f, t, (int)(f / 25.0)); if (c > 0) pw += Math.Pow(10, c / 40.0); }
                if (pw > 0) v = Math.Max(v, 40 * Math.Log10(pw + 1e-9));
                // émission propre
                if (ptt) { double d = ((double)f - OwnCenter(rx)); if (Math.Abs(d) < 1500) v = Math.Max(v, 150 - Math.Abs(d) / 40 + nz * 3); }
                v += gain + refAdj;
                if (v < 0) v = 0; if (v > 160) v = 160;
                pts[i] = (byte)v;
            }
            // S-mètre : max dans la bande passante
            double smax = 0; double cf = freq[rx]; int wd = width[rx];
            double flo = cf, fhi = cf;
            switch (mode[rx]) { case 1: fhi = cf + wd; break; case 0: flo = cf - wd; break; default: flo = cf - wd / 2.0; fhi = cf + wd / 2.0; break; }
            for (int i = 0; i < N; i++) { double f = lo + step * i; if (f >= flo && f <= fhi) { double raw = pts[i] - gain - refAdj; if (raw > smax) smax = raw; } }
            double target = Math.Max(0, (smax - 28) * 2.0 + gain * 1.0); if (target > 241) target = 241;
            sm[rx] = sm[rx] * 0.6f + (float)target * 0.4f;

            // trames : division 1 = en-tête ; divisions 2..n = 50 points
            int divs = 11;
            Emit(Civ.Cat((byte)0x27, (byte)0x00, (byte)rx, (byte)0x01, Civ.ToBcd(divs), (byte)(mdl == 1 || mdl == 3 ? 1 : 0), Civ.FreqBytes(f1), Civ.FreqBytes(f2), (byte)0));
            int pos = 0;
            for (int d = 2; d <= divs && pos < N; d++)
            {
                int cnt = Math.Min(d == divs ? 25 : 50, N - pos);
                byte[] b = new byte[5 + cnt]; b[0] = 0x27; b[1] = 0; b[2] = (byte)rx; b[3] = Civ.ToBcd(d); b[4] = Civ.ToBcd(divs);
                Array.Copy(pts, pos, b, 5, cnt); pos += cnt; Emit(b);
            }
        }
        int Val(int rx, string key) { int v; if (vals.TryGetValue(rx + ":" + key, out v)) return v; return 0; }
        double OwnCenter(int rx) { double f = freq[rx]; return mode[rx] == 1 ? f + 1500 : (mode[rx] == 0 ? f - 1500 : f); }

        double Contribution(Station s, double f, double t, int bin)
        {
            double d = f - s.F;
            switch (s.Kind)
            {
                case 1: // SSB : bande 2,7 kHz modulée syllabiquement
                    {
                        int seg = (int)(t * 5.5 + s.Phase * 7);
                        double e1 = noiseTab[(seg * 31 + (int)(s.Phase * 13)) & 4095];
                        if (e1 < 0.22) return 0;                                 // silences entre les syllabes
                        double env = 0.45 + 0.55 * e1;
                        if (d < -100 || d > 2800) return 0;
                        double sh = d < 250 ? (d + 100) / 350.0 : (d > 2400 ? (2800 - d) / 400.0 : 1);
                        double tilt = 1.0 - 0.45 * (d / 2700.0);
                        double fm = 0.5 + 0.5 * Math.Sin(d / 420.0 + seg * 0.9);
                        double gr = noiseTab[(bin * 17 + seg * 53 + (int)(t * 40)) & 4095];
                        double lv = s.Level * env * tilt * (0.55 + 0.25 * fm + 0.35 * gr) * sh;
                        return Math.Max(0, lv - 12);
                    }
                case 2: // CW manipulée
                    {
                        double key = Math.Sin(t * (3 + s.Rate * 0.3) + s.Phase) * Math.Sin(t * 1.3 + s.Phase * 2);
                        if (key < 0) return 0;
                        double g = Math.Exp(-(d * d) / (2 * 40.0 * 40.0));
                        return g < 0.02 ? 0 : s.Level * 0.85 * g + 5;
                    }
                case 3: // balise
                    {
                        double on = (Math.Floor(t / 1.0) % 4 < 3) ? 1 : 0;
                        double g = Math.Exp(-(d * d) / (2 * 35.0 * 35.0));
                        return on * g < 0.02 ? 0 : s.Level * 0.9 * g;
                    }
                case 4: // FT8 : slots de 15 s, tonalité 6,25 Hz
                    {
                        double slot = (t + s.Rate) % 15.0; if (slot > 12.6) return 0;
                        double tone = ((int)(t * 6.25 + s.Phase * 3) % 8) * 6.25;
                        double dd = d - tone;
                        double g = Math.Exp(-(dd * dd) / (2 * 14.0 * 14.0));
                        return g < 0.03 ? 0 : s.Level * 0.8 * g;
                    }
                case 5: // RTTY : deux tonalités
                    {
                        double sw = Math.Sin(t * 22 + s.Phase) > 0 ? 0 : 170;
                        double dd = d - sw; double g = Math.Exp(-(dd * dd) / (2 * 28.0 * 28.0));
                        return g < 0.03 ? 0 : s.Level * 0.75 * g;
                    }
                case 6: // AM broadcast : porteuse + bandes latérales
                    {
                        double ad = Math.Abs(d); if (ad > 4800) return 0;
                        double car = Math.Exp(-(d * d) / (2 * 25.0 * 25.0)) * s.Level;
                        double mod = ad < 4500 ? (s.Level - 22) * (0.35 + 0.5 * noiseTab[((int)(ad / 40) * 7 + (int)(t * 12) * 13 + bin) & 4095]) * (1 - ad / 5200.0) : 0;
                        double fade = 0.85 + 0.15 * Math.Sin(t * 0.4 + s.Phase);
                        return Math.Max(car, mod) * fade;
                    }
                case 7: // QRM large dérivant
                    {
                        double c = s.F + 15000 * Math.Sin(t * 0.05 + s.Phase);
                        double dd = Math.Abs(f - c); if (dd > 3500) return 0;
                        return s.Level * (1 - dd / 3500.0) * (0.6 + 0.4 * noiseTab[(bin * 11 + (int)(t * 30) * 17) & 4095]);
                    }
            }
            return 0;
        }
    }
}
