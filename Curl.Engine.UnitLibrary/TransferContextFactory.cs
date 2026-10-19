using System.Net;
using System.Text;

using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;
using EasyFtpClearCommandChannel = Curl.Api.Options.Protocols.FtpClearCommandChannel;
using EasyFtpFileMethod = Curl.Api.Options.Protocols.FtpFileMethod;
using EasyTimeCondition = Curl.Api.Options.Behaviour.TimeCondition;
using FtpFileMethod = Curl.Protocol.Abstractions.FtpFileMethod;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;
using TimeCondition = Curl.Protocol.Abstractions.TimeCondition;

namespace Curl.Engine;

/// <summary>
/// Builds the <see cref="TransferContext" /> for one URL of an accepted command line,
/// carrying every option a handler reads.
/// </summary>
/// <param name="standardInput">
/// The raw standard input stream, given to a <c>telnet</c> transfer as its
/// <see cref="ITransferContext.Upload" />, because curl's telnet "sends what it reads on
/// stdin" (ADR-0006). Every other scheme's upload is <see langword="null" />.
/// </param>
/// <param name="timeProvider">
/// Every context's <see cref="TransferContext.TimeProvider" />, the runner's clock, which
/// <see cref="Curl.Core.TransferRetrier" /> waits on; <see langword="null" /> for
/// <see cref="TimeProvider.System" />.
/// </param>
/// <param name="commandLineTextEncoding">
/// Every context's <see cref="Curl.Protocol.Abstractions.HttpRequestOptions.CommandLineTextEncoding" />,
/// the platform curl's argument encoding (ADR-0067); <see langword="null" /> for Latin-1.
/// </param>
internal sealed class TransferContextFactory(Stream standardInput, TimeProvider? timeProvider = null, Encoding? commandLineTextEncoding = null)
{
    /// <summary>The one scheme whose transfer uploads standard input.</summary>
    private const string TelnetScheme = "telnet";

    /// <summary>The schemes the HTTP handler authenticates, for <see cref="AuthorizationIdentityNtlmCredentialsOf" />.</summary>
    private static readonly string[] HttpAuthenticationSchemes = ["http", "https", "ws", "wss"];

    /// <summary>The TFTP options at libcurl's defaults, whose block size <see cref="BlockSizeOf" /> compares with.</summary>
    private static readonly EasyTftpOptions DefaultTftpOptions = new();

    /// <summary>Every context's clock: the one given, or <see cref="TimeProvider.System" />.</summary>
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Gets or sets every context's <see cref="TransferContext.DiagnosticLog" />: the run's diagnostic log, which the
    /// runner sets once it has opened it, and <see cref="NoDiagnosticLog.Instance" /> until then (ADR-0222).
    /// </summary>
    internal IDiagnosticLog DiagnosticLog { get; set; } = NoDiagnosticLog.Instance;

    /// <summary>
    /// Gets or sets every context's <see cref="HttpRequestOptions.HstsStore" />: the run's HSTS cache, which the
    /// runner sets once it has opened the diagnostic log, so the HTTP handler learns each
    /// <c>Strict-Transport-Security</c> header as it arrives (ADR-0409); <see langword="null" /> until then.
    /// </summary>
    internal IHstsStore? HstsStore { get; set; }

    /// <summary>
    /// Gets or sets every context's <see cref="HttpRequestOptions.KeptDigestChallenges" />: the
    /// <c>WWW-Authenticate</c> values of the 401 that ended a Libcurl handle's previous transfer,
    /// empty by default (BL-2747, ADR-0692).
    /// </summary>
    internal IReadOnlyList<string> KeptDigestChallenges { get; set; } = [];

    /// <summary>
    /// Gets or sets every context's <see cref="HttpRequestOptions.AuthSchemePicked" />: the scheme a
    /// Libcurl handle's previous transfer picked, which libcurl 8.22.0 keeps on the easy handle
    /// (<c>data->state.authhost.picked</c>, upstream test694, BL-2709); <see cref="HttpAuthSchemes.None" /> by default.
    /// </summary>
    internal HttpAuthSchemes KeptAuthSchemePicked { get; set; }

