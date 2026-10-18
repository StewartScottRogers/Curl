using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.Client;

/// <summary>An <see cref="IEasyTransfer"/> that only stores its options; every transfer member is unsupported.</summary>
internal sealed class FakeEasyTransfer : IEasyTransfer
{
    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
