using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>
/// An <see cref="IEasyWebSocket"/> answering each send from <see cref="SendResults"/> (everything sent once it is empty)
/// and each receive from <see cref="Receives"/>, the text copied into the buffer when the code is
/// <see cref="CurlCode.Ok"/> (<see cref="CurlCode.Again"/> once it is empty); <see cref="CurrentFrame"/> is settable.
/// </summary>
internal sealed class FakeEasyWebSocket : IEasyWebSocket
{
    public CurlWebSocketFrame? CurrentFrame { get; set; }

    public Queue<CurlCode> SendResults { get; } = new();

    public Queue<(CurlCode Code, string Text, CurlWebSocketFlags Flags)> Receives { get; } = new();

    /// <summary>Every frame sent: its flags and its payload as ASCII.</summary>
    public List<(CurlWebSocketFlags Flags, string Payload)> Sent { get; } = [];

    public ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var (code, text, flags) = Receives.Count > 0 ? Receives.Dequeue() : (CurlCode.Again, string.Empty, CurlWebSocketFlags.None);
        int count = code == CurlCode.Ok ? Encoding.ASCII.GetBytes(text, buffer.Span) : 0;
        return ValueTask.FromResult(new CurlWebSocketReceiveResult(code, count, new CurlWebSocketFrame(0, flags, 0, 0, count)));
    }

    public ValueTask<CurlWebSocketSendResult> SendAsync(ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        CurlCode code = SendResults.Count > 0 ? SendResults.Dequeue() : CurlCode.Ok;
        Sent.Add((flags, Encoding.ASCII.GetString(buffer.Span)));
        return ValueTask.FromResult(new CurlWebSocketSendResult(code, code == CurlCode.Ok ? buffer.Length : 0));
    }

    public ValueTask<CurlCode> StartFrameAsync(CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken) => throw new NotSupportedException();
}
