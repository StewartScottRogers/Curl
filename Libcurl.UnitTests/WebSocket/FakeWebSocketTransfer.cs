using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.UnitTests.WebSocket;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose <see cref="WebSocket"/> is a <see cref="FakeEasyWebSocket"/>. Its other
/// members are not called by these tests, except <see cref="Options"/> and <see cref="DisposeAsync"/>.
/// </summary>
internal sealed class FakeWebSocketTransfer : IEasyTransfer
{
    public FakeEasyWebSocket FakeWebSocket { get; } = new();

    public IEasyWebSocket WebSocket => FakeWebSocket;

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// An <see cref="IEasyWebSocket"/> that answers the results it is given and records each call's arguments.
/// <see cref="ReceiveAsync"/> also writes <see cref="ReceivePayload"/> into the buffer.
/// </summary>
internal sealed class FakeEasyWebSocket : IEasyWebSocket
{
    public CurlWebSocketFrame? CurrentFrame { get; set; }

    public CurlWebSocketReceiveResult ReceiveResult { get; set; }

    public byte[] ReceivePayload { get; set; } = [];

    public CurlWebSocketSendResult SendResult { get; set; }

    public CurlCode StartFrameResult { get; set; }

    public int CallCount { get; private set; }

    public CancellationToken LastToken { get; private set; }

    public int ReceiveBufferLength { get; private set; }

    public byte[]? Sent { get; private set; }

    public long SentFragmentSize { get; private set; }

    public CurlWebSocketFlags LastFlags { get; private set; }

    public long StartedFrameLength { get; private set; }

    public ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        CallCount++;
        LastToken = cancellationToken;
        ReceiveBufferLength = buffer.Length;
        ReceivePayload.CopyTo(buffer);
        return ValueTask.FromResult(ReceiveResult);
    }

    public ValueTask<CurlWebSocketSendResult> SendAsync(ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        CallCount++;
        LastToken = cancellationToken;
        Sent = buffer.ToArray();
        SentFragmentSize = fragmentSize;
        LastFlags = flags;
        return ValueTask.FromResult(SendResult);
    }

    public ValueTask<CurlCode> StartFrameAsync(CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken)
    {
        CallCount++;
        LastToken = cancellationToken;
        LastFlags = flags;
        StartedFrameLength = frameLength;
        return ValueTask.FromResult(StartFrameResult);
    }
}
