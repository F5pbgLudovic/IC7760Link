using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace IC7760Link
{
    // Périphériques audio de Windows (Core Audio) : retrouve les deux bouts d'un câble virtuel et renomme
    // le bout "enregistrement" (celui que WSJT-X choisit comme entrée), comme le fait le panneau Son de Windows ;
    // coupe aussi le son d'un autre logiciel (ExpertSDR) dans le mélangeur de volume.
    public static class AudioEndpoints
    {
        public class Endpoint
        {
            public string Id;
            public bool Capture;        // true : enregistrement (entrée), false : lecture (sortie)
            public string Name;         // nom modifiable, ex. "CABLE Output"
            public string Adapter;      // pilote, ex. "VB-Audio Virtual Cable"
            public string FullName;     // "CABLE Output (VB-Audio Virtual Cable)"
        }

        const int eRender = 0, eCapture = 1, DEVICE_STATE_ACTIVE = 1;
        const int STGM_READ = 0, STGM_READWRITE = 2;
        const int VT_LPWSTR = 31;
        public const int E_ACCESSDENIED = unchecked((int)0x80070005);

        static PropertyKey DeviceDesc = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
        static PropertyKey FriendlyName = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        static PropertyKey InterfaceName = new PropertyKey(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);

        public static List<Endpoint> All()
        {
            List<Endpoint> r = new List<Endpoint>();
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            try
            {
                foreach (int flow in new[] { eRender, eCapture })
                {
                    IMMDeviceCollection col;
                    if (en.EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE, out col) != 0) continue;
                    int n;
                    col.GetCount(out n);
                    for (int i = 0; i < n; i++)
                    {
                        IMMDevice d;
                        if (col.Item(i, out d) != 0) continue;
                        Endpoint e = new Endpoint();
                        e.Capture = flow == eCapture;
                        d.GetId(out e.Id);
                        IPropertyStore ps;
                        if (d.OpenPropertyStore(STGM_READ, out ps) == 0)
                        {
                            e.Name = GetString(ps, DeviceDesc);
                            e.FullName = GetString(ps, FriendlyName);
                            e.Adapter = GetString(ps, InterfaceName);
                            Marshal.ReleaseComObject(ps);
                        }
                        Marshal.ReleaseComObject(d);
                        r.Add(e);
                    }
                    Marshal.ReleaseComObject(col);
                }
            }
            finally { Marshal.ReleaseComObject(en); }
            return r;
        }

        // Bout "enregistrement" du câble dont la sortie waveOut porte ce nom (Windows le tronque à 31 caractères).
        public static Endpoint CaptureOf(string waveOutName)
        {
            if (string.IsNullOrEmpty(waveOutName)) return null;
            List<Endpoint> all = All();
            Endpoint render = all.Find(delegate(Endpoint e)
            {
                return !e.Capture && e.FullName != null && e.FullName.StartsWith(waveOutName, StringComparison.Ordinal);
            });
            if (render == null || string.IsNullOrEmpty(render.Adapter)) return null;
            List<Endpoint> caps = all.FindAll(delegate(Endpoint e) { return e.Capture && e.Adapter == render.Adapter; });
            return caps.Count == 1 ? caps[0] : null;   // un seul bout possible, sinon on ne devine pas
        }

        // Renomme un périphérique. Retourne 0, ou E_ACCESSDENIED s'il faut les droits d'administrateur.
        public static int SetName(string id, string name)
        {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice d = null;
            IPropertyStore ps = null;
            try
            {
                int hr = en.GetDevice(id, out d);
                if (hr != 0) return hr;
                hr = d.OpenPropertyStore(STGM_READWRITE, out ps);
                if (hr != 0) return hr;
                PropVariant v = new PropVariant();
                v.vt = VT_LPWSTR;
                v.p = Marshal.StringToCoTaskMemUni(name);
                try
                {
                    hr = ps.SetValue(ref DeviceDesc, ref v);
                    if (hr == 0) hr = ps.Commit();
                }
                finally { Marshal.FreeCoTaskMem(v.p); }
                return hr;
            }
            finally
            {
                if (ps != null) Marshal.ReleaseComObject(ps);
                if (d != null) Marshal.ReleaseComObject(d);
                Marshal.ReleaseComObject(en);
            }
        }

        // Interface Core Audio (IAudioClient…) de la sortie par défaut de Windows (haut-parleurs), avec son nom.
        // Retourne un pointeur COM à libérer par l'appelant.
        public static IntPtr ActivateDefaultOutput(Guid iid, out string name)
        {
            return Activate(null, iid, out name);
        }

        // Même chose pour un périphérique donné par son identifiant (entrée d'une carte son, par exemple).
        public static IntPtr ActivateDevice(string id, Guid iid, out string name)
        {
            return Activate(id, iid, out name);
        }

        static IntPtr Activate(string id, Guid iid, out string name)
        {
            name = null;
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice d = null;
            try
            {
                int found = id == null ? en.GetDefaultAudioEndpoint(eRender, 0, out d) : en.GetDevice(id, out d);
                if (found != 0 && id != null) throw new System.IO.IOException(Lang.T("carte son introuvable", "sound card not found"));
                Marshal.ThrowExceptionForHR(found);
                int state;
                if (id != null && (d.GetState(out state) != 0 || state != DEVICE_STATE_ACTIVE))
                    throw new System.IO.IOException(Lang.T("carte son débranchée ou désactivée", "sound card unplugged or disabled"));
                IPropertyStore ps;
                if (d.OpenPropertyStore(STGM_READ, out ps) == 0)
                {
                    name = GetString(ps, FriendlyName);
                    Marshal.ReleaseComObject(ps);
                }
                IntPtr p;
                Marshal.ThrowExceptionForHR(d.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out p));
                return p;
            }
            finally
            {
                if (d != null) Marshal.ReleaseComObject(d);
                Marshal.ReleaseComObject(en);
            }
        }

        // Coupure du son d'un logiciel dans le mélangeur de volume de Windows (toutes les sorties) :
        // mute = null lit l'état, sinon l'applique. Retourne true si toutes ses sessions sont coupées,
        // false sinon, null s'il n'a aucune session audio.
        public static bool? ProcessMute(Predicate<int> isProcess, bool? mute)
        {
            bool allMuted = true;
            bool any = ForEachSession(isProcess, delegate(ISimpleAudioVolume vol)
            {
                if (mute.HasValue) vol.SetMute(mute.Value ? 1 : 0, IntPtr.Zero);
                int m;
                vol.GetMute(out m);
                if (m == 0) allMuted = false;
            });
            return any ? allMuted : (bool?)null;
        }

        // Volume d'un logiciel dans le mélangeur (0 à 100 %) : percent = null le lit (le plus fort de ses sessions),
        // sinon l'applique à toutes ses sessions. Retourne -1 s'il n'a aucune session audio.
        // Volume exact d'un logiciel dans le mélangeur (0 à 1, plus fin que le pour cent) : false s'il n'a aucune session.
        public static bool ProcessLevel(Predicate<int> isProcess, float level)
        {
            return ForEachSession(isProcess, delegate(ISimpleAudioVolume vol) { vol.SetMasterVolume(Math.Max(0f, Math.Min(1f, level)), IntPtr.Zero); });
        }

        public static int ProcessVolume(Predicate<int> isProcess, int? percent)
        {
            float max = -1;
            ForEachSession(isProcess, delegate(ISimpleAudioVolume vol)
            {
                if (percent.HasValue) vol.SetMasterVolume(Math.Max(0, Math.Min(100, percent.Value)) / 100f, IntPtr.Zero);
                float level;
                if (vol.GetMasterVolume(out level) == 0 && level > max) max = level;
            });
            return max < 0 ? -1 : (int)Math.Round(max * 100);
        }

        delegate void SessionAction(ISimpleAudioVolume vol);

        // Applique l'action à chaque session audio (toutes les sorties) des processus choisis ; false s'il n'y en a aucune.
        static bool ForEachSession(Predicate<int> isProcess, SessionAction action)
        {
            bool any = false;
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            try
            {
                IMMDeviceCollection col;
                if (en.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out col) != 0) return false;
                int n;
                col.GetCount(out n);
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d;
                    if (col.Item(i, out d) != 0) continue;
                    Guid iid = typeof(IAudioSessionManager2).GUID;
                    IntPtr p;
                    if (d.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out p) == 0)
                    {
                        IAudioSessionManager2 mgr = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(p);
                        Marshal.Release(p);
                        IAudioSessionEnumerator sessions;
                        if (mgr.GetSessionEnumerator(out sessions) == 0)
                        {
                            int count;
                            sessions.GetCount(out count);
                            for (int k = 0; k < count; k++)
                            {
                                object o;
                                if (sessions.GetSession(k, out o) != 0) continue;
                                IAudioSessionControl2 ctl = o as IAudioSessionControl2;
                                int pid;
                                if (ctl != null && ctl.GetProcessId(out pid) == 0 && isProcess(pid))
                                {
                                    action((ISimpleAudioVolume)o);
                                    any = true;
                                }
                                Marshal.ReleaseComObject(o);
                            }
                            Marshal.ReleaseComObject(sessions);
                        }
                        Marshal.ReleaseComObject(mgr);
                    }
                    Marshal.ReleaseComObject(d);
                }
                Marshal.ReleaseComObject(col);
            }
            finally { Marshal.ReleaseComObject(en); }
            return any;
        }

        const int CLSCTX_ALL = 23;

        static string GetString(IPropertyStore ps, PropertyKey key)
        {
            PropVariant v;
            if (ps.GetValue(ref key, out v) != 0) return null;
            try { return v.vt == VT_LPWSTR ? Marshal.PtrToStringUni(v.p) : null; }
            finally { PropVariantClear(ref v); }
        }

        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant v);

        [StructLayout(LayoutKind.Sequential)]
        struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
            public PropertyKey(Guid g, int id) { fmtid = g; pid = id; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct PropVariant
        {
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public IntPtr p;
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr instance);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetAt(int index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
        }

        // IAudioSessionControl puis IAudioSessionControl2 : seules les méthodes de lecture du processus servent.
        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName(out IntPtr name);
            [PreserveSig] int SetDisplayName(IntPtr name, IntPtr context);
            [PreserveSig] int GetIconPath(out IntPtr path);
            [PreserveSig] int SetIconPath(IntPtr path, IntPtr context);
            [PreserveSig] int GetGroupingParam(out Guid group);
            [PreserveSig] int SetGroupingParam(IntPtr group, IntPtr context);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int GetSessionIdentifier(out IntPtr id);
            [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
            [PreserveSig] int GetProcessId(out int pid);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, IntPtr context);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute(int mute, IntPtr context);
            [PreserveSig] int GetMute(out int mute);
        }
    }
}


