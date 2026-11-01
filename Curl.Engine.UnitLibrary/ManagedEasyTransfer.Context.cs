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

// The transfer context a perform runs in: outputs, upload, settings and credentials.
internal sealed partial class ManagedEasyTransfer
{
    /// <summary>
    /// The write-callback output the last context built wrote the body to, or <see langword="null" /> when it wrote
    /// to the runner's <see cref="ToolOutputs" />, to no callback, or no context was built; its count is
    /// <see cref="TransferInfo.SizeDelivered" /> (CGR-126).
    /// </summary>
    private CallbackWriteStream? lastWriteCallbackOutput;

    /// <summary>
    /// The header callback the last context built wrote the response head to, or <see langword="null" /> when none was set;
    /// a callback that took nothing before it failed the transfer leaves no parsed head on the info (CGR-252).
    /// </summary>
    private CallbackWriteStream? lastHeaderCallbackOutput;

    /// <summary>
    /// Builds the transfer's context with <paramref name="toolDispatch" />'s factory, or without one a
    /// factory with no standard input over the engine's clock, the caches' HSTS store and <see cref="Host" />'s
    /// diagnostic log, and keeps it as <see cref="LastContext" />.
    /// </summary>
    /// <param name="toolDispatch">The runner's follower and context factory, or <see langword="null" /> for the handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="progress">Where the progress goes, or <see langword="null" />.</param>
    /// <param name="events">Where the events go.</param>
    /// <param name="inputs">The pieces chosen before connecting.</param>
    /// <param name="state">The running transfer, which keeps the watchdogs.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The context.</returns>
    private TransferContext CreateContext(
        EngineToolTransferDispatch? toolDispatch,
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        ITransferProgress? progress,
        ITransferEvents events,
        EngineToolTransferInputs inputs,
        EngineTransferState state,
        CancellationToken cancellationToken)
    {
        EngineToolTransferOutputs outputs = ToolOutputs ?? OwnOutputsOf(options.Callbacks, PauseGateFor(url), RecordsHeaders(url) ? headers : null);
        lastWriteCallbackOutput = outputs.Output as CallbackWriteStream;
        lastHeaderCallbackOutput = HeaderCallbackOf(outputs.HeaderOutput);
        TransferContextFactory factory = toolDispatch?.ContextFactory ?? new TransferContextFactory(Stream.Null, shared.TimeProvider) { ReadsTelnetStandardInputInTextMode = false, HstsStore = PerformShared.Hsts, DiagnosticLog = Host.DiagnosticLog, KeptDigestChallenges = OriginOf(url) == keptDigestOrigin ? keptDigestChallenges : [], KeptDigestStaysAvailable = KeptDigestStaysAvailableFor(url), KeptAuthSchemePicked = keptAuthSchemePicked };
        return LastContext = factory.Create(
            settings,
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            url,
            outputs.Output,
            range: inputs.Range,
            resumeFrom: inputs.ResumeFrom,
            headerOutput: outputs.HeaderOutput,
            formBody: inputs.FormBody,
            upload: inputs.Upload,
            proxy: inputs.Proxy,
            watchHeaderOutput: outputs.WatchHeaderOutput,
            progress: progress,
            events: events,
            lowSpeedWatchdog: state.AttemptLowSpeedWatchdog,
            maxTimeWatchdog: state.AttemptMaxTimeWatchdog,
            lookedUpCredentials: inputs.LookedUpCredentials,
            ifNoneMatchHeaders: inputs.IfNoneMatchHeaders,
            altSvc: inputs.AltSvc,
            bodyHeaderStyles: outputs.BodyHeaderStyles,
            abortToken: cancellationToken,
            ssh: inputs.Ssh,
            featureMask: state.FeatureMask);
    }

    /// <summary>
    /// The outputs a handle with no <see cref="ToolOutputs" /> writes to: the body to the write callback and the
    /// header lines to the header callback through <see cref="CallbackWriteStream" />, the body discarded and the
    /// header lines dropped when the callback is unset; with <paramref name="headerStore" />, every header line is
    /// recorded there first (<see cref="HeaderRecordingStream" />).
    /// </summary>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="pauseGate">The pause state a callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <param name="headerStore">Where the header lines are recorded, or <see langword="null" /> to record none.</param>
    /// <returns>The outputs.</returns>
    private static EngineToolTransferOutputs OwnOutputsOf(EasyCallbackOptions callbacks, EasyPauseGate? pauseGate, TransferHeaderStore? headerStore)
    {
        Stream? headerCallback = callbacks.Header is { } header ? CallbackWriteStream.Of(header, pauseGate) : null;
        return new(
            callbacks.Write is { } write ? CallbackWriteStream.Of(write, pauseGate) : Stream.Null,
            HeaderOutput: headerStore is null ? headerCallback : new HeaderRecordingStream(headerStore, headerCallback));
    }