    /// <summary>
    /// Creates the context for one transfer, carrying every option a handler reads.
    /// </summary>
    /// <param name="settings">The tool settings: the post data, TLS level, FTP and mail flags and the creation mode no option group carries.</param>
    /// <param name="http">The option group's HTTP options, which <see cref="HttpRequestOptionsMapping.Of" /> maps onto <see cref="TransferContext.Http" />.</param>
    /// <param name="behaviour">The option group's behaviour options: <see cref="EasyBehaviourOptions.IncludeHeader" /> (<c>-i</c>).</param>
    /// <param name="transfer">
    /// The transfer's options: the <c>-r</c> text, <c>-I</c>'s no body, the file time, the time condition, text
    /// mode, CRLF, the file size limit and the timeout; a <c>-T</c> upload under <c>-C -</c> has
    /// <see cref="EasyTransferOptions.Upload" /> set and <see cref="EasyTransferOptions.ResumeFrom" /> -1, as
    /// curl's tool asks libcurl to resume it from the end of the remote file.
    /// </param>
    /// <param name="protocol">
    /// The option group's protocol options: FTP's <c>-P</c>, <c>-Q</c>, <c>-a</c> and <c>--ftp-*</c> settings and
    /// <c>--disable-epsv</c>/<c>--disable-eprt</c>, the <c>--mail-*</c> sender and recipients,
    /// <c>--tftp-blksize</c>, <c>--tftp-no-options</c> and <c>-t</c>.
    /// </param>
    /// <param name="proxyOptions">
    /// The option group's proxy options: <c>-p</c> (<see cref="EasyProxyOptions.HttpProxyTunnel" />) and
    /// <c>--suppress-connect-headers</c>.
    /// </param>
    /// <param name="authentication">
    /// The option group's authentication options: <c>-u</c> (<see cref="EasyAuthenticationOptions.UserPassword" />),
    /// split at its first colon into <see cref="TransferContext.Credentials" />, and the settings
    /// <see cref="HttpRequestOptionsMapping.Of" /> and <see cref="MailRequestOptionsMapping.FromCommandLine" /> read.
    /// </param>
    /// <param name="network">
    /// The option group's network options: <c>--connect-timeout</c> (zero is not set) and the <c>--connect-to</c>
    /// mappings, which <see cref="ConnectToTunnelsThroughProxy" /> reads.
    /// </param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="output">Where the transfer's bytes go.</param>
    /// <param name="range">The parsed <c>-r</c> range, or <see langword="null" /> for the whole resource.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset, already resolved for <c>-C -</c>.</param>
    /// <param name="headerOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <param name="formBody">The <c>-F</c> body built for this transfer, or <see langword="null" /> without <c>-F</c>.</param>
    /// <param name="upload">
    /// The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>, when a <c>telnet</c>
    /// transfer uploads standard input and any other uploads nothing.
    /// </param>
    /// <param name="proxy">
    /// The proxy chosen for this transfer (<see cref="TransferProxySelection" />), or
    /// <see langword="null" /> to connect directly; it becomes both
    /// <see cref="TransferContext.Proxy" /> and <see cref="Curl.Protocol.Abstractions.HttpRequestOptions.ForwardProxy" />.
    /// </param>
    /// <param name="watchHeaderOutput">
    /// Wraps the header output <see cref="HeaderOutputOf" /> chose, for <c>-J</c>, whose
    /// <c>RemoteHeaderNameStream</c> must read each header line before it goes anywhere;
    /// <see langword="null" /> to use that output as it is.
    /// </param>
    /// <param name="progress">
    /// Where the handler reports how far the transfer got, or <see langword="null" /> for
    /// <see cref="NoTransferProgress.Instance" />.
    /// </param>
    /// <param name="events">
    /// Where the handler and its connector report transfer events for <c>-v</c> and <c>--trace</c>, or
    /// <see langword="null" /> for <see cref="NoTransferEvents.Instance" />.
    /// </param>
    /// <param name="lowSpeedWatchdog">
    /// The attempt's <c>-Y</c>/<c>-y</c> watchdog, which counts the bytes written to
    /// <paramref name="output" /> and reported uploaded to <paramref name="progress" />, and whose
    /// token becomes <see cref="TransferContext.CancellationToken" />; <see langword="null" /> when
    /// the speed is not watched.
    /// </param>
    /// <param name="abortToken">
    /// The token <c>--fail-early</c> aborts the transfer through under <c>-Z</c> (ADR-0127), which also
    /// cancels <see cref="TransferContext.CancellationToken" />; <see cref="CancellationToken.None" /> otherwise.
    /// </param>
    /// <param name="maxTimeWatchdog">
    /// The attempt's <c>-m</c> watchdog (ADR-0117), whose start becomes
    /// <see cref="TransferContext.OperationStarted" />, which watches the reports made to
    /// <paramref name="progress" />, and whose token also cancels
    /// <see cref="TransferContext.CancellationToken" />; <see langword="null" /> without a positive <c>-m</c>.
    /// </param>
    /// <param name="lookedUpCredentials">
    /// The credentials <see cref="TransferCredentialLookup" /> chose, which the context carries in place
    /// of <c>-u</c>'s; <see langword="null" /> to carry <c>-u</c>'s.
    /// </param>
    /// <param name="ifNoneMatchHeaders">
    /// The <c>If-None-Match</c> lines <c>--etag-compare</c> added to the option group so far, sent after every
    /// other header; <see langword="null" /> for none.
    /// </param>
    /// <param name="altSvc">
    /// The transfer's <c>--alt-svc</c> cache, which the HTTP handler stores <c>Alt-Svc</c> headers in and whose
    /// <see cref="AltSvcTransferCache.ApplyTo" /> gives the alternative it connects to and the HTTP version it uses;
    /// <see langword="null" /> for none.
    /// </param>
    /// <param name="bodyHeaderStyles">
    /// How <c>-i</c> and <c>-I</c> style the header lines they write to <paramref name="output" />, a terminal under
    /// <c>--styled-output</c> (ADR-0246); <see langword="null" /> to write them as they come.
    /// </param>
    /// <param name="ssh">
    /// The SSH options of an <c>scp</c> or <c>sftp</c> transfer (<see cref="SshOptionsMapping" />), with the
    /// known-hosts file the runner resolved; <see langword="null" /> for any other scheme.
    /// </param>
    /// <returns>
    /// The context. Its <see cref="TransferContext.NoBody" /> is <c>-I</c>, its
    /// <see cref="TransferContext.ResumeUploadFromUnknownOffset" /> is <c>-C -</c> with a
    /// <c>-T</c> <paramref name="upload" />, and its
    /// <see cref="TransferContext.HeaderOutput" /> is <see cref="HeaderOutputOf" />'s, wrapped by
    /// <paramref name="watchHeaderOutput" /> when given; its <see cref="TransferContext.DumpHeaderOutput" /> is
    /// <paramref name="headerOutput" />, the <c>-D</c> stream alone, whatever <c>-i</c> says.
    /// </returns>
    internal TransferContext Create(
        EngineTransferSettings settings,
        EasyHttpOptions http,
        EasyBehaviourOptions behaviour,
        EasyTransferOptions transfer,
        EasyProtocolOptions protocol,
        EasyProxyOptions proxyOptions,
        EasyAuthenticationOptions authentication,
        EasyNetworkOptions network,
        CurlUrl url,
        Stream output,
        ByteRange? range,
        long? resumeFrom,
        Stream? headerOutput,
        HttpRequestBody? formBody = null,
        Stream? upload = null,
        ProxyEndpoint? proxy = null,
        Func<Stream?, Stream>? watchHeaderOutput = null,
        ITransferProgress? progress = null,
        ITransferEvents? events = null,
        LowSpeedWatchdog? lowSpeedWatchdog = null,
        CancellationToken abortToken = default,
        MaxTimeWatchdog? maxTimeWatchdog = null,
        NetworkCredential? lookedUpCredentials = null,
        IReadOnlyList<string>? ifNoneMatchHeaders = null,
        AltSvcTransferCache? altSvc = null,
        StyledHeaderLines? bodyHeaderStyles = null,
        SshOptions? ssh = null)
    {
        Stream? transferHeaderOutput = watchHeaderOutput is null
            ? HeaderOutputOf(behaviour, transfer, url, output, headerOutput, bodyHeaderStyles)
            : watchHeaderOutput(HeaderOutputOf(behaviour, transfer, url, output, headerOutput, bodyHeaderStyles));
        return new()
        {
            Url = ProxyTransferModeUrl.Of(url, proxy, TunnelsThroughProxy(proxyOptions, network, url, proxy), transfer.ProxyTransferMode, transfer.TransferText, network.PathAsIs),
            Output = WatchedOutput(output, lowSpeedWatchdog),
            HeaderOutput = transferHeaderOutput,
            DumpHeaderOutput = headerOutput,
            NoBody = transfer.NoBody,
            Range = range,
            RangeText = transfer.Range,
            ResumeFrom = resumeFrom,
            ResumeUploadFromUnknownOffset = ResumesUploadFromUnknownOffset(transfer, upload),
            MaxFileSize = MaxFileSizeOf(transfer),
            Upload = UploadOf(url, upload),
            PostData = settings.PostData,
            PostWithoutData = settings.PostWithoutData,
            Credentials = lookedUpCredentials ?? CredentialsOf(authentication) ?? AuthorizationIdentityNtlmCredentialsOf(authentication, url),
            TelnetOptions = protocol.Telnet.TelnetOptions,
            TftpBlockSize = BlockSizeOf(protocol.Tftp),
            TftpNoOptions = protocol.Tftp.NoOptions,
            FtpDisableEpsv = !protocol.Ftp.UseEpsv,
            FtpSkipPasvIp = protocol.Ftp.SkipPasvIp,
            FtpFileMethod = FileMethodOf(protocol.Ftp.FileMethod),
            FtpCreateDirectories = protocol.Ftp.CreateMissingDirectories != FtpCreateMissingDirectories.None,
            FtpAccount = protocol.Ftp.Account,
            FtpAlternativeToUser = protocol.Ftp.AlternativeToUser,
            FtpSendPret = protocol.Ftp.UsePret,
            FtpPort = protocol.Ftp.ActiveAddress,
            FtpUseEprt = protocol.Ftp.UseEprt,
            FtpAcceptTimeout = FtpAcceptTimeoutOf(network),
            SslLevel = settings.SslLevel,
            FtpSslControlOnly = settings.FtpSslControlOnly && !settings.SslRequired,
            FtpCommandChannelClearing = CommandChannelClearingOf(protocol.Ftp.ClearCommandChannel),
            ListOnly = settings.ListOnly,
            UseAscii = transfer.TransferText,
            Append = protocol.Ftp.Append,
            ConvertLineEndings = transfer.Crlf,
            QuoteCommands = QuoteCommandsOf(protocol.Ftp),
            CreateFileMode = settings.CreateFileMode ?? TransferContext.DefaultCreateFileMode,
            PathAsIs = network.PathAsIs,
            ConnectTimeout = ConnectTimeoutOf(network),
            MaxTime = MaxTimeOf(transfer),
            OperationStarted = OperationStartedOf(maxTimeWatchdog),
            TimeCondition = TimeConditionOf(transfer),
            RemoteTime = transfer.FileTime,
            Proxy = proxy,
            Http = HttpOptionsWithAltSvc(
                url,
                HttpRequestOptionsMapping.Of(settings, http, behaviour, transfer, authentication, formBody, proxy, commandLineTextEncoding, ifNoneMatchHeaders) with
                {
                    HstsStore = HstsStore,
                    KeptDigestChallenges = KeptDigestChallenges,
                    AuthSchemePicked = KeptAuthSchemePicked,
                    ProxyTunnel = TunnelsThroughProxy(proxyOptions, network, url, proxy),
                },
                altSvc),
            Mail = MailRequestOptionsMapping.FromCommandLine(settings, transfer, protocol.Smtp, authentication, url.Scheme),
            Ssh = ssh,
            Progress = WatchedProgress(progress, lowSpeedWatchdog, maxTimeWatchdog),
            Events = EventsWritingConnectReplyHeads(proxyOptions, proxy, EventsOrNone(events), transferHeaderOutput),
            TimeProvider = clock,
            DiagnosticLog = DiagnosticLog,
            CancellationToken = TokenOf(abortToken, lowSpeedWatchdog, maxTimeWatchdog),
        };
    }

