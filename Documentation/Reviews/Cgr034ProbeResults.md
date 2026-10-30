# CGR-034 probe results

The probe behind CGR-034 (`curl_multi_socket_action` with an application event loop), rebuilt as a C# file-based
app for BL-3978. It runs each case through REF, a real `libcurl-4.dll` (curl 8.22.0, Schannel, Git for Windows
`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) called through P/Invoke, and through PORT, Curl's managed `Libcurl`.
Both sides use the same loop: `WSAPoll` on the sockets the socket callback names, `curl_multi_socket_action(fd, ev)`
on each event, `curl_multi_socket_action(CURL_SOCKET_TIMEOUT)` when the timer is due, and `getsockopt(SO_TYPE)` on
every socket it is told. A recording server answers on 127.0.0.1: 21700 `200 hello`, 21701 `200 other`, 21702
accepts and stays silent; 21799 is closed.

## Commands

Copy the source below to `.cgr034probe/probe.cs` at the repository root (not committed) and run, on Windows:

```sh
dotnet run probe.cs -- REF "C:\Program Files\Git\ucrt64\bin\libcurl-4.dll" C_20,C_21,C_65,C_74,C_75
dotnet run probe.cs -- PORT x C_20,C_21,C_65,C_74,C_75
```

The log reads: `Tn` the timer callback's value (`T400+` for 400 ms or more), `add` after every handle is added,
`kick` after the first `curl_multi_socket_action(CURL_SOCKET_TIMEOUT)` returns, `Sn=w` the socket callback for the
n-th distinct socket with `what` (1 IN, 2 OUT, 4 REMOVE), and in `C_21` `(e<handle>,p<socketp>)`, where the loop
calls `curl_multi_assign(s, 100 + handle)` on the first callback. `wire:` lines are the requests the server received.

## Results (2026-10-29, Windows, after BL-4075 and BL-4086)

| Case | REF | PORT | Same |
|------|-----|------|------|
| `C_20` one transfer | `T0 add S1=2 T-1 kick S1=1 S1=4`, 0, `hello`, `S1:real`, no poll errors, socket events | the same | yes |
| `C_21` three, `curl_multi_assign` | `T0 T0 T0 add S1=2(e0,p0) S2=2(e1,p0) S3=2(e2,p0) T-1 kick S1=1(e0,p100) S2=1(e1,p101) S3=1(e2,p102)` then the three removals with their `p`, 0,0,0, `hello,other,hello`, all real | the same in 4 of 4 runs | yes |
| `C_65` refused | `T0 add S1=2 T-1 kick S1=4`, 7, `S1:real` | the same in 4 of 4 runs | yes |
| `C_74` timer only, perform | `T0 add T-1`, 0, `hello`, no socket | the same | yes |
| `C_75` `TIMEOUT_MS` 600, silent | `T0 add S1=2 T400+ kick S1=1 S1=4 T-1`, 28, `S1:real` | the same in 4 of 4 runs | yes |

The removal order of `C_21`'s three sockets follows completion order and varies in REF as well (`S2 S1 S3` in
this run, `S1 S3 S2` and `S1 S2 S3` in earlier ones; PORT gave `S1 S2 S3` in all four), so it is not a difference.
Every request on the wire is the same on both sides:
`GET <path> HTTP/1.1\r\nHost: 127.0.0.1:<port>\r\nAccept: */*\r\n\r\n`.

History: the first run differed in `C_21` (intermittently) and `C_75`, because PORT's kick could return before
every transfer reached the wait on its real socket; BL-4075 fixed that. The next run differed in `C_65`, because
PORT closed a refused connect's socket before its REMOVE callback (`S1:NOT-A-SOCKET`, poll errors); BL-4086 fixed
that. All five cases now match.

## Probe source

```csharp
#:project ../Libcurl/Libcurl.csproj
#:property PublishAot=false
#:property TreatWarningsAsErrors=false
#:property Nullable=enable
#:property AllowUnsafeBlocks=true

// CGR-034 probe: drives REF (a real libcurl-4.dll through P/Invoke) and PORT (Curl's managed Libcurl) through one
// application event loop - WSAPoll on the sockets the socket callback names, curl_multi_socket_action on each event and
// on the timer - against a recording server: 21700 answers "200 hello", 21701 "200 other", 21702 accepts and stays
// silent, 21799 is closed. Usage: dotnet run probe.cs -- <REF|PORT> <libcurl-4.dll path> <case,...>
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

var side = args[0];
var cases = args[2].Split(',');
var server = new RecordingServer();
server.Start();
IProbeLibrary library = side == "REF" ? new NativeLibcurl(args[1]) : new ManagedLibcurl();
foreach (var name in cases)
{
    server.Requests.Clear();
    var log = Probe.Run(library, name);
    Thread.Sleep(100);
    Console.WriteLine($"{side} {name}: {log}");
    foreach (var request in server.Requests)
    {
        Console.WriteLine($"{side} {name} wire: {request}");
    }
}

return 0;

internal interface IProbeLibrary
{
    void NewMulti(Func<nint, int, int, nint, int> socketCallback, Func<long, int> timerCallback);
    int AddEasy(string url, long timeoutMs);
    int SocketAction(nint socket, int events);
    int Perform();
    List<(int Index, int Result)> ReadDone();
    int Assign(nint socket, nint value);
    string Body(int index);
    void Cleanup();
}

internal static class Probe
{
    private static readonly nint Timeout = -1;

    public static string Run(IProbeLibrary lib, string name)
    {
        var log = new List<string>();
        var sockets = new Dictionary<nint, int>();
        var socketNames = new Dictionary<nint, string>();
        var real = new Dictionary<string, string>();
        long timer = -1;
        long timerSetAt = Environment.TickCount64;
        var assign = name.StartsWith("C_21", StringComparison.Ordinal);
        int pollErrors = 0, socketEvents = 0;
        var socketActions = !name.StartsWith("C_74", StringComparison.Ordinal);
        string SocketName(nint s)
        {
            if (!socketNames.TryGetValue(s, out var n))
            {
                n = "S" + (socketNames.Count + 1);
                socketNames[s] = n;
                real[n] = Native.IsRealSocket(s) ? "real" : "NOT-A-SOCKET";
            }

            return n;
        }

        lib.NewMulti(
            socketActions
                ? (s, what, easy, socketp) =>
                {
                    lock (log)
                    {
                        log.Add($"{SocketName(s)}={what}" + (assign ? $"(e{easy},p{socketp})" : ""));
                        if (what == 4) { sockets.Remove(s); } else { sockets[s] = what; }
                    }

                    if (assign && socketp == 0 && what != 4) { lib.Assign(s, 100 + easy); }
                    return 0;
                }
        : null!,
            ms =>
            {
                lock (log) { log.Add(ms < 0 ? "T-1" : ms == 0 ? "T0" : ms >= 400 ? "T400+" : $"T{ms}"); timer = ms; timerSetAt = Environment.TickCount64; }
                return 0;
            });
        var urls = name switch
        {
            "C_20" or "C_74" => new[] { ("http://127.0.0.1:21700/one", 0L) },
            "C_21" => new[] { ("http://127.0.0.1:21700/a", 0L), ("http://127.0.0.1:21701/b", 0L), ("http://127.0.0.1:21700/c", 0L) },
            "C_65" => new[] { ("http://127.0.0.1:21799/refused", 0L) },
            "C_75" => new[] { ("http://127.0.0.1:21702/silent", 600L) },
            _ => throw new ArgumentException(name),
        };
        foreach (var (url, timeoutMs) in urls) { lib.AddEasy(url, timeoutMs); }
        lock (log) { log.Add("add"); }
        var results = new SortedDictionary<int, int>();
        int running;
        if (socketActions)
        {
            running = lib.SocketAction(Timeout, 0);
            lock (log) { log.Add("kick"); }
        }
        else
        {
            running = lib.Perform();
        }

        var giveUp = Environment.TickCount64 + 15000;
        while ((running > 0 || results.Count < urls.Length) && Environment.TickCount64 < giveUp)
        {
            foreach (var (i, r) in lib.ReadDone()) { results[i] = r; }
            if (running == 0 && results.Count == urls.Length) { break; }
            if (!socketActions)
            {
                Thread.Sleep(5);
                running = lib.Perform();
                continue;
            }

            Native.PollFd[] fds;
            long wait;
            lock (log)
            {
                fds = sockets.Select(p => new Native.PollFd { Fd = p.Key, Events = (short)(((p.Value & 1) != 0 ? 0x100 : 0) | ((p.Value & 2) != 0 ? 0x10 : 0)) }).ToArray();
                wait = timer < 0 ? 1000 : Math.Max(0, timer - (Environment.TickCount64 - timerSetAt));
            }

            var n = 0;
            if (fds.Length == 0) { Thread.Sleep((int)Math.Min(wait, 1000)); }
            else
            {
                n = Native.WSAPoll(fds, (uint)fds.Length, (int)Math.Min(wait, 1000));
                if (n < 0) { pollErrors++; Thread.Sleep((int)Math.Min(wait, 10)); }
            }

            if (n > 0)
            {
                foreach (var fd in fds.Where(f => f.REvents != 0))
                {
                    socketEvents++;
                    var ev = ((fd.REvents & 0x100) != 0 || (fd.REvents & 2) != 0 ? 1 : 0) | ((fd.REvents & 0x10) != 0 ? 2 : 0) | ((fd.REvents & 1) != 0 ? 4 : 0);
                    running = lib.SocketAction(fd.Fd, ev);
                }
            }
            else
            {
                bool due;
                lock (log) { due = timer >= 0 && Environment.TickCount64 - timerSetAt >= timer; if (due) { timer = -1; } }
                if (due) { running = lib.SocketAction(Timeout, 0); }
            }
        }

        foreach (var (i, r) in lib.ReadDone()) { results[i] = r; }
        var bodies = string.Join(",", urls.Select((_, i) => lib.Body(i)));
        lib.Cleanup();
        lock (log)
        {
            return $"{string.Join(' ', log)} | results {string.Join(",", results.Values)} | bodies [{bodies}] | real: {string.Join(" ", real.Select(p => $"{p.Key}:{p.Value}"))} | pollErrors>0={pollErrors > 0} socketEvents>0={socketEvents > 0}";
        }
    }
}

internal static partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PollFd
    {
        public nint Fd;
        public short Events;
        public short REvents;
    }

    [DllImport("ws2_32.dll")]
    public static extern int WSAPoll([In, Out] PollFd[] fds, uint count, int timeout);

    [DllImport("ws2_32.dll")]
    private static extern int getsockopt(nint s, int level, int name, out int value, ref int length);

    public static bool IsRealSocket(nint s)
    {
        var length = 4;
        return getsockopt(s, 0xffff, 0x1008, out _, ref length) == 0;
    }
}

internal sealed class NativeLibcurl : IProbeLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint InitFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GlobalInitFn(long flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetPtrFn(nint h, int option, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetLongFn(nint h, int option, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int HandleFn(nint m, nint e);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SocketActionFn(nint m, nint s, int ev, out int running);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PerformFn(nint m, out int running);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint InfoReadFn(nint m, out int left);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int AssignFn(nint m, nint s, nint p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CleanupFn(nint m);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SocketCb(nint easy, nint s, int what, nint userp, nint socketp);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TimerCb(nint multi, int ms, nint userp);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint WriteCb(nint data, nuint size, nuint count, nint userp);

    private readonly nint lib;
    private nint multi;
    private readonly List<nint> easies = [];
    private readonly List<StringBuilder> bodies = [];
    private readonly List<object> keepAlive = [];

    public NativeLibcurl(string path)
    {
        Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(path) + ";" + Environment.GetEnvironmentVariable("PATH"));
        lib = NativeLibrary.Load(path);
        F<GlobalInitFn>("curl_global_init")(3);
    }

    private T F<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, name));

    public void NewMulti(Func<nint, int, int, nint, int> socketCallback, Func<long, int> timerCallback)
    {
        easies.Clear();
        bodies.Clear();
        multi = F<InitFn>("curl_multi_init")();
        if (socketCallback is not null)
        {
            SocketCb s = (easy, sock, what, _, socketp) => socketCallback(sock, what, easies.IndexOf(easy), socketp);
            keepAlive.Add(s);
            F<SetPtrFn>("curl_multi_setopt")(multi, 20001, Marshal.GetFunctionPointerForDelegate(s));
        }

        TimerCb t = (_, ms, _) => timerCallback(ms);
        keepAlive.Add(t);
        F<SetPtrFn>("curl_multi_setopt")(multi, 20004, Marshal.GetFunctionPointerForDelegate(t));
    }

    public int AddEasy(string url, long timeoutMs)
    {
        var easy = F<InitFn>("curl_easy_init")();
        var index = easies.Count;
        easies.Add(easy);
        var body = new StringBuilder();
        bodies.Add(body);
        var urlPtr = Marshal.StringToHGlobalAnsi(url);
        F<SetPtrFn>("curl_easy_setopt")(easy, 10002, urlPtr);
        WriteCb w = (data, size, count, _) =>
        {
            var bytes = new byte[(int)(size * count)];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            body.Append(Encoding.ASCII.GetString(bytes));
            return size * count;
        };
        keepAlive.Add(w);
        F<SetPtrFn>("curl_easy_setopt")(easy, 20011, Marshal.GetFunctionPointerForDelegate(w));
        if (timeoutMs > 0) { F<SetLongFn>("curl_easy_setopt")(easy, 155, (int)timeoutMs); }
        F<HandleFn>("curl_multi_add_handle")(multi, easy);
        return index;
    }

    public int SocketAction(nint socket, int events)
    {
        F<SocketActionFn>("curl_multi_socket_action")(multi, socket, events, out var running);
        return running;
    }

    public int Perform()
    {
        F<PerformFn>("curl_multi_perform")(multi, out var running);
        return running;
    }

    public List<(int Index, int Result)> ReadDone()
    {
        var done = new List<(int, int)>();
        nint message;
        while ((message = F<InfoReadFn>("curl_multi_info_read")(multi, out _)) != 0)
        {
            if (Marshal.ReadInt32(message) == 1) { done.Add((easies.IndexOf(Marshal.ReadIntPtr(message, 8)), Marshal.ReadInt32(message, 16))); }
        }

        return done;
    }

    public int Assign(nint socket, nint value) => F<AssignFn>("curl_multi_assign")(multi, socket, value);

    public string Body(int index) => bodies[index].ToString();

    public void Cleanup()
    {
        foreach (var easy in easies) { F<HandleFn>("curl_multi_remove_handle")(multi, easy); }
        F<CleanupFn>("curl_multi_cleanup")(multi);
    }
}

internal sealed class ManagedLibcurl : IProbeLibrary
{
    private readonly CurlLibrary library = CurlGlobal.Init();
    private CurlMulti multi = null!;
    private readonly List<Libcurl.Easy.CurlEasy> easies = [];
    private readonly List<StringBuilder> bodies = [];

    public void NewMulti(Func<nint, int, int, nint, int> socketCallback, Func<long, int> timerCallback)
    {
        easies.Clear();
        bodies.Clear();
        multi = library.MultiInit();
        if (socketCallback is not null)
        {
            CurlMultiSocketCallback s = (easy, sock, what, state) => socketCallback(sock.Handle, (int)what, easies.IndexOf(easy), state is int p ? p : 0);
            multi.SetOption(CurlMultiOption.SocketFunction, s);
        }

        CurlMultiTimerCallback t = (_, ms) => timerCallback(ms);
        multi.SetOption(CurlMultiOption.TimerFunction, t);
    }

    public int AddEasy(string url, long timeoutMs)
    {
        var easy = library.EasyInit();
        var index = easies.Count;
        easies.Add(easy);
        var body = new StringBuilder();
        bodies.Add(body);
        easy.SetOption(CurlOption.Url, url);
        WriteCallback w = data => { body.Append(Encoding.ASCII.GetString(data)); return data.Length; };
        easy.SetOption(CurlOption.Writefunction, w);
        if (timeoutMs > 0) { easy.SetOption(CurlOption.TimeoutMs, timeoutMs); }
        multi.AddHandle(easy);
        return index;
    }

    public int SocketAction(nint socket, int events) =>
        multi.SocketActionAsync(new CurlSocket(socket), (CurlSocketSelect)events).AsTask().GetAwaiter().GetResult().RunningHandles;

    public int Perform() => multi.PerformAsync().AsTask().GetAwaiter().GetResult().RunningHandles;

    public List<(int Index, int Result)> ReadDone()
    {
        var done = new List<(int, int)>();
        while (multi.InfoRead(out _) is { } message)
        {
            done.Add((easies.IndexOf(message.Easy), (int)message.Result));
        }

        return done;
    }

    public int Assign(nint socket, nint value) => (int)multi.Assign(new CurlSocket(socket), (int)value);

    public string Body(int index) => bodies[index].ToString();

    public void Cleanup()
    {
        foreach (var easy in easies) { multi.RemoveHandle(easy); }
        multi.CleanupAsync().AsTask().GetAwaiter().GetResult();
    }
}

internal sealed class RecordingServer
{
    public List<string> Requests { get; } = [];

    public void Start()
    {
        Listen(21700, "hello");
        Listen(21701, "other");
        Listen(21702, null);
    }

    private void Listen(int port, string? body)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    var stream = client.GetStream();
                    var buffer = new byte[4096];
                    var request = new StringBuilder();
                    while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var read = await stream.ReadAsync(buffer);
                        if (read == 0) { break; }
                        request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }

                    lock (Requests) { Requests.Add($"{port} " + request.ToString().Replace("\r\n", "\\r\\n")); }
                    if (body is null) { await Task.Delay(5000); client.Dispose(); return; }
                    var response = $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
                    await Task.Delay(2000);
                    client.Dispose();
                });
            }
        });
    }
}
```