namespace IC7760Link
{
    // interfaces COM WASAPI utilisées par RigMonitor
    public static class AudioRecorder
    {
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out int frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out int frames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, out IntPtr service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePosition, out long qpcPosition);
        [PreserveSig] int ReleaseBuffer(int frames);
        [PreserveSig] int GetNextPacketSize(out int frames);
    }
    }
    // pas de réducteur de bruit ni de notch dans HamRadioLink : le micro passe brut
    class NoiseReducer { public static bool Active { get { return false; } } public void Process(int rate, float[] m, int n) { } }
    class RigNotch { public static bool Active { get { return false; } } public void Process(int rate, float[] m, int n) { } }
}


namespace IC7760Link
{
    // Écoute du poste sur le PC (clic droit sur Mute TX) : le son de l'entrée de la carte son du poste (celle choisie au
    // clic droit sur REC) est rejoué sur la sortie par défaut de Windows (haut-parleurs), en WASAPI partagé, avec peu de
    // retard (50 à 120 ms). Muted : la carte reste ouverte mais rien n'est joué (Mute TX actif, émission), pour repartir
    // tout de suite.
    public class RigMonitor
    {
        const int AUDCLNT_SHAREMODE_SHARED = 0, AUDCLNT_BUFFERFLAGS_SILENT = 2;
        const long BufferHns = 1000000;   // 100 ms de tampon de chaque côté
        static readonly Guid FloatFormat = new Guid("00000003-0000-0010-8000-00aa00389b71");