    /// <summary>
    /// Gets the <c>-u</c> credentials: <see cref="EasyAuthenticationOptions.UserPassword" /> split at its first colon
    /// into the user name and the password (empty when there is no colon), as the curl tool splits <c>-u</c>, with
    /// <see cref="EasyAuthenticationOptions.UserName" /> and <see cref="EasyAuthenticationOptions.Password" />
    /// (<c>CURLOPT_USERNAME</c>, <c>CURLOPT_PASSWORD</c>) each replacing its half; <see langword="null" /> with none
    /// of the three.
    /// </summary>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <returns>The credentials the context carries when no lookup chose others.</returns>
    private static NetworkCredential? CredentialsOf(EasyAuthenticationOptions authentication)
    {
        if ((authentication.UserPassword ?? authentication.UserName ?? authentication.Password) is null)
        {
            return null;
        }

        (string user, string password) = SplitAtFirstColon(authentication.UserPassword ?? string.Empty);
        return new NetworkCredential(FirstGiven(authentication.UserName, user), FirstGiven(authentication.Password, password));
    }

    private static string FirstGiven(string? given, string fallback) => given ?? fallback;

    private static (string User, string Password) SplitAtFirstColon(string userPassword)
    {
        int colon = userPassword.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (userPassword, string.Empty) : (userPassword[..colon], userPassword[(colon + 1)..]);
    }

