using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Threading;

namespace IC7760Link
{
    // ---------------------------------------------------------------- trames CI-V
    public static class Civ
    {
        public const byte Controller = 0xE0;

        public static byte[] Build(byte to, byte from, byte[] payload)
        {
            byte[] f = new byte[payload.Length + 5];
            f[0] = 0xFE; f[1] = 0xFE; f[2] = to; f[3] = from;
            Array.Copy(payload, 0, f, 4, payload.Length);
            f[f.Length - 1] = 0xFD;
            return f;
        }

        public static byte[] Cat(params object[] parts)
        {
            List<byte> l = new List<byte>();
            foreach (object o in parts)
            {
                if (o is byte) l.Add((byte)o);
                else if (o is int) l.Add((byte)(int)o);
                else if (o is byte[]) l.AddRange((byte[])o);
                else throw new ArgumentException("type");
            }
            return l.ToArray();
        }

        public static int Bcd(byte b) { return ((b >> 4) & 15) * 10 + (b & 15); }
        public static byte ToBcd(int v) { v %= 100; return (byte)(((v / 10) << 4) | (v % 10)); }
        // 2 octets BCD "00 00 ~ 02 55" -> 0..255 (octet de poids fort = centaines)
        public static int Bcd2(byte hi, byte lo) { return Bcd(hi) * 100 + Bcd(lo); }
        public static byte[] ToBcd2(int v) { if (v < 0) v = 0; if (v > 9999) v = 9999; return new byte[] { ToBcd(v / 100), ToBcd(v % 100) }; }