        public readonly string CaptureId;
        // Micro du PC vers le poste (1.51) : sortie choisie (carte son d'émission du poste) au lieu de la sortie par défaut,
        // son brut (ni notch ni DNR) et retard plus court.
        public readonly string OutputId;
        readonly bool raw;
        readonly string label;
        public volatile bool Muted;
        // « Volume TX sur PC » (1.45) : seulement le son rejoué dans les haut-parleurs. REC, la diversité et les décodeurs
        // ouvrent leur propre capture de la carte : ce volume, même à 0, ne leur enlève rien.
        public volatile float Gain = 1f;
        // Problème d'ouverture ou de lecture (appelé depuis le fil audio, une fois par message différent) ; null = tout va bien.
        public event Action<string> Problem;
        public string CardName { get; private set; }
        public string OutputName { get; private set; }

        readonly Thread thread;
        volatile bool stopping;
        string lastProblem = "";

        AudioRecorder.IAudioClient inClient, outClient;
        AudioRecorder.IAudioCaptureClient capture;
        IAudioRenderClient render;
        Fmt inFmt, outFmt;
        int outBuffer;

        // Son en attente, stéréo à la fréquence de la sortie.
        float[] ring = new float[0];
        int head, count;          // en trames (2 valeurs)
        bool waiting = true;      // après un manque de son : on attend un petit matelas avant de rejouer
        double step, t, lastL, lastR;
        bool haveLast;
        byte[] inBytes = new byte[0], outBytes = new byte[0];
        // DNR du poste (1.48) : même type et même niveau que celui du WebSDR, si la case du menu DNR le demande.