    /// <summary>
    /// Gets an empty user name and password for an HTTP transfer with <c>--sasl-authzid</c>, <c>--ntlm</c> alone
    /// and no other credentials, as curl 8.22.0 then sends its NTLM Type-1 message with an empty user (the
    /// current Windows user under SSPI), as for <c>-u :</c>; a Basic or Digest transfer sends nothing until a
    /// user is given (measured 2026-10-17, AF-0236, BL-2573). <see langword="null" /> otherwise.
    /// </summary>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The credentials the context carries when neither a lookup nor <c>-u</c> gave any.</returns>
    private static NetworkCredential? AuthorizationIdentityNtlmCredentialsOf(EasyAuthenticationOptions authentication, CurlUrl url) =>
        SendsNtlmUnderAuthorizationIdentity(authentication) && Array.IndexOf(HttpAuthenticationSchemes, url.Scheme) >= 0
            ? new NetworkCredential(string.Empty, string.Empty)
            : null;

    /// <summary>Gets whether <c>--sasl-authzid</c> is given with <c>--ntlm</c> as the only HTTP authentication scheme.</summary>
    private static bool SendsNtlmUnderAuthorizationIdentity(EasyAuthenticationOptions authentication) =>
        authentication.SaslAuthorizationIdentity is not null
            && HttpRequestOptionsMapping.AuthSchemesOf(authentication) == HttpAuthSchemes.Ntlm;

