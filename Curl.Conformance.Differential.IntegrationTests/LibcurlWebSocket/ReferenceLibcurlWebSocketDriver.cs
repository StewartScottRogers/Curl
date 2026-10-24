using System.Runtime.InteropServices;
using Curl.Conformance.Differential.Libcurl.WebSocket;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlWebSocket;

/// <summary>
/// Runs a WebSocket case through the reference libcurl 8.22.0 shared library's <c>curl_ws_*</c>
/// functions, loaded with <see cref="NativeLibrary"/> and called through unmanaged function
/// pointers as <c>ReferenceLibcurlEasyDriver</c> does (ADR-0792).
/// </summary>
internal sealed class ReferenceLibcurlWebSocketDriver : IDisposable
{
    private const int UrlOption = 10002;
    private const int ConnectOnlyOption = 141;
    private const int WriteDataOption = 10001;
    private const int WriteFunctionOption = 20011;
    private const int Again = 81;

    private readonly nint _library;
    private readonly unsafe delegate* unmanaged<nint> _easyInit;
    private readonly unsafe delegate* unmanaged<nint, int, nint, int> _setOption;
    private readonly unsafe delegate* unmanaged<nint, int> _perform;
    private readonly unsafe delegate* unmanaged<nint, void> _cleanup;
    private readonly unsafe delegate* unmanaged<nint, byte*, nuint, nuint*, long, uint, int> _send;
    private readonly unsafe delegate* unmanaged<nint, byte*, nuint, nuint*, nint*, int> _recv;
    private readonly unsafe delegate* unmanaged<nint, nint> _meta;

    public unsafe ReferenceLibcurlWebSocketDriver(string libraryPath)
    {
        _library = NativeLibrary.Load(libraryPath);
        _easyInit = (delegate* unmanaged<nint>)Export("curl_easy_init");
        _setOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_setopt");
        _perform = (delegate* unmanaged<nint, int>)Export("curl_easy_perform");
        _cleanup = (delegate* unmanaged<nint, void>)Export("curl_easy_cleanup");
        _send = (delegate* unmanaged<nint, byte*, nuint, nuint*, long, uint, int>)Export("curl_ws_send");
        _recv = (delegate* unmanaged<nint, byte*, nuint, nuint*, nint*, int>)Export("curl_ws_recv");
        _meta = (delegate* unmanaged<nint, nint>)Export("curl_ws_meta");
    }

    public void Dispose() => NativeLibrary.Free(_library);

    /// <summary>Runs <paramref name="webSocketCase"/> against <paramref name="url"/>.</summary>
    public async Task<IReadOnlyList<string>> RunAsync(LibcurlWebSocketCase webSocketCase, string url, CancellationToken cancellationToken)
    {
        var easy = EasyInit();
        var text = Marshal.StringToCoTaskMemUTF8(url);
        var sink = new WriteSink(this, easy);
        var handle = GCHandle.Alloc(sink);
        try
        {
            sink.Lines.Add(LibcurlWebSocketLineWriter.Perform(Perform(easy, text, webSocketCase.ConnectOnly, GCHandle.ToIntPtr(handle))));
            foreach (var step in webSocketCase.Steps)
            {
                sink.Lines.Add(await StepAsync(easy, step, cancellationToken));
            }

            return sink.Lines;
        }
        finally
        {
            Cleanup(easy);
            handle.Free();
            Marshal.FreeCoTaskMem(text);
        }
    }

    private unsafe nint EasyInit() => _easyInit();

    private unsafe void Cleanup(nint easy) => _cleanup(easy);

    private unsafe int Perform(nint easy, nint url, bool connectOnly, nint sink)
    {
        _setOption(easy, UrlOption, url);
        if (connectOnly)
        {
            _setOption(easy, ConnectOnlyOption, 2);
        }
        else
        {
            _setOption(easy, WriteFunctionOption, (nint)(delegate* unmanaged<byte*, nuint, nuint, nint, nuint>)&Write);
            _setOption(easy, WriteDataOption, sink);
        }

        return _perform(easy);
    }

    private async Task<string> StepAsync(nint easy, LibcurlWebSocketStep step, CancellationToken cancellationToken) => step.Kind switch
    {
        LibcurlWebSocketStepKind.Send => Send(easy, step),
        LibcurlWebSocketStepKind.Receive => await ReceiveAsync(easy, step, cancellationToken),
        _ => LibcurlWebSocketLineWriter.Meta(Meta(easy)),
    };

    private unsafe LibcurlWebSocketFrameFields? Meta(nint easy) => Fields(_meta(easy));

    private unsafe string Send(nint easy, LibcurlWebSocketStep step)
    {
        nuint sent = 0;
        int code;
        fixed (byte* payload = step.Payload)
        {
            code = _send(easy, payload, (nuint)step.Payload.Length, &sent, step.FragmentSize, (uint)step.Flags);
        }

        return LibcurlWebSocketLineWriter.Send(step, code, (long)sent);
    }

    private async Task<string> ReceiveAsync(nint easy, LibcurlWebSocketStep step, CancellationToken cancellationToken)
    {
        var buffer = new byte[step.BufferSize];
        for (var attempt = 0; ; attempt++)
        {
            var (code, received, frame) = Receive(easy, buffer);
            if (code != Again || attempt >= LibcurlWebSocketDifferentialTests.ReceiveAttempts)
            {
                return LibcurlWebSocketLineWriter.Receive(step, code, buffer.AsSpan(0, received), frame);
            }

            await Task.Delay(LibcurlWebSocketDifferentialTests.ReceivePause, cancellationToken);
        }
    }

    private unsafe (int Code, int Received, LibcurlWebSocketFrameFields? Frame) Receive(nint easy, byte[] buffer)
    {
        nuint received = 0;
        nint meta = 0;
        int code;
        fixed (byte* data = buffer)
        {
            code = _recv(easy, data, (nuint)buffer.Length, &received, &meta);
        }

        return (code, (int)received, Fields(meta));
    }

    private static unsafe LibcurlWebSocketFrameFields? Fields(nint frame)
    {
        if (frame == 0)
        {
            return null;
        }

        // struct curl_ws_frame { int age; int flags; curl_off_t offset; curl_off_t bytesleft; size_t len; }
        var fields = (byte*)frame;
        return new LibcurlWebSocketFrameFields(*(int*)fields, *(int*)(fields + 4), *(long*)(fields + 8), *(long*)(fields + 16), (long)*(nuint*)(fields + 24));
    }

    [UnmanagedCallersOnly]
    private static unsafe nuint Write(byte* data, nuint size, nuint count, nint sinkHandle)
    {
        var length = size * count;
        var sink = (WriteSink)GCHandle.FromIntPtr(sinkHandle).Target!;
        sink.Lines.Add(LibcurlWebSocketLineWriter.Write(new ReadOnlySpan<byte>(data, (int)length), sink.Driver.Meta(sink.Easy)));
        return length;
    }

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);

    private sealed record WriteSink(ReferenceLibcurlWebSocketDriver Driver, nint Easy)
    {
        public List<string> Lines { get; } = [];
    }
}