        struct Fmt { public int Rate, Channels, Bits; public bool Float; public int Align { get { return Bits / 8 * Channels; } } }

        public RigMonitor(string captureId, bool muted) : this(captureId, null, muted, false, "Écoute du poste") { }

        public RigMonitor(string captureId, string outputId, bool muted, bool raw, string label)
        {
            CaptureId = captureId;
            OutputId = outputId;
            this.raw = raw;
            this.label = label;
            Muted = muted;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = label;
            thread.Start();
        }

        public void Stop()
        {
            stopping = true;
            thread.Join(3000);
        }

        void Run()
        {
            while (!stopping)
            {
                if (inClient == null)
                {
                    try { Open(); Report(null); }
                    catch (Exception ex) { Report(ex.Message); Close(); Sleep(1000); continue; }
                }
                try { Pump(); }
                catch (Exception ex) { Report(ex.Message); Close(); Sleep(500); continue; }
                Thread.Sleep(5);
            }
            Close();
        }

        void Sleep(int ms) { for (int i = 0; i < ms / 50 && !stopping; i++) Thread.Sleep(50); }

        void Report(string message)
        {
            if ((message ?? "") == lastProblem) return;
            lastProblem = message ?? "";
            Action<string> h = Problem;
            if (h != null) h(message);
        }