    /// <summary>
    /// Gets <paramref name="output" /> counted by <paramref name="lowSpeedWatchdog" />, or as it is
    /// when there is no watchdog.
    /// </summary>
    /// <param name="output">The attempt's body output.</param>
    /// <param name="lowSpeedWatchdog">The attempt's watchdog, or <see langword="null" />.</param>
    /// <returns>The output the context carries.</returns>
    private static Stream WatchedOutput(Stream output, LowSpeedWatchdog? lowSpeedWatchdog) =>
        lowSpeedWatchdog is null ? output : lowSpeedWatchdog.WatchOutput(output);

    /// <summary>
    /// Gets <paramref name="http" /> storing <c>Alt-Svc</c> headers in <paramref name="altSvc" /> and with the
    /// route and HTTP version its <see cref="AltSvcTransferCache.ApplyTo" /> gives for <paramref name="url" />;
    /// as it is without a cache.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="http">The HTTP options the command line maps to.</param>
    /// <param name="altSvc">The transfer's <c>--alt-svc</c> cache, or <see langword="null" />.</param>
    /// <returns>The HTTP options the context carries.</returns>
    private static HttpRequestOptions HttpOptionsWithAltSvc(CurlUrl url, HttpRequestOptions http, AltSvcTransferCache? altSvc) =>
        altSvc is null ? http : altSvc.ApplyTo(url, http with { AltSvcStore = altSvc });

    /// <summary>
    /// Gets <paramref name="progress" />, or <see cref="NoTransferProgress.Instance" /> when it is
    /// <see langword="null" />, watched by each watchdog given, the <c>-m</c> one outermost.
    /// </summary>
    /// <param name="progress">The attempt's progress sink, or <see langword="null" /> for none.</param>
    /// <param name="lowSpeedWatchdog">The attempt's <c>-Y</c>/<c>-y</c> watchdog, or <see langword="null" />.</param>
    /// <param name="maxTimeWatchdog">The attempt's <c>-m</c> watchdog, or <see langword="null" />.</param>
    /// <returns>The sink the context carries.</returns>
    private static ITransferProgress WatchedProgress(
        ITransferProgress? progress,
        LowSpeedWatchdog? lowSpeedWatchdog,
        MaxTimeWatchdog? maxTimeWatchdog)
    {
        ITransferProgress given = progress ?? NoTransferProgress.Instance;
        ITransferProgress speedWatched = lowSpeedWatchdog is null ? given : lowSpeedWatchdog.WatchProgress(given);
        return maxTimeWatchdog is null ? speedWatched : maxTimeWatchdog.WatchProgress(speedWatched);
    }

    /// <summary>
    /// Gets what the transfer uploads: the <c>-T</c> <paramref name="upload" /> when given, standard
    /// input for a <c>telnet</c> URL without one, and nothing for any other.
    /// </summary>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="upload">The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>.</param>
    /// <returns>The context's <see cref="TransferContext.Upload" />.</returns>
    private Stream? UploadOf(CurlUrl url, Stream? upload) =>
        upload ?? (string.Equals(url.Scheme, TelnetScheme, StringComparison.Ordinal) ? standardInput : null);

    /// <summary>
    /// Gets the <c>-m</c> watchdog's start, which every limit of the attempt counts from, or
    /// <see langword="null" /> without one, when the handler takes its own start.
    /// </summary>
    /// <param name="maxTimeWatchdog">The attempt's <c>-m</c> watchdog, or <see langword="null" />.</param>
    /// <returns>The context's <see cref="TransferContext.OperationStarted" />.</returns>
    private static long? OperationStartedOf(MaxTimeWatchdog? maxTimeWatchdog) => maxTimeWatchdog?.OperationStarted;

    /// <summary>
    /// Gets the token that cancels the attempt: <see cref="CancellationToken.None" /> when neither
    /// <paramref name="abortToken" /> can be cancelled nor a watchdog is given, the one token that
    /// can cancel when there is only one, and one cancelled by any of them otherwise.
    /// </summary>
    /// <param name="abortToken">The token <c>--fail-early</c> aborts the transfer through under <c>-Z</c>.</param>
    /// <param name="lowSpeedWatchdog">The attempt's <c>-Y</c>/<c>-y</c> watchdog, or <see langword="null" />.</param>
    /// <param name="maxTimeWatchdog">The attempt's <c>-m</c> watchdog, or <see langword="null" />.</param>
    /// <returns>The token the context carries.</returns>
    private static CancellationToken TokenOf(CancellationToken abortToken, LowSpeedWatchdog? lowSpeedWatchdog, MaxTimeWatchdog? maxTimeWatchdog)
    {
        CancellationToken[] cancellable = [.. new[] { abortToken, lowSpeedWatchdog?.Token ?? default, maxTimeWatchdog?.Token ?? default }
            .Where(token => token.CanBeCanceled)];
        return cancellable.Length switch
        {
            0 => CancellationToken.None,
            1 => cancellable[0],
            _ => CancellationTokenSource.CreateLinkedTokenSource(cancellable).Token,
        };
    }

