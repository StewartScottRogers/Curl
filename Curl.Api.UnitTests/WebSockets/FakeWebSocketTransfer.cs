using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.WebSockets;

/// <summary>An <see cref="IEasyTransfer"/> whose perform answers <see cref="PerformResult"/> and whose WebSocket is a <see cref="FakeEasyWebSocket"/>.</summary>
internal sealed class FakeWebSocketTransfer : IEasyTransfer
{
    public EasyOptions Options { get; set; } = new();

    public CurlCode PerformResult { get; set; } = CurlCode.Ok;

    public EasyOptions? PerformedOptions { get; private set; }

    public int DisposeCount { get; private set; }

    public FakeEasyWebSocket FakeWebSocket { get; } = new();

    public TransferInfo Info => throw new NotSupportedException();

    public string ErrorText { get; set; } = string.Empty;

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => FakeWebSocket;

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    /// <summary>When set, the perform throws it in place of answering <see cref="PerformResult"/>.</summary>
    public Exception? PerformException { get; set; }

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedOptions = Options;
        return PerformException is null ? ValueTask.FromResult(PerformResult) : ValueTask.FromException<CurlCode>(PerformException);
    }

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
