using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Api.WebSockets;

/// <summary>
/// An <see cref="IEasyWebSocket"/> that hands over queued receive results, answering
/// <see cref="CurlCode.Again"/> once the queue is empty, and records every send.
/// </summary>
internal sealed class FakeEasyWebSocket : IEasyWebSocket
{
    private readonly Queue<(CurlCode Result, byte[] Bytes, CurlWebSocketFrame Frame)> _receives = new();

    public CurlWebSocketFrame? CurrentFrame { get; set; }

    /// <summary>The results the next sends answer, oldest first; once empty every send sends all its bytes.</summary>
    public Queue<CurlWebSocketSendResult> SendResults { get; } = new();

    public List<(byte[] Data, long FragmentSize, CurlWebSocketFlags Flags)> Sends { get; } = [];

    public int ReceiveCount { get; private set; }

    /// <summary>Queues a piece of a frame with <paramref name="bytesLeft"/> bytes of it still to come.</summary>
    public FakeEasyWebSocket Queue(byte[] bytes, CurlWebSocketFlags flags, long bytesLeft = 0)
    {
        _receives.Enqueue((CurlCode.Ok, bytes, new CurlWebSocketFrame(0, flags, 0, bytesLeft, bytes.Length)));
        return this;
    }

    public FakeEasyWebSocket QueueResult(CurlCode result)
    {
        _receives.Enqueue((result, [], default));
        return this;
    }

    public ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ReceiveCount++;
        if (!_receives.TryDequeue(out (CurlCode Result, byte[] Bytes, CurlWebSocketFrame Frame) next))
        {
            return ValueTask.FromResult(new CurlWebSocketReceiveResult(CurlCode.Again, 0, default));
        }

        next.Bytes.CopyTo(buffer);
        return ValueTask.FromResult(new CurlWebSocketReceiveResult(next.Result, next.Bytes.Length, next.Frame));
    }

    public ValueTask<CurlWebSocketSendResult> SendAsync(ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        Sends.Add((buffer.ToArray(), fragmentSize, flags));
        return ValueTask.FromResult(SendResults.TryDequeue(out CurlWebSocketSendResult result) ? result : new CurlWebSocketSendResult(CurlCode.Ok, buffer.Length));
    }

    public ValueTask<CurlCode> StartFrameAsync(CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