    /// <summary>
    /// Tells whether <c>-C -</c> resumes the <c>-T</c> <paramref name="upload" /> from an offset
    /// only the server knows, as curl 8.21.0's offset -1 does.
    /// </summary>
    /// <param name="transfer">The transfer's options, an upload's <see cref="EasyTransferOptions.ResumeFrom" /> -1 for <c>-C -</c>.</param>
    /// <param name="upload">The <c>-T</c> source given to <see cref="Create" />, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> for <c>-C -</c> with a <c>-T</c> source.</returns>
    private static bool ResumesUploadFromUnknownOffset(EasyTransferOptions transfer, Stream? upload) =>
        transfer is { Upload: true, ResumeFrom: -1 } && upload is not null;

    /// <summary>
    /// Gets the <c>--max-filesize</c> limit, or <see langword="null" /> for libcurl's 0, no limit.
    /// </summary>
    /// <param name="transfer">The transfer's options.</param>
    /// <returns>The context's <see cref="TransferContext.MaxFileSize" />.</returns>
    private static long? MaxFileSizeOf(EasyTransferOptions transfer) => transfer.MaxFileSize > 0 ? transfer.MaxFileSize : null;

    /// <summary>
    /// Gets the <c>-m</c> limit, or <see langword="null" /> for libcurl's zero, which never times out.
    /// </summary>
    /// <param name="transfer">The transfer's options.</param>
    /// <returns>The context's <see cref="TransferContext.MaxTime" />.</returns>
    private static TimeSpan? MaxTimeOf(EasyTransferOptions transfer) => transfer.Timeout > TimeSpan.Zero ? transfer.Timeout : null;

    /// <summary>
    /// Gets the <c>-z</c> condition the handlers read from <see cref="EasyTransferOptions.TimeCondition" /> and
    /// the whole seconds of <see cref="EasyTransferOptions.TimeValue" />, or <see langword="null" /> for none.
    /// </summary>
    /// <param name="transfer">The transfer's options.</param>
    /// <returns>The context's <see cref="TransferContext.TimeCondition" />.</returns>
    private static TimeCondition? TimeConditionOf(EasyTransferOptions transfer) => transfer.TimeCondition switch
    {
        EasyTimeCondition.None => null,
        EasyTimeCondition.IfUnmodifiedSince => TimeCondition.FromUnixSeconds(transfer.TimeValue, TimeConditionKind.IfUnmodifiedSince),
        _ => TimeCondition.FromUnixSeconds(transfer.TimeValue, TimeConditionKind.IfModifiedSince),
    };

    /// <summary>
    /// Gets the <c>CCC</c> behaviour the FTP handler reads for <c>--ftp-ssl-ccc</c> and
    /// <c>--ftp-ssl-ccc-mode</c> (BL-636).
    /// </summary>
    /// <param name="clearing">The option group's <see cref="EasyFtpOptions.ClearCommandChannel" />.</param>
    /// <returns>The same choice in the handler's terms.</returns>
    private static FtpCommandChannelClearing CommandChannelClearingOf(EasyFtpClearCommandChannel clearing) => clearing switch
    {
        EasyFtpClearCommandChannel.Active => FtpCommandChannelClearing.Active,
        EasyFtpClearCommandChannel.Passive => FtpCommandChannelClearing.Passive,
        _ => FtpCommandChannelClearing.Off,
    };

    /// <summary>
    /// Gets the <c>--tftp-blksize</c> the TFTP handler reads: <see langword="null" /> for libcurl's default of 512,
    /// which the handler asks for when none is given, else the size as given, which the handler clamps.
    /// </summary>
    /// <param name="tftp">The option group's TFTP options.</param>
    /// <returns>The context's <see cref="TransferContext.TftpBlockSize" />.</returns>
    private static int? BlockSizeOf(EasyTftpOptions tftp) =>
        tftp.BlockSize == DefaultTftpOptions.BlockSize ? null : tftp.BlockSize;

    /// <summary>
    /// Gets the <c>--connect-timeout</c> a handler reads: <see langword="null" /> when the record's
    /// <see cref="EasyNetworkOptions.ConnectTimeout" /> is zero, which means not set (ADR-0513).
    /// </summary>
    /// <param name="network">The option group's network options.</param>
    /// <returns>The context's <see cref="TransferContext.ConnectTimeout" />.</returns>
    private static TimeSpan? ConnectTimeoutOf(EasyNetworkOptions network) =>
        network.ConnectTimeout == TimeSpan.Zero ? null : network.ConnectTimeout;

