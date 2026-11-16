using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace IC7760Link
{
    public class RxState
    {
        public long Freq; public int Mode, Data, Filter, Width = 2400;
        public readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> V = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        public int Get(string k) { int v; return V.TryGetValue(k, out v) ? v : 0; }
    }

    public class ScopeFrame
    {
        public int Rx; public bool Fixed; public long F1, F2; public bool Oor; public byte[] Data;
        public long Lo { get { return Fixed ? F1 : F1 - F2; } }
        public long Hi { get { return Fixed ? F2 : F1 + F2; } }
        public long Stamp;
    }

    public class Rig
    {
        public ILink Link; public byte Addr = 0x94; bool heardAddr; public bool AddrAdopted;
        public readonly RxState[] Rx = { new RxState(), new RxState() };
        public volatile int Sel; public volatile bool Split, Dual = true, Ptt;
        public volatile int Version;                       // incrémenté à chaque changement d'état
        public int Rit; public bool RitOn, XitOn;
        public readonly bool[] ScopeFixed = new bool[2];
        public readonly int[] ScopeMode = new int[2];      // 0 centre, 1 fixe, 2 scroll-C, 3 scroll-F
        public readonly long[] HalfSpan = { 25000, 25000 }; public readonly long[] WinWidth = new long[2];
        public readonly int[] RefTenth = new int[2];       // dixièmes de dB
        public readonly int[] Sweep = new int[2];
        public bool ScopeHold; public bool ScopeDual; public int ScopeRx;
        public readonly ConcurrentQueue<ScopeFrame> ScopeQueue = new ConcurrentQueue<ScopeFrame>();
        public event Action<string, bool> Log;             // ligne, true = envoyée
        public volatile bool Connected; public long LastRx; public int FramesPerSec; int fpsCount; long fpsT;
        public string Status = "Déconnecté";
        public bool ScopeAlive { get { return Environment.TickCount - LastScope < 1500; } }
        public int LastScope;

        readonly object sync = new object();
        readonly List<Pending> pending = new List<Pending>();
        readonly Dictionary<string, int> coalesce = new Dictionary<string, int>();
        readonly Dictionary<string, long> holdoff = new Dictionary<string, long>();
        readonly List<byte[]> immediate = new List<byte[]>();
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        Thread poll; volatile bool run;
        readonly List<byte>[] scopeAsm = { new List<byte>(), new List<byte>() };
        readonly ScopeFrame[] scopeHead = new ScopeFrame[2];
        long coalFreqRx = -1;
        struct Pending { public string Key; public int Rx; public long T; }

        public static readonly string[] RxKeys = {
            "14 01","14 02","14 03","14 05","14 06","14 07","14 08","14 0D","14 12","14 13",
            "16 02","16 12","16 22","16 32","16 40","16 41","16 48","16 4E","16 4F","16 56","16 57","16 65","11","12 00","12 01","12 02","12 03" };
        public static readonly string[] GlobalKeys = {
            "14 09","14 0A","14 0B","14 0C","14 0E","14 0F","14 14","14 15","14 16","14 17","16 44","16 45","16 46","16 47","16 5E","16 50","16 58","1C 01","1C 02","10","21 01","21 02" };

        // ------------------------------------------------------------- connexion
        public bool Connect(ILink link, byte addr, out string err)
        {
            Disconnect();
            Addr = addr; Link = link; heardAddr = false; AddrAdopted = false;
            link.Frame += OnFrame;
            if (!link.Open(out err)) { link.Frame -= OnFrame; Link = null; Status = "Échec : " + err; return false; }
            Status = "Connexion à " + link.Name + "…";
            run = true; Connected = false; LastRx = 0;
            poll = new Thread(PollLoop); poll.IsBackground = true; poll.Name = "RigPoll"; poll.Start();
            return true;
        }
        public void Disconnect()
        {
            run = false;
            ILink l = Link; Link = null;
            if (l != null) { try { l.Frame -= OnFrame; l.Close(); } catch { } }
            Connected = false; Status = "Déconnecté"; Version++;
        }

        public event Action<byte> Heard; public int TraceScopeMax; public int ScopeRaw;
        void Trace(byte[] f, bool tx)
        {
            if (!tx && f.Length > 3) { Action<byte> hh = Heard; if (hh != null) hh(f[3]); }
            Action<string, bool> l = Log; if (l == null) return;
            if (!tx && f.Length > 5 && f[4] == 0x27 && f[5] == 0x00)   // flux du scope : pas de trace, sauf en diagnostic (quelques trames)
            {
                ScopeRaw++;
                if (TraceScopeMax <= 0 || ScopeRaw > TraceScopeMax) return;
            }
            l(Civ.Hex(f, f.Length), tx);
        }

        void SendNow(byte[] payload)
        {
            ILink l = Link; if (l == null) return;
            byte[] f = Civ.Build(Addr, Civ.Controller, payload);
            Trace(f, true); l.Send(f);
        }

        // ------------------------------------------------------------- envoi
        public void Raw(params byte[] payload) { lock (sync) immediate.Add(payload); }
        public void Raw(byte[] payload, int rx, string key)
        {
            lock (sync) { pending.Add(new Pending { Key = key, Rx = rx, T = clock.ElapsedMilliseconds }); immediate.Add(payload); }
        }

        byte[] Prefix(CatEntry e, int rx, byte[] body)
        {
            if (e != null && e.Direct29) return Civ.Cat((byte)0x29, (byte)rx, body);
            return body;
        }
        static byte[] KeyBytes(CatEntry e) { return Civ.Cat((byte)e.Cmd, e.Sub); }

        public void Read(int rx, string key)
        {
            CatEntry e = Catalog.Find(key); if (e == null) return;
            Raw(Prefix(e, rx, KeyBytes(e)), rx, key);
        }

        // Écriture (valeur numérique d'après le catalogue). Mise à jour locale immédiate, envoi groupé.
        public void Write(int rx, string key, int val)
        {
            CatEntry e = Catalog.Find(key); if (e == null) return;
            int r = e.Direct29 ? rx : 0;
            Rx[r].V[key] = val;
            lock (sync) { coalesce[r + "|" + key] = val; holdoff[r + "|" + key] = clock.ElapsedMilliseconds + 700; }
            Version++;
        }

        public int Get(int rx, string key)
        {
            CatEntry e = Catalog.Find(key); int r = (e != null && e.Direct29) ? rx : 0;
            return Rx[r].Get(key);
        }

        void FlushWrite(string rk, int val)
        {
            int bar = rk.IndexOf('|'); int rx = int.Parse(rk.Substring(0, bar)); string key = rk.Substring(bar + 1);
            CatEntry e = Catalog.Find(key); if (e == null) return;
            byte[] data = e.DataLen == 2 ? Civ.ToBcd2(val) : new byte[] { Civ.ToBcd(val) };
            SendNow(Prefix(e, rx, Civ.Cat(KeyBytes(e), data)));
        }

        public void SetFreq(int rx, long hz)
        {
            if (hz < 30000) hz = 30000; if (hz > 60000000) hz = 60000000;
            Rx[rx].Freq = hz; Version++;
            lock (sync) { coalesce[rx + "|FREQ"] = 1; holdoff[rx + "|FREQ"] = clock.ElapsedMilliseconds + 600; }
        }
        public void SetMode(int rx, int mode, int data, int filter)
        {
            RxState s = Rx[rx]; s.Mode = mode; s.Data = data; s.Filter = filter; s.Width = Widths.DefaultFor(mode); Version++;
            Raw(Civ.Cat((byte)0x26, (byte)rx, (byte)mode, (byte)data, (byte)filter));
            lock (sync) holdoff[rx + "|MODE"] = clock.ElapsedMilliseconds + 800;
            Raw(Civ.Cat((byte)0x29, (byte)rx, (byte)0x1A, (byte)0x03), rx, "1A 03");
        }
        public void SetWidth(int rx, int hz)
        {
            RxState s = Rx[rx]; s.Width = hz; Version++;
            lock (sync) { coalesce[rx + "|WIDTH"] = hz; holdoff[rx + "|WIDTH"] = clock.ElapsedMilliseconds + 800; }
        }
        public void SelectRx(int rx) { Sel = rx; Version++; Raw((byte)0x07, (byte)(rx == 0 ? 0xD0 : 0xD1)); }
        public void SetSplit(bool on) { Split = on; Version++; Raw((byte)0x0F, (byte)(on ? 1 : 0)); }
        public void SetDual(bool on) { Dual = on; Version++; Raw((byte)0x07, (byte)(on ? 0xC1 : 0xC0)); }
        public void Swap() { Raw((byte)0x07, (byte)0xB0); }
        public void Equalize() { Raw((byte)0x07, (byte)0xB1); }
        public void SetPtt(bool on) { Ptt = on; Version++; Raw((byte)0x1C, (byte)0x00, (byte)(on ? 1 : 0)); }
        public void Tune() { Raw((byte)0x1C, (byte)0x01, (byte)0x02); }
        public void SetRit(bool on) { RitOn = on; Version++; Raw((byte)0x21, (byte)0x01, (byte)(on ? 1 : 0)); }
        public void SetXit(bool on) { XitOn = on; Version++; Raw((byte)0x21, (byte)0x02, (byte)(on ? 1 : 0)); }
        public void SetRitFreq(int hz)
        {
            Rit = hz; Version++;
            int a = Math.Abs(hz);
            Raw(new byte[] { 0x21, 0x00, Civ.ToBcd((a / 10) % 10 * 10 + a % 10), Civ.ToBcd((a / 1000) * 10 + (a / 100) % 10), (byte)(hz < 0 ? 1 : 0) });
        }
        public void Memory(int cmd, int ch) { if (ch > 0) Raw(new byte[] { 0x08, Civ.ToBcd(ch / 100), Civ.ToBcd(ch % 100) }); else Raw((byte)cmd); }
        public void SendCw(string text)
        {
            byte[] a = Encoding.ASCII.GetBytes(text);
            for (int i = 0; i < a.Length; i += 30)
            {
                int n = Math.Min(30, a.Length - i); byte[] p = new byte[n + 1]; p[0] = 0x17; Array.Copy(a, i, p, 1, n); Raw(p);
            }
        }
        public void StopCw() { Raw(new byte[] { 0x17, 0xFF }); }

        // -------- scope
        public void ScopeSetMode(int rx, int m) { ScopeMode[rx] = m; ScopeFixed[rx] = m == 1 || m == 3; Version++; Raw(new byte[] { 0x27, 0x14, (byte)rx, (byte)m }); }
        public void ScopeSetSpan(int rx, long half) { HalfSpan[rx] = half; Version++; Raw(Civ.Cat((byte)0x27, (byte)0x15, (byte)rx, Civ.FreqBytes(half))); }
        public void ScopeSetRef(int rx, int tenth)
        {
            if (tenth < -300) tenth = -300; if (tenth > 100) tenth = 100; RefTenth[rx] = tenth; Version++;
            int a = Math.Abs(tenth); Raw(new byte[] { 0x27, 0x19, (byte)rx, Civ.ToBcd(a / 100), Civ.ToBcd(a % 100), (byte)(tenth < 0 ? 1 : 0) });
        }
        public void ScopeSetSweep(int rx, int s) { Sweep[rx] = s; Version++; Raw(new byte[] { 0x27, 0x1A, (byte)rx, (byte)s }); }
        public void ScopeSetHold(bool on) { ScopeHold = on; Raw(new byte[] { 0x27, 0x17, 0, (byte)(on ? 1 : 0) }); Raw(new byte[] { 0x27, 0x17, 1, (byte)(on ? 1 : 0) }); }
        public void ScopeSetDual(bool on) { ScopeDual = on; Version++; Raw(new byte[] { 0x27, 0x13, (byte)(on ? 1 : 0) }); }
        public void ScopeSetRx(int rx) { ScopeRx = rx; Raw(new byte[] { 0x27, 0x12, (byte)rx }); }
        public void ScopeEnable(bool on) { Raw(new byte[] { 0x27, 0x10, (byte)(on ? 1 : 0) }); Raw(new byte[] { 0x27, 0x11, (byte)(on ? 1 : 0) }); }

        // ------------------------------------------------------------- boucle d'interrogation
        void PollLoop()
        {
            // séquence d'ouverture
            Raw((byte)0x19, (byte)0x00);
            Raw(new byte[] { 0x27, 0x10, 1 }); Raw(new byte[] { 0x27, 0x11, 1 });
            for (int r = 0; r < 2; r++)
            {
                Raw(new byte[] { 0x27, 0x14, (byte)r }); Raw(new byte[] { 0x27, 0x15, (byte)r }); Raw(new byte[] { 0x27, 0x19, (byte)r }); Raw(new byte[] { 0x27, 0x1A, (byte)r });
            }
            Raw(new byte[] { 0x27, 0x12 }); Raw(new byte[] { 0x27, 0x13 }); Raw(new byte[] { 0x27, 0x17, 0 });
            long tMeter = 0, tMid = 0, tSlow = 0, tTx = 0; int midI = 0, slowI = 0;
            List<string> slow = new List<string>();
            for (int r = 0; r < 2; r++) foreach (string k in RxKeys) slow.Add(r + "|" + k);
            foreach (string k in GlobalKeys) slow.Add("0|" + k);
            slow.Add("0|1A 03"); slow.Add("1|1A 03");
            while (run)
            {
                long now = clock.ElapsedMilliseconds;
                // écritures et commandes immédiates
                List<byte[]> imm = null; List<KeyValuePair<string, int>> co = null;
                lock (sync)
                {
                    if (immediate.Count > 0) { imm = new List<byte[]>(immediate); immediate.Clear(); }
                    if (coalesce.Count > 0) { co = new List<KeyValuePair<string, int>>(coalesce); coalesce.Clear(); }
                }
                if (imm != null) foreach (byte[] p in imm) SendNow(p);
                if (co != null)
                    foreach (KeyValuePair<string, int> kv in co)
                    {
                        if (kv.Key.EndsWith("|FREQ")) { int rx = kv.Key[0] - '0'; SendNow(Civ.Cat((byte)0x25, (byte)rx, Civ.FreqBytes(Rx[rx].Freq))); }
                        else if (kv.Key.EndsWith("|WIDTH")) { int rx = kv.Key[0] - '0'; SendNow(Civ.Cat((byte)0x29, (byte)rx, (byte)0x1A, (byte)0x03, Civ.ToBcd2(Widths.ToIndex(Rx[rx].Mode, kv.Value)))); }
                        else FlushWrite(kv.Key, kv.Value);
                    }
                bool have = Link != null;
                if (have)
                {
                    if (now - tMeter >= 90)
                    {
                        tMeter = now;
                        Read(0, "15 02"); Read(1, "15 02");
                        if (Ptt) { if (now - tTx >= 120) { tTx = now; Read(0, "15 11"); Read(0, "15 12"); Read(0, "15 13"); Read(0, "15 14"); } }
                        else if (now - tTx >= 1000) { tTx = now; Read(0, "15 15"); Read(0, "15 16"); Read(0, "15 07"); }
                    }
                    if (now - tMid >= 120)
                    {
                        tMid = now;
                        switch (midI++ % 8)
                        {
                            case 0: Raw(Civ.Cat((byte)0x25, (byte)0)); break;
                            case 1: Raw(Civ.Cat((byte)0x25, (byte)1)); break;
                            case 2: Raw(Civ.Cat((byte)0x26, (byte)0)); break;
                            case 3: Raw(Civ.Cat((byte)0x26, (byte)1)); break;
                            case 4: Raw(new byte[] { 0x1C, 0x00 }); break;
                            case 5: Raw(new byte[] { 0x07, 0xD2 }); break;
                            case 6: Raw(new byte[] { 0x0F }); break;
                            case 7: Raw(new byte[] { 0x21, 0x00 }); Raw(new byte[] { 0x07, 0xC2 }); break;
                        }
                    }
                    if (now - tSlow >= 28)
                    {
                        tSlow = now;
                        string rk = slow[slowI++ % slow.Count]; int rx = rk[0] - '0'; string key = rk.Substring(2);
                        if (key == "1A 03") Raw(Civ.Cat((byte)0x29, (byte)rx, (byte)0x1A, (byte)0x03), rx, "1A 03"); else Read(rx, key);
                        if (slowI % 48 == 0) { Raw(new byte[] { 0x27, 0x14, (byte)(slowI / 48 & 1) }); Raw(new byte[] { 0x27, 0x15, (byte)(slowI / 48 & 1) }); }
                    }
                    if (Connected && Environment.TickCount - (int)LastRx > 4000) { Connected = false; Status = "Poste muet (aucune réponse)"; Version++; }
                }
                // purge des lectures sans réponse
                lock (sync) { for (int i = pending.Count - 1; i >= 0; i--) if (now - pending[i].T > 2500) pending.RemoveAt(i); }
                Thread.Sleep(4);
            }
        }

        // ------------------------------------------------------------- réception
        void OnFrame(byte[] f)
        {
            if (f.Length < 6) return;
            if (f[3] == Civ.Controller && f[2] != 0x00 && f[2] != Civ.Controller) return;   // écho de notre propre trame
            if (f[3] == Civ.Controller) return;
            if (f[3] != Addr && f[3] != 0x00 && !heardAddr) { Addr = f[3]; heardAddr = true; AddrAdopted = true; Version++; Raw((byte)0x19, (byte)0x00); Raw(new byte[] { 0x27, 0x10, 1 }); Raw(new byte[] { 0x27, 0x11, 1 }); for (int r = 0; r < 2; r++) { Raw(new byte[] { 0x27, 0x14, (byte)r }); Raw(new byte[] { 0x27, 0x15, (byte)r }); Raw(new byte[] { 0x27, 0x19, (byte)r }); Raw(new byte[] { 0x27, 0x1A, (byte)r }); } Raw(new byte[] { 0x27, 0x12 }); Raw(new byte[] { 0x27, 0x13 }); }   // the radio answers with another CI-V address: adopt it
            if (f[3] == Addr) heardAddr = true;
            LastRx = clock.ElapsedMilliseconds; LastRx = Environment.TickCount;
            if (!Connected) { Connected = true; Status = "Connecté : " + Link.Name; Version++; }
            fpsCount++;
            long ms = clock.ElapsedMilliseconds; if (ms - fpsT >= 1000) { FramesPerSec = fpsCount; fpsCount = 0; fpsT = ms; }
            byte[] p = new byte[f.Length - 5]; Array.Copy(f, 4, p, 0, p.Length);
            Trace(f, false);
            try { Parse(p); } catch { }
        }

        int TakePending(string key, int dflt)
        {
            lock (sync)
            {
                for (int i = 0; i < pending.Count; i++) if (pending[i].Key == key) { int r = pending[i].Rx; pending.RemoveAt(i); return r; }
            }
            return dflt;
        }
        bool Held(int rx, string key) { long t; lock (sync) { return holdoff.TryGetValue(rx + "|" + key, out t) && clock.ElapsedMilliseconds < t; } }

        void Parse(byte[] p)
        {
            int rx = -1; bool pref = false;
            int c = p[0];
            if (c == 0xFB || c == 0xFA) return;
            if (c == 0x29 && p.Length >= 3) { rx = p[1] & 1; pref = true; byte[] q = new byte[p.Length - 2]; Array.Copy(p, 2, q, 0, q.Length); p = q; c = p[0]; }
            int n = p.Length;
            switch (c)
            {
                case 0x00: case 0x03: case 0x05:
                    if (n >= 6) { int r = rx >= 0 ? rx : Sel; if (!Held(r, "FREQ")) Rx[r].Freq = Civ.Freq(p, 1); Version++; } return;
                case 0x01: case 0x04: case 0x06:
                    if (n >= 2) { int r = rx >= 0 ? rx : Sel; if (!Held(r, "MODE")) { Rx[r].Mode = p[1]; if (n >= 3) Rx[r].Filter = p[2]; } Version++; } return;
                case 0x25: if (n >= 7) { int r = p[1] & 1; if (!Held(r, "FREQ")) Rx[r].Freq = Civ.Freq(p, 2); Version++; } return;
                case 0x26: if (n >= 5) { int r = p[1] & 1; if (!Held(r, "MODE")) { Rx[r].Mode = p[2]; Rx[r].Data = p[3]; Rx[r].Filter = p[4]; } Version++; } return;
                case 0x0F: if (n >= 2) { Split = p[1] == 1; Version++; } return;
                case 0x19: if (n >= 3) { Addr = p[2]; } return;
                case 0x07:
                    if (n >= 3 && p[1] == 0xD2) { Sel = p[2] & 1; Version++; }
                    else if (n >= 3 && p[1] == 0xC2) { Dual = p[2] == 1; Version++; }
                    return;
                case 0x21:
                    if (n >= 3 && p[1] == 0x00 && n >= 5) { int a = Civ.Bcd(p[3]) * 100 + Civ.Bcd(p[2]); Rit = p[4] == 1 ? -a : a; Version++; }
                    else if (n >= 3 && p[1] == 0x01) { RitOn = p[2] == 1; Version++; }
                    else if (n >= 3 && p[1] == 0x02) { XitOn = p[2] == 1; Version++; }
                    return;
                case 0x1A:
                    if (n >= 4 && p[1] == 0x03)
                    {
                        int r = rx >= 0 ? rx : TakePending("1A 03", Sel);
                        if (!Held(r, "WIDTH")) Rx[r].Width = Widths.FromIndex(Rx[r].Mode, Civ.Bcd2(p[2], p[3]));
                        Version++; return;
                    }
                    break;
                case 0x1C:
                    if (n >= 3 && p[1] == 0x00) { Ptt = p[2] == 1; Version++; return; }
                    break;
                case 0x27: ParseScope(p); return;
            }
            GenericReply(p, rx);
        }

        void GenericReply(byte[] p, int rx)
        {
            CatEntry e = null; int subLen = 0; string key = null;
            for (int sl = Math.Min(3, p.Length - 1); sl >= 0; sl--)
            {
                string k = p[0].ToString("X2");
                for (int i = 0; i < sl; i++) k += " " + p[1 + i].ToString("X2");
                e = Catalog.Find(k); if (e != null) { subLen = sl; key = k; break; }
            }
            if (e == null) return;
            int dlen = p.Length - 1 - subLen; if (dlen < e.DataLen) return;
            int r = e.Direct29 ? (rx >= 0 ? rx : TakePending(key, Sel)) : 0;
            if (!e.Direct29 && rx < 0) TakePending(key, 0);
            if (Held(r, key)) return;
            int v = e.DataLen == 2 ? Civ.Bcd2(p[1 + subLen], p[2 + subLen]) : Civ.Bcd(p[1 + subLen]);
            Rx[r].V[key] = v; Version++;
        }

        // ------------------------------------------------------------- flux du scope
        void ParseScope(byte[] p)
        {
            if (p.Length < 5) return;
            if (p[1] == 0x00)
            {
                int rx = p[2] & 1; int div = Civ.Bcd(p[3]); int max = Civ.Bcd(p[4]);
                if (div == 1)
                {
                    scopeAsm[rx].Clear();
                    ScopeFrame h = new ScopeFrame(); h.Rx = rx;
                    int dataStart = 5;
                    if (p.Length >= 17) { h.Fixed = p[5] == 1; h.F1 = Civ.Freq(p, 6); h.F2 = Civ.Freq(p, 11); h.Oor = p[16] == 1; dataStart = 17; }
                    scopeHead[rx] = h;
                    for (int i = dataStart; i < p.Length; i++) scopeAsm[rx].Add(p[i]);
                }
                else
                {
                    if (scopeHead[rx] == null) return;
                    for (int i = 5; i < p.Length; i++) scopeAsm[rx].Add(p[i]);
                }
                if (div == max && scopeHead[rx] != null && scopeAsm[rx].Count > 0)
                {
                    ScopeFrame fr = scopeHead[rx]; fr.Data = scopeAsm[rx].ToArray(); fr.Stamp = clock.ElapsedMilliseconds;
                    scopeHead[rx] = null; scopeAsm[rx].Clear(); LastScope = Environment.TickCount;
                    ScopeFrame dropped; while (ScopeQueue.Count >= 48) ScopeQueue.TryDequeue(out dropped);   // si l interface prend du retard, on jette les plus anciennes
                    ScopeQueue.Enqueue(fr);
                    ScopeFixed[rx] = fr.Fixed; if (fr.Fixed) WinWidth[rx] = fr.Hi - fr.Lo;
                }
                return;
            }
            // réponses de réglage du scope
            int s = p[1]; int r2 = p.Length > 2 ? (p[2] & 1) : 0;
            switch (s)
            {
                case 0x13: if (p.Length >= 3) { ScopeDual = p[2] == 1; Version++; } break;
                case 0x12: if (p.Length >= 3) { ScopeRx = p[2] & 1; Version++; } break;
                case 0x14: if (p.Length >= 4) { ScopeMode[r2] = p[3] & 3; Version++; } break;
                case 0x15: if (p.Length >= 8) { HalfSpan[r2] = Civ.Freq(p, 3); Version++; } break;
                case 0x19: if (p.Length >= 6) { int a = Civ.Bcd(p[3]) * 100 + Civ.Bcd(p[4]); RefTenth[r2] = p[5] == 1 ? -a : a; Version++; } break;
                case 0x1A: if (p.Length >= 4) { Sweep[r2] = p[3]; Version++; } break;
                case 0x17: if (p.Length >= 4) { ScopeHold = p[3] == 1; Version++; } break;
            }
        }

        // ------------------------------------------------------------- utilitaires d'affichage
        public static string SText(int v)
        {
            if (v <= 120) return "S" + (int)Math.Round(v / 120.0 * 9);
            return "S9+" + (int)Math.Round((v - 120) / 121.0 * 60) + "dB";
        }
        public static double ToWatts(int v) { return v / 143.0 * 100.0; }
        public static double ToSwr(int v)
        {
            if (v <= 48) return 1.0 + v / 48.0 * 0.5;
            if (v <= 80) return 1.5 + (v - 48) / 32.0 * 0.5;
            if (v <= 120) return 2.0 + (v - 80) / 40.0 * 1.0;
            return 3.0 + (v - 120) / 135.0 * 7.0;
        }
    }
}
