using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

// One perform: entry, pushed responses, caches, proxy and upload resume.
internal sealed partial class ManagedEasyTransfer
{
    /// <inheritdoc />
    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        // A perform of this handle already running means the call comes from one of its callbacks, which
        // curl 8.22.0's Curl_is_in_callback check refuses (CGR-146).
        if (ConnectOnly.PerformRunning)
        {
            return CurlCode.RecursiveApiCall;
        }

        // The multi handle gives the marshal of each perform it starts just before starting it, so a perform of a
        // handle in a multi handle with none is the application's curl_easy_perform, which curl 8.22.0 refuses (CGR-147).
        runningMultiCallbacks = MultiCallbacks;
        MultiCallbacks = null;
        if (runningMultiCallbacks is null && shared.MultiOwners.ContainsKey(this))
        {
            ErrorText = "easy handle already used in multi handle";
            return CurlCode.FailedInit;
        }

        ConnectOnly.PerformRunning = true;
        try
        {
            return await PerformRunningAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ConnectOnly.PerformRunning = false;
        }
    }

    private async ValueTask<CurlCode> PerformRunningAsync(CancellationToken cancellationToken)
    {
        headers.Clear();
        headers.ResponseHeadRead = ShowResponseHeadOnInfo;
        performStartedTimestamp = shared.TimeProvider.GetTimestamp();
        connectedTimestamp = null;
        await ConnectOnly.CloseAsync().ConfigureAwait(false);
        await WebSocketConnection.CloseAsync().ConfigureAwait(false);
        if (PushedResponse is { } pushed)
        {
            PushedResponse = null;
            return await PerformPushedAsync(pushed, cancellationToken).ConfigureAwait(false);
        }

        return await PerformFromOptionsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the HTTP/2 server push this handle was duplicated for (<see cref="DuplicateForPush" />), which its next
    /// perform reads in place of sending a request; <see langword="null" /> for every other handle and after that perform.
    /// </summary>
    internal IHttpPushedResponse? PushedResponse { get; private set; }

    /// <summary>
    /// Gets or sets what puts the callbacks of the perform a multi handle starts through that handle's
    /// <see cref="MultiCallbackQueue" />, so they run inside its perform on its caller's thread (CGR-033);
    /// <see langword="null" /> for a perform outside any multi handle. <see cref="ManagedMultiTransfer" /> sets it before
    /// each perform it starts, and the perform takes it once, as it starts, leaving it <see langword="null" /> again.
    /// </summary>
    internal TransferCallbackMarshal? MultiCallbacks { get; set; }

    /// <summary>The <see cref="MultiCallbacks" /> the latest perform took as it started, <see langword="null" /> outside a multi handle.</summary>
    private TransferCallbackMarshal? runningMultiCallbacks;

    private EasyOptions MarshalledOf(EasyOptions options) => runningMultiCallbacks is { } marshal ? options with { Callbacks = marshal.Over(options.Callbacks) } : options;

    /// <summary>
    /// A duplicate of this handle bound to <paramref name="response" />, as curl 8.22.0's <c>push_promise</c> makes
    /// the pushed transfer with <c>curl_easy_duphandle</c> of the parent (BL-2469).
    /// </summary>
    /// <param name="response">The pushed response the duplicate's next perform reads.</param>
    /// <returns>The duplicate.</returns>
    internal ManagedEasyTransfer DuplicateForPush(IHttpPushedResponse response)
    {
        ManagedEasyTransfer pushed = (ManagedEasyTransfer)Duplicate();
        pushed.PushedResponse = response;
        return pushed;
    }

    /// <summary>
    /// The push sink the running perform's HTTP handler offers HTTP/2 server pushes to: an
    /// <see cref="EngineHttp2PushSink" /> while this handle is in a multi handle whose <c>CURLMOPT_PUSHFUNCTION</c>
    /// is set, else <see langword="null" />, so the handler keeps advertising <c>SETTINGS_ENABLE_PUSH</c> 0 (BL-2469).
    /// </summary>
    /// <returns>The sink, or <see langword="null" />.</returns>
    private EngineHttp2PushSink? PushSinkOf() =>
        shared.MultiOwners.TryGetValue(this, out ManagedMultiTransfer? multi) && multi.Options.Push is not null
            ? new EngineHttp2PushSink(this, multi)
            : null;

    /// <summary>
    /// Runs a pushed transfer: the pushed response's head goes to the header callback a line at a time and its body
    /// to the write callback (<see cref="PushedResponseCopier" />), nothing being sent; a failed stream answers
    /// <see cref="CurlCode.RecvError" /> and a short take by a callback <see cref="CurlCode.WriteError" />, each with
    /// its message as <see cref="ErrorText" />.
    /// </summary>
    /// <param name="pushed">The pushed response.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformPushedAsync(IHttpPushedResponse pushed, CancellationToken cancellationToken)
    {
        EngineToolTransferOutputs outputs = OwnOutputsOf(MarshalledOf(Options).Callbacks, pauseGate: null, headers);
        try
        {
            await PushedResponseCopier.CopyAsync(pushed.Content, outputs.HeaderOutput!, outputs.Output, cancellationToken).ConfigureAwait(false);
            ErrorText = string.Empty;
            return CurlCode.Ok;
        }
        catch (OutputWriteFailedException exception)
        {
            ErrorText = exception.Message;
            return CurlCode.WriteError;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ErrorText = exception.Message;
            return CurlCode.RecvError;
        }
    }

    /// <summary>
    /// Runs one transfer from <see cref="Options" />, the connections it opens the handle's own
    /// (<see cref="OwnTheConnectionsOpened" />), over the engine's caches, or, while
    /// <see cref="Api.Options.Behaviour.EasyBehaviourOptions.Share" /> names a <see cref="ManagedShareHandle" />,
    /// attached to it and over the caches it shares (ADR-0606); a share handle of another engine kind shares nothing.
    /// </summary>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformFromOptionsAsync(CancellationToken cancellationToken)
    {
        OwnTheConnectionsOpened();
        if (Options.Behaviour.Share is not ManagedShareHandle share)
        {
            return await PerformFromOptionsOverCachesAsync(cancellationToken).ConfigureAwait(false);
        }

        performShare = share;
        attachedShared = share.Attach(this, shared, Options.Http.CookieFiles.Count > 0, ShareHostKeyOf(Options));
        try
        {
            return await PerformFromOptionsOverCachesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            share.LockConnectionsAtDone(this);
            share.LockDnsAtDone(this);
            performShare = null;
            attachedShared = null;
        }
    }

    /// <summary>
    /// The <c>host:port</c> a perform from <paramref name="options" /> resolves, which a share handle sharing DNS
    /// looks up and adds under its lock (<see cref="ManagedShareHandle.Attach" />); <see langword="null" /> when no URL
    /// is set or it does not parse.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The name, or <see langword="null" />.</returns>
    internal static string? ShareHostKeyOf(EasyOptions options) =>
        options.Network.Url is { } urlText && CurlUrl.TryParse(urlText, options.Network.PathAsIs, out CurlUrl? url)
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{url.IdnHost}:{url.Port}")
            : null;

    /// <summary>
    /// The cookie store the transfer's HTTP handler keeps: <paramref name="cookies" />' own, under the share
    /// handle's cookie lock while the perform is attached to one (<see cref="ManagedShareHandle.CookieStoreFor" />).
    /// </summary>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <returns>The store, or <see langword="null" /> for none.</returns>
    private ICookieStore? HandlerCookieStoreOf(CookieEngine? cookies) =>
        performShare is { } share ? share.CookieStoreFor(cookies?.HandlerStore, this) : cookies?.HandlerStore;

    /// <summary>
    /// <paramref name="options" /> with no <c>CURLOPT_USERAGENT</c> set as an empty one, which sends no
    /// <c>User-Agent</c> line to the origin or a proxy's <c>CONNECT</c>, as libcurl 8.22.0 sends none without it; the
    /// HTTP handler's <c>curl/8.22.0</c> default is the curl tool's, which a perform with <see cref="ToolSettings" />
    /// keeps (BL-2559, ADR-0659).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options the perform runs with.</returns>
    internal static EasyOptions WithoutDefaultUserAgent(EasyOptions options) =>
        options.Http.UserAgent is null ? options with { Http = options.Http with { UserAgent = string.Empty } } : options;

    /// <summary>
    /// <paramref name="options" /> with <see cref="Curl.Api.Options.Http.EasyHttpOptions.PostFields" /> dropped when
    /// <see cref="Curl.Api.Options.Http.EasyHttpOptions.HttpGet" /> is set and <see cref="Curl.Api.Options.Http.EasyHttpOptions.Post" /> is not, so a
    /// <c>CURLOPT_HTTPGET</c> after <c>CURLOPT_POSTFIELDS</c> sends a <c>GET</c> with no body, as libcurl 8.22.0 switches
    /// only its method and keeps the fields for a later <c>CURLOPT_POST</c> (BL-2461, ADR-0565).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options the perform runs with.</returns>
    internal static EasyOptions WithoutPostFieldsUnderHttpGet(EasyOptions options) =>
        options.Http is { HttpGet: true, Post: false, PostFields: not null }
            ? options with { Http = options.Http with { PostFields = null } }
            : options;

    /// <summary>Runs one transfer from <see cref="Options" />, as <see cref="PerformAsync" /> describes, over <see cref="PerformShared" />.</summary>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformFromOptionsOverCachesAsync(CancellationToken cancellationToken)
    {
        EasyOptions options = MarshalledOf(UrlPortOverride.Apply(UrlHandleOverride.Apply(WithoutPostFieldsUnderHttpGet(ToolSettings is null ? WithoutDefaultUserAgent(Options) : Options))));
        LastResult = null;
        LastContext = null;
        lastWriteCallbackOutput = null;
        lastHeaderCallbackOutput = null;
        StartSocketWait(options);
        if (options.Network.Url is not { } urlText)
        {
            return Finish(CurlCode.UrlMalformat, NoUrlSetText, new TransferInfo());
        }

        if (TransferUrlOf(urlText, options, out CurlUrlRejection rejection) is not { } url)
        {
            return Finish(CurlCode.UrlMalformat, TransferPerformer.UrlRejectionText(rejection), new TransferInfo { EffectiveUrl = urlText });
        }

        EngineTransferSettings settings = ToolSettings ?? TransferSettingsOf(options);
        long transferId = TransferIdOf(ToolEvents);
        Info = new TransferInfo { EffectiveUrl = urlText, TransferId = transferId, ConnectionId = NoConnectionId };
        performedHsts = PerformShared.Hsts;
        CurlCode loaded = LoadHstsFromCallback(options, performedHsts);
        if (loaded != CurlCode.Ok)
        {
            // The handle's cache never loaded, so cleanup hands the write callback this handle's own empty one, not entries other handles loaded into the engine's (upstream case 1915).
            performedHsts = new HstsTransferPolicy(shared.TimeProvider);
            return Finish(loaded, string.Empty, Info);
        }

        return await PerformThroughProxyAsync(options, settings, url, urlText, transferId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chooses the perform's proxy (<see cref="TrySelectProxy" />), then only connects through it
    /// (<see cref="PerformConnectOnlyAsync" />) or runs the transfer with it on the context; proxy text that cannot
    /// be used ends the perform with the selector's failure before anything is set up or dialled.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private Task<CurlCode> PerformThroughProxyAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        CancellationToken cancellationToken)
    {
        if (!TrySelectProxy(options, settings, url, out ProxyEndpoint? proxy, out TransferResult? refused))
        {
            return Task.FromResult(Finish(refused.ExitCode.ToCurlCode(), refused.ErrorMessage!, Info));
        }

        if (IsRawConnectOnly(options.Network.ConnectOnly, url))
        {
            return PerformConnectOnlyAsync(options, url, proxy, cancellationToken);
        }

        selectedProxy = proxy;
        if (!TryLookUpUrlAndNetrcCredentials(options, url, out TransferResult? credentialFailure))
        {
            return Task.FromResult(Finish(credentialFailure.ExitCode.ToCurlCode(), credentialFailure.ErrorMessage!, Info));
        }

        return PerformResumingUploadAsync(options, settings, url, urlText, transferId, cancellationToken);
    }

    /// <summary>
    /// The perform's <c>%{xfer_id}</c>: the curl tool's runner's (its <c>--retry</c> attempt's, else its transfer's)
    /// when it gave <paramref name="toolEvents" />, so the handle answers its numbering (BL-2495); else the engine's next.
    /// </summary>
    /// <param name="toolEvents">The runner's events and state, or <see langword="null" /> for a handle's own.</param>
    /// <returns>The <c>%{xfer_id}</c>.</returns>
    private long TransferIdOf(EngineToolTransferEvents? toolEvents) =>
        toolEvents is { State: var state } ? state.RetryTransferId ?? state.TransferId : shared.Performer.TakeTransferId();

    /// <summary>
    /// Skips a read-callback upload's <c>CURLOPT_RESUME_FROM</c> offset (<see cref="CallbackUploadResume.SkipUnlessToolGave" />)
    /// and runs the transfer, or ends the perform with the skip's failure before anything is set up or sent (ADR-0641).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private Task<CurlCode> PerformResumingUploadAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        CancellationToken cancellationToken) =>
        CallbackUploadResume.SkipUnlessToolGave(ToolInputs, options) is { } refused
            ? Task.FromResult(Finish(refused.Code, refused.Text, Info))
            : PerformBuildingMimePostAsync(options, settings, url, urlText, transferId, cancellationToken);

    /// <summary>
    /// The URL the perform transfers: the one the curl tool's runner parsed and checked (<see cref="EngineToolTransferInputs.Url" />),
    /// else <paramref name="urlText" /> parsed under <c>CURLOPT_PATH_AS_IS</c> (BL-2394).
    /// </summary>
    /// <param name="urlText">The URL set.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="rejection">Why the URL set does not parse, as curl's <c>URL rejected:</c> text says.</param>
    /// <returns>The URL, or <see langword="null" /> when the URL set does not parse.</returns>
    private CurlUrl? TransferUrlOf(string urlText, EasyOptions options, out CurlUrlRejection rejection)
    {
        rejection = default;
        return ToolInputs?.Url ?? (CurlUrl.TryParse(urlText, options.Network.PathAsIs, out CurlUrl? parsed, out rejection) ? parsed : null);
    }
}
