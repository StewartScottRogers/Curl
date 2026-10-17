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

/// <summary>An <see cref="ICurlEngine"/> whose easy transfers perform with <see cref="PerformResult"/> and report <see cref="Info"/>, whose multi handle is <see cref="Multi"/>, and whose share and URL handles are fakes; it makes no MIME builder, and a form builder and connect-only connection only when given one.</summary>
internal sealed class FakeCurlEngine : ICurlEngine
{
    public CurlCode PerformResult { get; init; } = CurlCode.Ok;

    public TransferInfo Info { get; init; } = new();

    /// <summary>When set, gives each read of an easy transfer's info in place of <see cref="Info"/>.</summary>
    public Func<TransferInfo>? Informing { get; init; }

    public CurlVersionInfo VersionInfo { get; } = new();

    /// <summary>Runs as each easy transfer is created, before it is returned.</summary>
    public Action? CreatingEasy { get; init; }

    /// <summary>Runs as the multi handle is handed out.</summary>
    public Action? CreatingMulti { get; init; }

    /// <summary>Runs as each share handle is created.</summary>
    public Action? CreatingShare { get; init; }

    /// <summary>Runs in each perform with the transfer's options, in place of a transfer; answers the perform's code.</summary>
    public Func<EasyOptions, CurlCode>? Performing { get; init; }

    /// <summary>The share handle created last.</summary>
    public FakeShareHandle? LastShare { get; private set; }

    /// <summary>What a share handle's lock-callback setter answers.</summary>
    public CurlShareCode ShareLockResult { get; init; } = CurlShareCode.Ok;

    /// <summary>What a share handle's cleanup answers.</summary>
    public CurlShareCode ShareCleanupResult { get; init; } = CurlShareCode.Ok;

    public IEasyTransfer CreateEasy()
    {
        CreatingEasy?.Invoke();
        return new FakeEasyTransfer(PerformResult, Info, Performing) { Informing = Informing, ConnectOnly = ConnectOnly, Pauses = Pauses };
    }

    public FakeMultiTransfer Multi { get; } = new();

    public IMultiTransfer CreateMulti()
    {
        CreatingMulti?.Invoke();
        return Multi;
    }

    public IShareHandle CreateShare()
    {
        CreatingShare?.Invoke();
        LastShare = new FakeShareHandle { LockResult = ShareLockResult, CleanupResult = ShareCleanupResult };
        return LastShare;
    }

    public ICurlUrlHandle CreateUrl() => new FakeUrlHandle { SetResult = UrlSetResult };

    public CurlUrlCode UrlSetResult { get; init; } = CurlUrlCode.Ok;

    public IMimeBuilder CreateMime() => CreatingMime?.Invoke() ?? throw new NotSupportedException();

    public Func<IMimeBuilder>? CreatingMime { get; init; }

    public ICurlFormBuilder CreateForm() => CreatingForm?.Invoke() ?? throw new NotSupportedException();

    /// <summary>Gets the form builder each <c>curl_formadd</c> that starts a form gets; none throws.</summary>
    public Func<ICurlFormBuilder>? CreatingForm { get; init; }

    /// <summary>Gets the connection every easy transfer's <c>curl_easy_send</c> and <c>curl_easy_recv</c> use; none throws.</summary>
    public IConnectOnlyConnection? ConnectOnly { get; init; }

    /// <summary>Every <c>curl_easy_pause</c> any of its easy transfers was given.</summary>
    public List<CurlPause> Pauses { get; } = [];

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class FakeEasyTransfer(CurlCode performResult, TransferInfo info, Func<EasyOptions, CurlCode>? performing) : IEasyTransfer
    {
        public EasyOptions Options { get; set; } = new();

        public Func<TransferInfo>? Informing { get; init; }

        public TransferInfo Info => Informing?.Invoke() ?? info;

        public string ErrorText => string.Empty;

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection? ConnectOnly { get; init; }

        public IConnectOnlyConnection Connection => ConnectOnly ?? throw new NotSupportedException();

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) => ValueTask.FromResult(performing?.Invoke(Options) ?? performResult);

        public IEasyTransfer Duplicate() => new FakeEasyTransfer(performResult, info, performing) { Options = Options, Pauses = Pauses, Informing = Informing };

        public void Reset() => Options = new();

        public List<CurlPause> Pauses { get; init; } = [];

        public CurlCode Pause(CurlPause directions)
        {
            Pauses.Add(directions);
            return CurlCode.Ok;
        }

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
