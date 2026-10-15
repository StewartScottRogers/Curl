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
/// callback is set is <c>Libcurl</c>'s job, not the engine's. The header, WebSocket and connect-only
/// APIs are phase 3 work and throw <see cref="NotSupportedException" />.
/// </summary>
/// <param name="createDispatcher">Builds a transfer's dispatcher from its options.</param>
/// <param name="timeProvider">The clock each transfer runs on.</param>
internal sealed class ManagedEasyTransfer(Func<EasyOptions, ProtocolDispatcher> createDispatcher, TimeProvider timeProvider) : IEasyTransfer
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

        EngineTransferSettings settings = TransferSettingsOf(options);
        RedirectFollower follower = new(createDispatcher(options));
        TransferResult result = await TransferPerformer.FollowWatchingSpeedAsync(
            new EngineTransferState(),
            follower,
            CreateContext(options, settings, url, cancellationToken),
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication)).ConfigureAwait(false);
        return Finish(
            result.ExitCode.ToCurlCode(),
            result.ErrorMessage ?? string.Empty,
            TransferInfoFromReport.Of(result, urlText, options.Transfer.FileTime, timeProvider));
    }

    /// <inheritdoc />
    public IEasyTransfer Duplicate() => new ManagedEasyTransfer(createDispatcher, timeProvider) { Options = Options };

    /// <inheritdoc />
    public void Reset() => Options = new EasyOptions();

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
        new TransferContextFactory(Stream.Null, timeProvider).Create(
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
            abortToken: cancellationToken);

    /// <summary>
    /// The curl tool's own settings for a transfer driven through the easy interface: what the
    /// easy groups say (<c>CURLOPT_SSL_VERIFYPEER</c>, <c>CURLOPT_KEYPASSWD</c>, <c>CURLOPT_SSLENGINE</c>,
    /// <c>CURLOPT_HTTPPROXYTUNNEL</c>, <c>CURLOPT_UNIX_SOCKET_PATH</c>), and libcurl's defaults for the rest,
    /// which have no easy option yet (ADR-0541).
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
            AllowedProtocols: null,
            AllowedRedirectProtocols: null,
            SslLevel: TransportSecurityLevel.None,
            FtpSslControlOnly: false,
            SslRequired: false,
            ListOnly: false,
            UploadFlags: [],
            ProxyHeaders: [],
            ProxyTunnel: options.Proxy.HttpProxyTunnel,
            OverUnixSocket: options.Network.UnixSocketPath is not null,
            HappyEyeballsTimeout: HttpRequestOptionsMapping.HappyEyeballsTimeoutOf(null),
            AppendUrlQuery: url => url);

    private CurlCode Finish(CurlCode code, string errorText, TransferInfo info)
    {
        ErrorText = errorText;
        Info = info;
        return code;
    }
}
