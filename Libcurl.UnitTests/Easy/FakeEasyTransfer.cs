using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform answers <see cref="Result"/> and records the options and
/// token it ran with, and which counts its disposals. Its other members are not called by these tests.
/// </summary>
internal sealed class FakeEasyTransfer : IEasyTransfer
{
    public CurlCode Result { get; init; } = CurlCode.Ok;

    public EasyOptions? PerformedWith { get; private set; }

    public CancellationToken PerformedToken { get; private set; }

    public int DisposeCount { get; private set; }

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info { get; init; } = new();

    public string ErrorText => throw new NotSupportedException();

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedWith = Options;
        PerformedToken = cancellationToken;
        return ValueTask.FromResult(Result);
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