        static AudioRecorder.IAudioClient Activate(string id, out string name)
        {
            IntPtr p = id == null ? AudioEndpoints.ActivateDefaultOutput(typeof(AudioRecorder.IAudioClient).GUID, out name)
                : AudioEndpoints.ActivateDevice(id, typeof(AudioRecorder.IAudioClient).GUID, out name);
            try { return (AudioRecorder.IAudioClient)Marshal.GetObjectForIUnknown(p); }
            finally { Marshal.Release(p); }
        }

        static Fmt Init(AudioRecorder.IAudioClient c)
        {
            IntPtr wf;
            Marshal.ThrowExceptionForHR(c.GetMixFormat(out wf));
            try
            {
                Fmt f = new Fmt();
                int tag = Marshal.ReadInt16(wf, 0) & 0xFFFF;
                f.Channels = Marshal.ReadInt16(wf, 2);
                f.Rate = Marshal.ReadInt32(wf, 4);
                f.Bits = Marshal.ReadInt16(wf, 14);
                f.Float = tag == 3;
                if (tag == 0xFFFE)
                {
                    byte[] g = new byte[16];
                    Marshal.Copy(new IntPtr(wf.ToInt64() + 24), g, 0, 16);
                    f.Float = new Guid(g) == FloatFormat;
                }
                if (f.Channels < 1 || f.Rate < 8000 || (f.Float ? f.Bits != 32 : f.Bits != 16 && f.Bits != 24 && f.Bits != 32))
                    throw new IOException(Lang.F("format audio non pris en charge ({0} bits, {1} Hz)", "unsupported audio format ({0} bits, {1} Hz)", f.Bits, f.Rate));
                Marshal.ThrowExceptionForHR(c.Initialize(AUDCLNT_SHAREMODE_SHARED, 0, BufferHns, 0, wf, IntPtr.Zero));
                return f;
            }
            finally { Marshal.FreeCoTaskMem(wf); }
        }

        static T Service<T>(AudioRecorder.IAudioClient c)
        {
            Guid iid = typeof(T).GUID;
            IntPtr p;
            Marshal.ThrowExceptionForHR(c.GetService(ref iid, out p));
            try { return (T)Marshal.GetObjectForIUnknown(p); }
            finally { Marshal.Release(p); }
        }

        void Open()
        {
            string name;
            inClient = Activate(CaptureId, out name);
            CardName = name ?? "?";
            inFmt = Init(inClient);
            capture = Service<AudioRecorder.IAudioCaptureClient>(inClient);
            outClient = Activate(OutputId, out name);
            OutputName = name ?? "?";
            outFmt = Init(outClient);
            render = Service<IAudioRenderClient>(outClient);
            Marshal.ThrowExceptionForHR(outClient.GetBufferSize(out outBuffer));
            ring = new float[outFmt.Rate * 2];   // 1 s au plus
            head = count = 0;
            waiting = true;
            step = (double)inFmt.Rate / outFmt.Rate;
            t = 0;
            haveLast = false;
            Marshal.ThrowExceptionForHR(inClient.Start());
            Marshal.ThrowExceptionForHR(outClient.Start());
            CatLog.Event("micro", label + " : « " + CardName + " » (" + inFmt.Rate + " Hz) rejouée sur « " + OutputName + " » (" + outFmt.Rate + " Hz, " + outFmt.Channels + " voies)");
        }

        void Close()
        {
            if (inClient != null) { try { inClient.Stop(); } catch { } }
            if (outClient != null) { try { outClient.Stop(); } catch { } }
            if (capture != null) { Marshal.ReleaseComObject(capture); capture = null; }
            if (render != null) { Marshal.ReleaseComObject(render); render = null; }
            if (inClient != null) { Marshal.ReleaseComObject(inClient); inClient = null; }
            if (outClient != null) { Marshal.ReleaseComObject(outClient); outClient = null; }
        }