    /// <summary>
    /// Gets the active-mode FTP accept wait a handler reads for <c>CURLOPT_ACCEPTTIMEOUT_MS</c>:
    /// curl's 60 seconds when the record's <see cref="EasyNetworkOptions.AcceptTimeout" /> is zero,
    /// which libcurl takes as not set (upstream case 593, BL-2622).
    /// </summary>
    /// <param name="network">The option group's network options.</param>
    /// <returns>The context's <see cref="TransferContext.FtpAcceptTimeout" />.</returns>
    private static TimeSpan FtpAcceptTimeoutOf(EasyNetworkOptions network) =>
        network.AcceptTimeout == TimeSpan.Zero ? TransferContext.DefaultFtpAcceptTimeout : network.AcceptTimeout;

    /// <summary>
    /// Gets the path walk the FTP handler reads for <c>--ftp-method</c>: libcurl's default and
    /// <c>multicwd</c> are both one <c>CWD</c> per directory.
    /// </summary>
    /// <param name="method">The option group's <see cref="EasyFtpOptions.FileMethod" />.</param>
    /// <returns>The same choice in the handler's terms.</returns>
    private static FtpFileMethod FileMethodOf(EasyFtpFileMethod method) => method switch
    {
        EasyFtpFileMethod.NoCwd => FtpFileMethod.NoCwd,
        EasyFtpFileMethod.SingleCwd => FtpFileMethod.SingleCwd,
        _ => FtpFileMethod.MultiCwd,
    };

    /// <summary>
    /// Gets the <c>-Q</c> commands as the FTP and SFTP handlers read them, one list whose <c>+</c> prefix marks a
    /// command sent just before the transfer and <c>-</c> one sent after it: <see cref="EasyFtpOptions.Quote" />
    /// as they are, then <see cref="EasyFtpOptions.PreQuote" /> each behind <c>+</c> and
    /// <see cref="EasyFtpOptions.PostQuote" /> each behind <c>-</c>. Each handler sorts the list by prefix, so
    /// each stage keeps its command-line order.
    /// </summary>
    /// <param name="ftp">The option group's FTP options.</param>
    /// <returns>The context's <see cref="TransferContext.QuoteCommands" />.</returns>
    private static string[] QuoteCommandsOf(EasyFtpOptions ftp) =>
        [.. ftp.Quote, .. ftp.PreQuote.Select(command => "+" + command), .. ftp.PostQuote.Select(command => "-" + command)];

    /// <summary>
    /// Gets <paramref name="events" />, or <see cref="NoTransferEvents.Instance" /> when it is <see langword="null" />.
    /// </summary>
    /// <param name="events">The sink given to <see cref="Create" />.</param>
    /// <returns>The sink the context carries.</returns>
    private static ITransferEvents EventsOrNone(ITransferEvents? events) => events ?? NoTransferEvents.Instance;

    /// <summary>Tells whether the transfer tunnels through its proxy: <c>-p</c>, or a <c>--connect-to</c> mapping that <see cref="ConnectToTunnelsThroughProxy" /> says does.</summary>
    /// <param name="proxyOptions">The option group's proxy options, whose <see cref="EasyProxyOptions.HttpProxyTunnel" /> is <c>-p</c>.</param>
    /// <param name="network">The option group's network options.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <returns>The context's <see cref="HttpRequestOptions.ProxyTunnel" />.</returns>
    private static bool TunnelsThroughProxy(EasyProxyOptions proxyOptions, EasyNetworkOptions network, CurlUrl url, ProxyEndpoint? proxy) =>
        proxyOptions.HttpProxyTunnel || ConnectToTunnelsThroughProxy(network, url, proxy);

    /// <summary>
    /// Tells whether a <c>--connect-to</c> mapping sends <paramref name="url" /> to another host or
    /// port through an HTTP or HTTPS proxy, which makes curl 8.21.0 tunnel through the proxy as
    /// <c>-p</c> would (<c>lib/url.c</c>'s <c>parse_connect_to_slist</c>; upstream tests 2050 and
    /// 2055). A mapping that names the URL's own host and port, or does not parse, does not.
    /// </summary>
    /// <param name="network">The option group's network options, whose <see cref="EasyNetworkOptions.ConnectTo" /> is <c>--connect-to</c>.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <returns><see langword="true" /> when the transfer tunnels because of <c>--connect-to</c>.</returns>
    internal static bool ConnectToTunnelsThroughProxy(EasyNetworkOptions network, CurlUrl url, ProxyEndpoint? proxy)
    {
        if (!IsHttpOrHttpsProxy(proxy) || network.ConnectTo.Count == 0)
        {
            return false;
        }

        Networking.ConnectDestination destination = new Networking.ConnectToMappings(network.ConnectTo).Map(url.Host, url.Port);
        return destination is { IsMapped: true, ParseError: null } && IsAnotherHostOrPort(destination, url);
    }