        // fréquence : 5 octets BCD, poids faible en premier
        public static long Freq(byte[] d, int off)
        {
            long v = 0, mul = 1;
            for (int i = 0; i < 5; i++) { v += Bcd(d[off + i]) * mul; mul *= 100; }
            return v;
        }
        public static byte[] FreqBytes(long hz)
        {
            byte[] b = new byte[5];
            for (int i = 0; i < 5; i++) { b[i] = ToBcd((int)(hz % 100)); hz /= 100; }
            return b;
        }
        public static string Hex(byte[] f, int n)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < n; i++) { if (i > 0) sb.Append(' '); sb.Append(f[i].ToString("X2")); }
            return sb.ToString();
        }
    }

    // Assemble les octets reçus en trames complètes FE FE .. FD
    public class FrameParser
    {
        readonly List<byte> buf = new List<byte>();
        public event Action<byte[]> Frame;
        public void Feed(byte[] data, int n)
        {
            for (int i = 0; i < n; i++)
            {
                byte b = data[i];
                if (buf.Count == 0 && b != 0xFE) continue;
                if (buf.Count == 1 && b != 0xFE) { buf.Clear(); continue; }
                buf.Add(b);
                if (b == 0xFD && buf.Count >= 6)
                {
                    byte[] f = buf.ToArray(); buf.Clear();
                    if (Frame != null) Frame(f);
                }
                else if (buf.Count > 1200) buf.Clear();
            }
        }
        public void Reset() { buf.Clear(); }
    }

    // ---------------------------------------------------------------- liaisons
    public interface ILink
    {
        string Name { get; }
        bool Open(out string err);
        void Close();
        void Send(byte[] frame);
        event Action<byte[]> Frame;
        bool IsOpen { get; }
    }

    public class SerialLink : ILink
    {
        SerialPort port; readonly FrameParser parser = new FrameParser();
        readonly string portName; readonly int baud;
        public event Action<byte[]> Frame;
        public SerialLink(string portName, int baud) { this.portName = portName; this.baud = baud; parser.Frame += delegate(byte[] f) { if (Frame != null) Frame(f); }; }
        public string Name { get { return portName + " @" + baud; } }
        public bool IsOpen { get { return port != null && port.IsOpen; } }
        public bool Open(out string err)
        {
            err = null;
            try
            {
                port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One);
                port.Handshake = Handshake.None;
                port.ReadBufferSize = 1 << 16; port.WriteBufferSize = 1 << 14;
                port.DtrEnable = false; port.RtsEnable = false; // jamais de PTT/CW par DTR/RTS
                port.ReadTimeout = 50; port.WriteTimeout = 500;
                port.DataReceived += OnData;
                port.Open();
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }
        void OnData(object s, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort p = port; if (p == null) return;
                int n = p.BytesToRead; if (n <= 0) return;
                byte[] b = new byte[n]; n = p.Read(b, 0, n);
                parser.Feed(b, n);
            }
            catch { }
        }
        public void Send(byte[] frame)
        {
            try { SerialPort p = port; if (p != null && p.IsOpen) p.Write(frame, 0, frame.Length); } catch { }
        }
        public void Close()
        {
            try { if (port != null) { port.DataReceived -= OnData; if (port.IsOpen) port.Close(); port.Dispose(); } } catch { }
            port = null;
        }
    }

    // Poste virtuel : l'IC-7760 simulé tourne dans le programme, aucun matériel nécessaire
    public class VirtualLink : ILink
    {
        public readonly VirtualRadio Radio;
        public event Action<byte[]> Frame;
        bool open;
        public VirtualLink(byte addr) { Radio = new VirtualRadio(addr); Radio.Output += delegate(byte[] f) { if (open && Frame != null) Frame(f); }; }
        public string Name { get { return "Poste virtuel IC-7760"; } }
        public bool IsOpen { get { return open; } }
        public bool Open(out string err) { err = null; open = true; Radio.Start(); return true; }
        public void Close() { open = false; Radio.Stop(); }
        public void Send(byte[] frame) { if (open) Radio.Receive(frame); }
    }

    // ---------------------------------------------------------------- catalogue (manuel CI-V extrait)
    public class CatEntry
    {
        public int Cmd; public byte[] Sub; public string SubText, Data, Name, Notes;
        public bool Direct29;
        public int DataLen = 1;        // octets de donnée
        public int Min, Max; public bool HasRange; public bool Raw;
        public string Key { get { return Cmd.ToString("X2") + (SubText.Length > 0 ? " " + SubText : ""); } }
    }

    public static class Catalog
    {
        public static readonly List<CatEntry> All = new List<CatEntry>();
        static readonly Dictionary<string, CatEntry> byKey = new Dictionary<string, CatEntry>();
        public static CatEntry Find(string key) { CatEntry e; byKey.TryGetValue(key, out e); return e; }

        static Catalog()
        {
            try
            {
                Assembly a = Assembly.GetExecutingAssembly();
                using (Stream s = a.GetManifestResourceStream("ic7760.civ"))
                {
                    if (s == null) return;
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            if (line.Length == 0 || line[0] == '#') continue;
                            string[] p = line.Split('|');
                            if (p.Length < 4) continue;
                            CatEntry e = new CatEntry();
                            int c; if (!int.TryParse(p[0], System.Globalization.NumberStyles.HexNumber, null, out c)) continue;
                            e.Cmd = c; e.SubText = p[1].Trim(); e.Data = p[2]; e.Name = p[3]; e.Notes = p.Length > 4 ? p[4] : "";
                            List<byte> sb = new List<byte>();
                            foreach (string t in e.SubText.Split(' ')) { int v; if (t.Length == 2 && int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out v)) sb.Add((byte)v); else { sb.Clear(); break; } }
                            e.Sub = sb.ToArray();
                            e.Direct29 = e.Notes.Contains("[29]");
                            ParseData(e);
                            All.Add(e);
                            byKey[e.Key] = e;
                        }
                    }
                }
            }
            catch { }
        }

        static void ParseData(CatEntry e)
        {
            string d = e.Data;
            e.Raw = true;
            if (d.StartsWith("BCD"))
            {
                e.Raw = false;
                e.DataLen = d.Contains("2 bytes") ? 2 : 1;
                int i = d.IndexOf("bytes ") >= 0 ? d.IndexOf("bytes ") + 6 : d.IndexOf("byte ") + 5;
                if (i > 4 && i < d.Length)
                {
                    string rest = d.Substring(i).Trim();
                    string[] w = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    List<string> nums = new List<string>();
                    foreach (string t in w) { string u = t.Trim('(', ')', ','); if (u == "~" || u == "/") { nums.Add(u); continue; } int dummy; if (int.TryParse(u, out dummy)) nums.Add(u); else break; }
                    // "00 ~ 10", "00/01", "00 00 ~ 02 55", "00 50 ~ 02 00"
                    try
                    {
                        if (e.DataLen == 1)
                        {
                            if (nums.Count >= 3 && nums[1] == "~") { e.Min = int.Parse(nums[0]); e.Max = int.Parse(nums[2]); e.HasRange = true; }
                            else if (rest.Length >= 5 && rest[2] == '/') { e.Min = int.Parse(rest.Substring(0, 2)); e.Max = int.Parse(rest.Substring(3, 2)); e.HasRange = true; }
                        }
                        else if (nums.Count >= 5 && nums[2] == "~") { e.Min = int.Parse(nums[0]) * 100 + int.Parse(nums[1]); e.Max = int.Parse(nums[3]) * 100 + int.Parse(nums[4]); e.HasRange = true; }
                    }
                    catch { }
                }
                if (!e.HasRange) e.Raw = true;
            }
        }
    }
}
