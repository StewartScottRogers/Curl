using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// One easy handle of a <see cref="ManagedCurlEngine" /> (ADR-0506, ADR-0541). <see cref="PerformAsync" />
/// runs one transfer from <see cref="Options" />: a dispatcher built from them, a context
/// <see cref="TransferContextFactory" /> builds, and the attempt <see cref="TransferPerformer.FollowWatchingSpeedAsync" />
/// performs through a <see cref="RedirectFollower" />. Body bytes go to the write callback and header
/// lines to the header callback, each discarded when unset; writing to standard output when no write
/// callback is set is <c>Libcurl</c>'s job, not the engine's. An upload (<see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" />)
/// reads the read callback through <see cref="CallbackReadStream" />. Cookies, the HSTS cache, pooled
/// connections and the <c>%{xfer_id}</c> numbering are the engine's (<see cref="ManagedEngineSharedState" />),
/// shared with its other handles (ADR-0542). The header, WebSocket and connect-only
/// APIs are phase 3 work and throw <see cref="NotSupportedException" />.
/// </summary>
/// <param name="createDispatcher">Builds a transfer's dispatcher from its options, the engine's shared state and the cookie store its HTTP handler keeps.</param>
/// <param name="shared">What the engine's handles share.</param>
internal sealed class ManagedEasyTransfer(
    Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, ProtocolDispatcher> createDispatcher,
    ManagedEngineSharedState shared) : IEasyTransfer
{
    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a perform with no URL set.</summary>
    internal const string NoUrlSetText = "No URL set";

    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a URL it cannot parse.</summary>
    internal const string MalformedUrlText = "URL rejected: Malformed input to a URL function";

    /// <inheritdoc />
    public EasyOptions Options { get; set; } = new();

    /// <inheritdoc />
    public TransferInfo Info { get; private set; } = new();

    /// <inheritdoc />
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>The directions <see cref="Pause" /> last left paused.</summary>
    internal CurlPause PausedDirections { get; private set; }

    /// <summary>
    /// Gets or sets the curl tool's own settings for the handle's transfers - <c>-s</c>, <c>-S</c>,
    /// <c>--fail-with-body</c> and the rest that <see cref="TransferSettingsOf" /> can only default - for
    /// <c>Curl.Console</c> to set from its command line once its runner performs through the engine (BL-2318, ADR-0542); <see langword="null" />, as for every
    /// <c>Curl.Api</c> and <c>Libcurl</c> handle, to take them from <see cref="Options" />.
    /// </summary>
    internal EngineTransferSettings? ToolSettings { get; set; }

    /// <summary>Not supported yet: the header API is phase 3's headers work (ADR-0506).</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ITransferHeaders Headers => throw new NotSupportedException("The header API is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: the WebSocket API is phase 3's WebSocket work (ADR-0506).</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IEasyWebSocket WebSocket => throw new NotSupportedException("The WebSocket API is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: <c>curl_easy_recv</c> and <c>curl_easy_send</c> are phase 3's connect-only work (ADR-0506).</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IConnectOnlyConnection Connection => throw new NotSupportedException("The connect-only API is phase 3 work (ADR-0506).");

    /// <inheritdoc />
    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        EasyOptions options = Options;
        if (options.Network.Url is not { } urlText)
        {
            return Finish(CurlCode.UrlMalformat, NoUrlSetText, new TransferInfo());
        }

        if (!CurlUrl.TryParse(urlText, options.Network.PathAsIs, out CurlUrl? url))
        {
            return Finish(CurlCode.UrlMalformat, MalformedUrlText, new TransferInfo { EffectiveUrl = urlText });
        }

        EngineTransferSettings settings = ToolSettings ?? TransferSettingsOf(options);
        long transferId = shared.Performer.TakeTransferId();
        CookieEngine? cookies = CookieEngine.FromHttpOptions(options.Http, shared.Cookies);
        RedirectFollower follower = new(createDispatcher(options, shared, cookies?.HandlerStore));
        TransferResult result = await TransferPerformer.FollowWatchingSpeedAsync(
            new EngineTransferState(),
            follower,
            CreateContext(options, settings, url, cancellationToken),
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication)).ConfigureAwait(false);
        return Finish(
            result.ExitCode.ToCurlCode(),
            result.ErrorMessage ?? string.Empty,
            TransferInfoFromReport.Of(result, urlText, options.Transfer.FileTime, shared.TimeProvider) with { TransferId = transferId });
    }

    /// <inheritdoc />
    public IEasyTransfer Duplicate() => new ManagedEasyTransfer(createDispatcher, shared) { Options = Options, ToolSettings = ToolSettings };

    /// <inheritdoc />
    public void Reset()
    {
        Options = new EasyOptions();
        ToolSettings = null;
    }

    /// <summary>
    /// <c>curl_easy_pause</c>: records the directions to leave paused. A handle outside a transfer has
    /// nothing to pause yet, so the call always succeeds, as libcurl's does on an idle handle.
    /// </summary>
    /// <param name="directions">The directions to leave paused.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public CurlCode Pause(CurlPause directions)
    {
        PausedDirections = directions;
        return CurlCode.Ok;
    }

    /// <summary>
    /// <c>curl_easy_upkeep</c>: the engine's pooled connections need no keep-alive frames of their own
    /// (no HTTP/2 PING is sent on idle connections yet), so there is nothing to do.
    /// </summary>
    /// <param name="cancellationToken">Stops the upkeep.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => ValueTask.FromResult(CurlCode.Ok);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private TransferContext CreateContext(EasyOptions options, EngineTransferSettings settings, CurlUrl url, CancellationToken cancellationToken) =>
        new TransferContextFactory(Stream.Null, shared.TimeProvider) { HstsStore = shared.Hsts }.Create(
            settings,
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            url,
            options.Callbacks.Write is { } write ? CallbackWriteStream.Of(write) : Stream.Null,
            range: null,
            resumeFrom: options.Transfer.ResumeFrom > 0 ? options.Transfer.ResumeFrom : null,
            headerOutput: options.Callbacks.Header is { } header ? CallbackWriteStream.Of(header) : null,
            upload: UploadOf(options),
            abortToken: cancellationToken);

    /// <summary>
    /// The upload source of a <see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" /> transfer: the read callback, or
    /// nothing to read when none is set, as the engine has no standard input of its own; the host hands
    /// standard input or a <c>-T</c> file over as a read callback (ADR-0542).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The upload stream, or <see langword="null" /> when the transfer uploads nothing.</returns>
    private static Stream? UploadOf(EasyOptions options) =>
        options.Transfer.Upload
            ? options.Callbacks.Read is { } read ? new CallbackReadStream(read) : Stream.Null
            : null;

    /// <summary>
    /// The curl tool's own settings for a transfer driven through the easy interface with no
    /// <see cref="ToolSettings" />: what the easy groups say (<c>CURLOPT_SSL_VERIFYPEER</c>,
    /// <c>CURLOPT_KEYPASSWD</c>, <c>CURLOPT_SSLENGINE</c>, <c>CURLOPT_PROTOCOLS_STR</c>,
    /// <c>CURLOPT_REDIR_PROTOCOLS_STR</c>, <c>CURLOPT_DIRLISTONLY</c>, <c>CURLOPT_PROXYHEADER</c>,
    /// <c>CURLOPT_HTTPPROXYTUNNEL</c>, <c>CURLOPT_UNIX_SOCKET_PATH</c>), and libcurl's defaults for the
    /// rest, which are the tool's alone (ADR-0541, ADR-0542). <c>CURLOPT_FAILONERROR</c> needs nothing here:
    /// <see cref="HttpRequestOptionsMapping" /> reads it from the behaviour group.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The settings.</returns>
    internal static EngineTransferSettings TransferSettingsOf(EasyOptions options) =>
        new(
            Silent: true,
            ShowError: false,
            FailMode: HttpFailMode.None,
            CreateFileMode: null,
            Insecure: !options.Tls.Origin.VerifyPeer,
            Passphrase: options.Tls.Origin.KeyPassword,
            Engine: options.Tls.Engine,
            EchModeIsMalformed: false,
            PostData: null,
            ProxyCredentials: null,
            AllowedProtocols: ProtocolSetOf(options.Network.Protocols),
            AllowedRedirectProtocols: ProtocolSetOf(options.Network.RedirectProtocols),
            SslLevel: TransportSecurityLevel.None,
            FtpSslControlOnly: false,
            SslRequired: false,
            ListOnly: options.Transfer.DirListOnly,
            UploadFlags: [],
            ProxyHeaders: options.Proxy.ProxyHeaders,
            ProxyTunnel: options.Proxy.HttpProxyTunnel,
            OverUnixSocket: options.Network.UnixSocketPath is not null,
            HappyEyeballsTimeout: HttpRequestOptionsMapping.HappyEyeballsTimeoutOf(null),
            AppendUrlQuery: url => url);

    /// <summary>
    /// The schemes a <c>CURLOPT_PROTOCOLS_STR</c> or <c>CURLOPT_REDIR_PROTOCOLS_STR</c> list names, lower
    /// cased with blanks around each name dropped.
    /// </summary>
    /// <param name="protocols">The comma-separated list, or <see langword="null" /> when not set.</param>
    /// <returns>The schemes, or <see langword="null" /> for libcurl's default.</returns>
    internal static IReadOnlySet<string>? ProtocolSetOf(string? protocols) =>
        protocols?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(protocol => protocol.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private CurlCode Finish(CurlCode code, string errorText, TransferInfo info)
    {
        ErrorText = errorText;
        Info = info;
        return code;
    }
}
