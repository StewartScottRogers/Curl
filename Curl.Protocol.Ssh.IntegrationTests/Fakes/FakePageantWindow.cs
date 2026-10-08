using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// A top-level window of class <c>Pageant</c> titled <c>Pageant</c>, on its own thread,
/// that answers <c>WM_COPYDATA</c> as PuTTY's Pageant does for libssh2 1.11.1: the data is
/// the name of a file mapping holding one length-prefixed agent request, the answer is
/// written over it, and the message returns nonzero. Windows only: the Integration tests
/// of <see cref="Authentication.WindowsPageantWindow" /> and the BL-1035 measurement.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class FakePageantWindow : IDisposable
{
    private const string Name = "Pageant";

    private const uint WmDestroy = 0x0002;

    private const uint WmClose = 0x0010;

    private const uint WmCopyData = 0x004A;

    private const nuint CopyDataId = 0x804e50ba;

    private readonly Func<byte[], byte[]?> answer;

    private readonly WindowProcedure procedure;

    private readonly ConcurrentQueue<string> mapNames = new();

    private readonly ConcurrentQueue<byte[]> requests = new();

    private readonly Thread thread;

    private readonly TaskCompletionSource<nint> created = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private nint window;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakePageantWindow" /> class and shows
    /// the window to <c>FindWindow</c> before returning.
    /// </summary>
    /// <param name="answer">Answers one request body with one answer body, or <see langword="null" /> to return zero from the message.</param>
    internal FakePageantWindow(Func<byte[], byte[]?> answer)
    {
        this.answer = answer;
        procedure = Procedure;
        thread = new Thread(Run) { IsBackground = true };
        thread.Start();
        window = created.Task.GetAwaiter().GetResult();
    }

    private delegate nint WindowProcedure(nint window, uint message, nint wParam, nint lParam);

    /// <summary>Gets the name of every file mapping a request arrived in, in order.</summary>
    internal IReadOnlyList<string> MapNames => [.. mapNames];

    /// <summary>Gets every request body, in order.</summary>
    internal IReadOnlyList<byte[]> Requests => [.. requests];

    /// <inheritdoc />
    public void Dispose()
    {
        if (window != 0)
        {
            PostMessageW(window, WmClose, 0, 0);
            thread.Join();
            window = 0;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern ushort RegisterClassW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool UnregisterClassW(string className, nint instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetMessageW(out Message message, nint window, uint first, uint last);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint DispatchMessageW(ref Message message);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint GetModuleHandleW(string? moduleName);

    private void Run()
    {
        nint instance = GetModuleHandleW(null);
        nint className = Marshal.StringToHGlobalUni(Name);
        try
        {
            WindowClass windowClass = new() { Procedure = Marshal.GetFunctionPointerForDelegate(procedure), Instance = instance, ClassName = className };
            RegisterClassW(ref windowClass);
            nint handle = CreateWindowExW(0, Name, Name, 0, 0, 0, 0, 0, 0, 0, instance, 0);
            created.SetResult(handle);
            while (handle != 0 && GetMessageW(out Message message, 0, 0, 0) > 0)
            {
                DispatchMessageW(ref message);
            }
        }
        finally
        {
            UnregisterClassW(Name, instance);
            Marshal.FreeHGlobal(className);
        }
    }

    private nint Procedure(nint handle, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmCopyData:
                return CopyData(Marshal.PtrToStructure<CopyDataStruct>(lParam));
            case WmDestroy:
                PostQuitMessage(0);
                return 0;
            default:
                return DefWindowProcW(handle, message, wParam, lParam);
        }
    }

    private nint CopyData(CopyDataStruct data)
    {
        if (data.Id != CopyDataId)
        {
            return 0;
        }

        string mapName = Marshal.PtrToStringAnsi(data.Data)!;
        mapNames.Enqueue(mapName);
        using MemoryMappedFile map = MemoryMappedFile.OpenExisting(mapName);
        using MemoryMappedViewStream view = map.CreateViewStream();
        byte[] length = new byte[4];
        view.ReadExactly(length);
        byte[] request = new byte[BinaryPrimitives.ReadUInt32BigEndian(length)];
        view.ReadExactly(request);
        requests.Enqueue(request);
        if (answer(request) is not { } reply)
        {
            return 0;
        }

        view.Position = 0;
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)reply.Length);
        view.Write(length);
        view.Write(reply);
        return 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Number;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyDataStruct
    {
        public nuint Id;
        public uint Size;
        public nint Data;
    }
}
