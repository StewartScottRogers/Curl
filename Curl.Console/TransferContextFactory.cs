using System.Net;
using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

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

    /// <summary>Every context's clock: the one given, or <see cref="TimeProvider.System" />.</summary>
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Gets or sets every context's <see cref="TransferContext.DiagnosticLog" />: the run's diagnostic log, which the
    /// runner sets once it has opened it, and <see cref="NoDiagnosticLog.Instance" /> until then (ADR-0222).
    /// </summary>
    internal IDiagnosticLog DiagnosticLog { get; set; } = NoDiagnosticLog.Instance;

    /// <summary>
    /// Creates the context for one transfer, carrying every option a handler reads.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
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
    /// <see cref="RemoteHeaderNameStream" /> must read each header line before it goes anywhere;
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
    /// <paramref name="watchHeaderOutput" /> when given.
    /// </returns>
    internal TransferContext Create(
        CommandLineOptions options,
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
            ? HeaderOutputOf(options, url, output, headerOutput, bodyHeaderStyles)
            : watchHeaderOutput(HeaderOutputOf(options, url, output, headerOutput, bodyHeaderStyles));
        return new()
        {
            Url = url,
            Output = WatchedOutput(output, lowSpeedWatchdog),
            HeaderOutput = transferHeaderOutput,
            NoBody = options.NoBody,
            Range = range,
            RangeText = options.Range,
            ResumeFrom = resumeFrom,
            ResumeUploadFromUnknownOffset = ResumesUploadFromUnknownOffset(options, upload),
            MaxFileSize = options.MaxFileSize,
            Upload = UploadOf(url, upload),
            PostData = options.PostData,
            Credentials = lookedUpCredentials ?? options.Credentials,
            TelnetOptions = options.TelnetOptions,
            TftpBlockSize = options.TftpBlockSize,
            TftpNoOptions = options.TftpNoOptions,
            FtpDisableEpsv = options.FtpDisableEpsv,
            FtpSkipPasvIp = options.FtpSkipPasvIp,
            FtpFileMethod = options.FtpFileMethod,
            FtpCreateDirectories = options.FtpCreateDirectories,
            FtpAccount = options.FtpAccount,
            FtpAlternativeToUser = options.FtpAlternativeToUser,
            FtpSendPret = options.FtpSendPret,
            FtpPort = options.FtpPort,
            FtpUseEprt = options.FtpUseEprt,
            SslLevel = options.SslLevel,
            FtpSslControlOnly = options.FtpSslControlOnly,
            ListOnly = options.ListOnly,
            UseAscii = options.UseAscii,
            Append = options.Append,
            ConvertLineEndings = options.ConvertLineEndings,
            QuoteCommands = options.QuoteCommands,
            CreateFileMode = options.CreateFileMode ?? TransferContext.DefaultCreateFileMode,
            PathAsIs = options.PathAsIs,
            ConnectTimeout = options.ConnectTimeout,
            MaxTime = options.MaxTime,
            OperationStarted = OperationStartedOf(maxTimeWatchdog),
            TimeCondition = options.TimeCondition,
            Proxy = proxy,
            Http = HttpOptionsWithAltSvc(
                url,
                HttpRequestOptionsMapping.FromCommandLine(options, formBody, proxy, commandLineTextEncoding, ifNoneMatchHeaders),
                altSvc),
            Mail = MailRequestOptionsMapping.FromCommandLine(options, url.Scheme),
            Ssh = ssh,
            Progress = WatchedProgress(progress, lowSpeedWatchdog, maxTimeWatchdog),
            Events = EventsWritingConnectReplyHeads(options, proxy, EventsOrNone(events), transferHeaderOutput),
            TimeProvider = clock,
            DiagnosticLog = DiagnosticLog,
            CancellationToken = TokenOf(abortToken, lowSpeedWatchdog, maxTimeWatchdog),
        };
    }

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
    /// <param name="options">The parsed command line.</param>
    /// <param name="upload">The <c>-T</c> source given to <see cref="Create" />, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> for <c>-C -</c> with a <c>-T</c> source.</returns>
    private static bool ResumesUploadFromUnknownOffset(CommandLineOptions options, Stream? upload) =>
        options.ResumeFromOutputSize && upload is not null;

    /// <summary>
    /// Gets <paramref name="events" />, or <see cref="NoTransferEvents.Instance" /> when it is <see langword="null" />.
    /// </summary>
    /// <param name="events">The sink given to <see cref="Create" />.</param>
    /// <returns>The sink the context carries.</returns>
    private static ITransferEvents EventsOrNone(ITransferEvents? events) => events ?? NoTransferEvents.Instance;

    /// <summary>
    /// Gets <paramref name="events" /> writing each CONNECT reply head a tunnelling proxy sends to
    /// <paramref name="headerOutput" /> as well, as curl 8.21.0 writes it to <c>-i</c>, <c>-I</c> and
    /// <c>-D</c>; or <paramref name="events" /> as it is when there is no HTTP or HTTPS proxy to
    /// send CONNECT to, no header output, or <c>--suppress-connect-headers</c> leaves the heads
    /// out (measured 2026-09-30, BL-613 Notes).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <param name="events">The transfer's events.</param>
    /// <param name="headerOutput">The transfer's header output, or <see langword="null" /> for none.</param>
    /// <returns>The events the context carries.</returns>
    private static ITransferEvents EventsWritingConnectReplyHeads(CommandLineOptions options, ProxyEndpoint? proxy, ITransferEvents events, Stream? headerOutput) =>
        proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https } && headerOutput is not null && !options.SuppressConnectHeaders
            ? new ConnectReplyHeadWritingEvents(events, headerOutput)
            : events;

    /// <summary>
    /// Chooses where a transfer's header lines go. <c>-i</c> and <c>-I</c> send them to the
    /// body output as well as to any <c>-D</c> output, as curl 8.21.0 does, except on a
    /// <c>ws</c> or <c>wss</c> transfer, where curl writes the upgrade reply head only to
    /// <c>-D</c> (ADR-0128 row 5; <c>-I</c> measured the same, BL-583).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
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
    private static Stream? HeaderOutputOf(CommandLineOptions options, CurlUrl url, Stream output, Stream? dumpHeaderOutput, StyledHeaderLines? bodyHeaderStyles)
    {
        if ((!options.ShowHeaders && !options.NoBody) || IsWebSocket(url))
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