    /// <summary>The header callback's stream behind <paramref name="headerOutput" />, or <see langword="null" /> when it is not one.</summary>
    /// <param name="headerOutput">The header output of a context.</param>
    /// <returns>The stream.</returns>
    private static CallbackWriteStream? HeaderCallbackOf(Stream? headerOutput) =>
        (headerOutput as HeaderRecordingStream)?.Callback;

    /// <summary>
    /// Whether a transfer of <paramref name="url" /> records its headers for the header API: only HTTP's and
    /// WebSocket's, as libcurl keeps only HTTP headers; other schemes' header lines (FTP's server replies and the
    /// like) are not <c>name: value</c> headers (ADR-0603).
    /// </summary>
    /// <param name="url">The URL transferred.</param>
    /// <returns>Whether its headers are recorded.</returns>
    private static bool RecordsHeaders(CurlUrl url) => url.Scheme.ToLowerInvariant() is "http" or "https" or "ws" or "wss";

    /// <summary>
    /// The upload source of a <see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" /> transfer: the read callback, or
    /// nothing to read when none is set, as the engine has no standard input of its own; the host hands
    /// standard input or a <c>-T</c> file over as a read callback (ADR-0542). With a seek callback too, the
    /// upload is a <see cref="SeekingCallbackReadStream" />, which a resend rewinds through it (BL-2417).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL to transfer: an <c>http</c> or <c>https</c> one sends <c>CURLOPT_INFILESIZE</c> bytes (<see cref="DeclaredUploadLengthOf" />).</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The upload stream, or <see langword="null" /> when the transfer uploads nothing.</returns>
    private Stream? UploadOf(EasyOptions options, CurlUrl url, EasyPauseGate? pauseGate) =>
        options.Transfer.Upload
            ? options.Callbacks.Read is { } read ? CallbackUploadOf(read, options.Callbacks, pauseGate, WebSocketConnection.UploadFrames, DeclaredUploadLengthOf(options, url)) : Stream.Null
            : null;

    /// <summary>
    /// The length a read-callback upload is held to (BL-2645): <c>CURLOPT_INFILESIZE</c> when it is known and the
    /// URL is <c>http</c> or <c>https</c>, so the upload stops there and goes with a <c>Content-Length</c>, as
    /// curl 8.22.0's <c>cr_in_read</c> stops at <c>total_len</c>; else <see langword="null" />, the upload ending when
    /// the callback returns 0 (ADR-0676).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <returns>The declared length, or <see langword="null" />.</returns>
    internal static long? DeclaredUploadLengthOf(EasyOptions options, CurlUrl url) =>
        options.Transfer.InFileSize >= 0 && (IsScheme(url, "http") || IsScheme(url, "https")) ? options.Transfer.InFileSize : null;

    /// <summary>Whether <paramref name="url" />'s scheme is <paramref name="scheme" />, in any case.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="scheme">The scheme.</param>
    /// <returns><see langword="true" /> when it is.</returns>
    private static bool IsScheme(CurlUrl url, string scheme) => string.Equals(url.Scheme, scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The read callback's upload stream: one the seek callback rewinds when one is set, else one the ioctl callback rewinds (BL-2607), carrying the trailer
    /// callback a chunked HTTP upload asks for its trailers (BL-2475).
    /// </summary>
    /// <param name="read">The read callback.</param>
    /// <param name="callbacks">The handle's callbacks, whose seek and trailer callbacks the stream takes.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" />.</param>
    /// <param name="uploadFrames">The frames a WebSocket upload goes out in (<see cref="ManagedEasyWebSocket.UploadFrames" />), or <see langword="null" /> for a POST body.</param>
    /// <param name="declaredLength">The length the upload is held to (<see cref="CallbackReadStream.DeclaredLength" />), or <see langword="null" />.</param>
    /// <returns>The upload stream.</returns>
    private static CallbackReadStream CallbackUploadOf(ReadCallback read, EasyCallbackOptions callbacks, EasyPauseGate? pauseGate, WsUploadFrames? uploadFrames = null, long? declaredLength = null) =>
        (callbacks.Seek, callbacks.Ioctl) switch
        {
            ({ } seek, _) => new SeekingCallbackReadStream(read, seek, pauseGate) { Trailer = callbacks.Trailer, UploadFrames = uploadFrames, DeclaredLength = declaredLength },
            (null, { } ioctl) => new IoctlRewoundCallbackReadStream(read, ioctl, pauseGate) { Trailer = callbacks.Trailer, UploadFrames = uploadFrames, DeclaredLength = declaredLength },
            _ => new CallbackReadStream(read, pauseGate) { Trailer = callbacks.Trailer, UploadFrames = uploadFrames, DeclaredLength = declaredLength },
        };

    /// <summary>
    /// The curl tool's own settings for a transfer driven through the easy interface with no
    /// <see cref="ToolSettings" />: what the easy groups say (<c>CURLOPT_SSL_VERIFYPEER</c>,
    /// <c>CURLOPT_KEYPASSWD</c>, <c>CURLOPT_SSLENGINE</c>, <c>CURLOPT_PROTOCOLS_STR</c>,
    /// <c>CURLOPT_REDIR_PROTOCOLS_STR</c>, <c>CURLOPT_DIRLISTONLY</c>, <c>CURLOPT_PROXYHEADER</c> (none under <c>CURLHEADER_UNIFIED</c>, whose request carries <c>CURLOPT_HTTPHEADER</c> alone),
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
            PostData: MqttPostDataOf(options),
            ProxyCredentials: ProxyCredentialsOf(options.Authentication),
            AllowedProtocols: ProtocolSetOf(options.Network.Protocols),
            AllowedRedirectProtocols: ProtocolSetOf(options.Network.RedirectProtocols),
            SslLevel: TransportSecurityLevel.None,
            FtpSslControlOnly: false,
            SslRequired: false,
            ListOnly: options.Transfer.DirListOnly,
            UploadFlags: UploadFlagNamesOf(options.Protocols.Smtp.UploadFlags),
            ProxyHeaders: options.Proxy.HeaderOption == HeaderOption.Unified ? [] : options.Proxy.ProxyHeaders,
            ProxyTunnel: options.Proxy.HttpProxyTunnel,
            OverUnixSocket: options.Network.UnixSocketPath is not null,
            HappyEyeballsTimeout: HttpRequestOptionsMapping.HappyEyeballsTimeoutOf(null),
            AppendUrlQuery: url => url,
            PostWithoutData: options.Http is { Post: true, PostFields: null } && IsMqttUrl(options));