    /// <summary>Whether <paramref name="proxy" /> is an HTTP, HTTP/1.0 or HTTPS proxy, which CONNECT can tunnel through.</summary>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <returns><see langword="true" /> for a proxy of one of those kinds.</returns>
    private static bool IsHttpOrHttpsProxy(ProxyEndpoint? proxy) =>
        proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https };

    /// <summary>Whether <paramref name="destination" /> names a different port or host (ignoring case) than <paramref name="url" />.</summary>
    /// <param name="destination">Where a <c>--connect-to</c> mapping sends the URL.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <returns><see langword="true" /> when the mapping moves the connection off the URL's own host and port.</returns>
    private static bool IsAnotherHostOrPort(Networking.ConnectDestination destination, CurlUrl url) =>
        destination.Port != url.Port || !string.Equals(destination.Host, url.Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets <paramref name="events" /> writing each CONNECT reply head a tunnelling proxy sends to
    /// <paramref name="headerOutput" /> as well, as curl 8.21.0 writes it to <c>-i</c>, <c>-I</c> and
    /// <c>-D</c>; or <paramref name="events" /> as it is when there is no HTTP or HTTPS proxy to
    /// send CONNECT to, no header output, or <c>--suppress-connect-headers</c> leaves the heads
    /// out (measured 2026-09-30, BL-613 Notes).
    /// </summary>
    /// <param name="proxyOptions">The option group's proxy options, whose <see cref="EasyProxyOptions.SuppressConnectHeaders" /> is <c>--suppress-connect-headers</c>.</param>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <param name="events">The transfer's events.</param>
    /// <param name="headerOutput">The transfer's header output, or <see langword="null" /> for none.</param>
    /// <returns>The events the context carries.</returns>
    private static ITransferEvents EventsWritingConnectReplyHeads(EasyProxyOptions proxyOptions, ProxyEndpoint? proxy, ITransferEvents events, Stream? headerOutput) =>
        IsHttpOrHttpsProxy(proxy) && headerOutput is not null && !proxyOptions.SuppressConnectHeaders
            ? new ConnectReplyHeadWritingEvents(events, headerOutput)
            : events;

    /// <summary>
    /// Chooses where a transfer's header lines go. <c>-i</c> and <c>-I</c> send them to the
    /// body output as well as to any <c>-D</c> output, as curl 8.21.0 does, except on a
    /// <c>ws</c> or <c>wss</c> transfer, where curl writes the upgrade reply head only to
    /// <c>-D</c> (ADR-0128 row 5; <c>-I</c> measured the same, BL-583).
    /// </summary>
    /// <param name="behaviour">The option group's behaviour options, whose <see cref="EasyBehaviourOptions.IncludeHeader" /> is <c>-i</c>.</param>
    /// <param name="transfer">The transfer's options, whose <see cref="EasyTransferOptions.NoBody" /> is <c>-I</c>.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="output">Where the transfer's body goes.</param>
    /// <param name="dumpHeaderOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
    /// <param name="bodyHeaderStyles">
    /// How the lines written to <paramref name="output" /> are styled, or <see langword="null" /> for not at all;
    /// the <c>-D</c> lines are never styled, as curl 8.21.0 styles only <c>-i</c>'s (BL-736 Notes).
    /// </param>
    /// <returns>
    /// <paramref name="dumpHeaderOutput" /> without <c>-i</c> or <c>-I</c>, or for a WebSocket URL;
    /// otherwise <paramref name="output" /> (as a <see cref="StyledHeaderStream" /> when
    /// <paramref name="bodyHeaderStyles" /> is given) when there is no <c>-D</c>, or a
    /// <see cref="HeaderLineTeeStream" /> writing each line to both.
    /// </returns>
    private static Stream? HeaderOutputOf(EasyBehaviourOptions behaviour, EasyTransferOptions transfer, CurlUrl url, Stream output, Stream? dumpHeaderOutput, StyledHeaderLines? bodyHeaderStyles)
    {
        if ((!behaviour.IncludeHeader && !transfer.NoBody) || IsWebSocket(url))
        {
            return dumpHeaderOutput;
        }

        Stream headerLinesOutput = bodyHeaderStyles is null ? output : new StyledHeaderStream(output, bodyHeaderStyles);
        return dumpHeaderOutput is null ? headerLinesOutput : new HeaderLineTeeStream(dumpHeaderOutput, headerLinesOutput);
    }

    /// <summary>Whether <paramref name="url" /> is a <c>ws</c> or <c>wss</c> URL.</summary>
    /// <param name="url">The URL to transfer.</param>
    /// <returns><see langword="true" /> for either WebSocket scheme.</returns>
    private static bool IsWebSocket(CurlUrl url) => url.Scheme is "ws" or "wss";
}