        void Pump()
        {
            // Entrée : tout ce que la carte a donné, rééchantillonné à la fréquence de la sortie.
            while (true)
            {
                int n;
                Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out n));
                if (n == 0) break;
                IntPtr data;
                int frames, flags;
                long pos, qpc;
                Marshal.ThrowExceptionForHR(capture.GetBuffer(out data, out frames, out flags, out pos, out qpc));
                try { if (!Muted) Take(data, frames, (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0); }
                finally { capture.ReleaseBuffer(frames); }
            }
            if (Muted) { count = 0; waiting = true; return; }
            Play();
        }

        // Sortie : le son en attente est donné aux haut-parleurs, autant qu'ils en acceptent.
        void Play()
        {
            // Retard limité : au-delà de 120 ms en attente, on saute pour revenir à 50 ms (micro : 60 ms, retour à 25 ms).
            int rate = outFmt.Rate;
            if (raw) { if (count > rate * 6 / 100) Drop(count - rate * 25 / 1000); }
            else if (count > rate * 12 / 100) Drop(count - rate * 5 / 100);

            int padding;
            Marshal.ThrowExceptionForHR(outClient.GetCurrentPadding(out padding));
            if (waiting)
            {
                if (count < rate * (raw ? 15 : 30) / 1000) return;   // matelas de 30 ms (micro : 15 ms) avant de (re)commencer
                waiting = false;
            }
            int free = outBuffer - padding, todo = Math.Min(free, count);
            if (count == 0 && padding == 0) { waiting = true; return; }
            if (todo <= 0) return;
            IntPtr buf;
            Marshal.ThrowExceptionForHR(render.GetBuffer(todo, out buf));
            int size = todo * outFmt.Align;
            if (outBytes.Length < size) outBytes = new byte[size];
            int o = 0, bytes = outFmt.Bits / 8;
            float g = Gain;
            for (int i = 0; i < todo; i++)
            {
                int k = (head + i) % (ring.Length / 2) * 2;
                float l = ring[k] * g, r = ring[k + 1] * g;
                for (int c = 0; c < outFmt.Channels; c++)
                {
                    float v = outFmt.Channels == 1 ? (l + r) * 0.5f : c % 2 == 0 ? l : r;
                    Put(v, o, bytes);
                    o += bytes;
                }
            }
            Marshal.Copy(outBytes, 0, buf, size);
            Marshal.ThrowExceptionForHR(render.ReleaseBuffer(todo, 0));
            Drop(todo);
        }

        void Drop(int frames)
        {
            frames = Math.Min(frames, count);
            head = (head + frames) % (ring.Length / 2);
            count -= frames;
        }

        void Put(float v, int o, int bytes)
        {
            if (outFmt.Float) { byte[] b = BitConverter.GetBytes(v); Buffer.BlockCopy(b, 0, outBytes, o, 4); return; }
            double d = Math.Max(-1.0, Math.Min(1.0, v));
            if (bytes == 2) { int s = (int)Math.Round(d * 32767); outBytes[o] = (byte)s; outBytes[o + 1] = (byte)(s >> 8); }
            else if (bytes == 3) { int s = (int)Math.Round(d * 8388607); outBytes[o] = (byte)s; outBytes[o + 1] = (byte)(s >> 8); outBytes[o + 2] = (byte)(s >> 16); }
            else { int s = (int)Math.Round(d * 2147483647.0); outBytes[o] = (byte)s; outBytes[o + 1] = (byte)(s >> 8); outBytes[o + 2] = (byte)(s >> 16); outBytes[o + 3] = (byte)(s >> 24); }
        }

        void Take(IntPtr data, int frames, bool silent)
        {
            int bytes = inFmt.Bits / 8, align = inFmt.Align, size = frames * align;
            if (!silent)
            {
                if (inBytes.Length < size) inBytes = new byte[size];
                Marshal.Copy(data, inBytes, 0, size);
            }
            int cap = ring.Length / 2;
            for (int i = 0; i < frames; i++)
            {
                double l = 0, r = 0;
                if (!silent)
                {
                    l = Sample(i * align, bytes);
                    r = inFmt.Channels > 1 ? Sample(i * align + bytes, bytes) : l;
                }
                if (!haveLast) { lastL = l; lastR = r; haveLast = true; }
                while (t < 1.0)
                {
                    if (count == cap) Drop(1);
                    int k = (head + count) % cap * 2;
                    ring[k] = (float)(lastL + (l - lastL) * t);
                    ring[k + 1] = (float)(lastR + (r - lastR) * t);
                    count++;
                    t += step;
                }
                t -= 1.0;
                lastL = l;
                lastR = r;
            }
        }

