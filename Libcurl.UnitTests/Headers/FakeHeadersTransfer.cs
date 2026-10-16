using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.UnitTests.Headers;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose <see cref="Headers"/> is a <see cref="FakeTransferHeaders"/>. Its other
/// members are not called by these tests, except <see cref="Options"/> and <see cref="DisposeAsync"/>.
/// </summary>
internal sealed class FakeHeadersTransfer : IEasyTransfer
{
    public FakeTransferHeaders FakeHeaders { get; } = new();

    public ITransferHeaders Headers => FakeHeaders;

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>An <see cref="ITransferHeaders"/> that answers what it is given and records each call's arguments.</summary>
internal sealed class FakeTransferHeaders : ITransferHeaders
{
    public CurlHeaderCode TryGetResult { get; set; }

    public CurlHeader? Header { get; set; }

    public int CallCount { get; private set; }

    public (string Name, int Index, CurlHeaderOrigin Origin, int Request)? LastTryGet { get; private set; }

    public (CurlHeaderOrigin Origin, int Request, CurlHeader? Previous)? LastNext { get; private set; }

    public CurlHeaderCode TryGet(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header)
    {
        CallCount++;
        LastTryGet = (name, index, origin, request);
        header = Header;
        return TryGetResult;
    }

    public CurlHeader? Next(CurlHeaderOrigin origin, int request, CurlHeader? previous)
    {
        CallCount++;
        LastNext = (origin, request, previous);
        return Header;
    }
}