    // The IMAP APPEND flag names CURLOPT_UPLOAD_FLAGS' CURLULFLAG_* bits (1, 2, 4, 8, 16) name, in curl's fixed order.
    private static readonly string[] UploadFlagNames = ["answered", "deleted", "draft", "flagged", "seen"];

    private static string[] UploadFlagNamesOf(byte uploadFlags) =>
        [.. UploadFlagNames.Where((_, bit) => (uploadFlags & (1 << bit)) != 0)];

    /// <summary>
    /// The <c>CURLOPT_POSTFIELDS</c> an MQTT transfer publishes, cut to <c>CURLOPT_POSTFIELDSIZE</c> when that is not
    /// negative and shorter (an empty set of fields is an empty payload), or <see langword="null" /> to subscribe
    /// instead; only an <c>mqtt</c> or <c>mqtts</c> URL reads it, as libcurl's <c>mqtt_do</c> does (upstream test1916, BL-2673).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The payload, or <see langword="null" />.</returns>
    private static ReadOnlyMemory<byte>? MqttPostDataOf(EasyOptions options)
    {
        if (options.Http.PostFields is not { } fields || !IsMqttUrl(options))
        {
            return null;
        }

        long size = options.Http.PostFieldSize;
        return size < 0 || size >= fields.Length ? fields : fields[..(int)size];
    }

    /// <summary>Whether the handle's URL names the <c>mqtt</c> or <c>mqtts</c> scheme.</summary>
    private static bool IsMqttUrl(EasyOptions options) =>
        options.Network.Url is { } url
        && (url.StartsWith("mqtt:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("mqtts:", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The proxy credentials <c>CURLOPT_PROXYUSERPWD</c>, <c>CURLOPT_PROXYUSERNAME</c> and <c>CURLOPT_PROXYPASSWORD</c>
    /// give, as the curl tool's <c>-U</c> gives them: <c>CURLOPT_PROXYUSERPWD</c> split at its first colon (no colon is
    /// an empty password), the user name and password options each winning over their half of it. They are used only
    /// when the proxy URL has no user information of its own, as in libcurl (upstream test1588, BL-2660, ADR-0677,
    /// CGR-079, ADR-0825).
    /// </summary>
    /// <param name="authentication">The handle's authentication options.</param>
    /// <returns>The credentials, or <see langword="null" /> when none of the three is set.</returns>
    internal static System.Net.NetworkCredential? ProxyCredentialsOf(EasyAuthenticationOptions authentication) =>
        authentication is { ProxyUserPassword: null, ProxyUserName: null, ProxyPassword: null } ? null : ProxyCredentialOf(authentication);

    private static System.Net.NetworkCredential ProxyCredentialOf(EasyAuthenticationOptions authentication)
    {
        (string user, string password) = SplitAtFirstColon(authentication.ProxyUserPassword ?? string.Empty);
        return new System.Net.NetworkCredential(authentication.ProxyUserName ?? user, authentication.ProxyPassword ?? password);
    }

    private static (string User, string Password) SplitAtFirstColon(string userPassword)
    {
        int colon = userPassword.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (userPassword, string.Empty) : (userPassword[..colon], userPassword[(colon + 1)..]);
    }

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
}
