using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="ICurlEngine"/> whose easy transfers perform with <see cref="PerformResult"/> and report <see cref="Info"/>; it makes no other handle.</summary>
internal sealed class FakeCurlEngine : ICurlEngine
{
    public CurlCode PerformResult { get; init; } = CurlCode.Ok;

    public TransferInfo Info { get; init; } = new();

    public CurlVersionInfo VersionInfo { get; } = new();

    public IEasyTransfer CreateEasy() => new FakeEasyTransfer(PerformResult, Info);

    public IMultiTransfer CreateMulti() => throw new NotSupportedException();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class FakeEasyTransfer(CurlCode performResult, TransferInfo info) : IEasyTransfer
    {
        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => info;

        public string ErrorText => string.Empty;

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) => ValueTask.FromResult(performResult);

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