        double Sample(int o, int bytes)
        {
            if (inFmt.Float) return BitConverter.ToSingle(inBytes, o);
            if (bytes == 2) return BitConverter.ToInt16(inBytes, o) / 32768.0;
            if (bytes == 3) return ((inBytes[o] << 8 | inBytes[o + 1] << 16 | inBytes[o + 2] << 24) >> 8) / 8388608.0;
            return BitConverter.ToInt32(inBytes, o) / 2147483648.0;
        }

        [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioRenderClient
        {
            [PreserveSig] int GetBuffer(int frames, out IntPtr data);
            [PreserveSig] int ReleaseBuffer(int frames, int flags);
        }
    }
}


namespace IC7760Link
{
    // Bouton Tune (1.49) : tonalité sinusoïdale (1500 Hz) jouée sur la carte son d'émission du poste (sortie Windows reliée
    // à l'entrée modulation / USB du poste), en WASAPI partagé au format de la sortie, sur son propre fil.
    // Montée et descente de 10 ms : pas de claquement au début ni à la fin.
    public class TuneTone
    {
        const int AUDCLNT_SHAREMODE_SHARED = 0;
        static readonly Guid FloatFormat = new Guid("00000003-0000-0010-8000-00aa00389b71");
        readonly string deviceId;
        readonly double freq;
        public volatile float Level;          // 0 à 1 (proportion de la pleine échelle)
        volatile bool stop;
        Thread thread;
        public volatile string Error;         // raison d'un échec (carte introuvable…), null si tout va bien
        public volatile string DeviceName;
        public long FramesPlayed;             // pour les essais

        public TuneTone(string deviceId, double freq, float level)
        {
            this.deviceId = deviceId;
            this.freq = freq;
            Level = level;
        }

        public void Start()
        {
            stop = false;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "Tune";
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        // Arrêt avec la descente de 10 ms, puis attente de la fin du fil.
        public void Stop()
        {
            stop = true;
            Thread t = thread;
            if (t != null) t.Join(1000);
            thread = null;
        }

        public bool Running { get { Thread t = thread; return t != null && t.IsAlive; } }

        void Run()
        {
            AudioRecorder.IAudioClient client = null;
            RigMonitor.IAudioRenderClient render = null;
            try
            {
                string name;
                IntPtr p = string.IsNullOrEmpty(deviceId)
                    ? AudioEndpoints.ActivateDefaultOutput(typeof(AudioRecorder.IAudioClient).GUID, out name)
                    : AudioEndpoints.ActivateDevice(deviceId, typeof(AudioRecorder.IAudioClient).GUID, out name);
                try { client = (AudioRecorder.IAudioClient)Marshal.GetObjectForIUnknown(p); }
                finally { Marshal.Release(p); }
                DeviceName = name;
                IntPtr wf;
                Marshal.ThrowExceptionForHR(client.GetMixFormat(out wf));
                int channels, rate, bits;
                bool isFloat;
                try
                {
                    int tag = Marshal.ReadInt16(wf, 0) & 0xFFFF;
                    channels = Marshal.ReadInt16(wf, 2);
                    rate = Marshal.ReadInt32(wf, 4);
                    bits = Marshal.ReadInt16(wf, 14);
                    isFloat = tag == 3;
                    if (tag == 0xFFFE)
                    {
                        byte[] g = new byte[16];
                        Marshal.Copy(new IntPtr(wf.ToInt64() + 24), g, 0, 16);
                        isFloat = new Guid(g) == FloatFormat;
                    }
                    if (channels < 1 || rate < 8000 || (isFloat ? bits != 32 : bits != 16 && bits != 24 && bits != 32))
                        throw new IOException(Lang.F("format audio non pris en charge ({0} bits, {1} Hz)", "unsupported audio format ({0} bits, {1} Hz)", bits, rate));
                    Marshal.ThrowExceptionForHR(client.Initialize(AUDCLNT_SHAREMODE_SHARED, 0, 1000000, 0, wf, IntPtr.Zero));   // 100 ms
                }
                finally { Marshal.FreeCoTaskMem(wf); }
                Guid iid = typeof(RigMonitor.IAudioRenderClient).GUID;
                IntPtr r;
                Marshal.ThrowExceptionForHR(client.GetService(ref iid, out r));
                try { render = (RigMonitor.IAudioRenderClient)Marshal.GetObjectForIUnknown(r); }
                finally { Marshal.Release(r); }
                int bufferFrames;
                Marshal.ThrowExceptionForHR(client.GetBufferSize(out bufferFrames));
                CatLog.Event("tune", "Tune : tonalité " + freq + " Hz sur « " + name + " » (" + rate + " Hz, " + bits + " bits" + (isFloat ? " flottants" : "") + ")");

                double phase = 0, dphi = 2 * Math.PI * freq / rate;
                double ramp = 0, rampStep = 1.0 / (rate * 0.01);
                int bpp = bits / 8;
                byte[] bytes = new byte[bufferFrames * bpp * channels];
                bool started = false, ending = false;
                while (true)
                {
                    if (stop) ending = true;
                    int padding;
                    Marshal.ThrowExceptionForHR(client.GetCurrentPadding(out padding));
                    int todo = bufferFrames - padding;
                    if (todo > 0)
                    {
                        IntPtr buf;
                        Marshal.ThrowExceptionForHR(render.GetBuffer(todo, out buf));
                        int o = 0;
                        float lvl = Math.Max(0f, Math.Min(1f, Level));
                        for (int i = 0; i < todo; i++)
                        {
                            if (ending) ramp = Math.Max(0, ramp - rampStep); else ramp = Math.Min(1, ramp + rampStep);
                            double v = Math.Sin(phase) * lvl * ramp;
                            phase += dphi;
                            if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
                            for (int c = 0; c < channels; c++)
                            {
                                if (isFloat) Buffer.BlockCopy(BitConverter.GetBytes((float)v), 0, bytes, o, 4);
                                else
                                {
                                    int s = bpp == 2 ? (int)Math.Round(v * 32767) : bpp == 3 ? (int)Math.Round(v * 8388607) : (int)Math.Round(v * 2147483647.0);
                                    for (int k = 0; k < bpp; k++) bytes[o + k] = (byte)(s >> (8 * k));
                                }
                                o += bpp;
                            }
                        }
                        Marshal.Copy(bytes, 0, buf, o);
                        Marshal.ThrowExceptionForHR(render.ReleaseBuffer(todo, 0));
                        FramesPlayed += todo;
                        if (!started) { Marshal.ThrowExceptionForHR(client.Start()); started = true; }
                    }
                    if (ending && ramp <= 0)
                    {
                        // Laisse jouer ce qui est déjà dans le tampon (fin de la descente), puis arrête.
                        for (int i = 0; i < 30; i++) { int pd; if (client.GetCurrentPadding(out pd) != 0 || pd == 0) break; Thread.Sleep(5); }
                        break;
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                CatLog.Event("tune", "Tune : sortie audio impossible : " + ex.Message);
            }
            finally
            {
                if (client != null) { try { client.Stop(); } catch { } }
                if (render != null) Marshal.ReleaseComObject(render);
                if (client != null) Marshal.ReleaseComObject(client);
            }
        }
    }
}
