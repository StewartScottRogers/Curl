using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// One <c>ftp://</c> or <c>ftps://</c> download or upload over an open control connection:
/// the conversation curl 8.21.0 holds, from the greeting to <c>QUIT</c>.
/// </summary>
/// <param name="connections">Opens, accepts and secures the data connection, and secures the control connection.</param>
/// <param name="control">The control connection, already open.</param>
/// <param name="controlName">How <c>-v</c> names the control connection when the transfer ends.</param>
/// <param name="context">The transfer being performed.</param>
/// <param name="implicitTls">
/// <see langword="true" /> for <c>ftps://</c>, whose control connection is TLS from its first
/// byte, so no <c>AUTH</c> is sent.
/// </param>
/// <param name="connectPhase">
/// Holds the greeting, the login and <c>PWD</c> to <c>--connect-timeout</c>, as curl holds its
/// states before <c>DO</c> (BL-512); the caller owns it.
/// </param>
/// <param name="trace">Writes the <c>--trace-config ftp</c> lines as the session steps (BL-1162).</param>
/// <remarks>
/// <para>
/// TLS (ADR-0102's BL-437 addendum): under <c>--ssl</c>, <c>--ftp-ssl-control</c> or
/// <c>--ssl-reqd</c> an <c>ftp://</c> greeting is followed by <c>AUTH SSL</c>, then
/// <c>AUTH TLS</c> when that is refused; a <c>234</c> or <c>334</c> upgrades the control
/// connection. Both refused is exit 64 with no <c>QUIT</c> under <c>--ftp-ssl-control</c> and
/// <c>--ssl-reqd</c>, and plaintext under <c>--ssl</c>. Once logged in over TLS,
/// <c>PBSZ 0</c> (its reply ignored) and <c>PROT P</c> (<c>PROT C</c> under
/// <c>--ftp-ssl-control</c>) are sent before <c>PWD</c>; an accepted <c>PROT P</c> upgrades
/// every data connection once the transfer command is answered, and a refused one is exit 64
/// with no <c>QUIT</c> under <c>--ssl-reqd</c>, plaintext data otherwise. Under
/// <c>--ftp-ssl-ccc</c>, <c>CCC</c> follows <c>PROT</c> and clears TLS from the control
/// connection, or is exit 81 where the TLS build cannot (BL-636, ADR-0280).
/// </para>
/// <para>
/// Active mode (<c>-P</c>) takes the place of <c>EPSV</c>: a port is bound on the
/// <c>-P</c> address (exit 30 after <c>QUIT</c> when none can be), announced with
/// <c>EPRT</c>, and when that is refused, or under <c>--disable-eprt</c>, bound afresh and
/// announced with <c>PORT</c>; <c>PORT</c> refused is exit 30 after <c>QUIT</c>. After the
/// transfer command is answered the server's connection is accepted, exit 12 after
/// <c>QUIT</c> when none arrives within 60 seconds.
/// </para>
/// <para>
/// The conversation is <c>USER</c>, <c>PASS</c> (skipped when <c>USER</c> is answered
/// with a 2xx; <c>ACCT</c> after a <c>332</c> under <c>--ftp-account</c>, and the
/// <c>--ftp-alternative-to-user</c> command once after a refusal, BL-635), <c>PWD</c>, one <c>CWD</c> per directory in the path, <c>EPSV</c> (and
/// <c>PASV</c> when <c>EPSV</c> is refused), <c>TYPE I</c>, <c>SIZE</c>, <c>RETR</c> and
/// <c>QUIT</c>. A path ending in <c>/</c> sends <c>TYPE A</c> and <c>LIST</c> instead of
/// <c>TYPE I</c>, <c>SIZE</c> and <c>RETR</c>, and copies the listing.
/// </para>
/// <para>
/// The directory the <c>257</c> reply to <c>PWD</c> quotes is reported as
/// <see cref="TransferReport.FtpEntryPath" />; a quoted name that never ends is exit 8
/// with no <c>QUIT</c>, as curl 8.21.0 does (<see cref="FtpEntryPath" />, BL-514). A
/// directory that does not start with <c>/</c> sends <c>SYST</c>, and a <c>215</c> naming
/// <c>OS/400</c> sends <c>SITE NAMEFMT 1</c> and, when that is accepted, <c>PWD</c> again
/// (<see cref="FtpServerSystem" />, BL-782).
/// </para>
/// <para>
/// A file download honours <c>-r</c> and <c>-C</c> through <see cref="FtpDownloadWindow" />:
/// a non-zero offset sends <c>REST</c> before <c>RETR</c> (exit 31, with no <c>QUIT</c>,
/// when it is refused; exit 36 when it lies past the <c>SIZE</c> count), and a range with
/// a byte limit stops reading at the limit and sends <c>ABOR</c> before <c>QUIT</c>. With
/// <c>-I</c> no data connection is opened: a file sends <c>MDTM</c>, <c>TYPE I</c>,
/// <c>SIZE</c> and <c>REST 0</c> and writes curl's <c>Last-Modified</c>,
/// <c>Content-Length</c> and <c>Accept-ranges</c> lines to the header output; a directory
/// sends nothing more. ADR-0323's BL-438 addendum records the measurements.
/// </para>
/// <para>
/// <c>--max-filesize</c> (BL-638): a <c>SIZE</c> count over the limit, the whole file's
/// whatever <c>-C</c> or <c>-r</c> asks for, is exit 63 before <c>REST</c> or <c>RETR</c>,
/// with <c>ABOR</c> for a range and then <c>QUIT</c>. When the size is not known, a file or
/// listing that delivers the limit with more arriving is exit 63 with no <c>QUIT</c>, the
/// limit counted from this transfer's first byte.
/// </para>
/// <para>
/// With <see cref="ITransferContext.Upload" /> set the conversation after <c>PWD</c> is
/// the <c>CWD</c>s, <c>EPSV</c> (or <c>PASV</c>), <c>TYPE I</c>, <c>STOR</c>, the upload
/// written to the data connection and closed, and <c>QUIT</c>. <c>-C</c> sends
/// <c>APPE</c> instead of <c>STOR</c> through <see cref="FtpUploadOffset" />, after
/// <c>SIZE</c> for <c>-C -</c>. A URL with no file name is exit 3 before the first
/// <c>CWD</c>, a refused <c>STOR</c> or <c>APPE</c> exit 25, and an end-of-transfer reply
/// other than <c>226</c> or <c>250</c> exit 18. ADR-0323's BL-439 addendum records the
/// measurements.
/// </para>
/// <para>
/// The FTP control options change that conversation as ADR-0323's BL-436 addendum records:
/// <c>--disable-epsv</c> goes straight to <c>PASV</c>, except over IPv6, where curl ignores
/// it (BL-903); <c>--no-ftp-skip-pasv-ip</c>
/// connects to the address a <c>227</c> reply names; <c>--ftp-method</c> picks the
/// <c>CWD</c>s through <see cref="FtpUrlPath" />; <c>--ftp-create-dirs</c> answers a refused
/// <c>CWD</c> with <c>MKD</c> and one more <c>CWD</c>; <c>-l</c> lists with <c>NLST</c>,
/// even for a file URL; and <c>-Q</c> commands are sent after <c>PWD</c>, after
/// <c>TYPE</c> (<c>+</c>) or after a successful transfer (<c>-</c>), as
/// <see cref="FtpQuoteCommands" /> sorts them. A refused quote is exit 21: before the
/// transfer with no <c>QUIT</c>, after it with <c>QUIT</c>.
/// </para>
/// <para>
/// ASCII mode and appending (BL-633): <c>-B</c> or a <c>;type=a</c> URL suffix
/// (<see cref="FtpTypeCode" />) sends <c>TYPE A</c> instead of <c>TYPE I</c>, for a
/// download, an upload and <c>-I</c> alike; an ASCII download sends no <c>SIZE</c> or
/// <c>REST</c>. <c>;type=d</c> lists with <c>NLST</c> as <c>-l</c> does. The data bytes
/// pass unchanged either way. <c>-a</c> uploads with <c>APPE</c> instead of <c>STOR</c>, and
/// <c>--crlf</c> converts each line feed of an upload not already after a carriage return
/// into a carriage-return line-feed pair.
/// </para>
/// <para>
/// <c>-v</c> and <c>--trace</c> (BL-931) see curl 8.21.0's info lines about the data
/// connection, worded in <see cref="FtpTransferMessages" />: how the data stream is connected,
/// where a passive one is dialled, the range and size of a download, an accepted active one,
/// the upload's byte count, the directory remembered and whether the control connection is
/// kept; and every byte read from or written to the data connection, with the zero-byte read
/// that ends a download.
/// </para>
/// <para>
/// Each failure ends the session with curl's exit code and message, and sends
/// <c>QUIT</c> first exactly where curl was measured to: not after a refused login, a
/// bad greeting, a path with a control character, an unreadable <c>227</c> reply, or a
/// short transfer. A lost control connection is exit 56, a failed send exit 55, and a
/// reply line of 65536 bytes or more exit 100. The user name and password are sent as
/// given, CR and LF included, as curl sends them.
/// </para>
/// </remarks>
internal sealed class FtpSession(
    FtpSessionConnections connections,
    FtpControlChannel control,
    FtpControlConnectionName controlName,
    ITransferContext context,
    bool implicitTls,
    FtpConnectPhaseLimit connectPhase,
    FtpStateTrace trace)
    : IAsyncDisposable
{
    private const string AnonymousUser = "anonymous";

    private const string AnonymousPassword = "ftp@example.com";

    private const int ReadBufferSize = 16384;

    /// <summary>The most a data read asks for: curl 8.21.0's receive buffer (measured, BL-1259 Notes).</summary>
    private const int DataReadBufferSize = 102400;

    /// <summary>
    /// How long curl 8.21.0 waits for the server to open an active-mode data connection,
    /// measured with <c>-P -</c> whatever <c>--connect-timeout</c> says.
    /// </summary>
    private static readonly TimeSpan AcceptTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The <c>AUTH</c> mechanisms curl 8.21.0 offers, in the order it offers them.</summary>
    private static readonly string[] AuthMechanisms = ["SSL", "TLS"];

    private readonly FtpQuoteCommands quotes = FtpQuoteCommands.Parse(context.QuoteCommands);

    /// <summary>Where each session step is written to Curl's own diagnostic log (BL-924).</summary>
    private readonly FtpDiagnosticLog log = new(context.DiagnosticLog);

    private readonly FtpTlsRequirement tlsRequirement = FtpTlsRequirements.Of(context);

    /// <summary>The URL path without its <c>;type=</c> suffix, and whether it asks for ASCII or a name-only listing.</summary>
    private readonly FtpTypeCode typeCode = FtpTypeCode.Of(context);

    /// <summary>The control connection's own address, which <c>-P -</c> listens on.</summary>
    private readonly EndPoint? controlLocalEndPoint = control.Connection.LocalEndPoint;

    /// <summary>
    /// The control connection's peer address, which <c>-v</c> names as where a passive data
    /// connection is dialled; the URL's host when it is unknown.
    /// </summary>
    private readonly string controlPeerAddress =
        Unmapped((control.Connection.RemoteEndPoint as IPEndPoint)?.Address)?.ToString() ?? context.Url.IdnHost;

    /// <summary>
    /// Whether the control connection's peer is an IPv6 address (not an IPv4-mapped one),
    /// which makes curl 8.21.0 send <c>EPSV</c> despite <c>--disable-epsv</c> and give up
    /// rather than fall back to <c>PASV</c> when it is refused (BL-903).
    /// </summary>
    private readonly bool controlPeerIsIPv6 = IsIPv6(control.Connection.RemoteEndPoint);

    /// <summary>
    /// The URL path's directories, each followed by <c>/</c>, which curl 8.21.0's <c>-v</c>
    /// says it remembers once the data has moved (BL-931).
    /// </summary>
    private string rememberedDirectory = string.Empty;

    /// <summary>Whether the control connection is TLS: from the start for <c>ftps://</c>, or after <c>AUTH</c>.</summary>
    private bool controlSecured = implicitTls;

    /// <summary>The control connection <c>AUTH</c> upgraded to TLS, which the session owns.</summary>
    private IConnection? securedControl;

    /// <summary>Whether an accepted <c>PROT P</c> makes every data connection TLS.</summary>
    private bool protectData;

    /// <summary>Whether the <c>--ftp-alternative-to-user</c> command has been sent, which curl does once.</summary>
    private bool alternativeUserSent;

    /// <summary>The port an active-mode data connection is accepted on, once bound.</summary>
    private IPendingConnection? pendingConnection;

    /// <summary>
    /// The address <c>EPRT</c> and <c>PORT</c> announce: the <c>-P</c> address, even when the
    /// port was bound on the control connection's address instead (BL-464).
    /// </summary>
    private IPAddress? announcedAddress;

    private IConnection? dataConnection;

    private FtpDownloadWindow window;

    private long? fileSize;

    private long? expectedSize;

    private long bytesTransferred;

    /// <summary>The bytes an upload of a known size has to send, or <see langword="null" /> for a download or an upload of unknown size.</summary>
    private long? uploadSize;

    /// <summary>The <c>--max-filesize</c> limit, or <see langword="null" /> when there is none; 0 is none, as in curl.</summary>
    private readonly long? maxFileSize = context.MaxFileSize > 0 ? context.MaxFileSize : null;

    /// <summary>
    /// The code of the last reply read before <c>QUIT</c>, <c>ABOR</c>'s included; 0 before
    /// the greeting.
    /// </summary>
    private int lastReplyCode;

    /// <summary>
    /// The directory the <c>257</c> reply to <c>PWD</c> named, reported as
    /// <see cref="TransferReport.FtpEntryPath" />; <see langword="null" /> before <c>PWD</c>
    /// or when the reply named none.
    /// </summary>
    private string? entryPath;

    /// <summary>
    /// The time the reply to <c>MDTM</c> named, reported as
    /// <see cref="TransferResult.SourceLastWriteTimeUtc" /> under <c>-R</c>;
    /// <see langword="null" /> before <c>MDTM</c> or when the reply named none.
    /// </summary>
    private DateTimeOffset? modifiedUtc;

    /// <summary>
    /// Holds the whole conversation. The data connection it opens stays open until the
    /// session is disposed.
    /// </summary>
    /// <returns>
    /// The transfer's outcome, its <see cref="TransferReport.ResponseCode" /> the code of
    /// the last reply read before <c>QUIT</c>, as curl 8.21.0 reports
    /// <c>%{response_code}</c> for FTP (BL-392), and under <c>-R</c> a success's
    /// <see cref="TransferResult.SourceLastWriteTimeUtc" /> the time <c>MDTM</c> named.
    /// </returns>
    public async ValueTask<TransferResult> RunAsync()
    {
        long started = context.TimeProvider.GetTimestamp();
        TransferResult result;
        try
        {
            result = await ConnectAsync().ConfigureAwait(false)
                ?? await TransferPathAsync().ConfigureAwait(false);
        }
        catch (FtpControlConversationFailedException lost)
        {
            result = lost.Result;
        }

        trace.Ended(result.ExitCode);
        log.SessionEnded(result, context.TimeProvider.GetElapsedTime(started));
        return result with
        {
            Report = new TransferReport { ResponseCode = lastReplyCode, FtpEntryPath = entryPath },
            SourceLastWriteTimeUtc = result.IsSuccess && context.RemoteTime ? modifiedUtc : null,
        };
    }

    /// <summary>
    /// Disposes the data connection, the active-mode listening port and the control
    /// connection <c>AUTH</c> secured, whichever exist. The control connection the session
    /// was given belongs to the caller.
    /// </summary>
    /// <returns>A task that completes when they are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeIfOpenAsync(dataConnection).ConfigureAwait(false);
        await DisposeIfOpenAsync(pendingConnection).ConfigureAwait(false);
        await DisposeIfOpenAsync(securedControl).ConfigureAwait(false);
    }

    private static ValueTask DisposeIfOpenAsync(IAsyncDisposable? disposable) =>
        disposable?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>
    /// Holds curl's connect phase - the greeting, the login, <c>PBSZ</c>, <c>PROT</c> and
    /// <c>PWD</c> - under the connect phase's limit: exit 28 once <c>--connect-timeout</c> has
    /// passed, and the transfer's own token for everything after it.
    /// </summary>
    private async ValueTask<TransferResult?> ConnectAsync()
    {
        try
        {
            TransferResult? failed = await GreetAndLogInAsync().ConfigureAwait(false)
                ?? await ProtectDataAsync().ConfigureAwait(false)
                ?? await ClearControlTlsAsync().ConfigureAwait(false)
                ?? await ReadEntryPathAsync().ConfigureAwait(false);
            if (failed is null)
            {
                trace.ConnectPhaseDone();
            }

            return failed;
        }
        catch (OperationCanceledException) when (connectPhase.HasPassed)
        {
            return connectPhase.Failure();
        }
        finally
        {
            control.CancellationToken = context.CancellationToken;
        }
    }

    private ValueTask<TransferResult?> ReadEntryPathAsync() => ReadEntryPathAsync(askSystemForRelativePath: true);

    /// <summary>
    /// Sends <c>PWD</c> and reads the entry path; a relative one sends <c>SYST</c> when
    /// <paramref name="askSystemForRelativePath" /> is set, as curl 8.21.0 does while it
    /// knows no server system yet (BL-782). The <c>-v</c> line for the reply follows it, or
    /// follows <c>SYST</c> once sent, as curl's does (BL-945).
    /// </summary>
    private async ValueTask<TransferResult?> ReadEntryPathAsync(bool askSystemForRelativePath)
    {
        FtpReply pwd = await ExchangeAsync("PWD").ConfigureAwait(false);
        if (!FtpEntryPath.TryRead(pwd, out entryPath))
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, FtpTransferMessages.WeirdServerReply);
        }

        string? entryPathLine = FtpTransferMessages.EntryPathReply(pwd.Code, entryPath);
        if (askSystemForRelativePath && entryPath?.StartsWith('/') == false)
        {
            return await AskServerSystemAsync(entryPathLine).ConfigureAwait(false);
        }

        ReportInfo(entryPathLine);
        return null;
    }

    /// <summary>
    /// Sends <c>SYST</c>, whose refusal curl carries on past, reporting
    /// <paramref name="entryPathLine" /> once it is sent. A <c>215</c> naming
    /// <c>OS/400</c> sends <c>SITE NAMEFMT 1</c>, and a 2xx to that sends <c>PWD</c> again
    /// for the entry path in the new name format, with no second <c>SYST</c>.
    /// </summary>
    private async ValueTask<TransferResult?> AskServerSystemAsync(string? entryPathLine)
    {
        FtpReply system = await ExchangeAsync("SYST", entryPathLine).ConfigureAwait(false);
        return FtpServerSystem.IsOs400(system)
            && (await ExchangeAsync("SITE NAMEFMT 1").ConfigureAwait(false)).IsCompletion
            ? await ReadEntryPathAsync(askSystemForRelativePath: false).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Reads the greeting and logs in: a <c>230</c> greeting means already logged in, a
    /// <c>220</c> starts the login, and anything else is exit 8.
    /// </summary>
    private async ValueTask<TransferResult?> GreetAndLogInAsync()
    {
        trace.AwaitingGreeting();
        FtpReply greeting = await ReadReplyAsync().ConfigureAwait(false);
        return greeting.Code switch
        {
            230 => LoggedIn(),
            220 => await SecureControlAsync().ConfigureAwait(false) ?? await LogInAsync().ConfigureAwait(false),
            _ => TransferResult.Failure(CurlExitCode.WeirdServerReply, FtpTransferMessages.UnexpectedGreeting(greeting.Code)),
        };
    }

    /// <summary>
    /// Asks for TLS on an <c>ftp://</c> control connection when <c>--ssl</c>,
    /// <c>--ftp-ssl-control</c> or <c>--ssl-reqd</c> was given: <c>AUTH SSL</c>, then
    /// <c>AUTH TLS</c>, and the upgrade after the first one accepted.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> to go on logging in; exit 64 with no <c>QUIT</c> when both are
    /// refused and TLS is required; or the handshake's failure.
    /// </returns>
    private async ValueTask<TransferResult?> SecureControlAsync()
    {
        if (controlSecured || tlsRequirement == FtpTlsRequirement.None)
        {
            return null;
        }

        foreach (string mechanism in AuthMechanisms)
        {
            if (await ExchangeAsync("AUTH " + mechanism).ConfigureAwait(false) is { Code: 234 or 334 })
            {
                return await UpgradeControlAsync().ConfigureAwait(false);
            }
        }

        if (tlsRequirement != FtpTlsRequirement.Try)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FtpTransferMessages.RequestedSslLevelFailed);
        }

        log.ControlLeftPlaintext();
        return null;
    }

    /// <summary>
    /// Runs the TLS handshake over the control connection, reporting it to the transfer's
    /// events, and carries on over the secured connection; a failed handshake ends the session
    /// with its exit code and no <c>QUIT</c>. A completed one reports the connect's
    /// <c>Established connection</c> line again, as curl 8.21.0 writes it a second time
    /// between <c>234</c> and <c>USER</c> (measured, BL-1084).
    /// </summary>
    private async ValueTask<TransferResult?> UpgradeControlAsync()
    {
        ConnectResult secured = await connections.TlsProvider
            .AuthenticateAsClientAsync(control.Connection, context.Url.IdnHost, context.Events, control.CancellationToken)
            .ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            return TransferResult.Failure(secured.ExitCode, secured.ErrorMessage!);
        }

        if (connections.ControlOpened is { } opened)
        {
            context.Events.ReportConnectionOpened(opened);
        }

        securedControl = connection;
        control.SwitchTo(connection);
        controlSecured = true;
        log.ControlSecured();
        return null;
    }

    /// <summary>
    /// Once logged in over TLS, sends <c>PBSZ 0</c>, whose reply curl does not check, and
    /// <c>PROT P</c>, or <c>PROT C</c> under <c>--ftp-ssl-control</c>. An accepted
    /// <c>PROT P</c> secures the data connections; a refused one is exit 64 with no
    /// <c>QUIT</c> under <c>--ssl-reqd</c> and plaintext data otherwise.
    /// </summary>
    private async ValueTask<TransferResult?> ProtectDataAsync()
    {
        if (!controlSecured)
        {
            return null;
        }

        await ExchangeAsync("PBSZ 0").ConfigureAwait(false);
        bool privateData = tlsRequirement != FtpTlsRequirement.ControlConnection;
        FtpReply prot = await ExchangeAsync(privateData ? "PROT P" : "PROT C").ConfigureAwait(false);
        protectData = privateData && prot.IsCompletion;
        log.DataProtection(privateData, prot.IsCompletion);
        return prot.IsCompletion || tlsRequirement != FtpTlsRequirement.AllConnections
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, FtpTransferMessages.RequestedSslLevelFailed);
    }

    /// <summary>
    /// Under <c>--ftp-ssl-ccc</c>, once <c>PBSZ</c> and <c>PROT</c> went out over TLS, sends
    /// <c>CCC</c>: a reply of 500 or more leaves the control connection in TLS, as curl 8.21.0
    /// ignores it; any other clears TLS, sending <c>close_notify</c> first only under
    /// <c>--ftp-ssl-ccc-mode active</c>, and carries on in plain text (BL-636, ADR-0280).
    /// </summary>
    /// <returns>
    /// <see langword="null" /> to go on to <c>PWD</c>; exit 81 with no <c>QUIT</c> when TLS
    /// could not be cleared, as it never can on curl's Schannel build.
    /// </returns>
    private async ValueTask<TransferResult?> ClearControlTlsAsync()
    {
        if (!controlSecured || context.FtpCommandChannelClearing == FtpCommandChannelClearing.Off)
        {
            return null;
        }

        FtpReply ccc = await ExchangeAsync("CCC").ConfigureAwait(false);
        if (ccc.Code >= 500)
        {
            log.ControlClearingRefused(ccc.Code);
            return null;
        }

        bool sendCloseNotifyFirst = context.FtpCommandChannelClearing == FtpCommandChannelClearing.Active;
        if (await control.Connection.ClearTlsAsync(sendCloseNotifyFirst, control.CancellationToken).ConfigureAwait(false) is not { } plaintext)
        {
            context.Events.ReportInfo(FtpTransferMessages.ClearCommandChannelFailed);
            return TransferResult.Failure(CurlExitCode.Again, FtpTransferMessages.ClearCommandChannelFailed);
        }

        control.SwitchTo(plaintext);
        log.ControlCleared();
        return null;
    }

    private async ValueTask<TransferResult?> LogInAsync()
    {
        FtpReply user = await ExchangeAsync("USER " + (context.Credentials?.UserName ?? AnonymousUser)).ConfigureAwait(false);
        return await AnswerLoginReplyAsync(user, passwordSent: false).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a reply to <c>USER</c>, <c>PASS</c> or the alternative user command as curl
    /// 8.21.0's <c>ftp_state_user_resp</c> does: <c>331</c> to a user command sends
    /// <c>PASS</c>, a 2xx logs in, <c>332</c> sends <c>ACCT</c>, and anything else sends the
    /// <c>--ftp-alternative-to-user</c> command once, or is exit 67.
    /// </summary>
    private async ValueTask<TransferResult?> AnswerLoginReplyAsync(FtpReply reply, bool passwordSent)
    {
        if (reply.Code == 331 && !passwordSent)
        {
            return await SendPasswordAsync().ConfigureAwait(false);
        }

        if (reply.IsCompletion)
        {
            return LoggedIn();
        }

        return reply.Code == 332
            ? await SendAccountAsync().ConfigureAwait(false)
            : await SendAlternativeUserAsync(reply.Code).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> SendPasswordAsync()
    {
        FtpReply pass = await ExchangeAsync("PASS " + (context.Credentials?.Password ?? AnonymousPassword)).ConfigureAwait(false);
        return await AnswerLoginReplyAsync(pass, passwordSent: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>ACCT</c> with the <c>--ftp-account</c> account: exit 67 when there is none,
    /// and exit 11 when the reply is anything but <c>230</c>, as curl 8.21.0 does (ADR-0216).
    /// </summary>
    private async ValueTask<TransferResult?> SendAccountAsync()
    {
        if (context.FtpAccount is not { } account)
        {
            return TransferResult.Failure(CurlExitCode.LoginDenied, FtpTransferMessages.AccountRequested);
        }

        FtpReply acct = await ExchangeAsync("ACCT " + account).ConfigureAwait(false);
        return acct.Code == 230
            ? LoggedIn()
            : TransferResult.Failure(CurlExitCode.FtpWeirdPassReply, FtpTransferMessages.AccountRejected(acct.Code));
    }

    /// <summary>
    /// Sends the <c>--ftp-alternative-to-user</c> command verbatim after a refused user or
    /// password, once; without one, or once it was sent, the refusal is exit 67.
    /// </summary>
    private async ValueTask<TransferResult?> SendAlternativeUserAsync(int refusedCode)
    {
        if (context.FtpAlternativeToUser is not { } command || alternativeUserSent)
        {
            return TransferResult.Failure(CurlExitCode.LoginDenied, FtpTransferMessages.AccessDenied(refusedCode));
        }

        alternativeUserSent = true;
        FtpReply user = await ExchangeAsync(command).ConfigureAwait(false);
        return await AnswerLoginReplyAsync(user, passwordSent: false).ConfigureAwait(false);
    }

    /// <summary>Logs the login complete and goes on.</summary>
    private TransferResult? LoggedIn()
    {
        log.LoggedIn();
        return null;
    }

    private async ValueTask<TransferResult> TransferPathAsync()
    {
        if (FtpUrlPath.Parse(typeCode.Path, context.FtpFileMethod) is not { } path)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, FtpTransferMessages.PathHasControlCharacters);
        }

        log.PathWalk(context.FtpFileMethod, path.Directories.Count);
        rememberedDirectory = string.Concat(path.Directories.Select(directory => directory + "/"));
        return context.Upload is { } upload
            ? await UploadAsync(path, upload).ConfigureAwait(false)
            : await RetrieveFromPathAsync(path).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> RetrieveFromPathAsync(FtpUrlPath path)
    {
        ReportWhetherInEntryDirectory(path);
        trace.DoPhaseStarts(IsListing(path) ? "LIST" : "RETR");
        return await SendQuotesAsync(quotes.AfterLogin, FtpQuoteStage.AfterLogin).ConfigureAwait(false)
            ?? await ChangeDirectoriesAsync(path.Directories).ConfigureAwait(false)
            ?? await CheckModificationTimeAsync(path.FileName).ConfigureAwait(false)
            ?? (context.NoBody
                ? await ReportHeadAsync(path.FileName).ConfigureAwait(false)
                : await DownloadAsync(path).ConfigureAwait(false));
    }

    /// <summary>
    /// Downloads the file, or the listing when the path names a directory or <c>-l</c> asks
    /// for one. The window <c>-r</c> or <c>-C</c> asks for applies to a file only. An ASCII
    /// download sends no <c>SIZE</c>, so no <c>REST</c> either: as curl 8.21.0 was measured
    /// to (BL-633), it reads from the start, <c>-C</c> ignored, and keeps only the byte
    /// limit of a <c>-r</c> range.
    /// </summary>
    private async ValueTask<TransferResult> DownloadAsync(FtpUrlPath path)
    {
        bool listing = IsListing(path);
        bool ascii = listing || typeCode.UseAscii;
        window = DownloadWindowOf(listing);
        return await OpenDownloadDataConnectionAsync(DownloadPretArgument(path, listing)).ConfigureAwait(false)
            ?? await SetTypeAsync(ascii).ConfigureAwait(false)
            ?? await SendQuotesAsync(quotes.BeforeTransfer, FtpQuoteStage.BeforeTransfer).ConfigureAwait(false)
            ?? await ReadSizeAsync(path.FileName, ascii).ConfigureAwait(false)
            ?? TraceRetrieveNext(listing)
            ?? await RefuseOversizedFileAsync().ConfigureAwait(false)
            ?? await PositionAsync().ConfigureAwait(false)
            ?? await RetrieveAsync(listing ? ListCommand(path) : "RETR " + path.FileName, listing).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes curl's <c>ftp_state_retr()</c> note for a file, once its size is known and
    /// before <c>--max-filesize</c> is checked or <c>REST</c> sent; never for a listing.
    /// </summary>
    /// <returns>Always <see langword="null" />: the download goes on.</returns>
    private TransferResult? TraceRetrieveNext(bool listing)
    {
        if (!listing)
        {
            trace.RetrieveNext();
        }

        return null;
    }

    /// <summary>
    /// Opens a download's data connection, then ends the download there when its <c>-r</c>
    /// text names no range (<see cref="EndWhenRangeTextNamesNoRangeAsync" />).
    /// </summary>
    private async ValueTask<TransferResult?> OpenDownloadDataConnectionAsync(string pretArgument) =>
        await OpenDataConnectionAsync(pretArgument).ConfigureAwait(false)
            ?? await EndWhenRangeTextNamesNoRangeAsync().ConfigureAwait(false);

    /// <summary>
    /// Ends a download, a listing included, whose <c>-r</c> text names no range (<c>abc</c>, <c>-0</c>,
    /// <c>5-2</c>: <see cref="ITransferContext.RangeText" /> set, <see cref="ITransferContext.Range" />
    /// <see langword="null" />) once its data connection is open, with no <c>TYPE</c>,
    /// <c>SIZE</c> or <c>RETR</c>, and exit 0: curl 8.21.0's <c>ftp_do_more</c> parses the
    /// range only then, and the range error it gets does not reach the exit code (measured,
    /// BL-1333).
    /// </summary>
    /// <returns><see langword="null" /> when the download goes on.</returns>
    private async ValueTask<TransferResult?> EndWhenRangeTextNamesNoRangeAsync()
    {
        if (context.Range is not null || context.RangeText is null)
        {
            return null;
        }

        context.Events.ReportInfo(FtpTransferMessages.RememberingDirectory(rememberedDirectory));
        return await QuitAndKeepConnectionAsync().ConfigureAwait(false);
    }

    /// <summary>Whether a download is a listing: the path names a directory, or <c>-l</c> or <c>;type=d</c> asks for one.</summary>
    private bool IsListing(FtpUrlPath path) => path.FileName.Length == 0 || typeCode.ListOnly;

    /// <summary>
    /// The window a download reads: none for a listing, and for an ASCII file the
    /// <c>-r</c> byte limit alone, from the start.
    /// </summary>
    private FtpDownloadWindow DownloadWindowOf(bool listing)
    {
        if (listing)
        {
            return default;
        }

        FtpDownloadWindow asked = FtpDownloadWindow.Of(context);
        return typeCode.UseAscii ? asked with { Offset = 0 } : asked;
    }

    /// <summary>
    /// The listing command: <c>NLST</c> under <c>-l</c>, <c>LIST</c> otherwise, with the
    /// directory as its argument under <c>--ftp-method nocwd</c>.
    /// </summary>
    private string ListCommand(FtpUrlPath path) =>
        path.ListArgument is { } argument ? ListVerb + " " + argument : ListVerb;

    /// <summary>What a download's <c>PRET</c> names: the listing verb alone, or <c>RETR</c> and the file.</summary>
    private string DownloadPretArgument(FtpUrlPath path, bool listing) =>
        listing ? ListVerb : "RETR " + path.FileName;

    /// <summary>The listing verb: <c>NLST</c> under <c>-l</c>, <c>LIST</c> otherwise.</summary>
    private string ListVerb => typeCode.ListOnly ? "NLST" : "LIST";

    /// <summary>
    /// Uploads <paramref name="upload" /> to the file <paramref name="path" /> names: exit 3,
    /// with no <c>QUIT</c>, when it names none.
    /// </summary>
    private async ValueTask<TransferResult> UploadAsync(FtpUrlPath path, Stream upload)
    {
        if (path.FileName.Length == 0)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, FtpTransferMessages.UploadWithoutFileName);
        }

        uploadSize = KnownSizeOf(upload);
        ReportWhetherInEntryDirectory(path);
        trace.DoPhaseStarts("STOR");
        return await SendQuotesAsync(quotes.AfterLogin, FtpQuoteStage.AfterLogin).ConfigureAwait(false)
            ?? await ChangeDirectoriesAsync(path.Directories).ConfigureAwait(false)
            ?? await CheckModificationTimeAsync(path.FileName).ConfigureAwait(false)
            ?? await OpenDataConnectionAsync("STOR " + path.FileName).ConfigureAwait(false)
            ?? await SetTypeAsync(typeCode.UseAscii).ConfigureAwait(false)
            ?? await SendQuotesAsync(quotes.BeforeTransfer, FtpQuoteStage.BeforeTransfer).ConfigureAwait(false)
            ?? await StoreAsync(path.FileName, upload).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>STOR</c> from offset 0 (<c>APPE</c> under <c>-a</c>), or skips the <c>-C</c>
    /// offset and sends <c>APPE</c>; an offset that covers the whole upload sends nothing
    /// more than <c>QUIT</c> and succeeds.
    /// </summary>
    private async ValueTask<TransferResult> StoreAsync(string fileName, Stream upload)
    {
        long offset = context.ResumeUploadFromUnknownOffset
            ? await ReadRemoteSizeAsync(fileName).ConfigureAwait(false)
            : context.ResumeFrom ?? 0;
        if (offset <= 0)
        {
            return await SendUploadAsync((context.Append ? "APPE " : "STOR ") + fileName, upload).ConfigureAwait(false);
        }

        if (FtpUploadOffset.TrySkip(upload, offset))
        {
            return await SendUploadAsync("APPE " + fileName, upload).ConfigureAwait(false);
        }

        context.Events.ReportInfo(FtpTransferMessages.AlreadyCompletelyUploaded);
        return await QuitAndSucceedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the server for the size of the file <c>-C -</c> resumes: the count a <c>213</c>
    /// reply carries, or 0 for any other reply, which uploads the whole source with <c>STOR</c>.
    /// </summary>
    private async ValueTask<long> ReadRemoteSizeAsync(string fileName) =>
        SizeOf(await ExchangeAsync("SIZE " + fileName).ConfigureAwait(false)) ?? 0;

    /// <summary>
    /// Sends <paramref name="command" /> and, unless it is answered with 400 or more (exit 25
    /// after <c>QUIT</c>), writes the upload to the data connection, closes it and reads the
    /// end-of-transfer reply.
    /// </summary>
    private async ValueTask<TransferResult> SendUploadAsync(string command, Stream upload)
    {
        uploadSize = KnownSizeOf(upload);
        FtpReply opened = await ExchangeAsync(command).ConfigureAwait(false);
        if (opened.Code >= 400)
        {
            return await QuitAndFailAsync(CurlExitCode.UploadFailed, FtpTransferMessages.UploadRefused(opened.Code)).ConfigureAwait(false);
        }

        log.TransferStarted(command);
        if (pendingConnection is not null)
        {
            trace.LeaveTransferState();
        }

        if (await ReadyDataConnectionAsync().ConfigureAwait(false) is { } notReady)
        {
            return notReady;
        }

        trace.TransferInitiated();
        return await CopyUploadAsync(upload).ConfigureAwait(false)
            ?? await ReadTransferCompleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Copies the upload to the data connection and closes it, which tells the server the
    /// file has ended. A failed read ends the upload as the end of the source does, as curl
    /// takes it; a failed write is exit 55. Under <c>--crlf</c> each chunk is converted
    /// first, and the converted bytes are the ones sent and counted, as curl 8.21.0 was
    /// measured to (BL-633).
    /// </summary>
    private async ValueTask<TransferResult?> CopyUploadAsync(Stream upload)
    {
        IConnection data = dataConnection!;
        long? expected = uploadSize;
        byte[] buffer = new byte[ReadBufferSize];
        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> convertChunk = context.ConvertLineEndings
            ? new CrlfUploadConverter(ReadBufferSize).Convert
            : static chunk => chunk;
        int read;
        while ((read = await ReadUploadAsync(upload, buffer).ConfigureAwait(false)) > 0)
        {
            ReadOnlyMemory<byte> chunk = convertChunk(buffer.AsMemory(0, read));
            try
            {
                await data.WriteAsync(chunk, context.CancellationToken).ConfigureAwait(false);
                await data.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException failure)
            {
                return TransferResult.Failure(CurlExitCode.SendError, FtpTransferMessages.SendFailed(failure), bytesTransferred);
            }

            context.Events.ReportDataSent(chunk.Span);
            bytesTransferred += chunk.Length;
            context.Progress.ReportUploaded(bytesTransferred, expected);
        }

        dataConnection = null;
        await data.DisposeAsync().ConfigureAwait(false);
        context.Events.ReportInfo(FtpTransferMessages.UploadSent(bytesTransferred));
        return null;
    }

    /// <summary>
    /// The bytes left in <paramref name="upload" /> when it can say, as curl knows the size of a
    /// file named to <c>-T</c>; <see langword="null" /> for one that cannot, as standard input.
    /// </summary>
    private static long? KnownSizeOf(Stream upload) =>
        upload.CanSeek ? Math.Max(0, upload.Length - upload.Position) : null;

    private async ValueTask<int> ReadUploadAsync(Stream upload, byte[] buffer)
    {
        try
        {
            return await upload.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Sends <c>MDTM</c> for a file when <c>-z</c>, <c>-R</c> or <c>-I</c> asks for its time,
    /// as curl 8.21.0 does straight after the <c>CWD</c>s, for a download, an <c>-l</c>
    /// listing of a file URL and an upload alike (BL-637). Under <c>-I</c> it writes the
    /// <c>Last-Modified</c> line; then it applies <c>-z</c>.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> to go on; a success with no body when <c>-z</c> is not met;
    /// or a failed header write.
    /// </returns>
    private async ValueTask<TransferResult?> CheckModificationTimeAsync(string fileName)
    {
        if (fileName.Length == 0 || !AsksForModificationTime)
        {
            return null;
        }

        FtpReply modified = await ExchangeAsync("MDTM " + fileName).ConfigureAwait(false);
        ReportInfo(FtpTransferMessages.ModificationTimeReply(modified.Code));
        modifiedUtc = FtpModificationTime.Of(modified);
        return await WriteHeaderAsync(context.NoBody ? FtpHeadHeaderLines.LastModified(modifiedUtc) : null).ConfigureAwait(false)
            ?? await ApplyTimeConditionAsync().ConfigureAwait(false);
    }

    private bool AsksForModificationTime => context.NoBody || context.RemoteTime || context.TimeCondition is not null;

    /// <summary>
    /// Applies <c>-z</c> to the <c>MDTM</c> time through <see cref="FtpTimeCondition" />: when
    /// it is not met, the post-transfer quotes and <c>QUIT</c> end the transfer with no data
    /// connection, as curl 8.21.0 does; a refused quote is still exit 21.
    /// </summary>
    private async ValueTask<TransferResult?> ApplyTimeConditionAsync()
    {
        if (context.TimeCondition is not { } condition)
        {
            return null;
        }

        (bool isMet, string? verboseLine) = FtpTimeCondition.Check(condition, modifiedUtc);
        ReportInfo(verboseLine);
        if (isMet)
        {
            return null;
        }

        TransferResult ended = await QuitAndSucceedAsync().ConfigureAwait(false);
        return ended.IsSuccess ? TransferResult.TimeConditionNotMet() : ended;
    }

    /// <summary>
    /// Reports curl 8.21.0's <see cref="FtpTransferMessages.SamePathAsPreviousTransfer" /> when
    /// <paramref name="path" /> needs no <c>CWD</c> to reach from the entry directory (BL-945).
    /// </summary>
    private void ReportWhetherInEntryDirectory(FtpUrlPath path) =>
        ReportInfo(path.IsInEntryDirectory ? FtpTransferMessages.SamePathAsPreviousTransfer : null);

    private void ReportInfo(string? line)
    {
        if (line is not null)
        {
            context.Events.ReportInfo(line);
        }
    }

    /// <summary>
    /// Answers <c>-I</c> as curl does, with no data connection, once
    /// <see cref="CheckModificationTimeAsync" /> has sent <c>MDTM</c> for a file: a directory
    /// URL sends nothing more; a file sends <c>TYPE I</c>, <c>SIZE</c> and <c>REST 0</c>,
    /// writing a <c>Content-Length</c> and <c>Accept-ranges</c> line for each that succeeded.
    /// </summary>
    private async ValueTask<TransferResult> ReportHeadAsync(string fileName)
    {
        if (fileName.Length == 0)
        {
            return await SendQuotesAsync(quotes.BeforeTransfer, FtpQuoteStage.BeforeTransfer).ConfigureAwait(false)
                ?? await QuitAndSucceedAsync().ConfigureAwait(false);
        }

        return await SetTypeAsync(typeCode.UseAscii).ConfigureAwait(false)
            ?? await ReadSizeAsync(fileName, false).ConfigureAwait(false)
            ?? await WriteHeaderAsync(fileSize is { } size ? FtpHeadHeaderLines.ContentLength(size) : null).ConfigureAwait(false)
            ?? await ReportRestAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> ReportRestAsync()
    {
        FtpReply rest = await ExchangeAsync("REST 0").ConfigureAwait(false);
        return await WriteHeaderAsync(rest.Code == 350 ? FtpHeadHeaderLines.AcceptRanges : null).ConfigureAwait(false)
            ?? await SendQuotesAsync(quotes.BeforeTransfer, FtpQuoteStage.BeforeTransfer).ConfigureAwait(false)
            ?? await QuitAndSucceedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one <c>-I</c> header line to <see cref="ITransferContext.HeaderOutput" />;
    /// nothing when <paramref name="line" /> or the header output is absent.
    /// </summary>
    private async ValueTask<TransferResult?> WriteHeaderAsync(string? line)
    {
        if (line is null || context.HeaderOutput is not { } headerOutput)
        {
            return null;
        }

        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(line);
        try
        {
            await headerOutput.WriteAsync(bytes, context.CancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException)
        {
            return await QuitAndFailAsync(CurlExitCode.WriteError, FtpTransferMessages.HeaderWriteFailed(bytes.Length)).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult?> ChangeDirectoriesAsync(IReadOnlyList<string> directories)
    {
        foreach (string directory in directories)
        {
            if (!await TryChangeDirectoryAsync(directory).ConfigureAwait(false))
            {
                return await QuitAndFailLeavingConnectionIntactAsync(CurlExitCode.RemoteAccessDenied, FtpTransferMessages.ChangeDirectoryDenied).ConfigureAwait(false);
            }
        }

        log.DirectoryReached(directories);
        return null;
    }

    /// <summary>
    /// Sends <c>CWD</c>; when it is refused under <c>--ftp-create-dirs</c>, sends <c>MKD</c>,
    /// whatever its reply, and <c>CWD</c> once more, as curl 8.21.0 does.
    /// </summary>
    private async ValueTask<bool> TryChangeDirectoryAsync(string directory)
    {
        FtpReply changed = await ExchangeAsync("CWD " + directory).ConfigureAwait(false);
        if (changed.IsCompletion || !context.FtpCreateDirectories)
        {
            return changed.IsCompletion;
        }

        await ExchangeAsync("MKD " + directory).ConfigureAwait(false);
        trace.ChangingDirectoryAgain();
        return (await ExchangeAsync("CWD " + directory).ConfigureAwait(false)).IsCompletion;
    }

    /// <summary>
    /// Prepares the data connection: under <c>-P</c> a listening port announced with
    /// <c>EPRT</c> or <c>PORT</c>, otherwise a passive connection, announced first with
    /// <c>PRET</c> under <c>--ftp-pret</c>.
    /// </summary>
    /// <param name="pretArgument">
    /// What <c>PRET</c> names: <c>RETR</c> and the file, <c>LIST</c> or <c>NLST</c> with no
    /// argument, or <c>STOR</c> and the file for an upload, <c>APPE</c> included, as curl
    /// 8.21.0 was measured to send (BL-635).
    /// </param>
    private async ValueTask<TransferResult?> OpenDataConnectionAsync(string pretArgument) =>
        context.FtpPort is { } ftpPort
            ? await AnnounceActivePortAsync(FtpPortArgument.Parse(ftpPort)).ConfigureAwait(false)
            : await SendPretAsync(pretArgument).ConfigureAwait(false)
                ?? await OpenPassiveDataConnectionAsync().ConfigureAwait(false);

    /// <summary>
    /// Sends <c>PRET</c> under <c>--ftp-pret</c>: anything but <c>200</c> ends the transfer
    /// with exit 84 and no <c>QUIT</c>, as curl 8.21.0 does.
    /// </summary>
    private async ValueTask<TransferResult?> SendPretAsync(string pretArgument)
    {
        if (!context.FtpSendPret)
        {
            return null;
        }

        FtpReply pret = await ExchangeAsync("PRET " + pretArgument).ConfigureAwait(false);
        return pret.Code == 200
            ? null
            : TransferResult.Failure(CurlExitCode.FtpPretFailed, FtpTransferMessages.PretNotAccepted(pret.Code));
    }

    /// <summary>
    /// Binds a port on the <c>-P</c> address and announces it: <c>EPRT</c> first, unless
    /// <c>--disable-eprt</c> on IPv4, then on IPv4 a fresh port with <c>PORT</c> when
    /// <c>EPRT</c> was skipped or refused. IPv6 has no <c>PORT</c>, so a refused <c>EPRT</c>
    /// there is exit 30 after <c>QUIT</c>. A <c>-P</c> value that names a network interface
    /// is that interface's address, and exit 30 after <c>QUIT</c> when it has none of the
    /// control connection's family and IPv6 scope, as curl 8.21.0's <c>Curl_if2ip</c> answers
    /// <c>IF2IP_AF_NOT_SUPPORTED</c> (ADR-0110).
    /// </summary>
    private async ValueTask<TransferResult?> AnnounceActivePortAsync(FtpPortArgument argument)
    {
        if (!argument.UsesControlAddress && connections.InterfaceLookup.FindAddresses(argument.Address) is { } interfaceAddresses)
        {
            return InterfaceAddressOf(interfaceAddresses) is { } interfaceAddress
                ? await AnnounceActivePortAsync(interfaceAddress, argument).ConfigureAwait(false)
                : await QuitAndFailAsync(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort).ConfigureAwait(false);
        }

        return await ActiveAddressOfAsync(argument).ConfigureAwait(false) is { } address
            ? await AnnounceActivePortAsync(address, argument).ConfigureAwait(false)
            : await RefuseActiveAddressAsync(argument).ConfigureAwait(false);
    }

    /// <summary>
    /// Binds a port on <paramref name="address" /> and announces it, as
    /// <see cref="AnnounceActivePortAsync(FtpPortArgument)" /> describes.
    /// </summary>
    private async ValueTask<TransferResult?> AnnounceActivePortAsync(IPAddress address, FtpPortArgument argument)
    {
        bool ipv6 = address.AddressFamily == AddressFamily.InterNetworkV6;
        if (context.FtpUseEprt || ipv6)
        {
            if (await ListenAsync(address, argument).ConfigureAwait(false) is { } failed)
            {
                return failed;
            }

            if ((await ExchangeAsync(FtpActiveCommand.Eprt(ListeningEndPoint)).ConfigureAwait(false)).IsCompletion)
            {
                return AnnouncedActively("EPRT");
            }

            await ReleasePendingConnectionAsync().ConfigureAwait(false);
            WarnEprtRefused(ipv6);
        }

        return ipv6
            ? await QuitAndFailAsync(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort).ConfigureAwait(false)
            : await ListenAsync(address, argument).ConfigureAwait(false) ?? await SendPortAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> SendPortAsync()
    {
        if ((await ExchangeAsync(FtpActiveCommand.Port(ListeningEndPoint)).ConfigureAwait(false)).IsCompletion)
        {
            return AnnouncedActively("PORT");
        }

        return await QuitAndFailAsync(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports curl 8.21.0's <c>-v</c> line for an accepted <c>EPRT</c> or <c>PORT</c>, logs
    /// it, and goes on.
    /// </summary>
    private TransferResult? AnnouncedActively(string verb)
    {
        context.Events.ReportInfo(FtpTransferMessages.ConnectDataStreamActively);
        trace.DoPhaseComplete();
        log.ActivePortAnnounced(verb);
        return null;
    }

    /// <summary>
    /// Reports curl 8.21.0's <c>-v</c> line for, and logs, a refused <c>EPRT</c> that
    /// <c>PORT</c> follows, which only IPv4 has.
    /// </summary>
    private void WarnEprtRefused(bool ipv6)
    {
        if (!ipv6)
        {
            context.Events.ReportInfo(FtpTransferMessages.DisablingEprt);
            log.EprtRefused();
        }
    }

    /// <summary>The announced address and the port the active-mode listener is bound to.</summary>
    private IPEndPoint ListeningEndPoint => new(announcedAddress!, ((IPEndPoint)pendingConnection!.LocalEndPoint).Port);

    /// <summary>
    /// The control connection's own address, an IPv4-mapped one as plain IPv4;
    /// <see langword="null" /> when it is unknown.
    /// </summary>
    private IPAddress? ControlAddress => Unmapped((controlLocalEndPoint as IPEndPoint)?.Address);

    /// <summary>
    /// The address <paramref name="argument" /> names, an IPv4-mapped one as plain IPv4: the
    /// control connection's own for <c>-</c>, the literal, or a name's first resolved address,
    /// as curl 8.21.0 announced <c>::1</c> for <c>-P localhost</c> (ADR-0108);
    /// <see langword="null" /> when the control connection's address is unknown or the name
    /// does not resolve.
    /// </summary>
    private async ValueTask<IPAddress?> ActiveAddressOfAsync(FtpPortArgument argument)
    {
        if (argument.UsesControlAddress)
        {
            return ControlAddress;
        }

        if (IPAddress.TryParse(argument.Address, out IPAddress? literal))
        {
            return Unmapped(literal);
        }

        IReadOnlyList<IPAddress> resolved = await connections.DnsResolver
            .ResolveAsync(argument.Address, context.CancellationToken)
            .ConfigureAwait(false);
        return resolved.Count == 0 ? null : Unmapped(resolved[0]);
    }

    /// <summary>
    /// The first of a <c>-P</c> interface's addresses with the control connection's own
    /// address family and, for IPv6, scope, without its scope ID, as curl 8.21.0's
    /// <c>Curl_if2ip</c> picks one and formats it with <c>inet_ntop</c> (ADR-0110);
    /// <see langword="null" /> when it has none or the control connection's address is unknown.
    /// </summary>
    private IPAddress? InterfaceAddressOf(IReadOnlyList<IPAddress> interfaceAddresses)
    {
        if (ControlAddress is not { } control)
        {
            return null;
        }

        IPAddress? found = interfaceAddresses.FirstOrDefault(candidate =>
            FamilyAndScopeOf(candidate) == FamilyAndScopeOf(control));
        return found is null ? null : new IPAddress(found.GetAddressBytes());
    }

    /// <summary>
    /// <paramref name="address" />'s family and the scope curl's <c>Curl_ipv6_scope</c> gives
    /// it: unique local, link-local, site-local, node-local for <c>::1</c>, or none of them
    /// for global, which is every IPv4 address.
    /// </summary>
    private static (AddressFamily Family, bool UniqueLocal, bool LinkLocal, bool SiteLocal, bool NodeLocal) FamilyAndScopeOf(IPAddress address) =>
        (address.AddressFamily, address.IsIPv6UniqueLocal, address.IsIPv6LinkLocal, address.IsIPv6SiteLocal, address.Equals(IPAddress.IPv6Loopback));

    private static IPAddress? Unmapped(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;

    private static bool IsIPv6(EndPoint? endPoint) =>
        Unmapped((endPoint as IPEndPoint)?.Address) is { AddressFamily: AddressFamily.InterNetworkV6 };

    /// <summary>
    /// Ends a transfer whose <c>-P</c> address could not be used: exit 30 after <c>QUIT</c>
    /// when the control connection's own address is unknown, and for a name that does not
    /// resolve exit 6 with no <c>QUIT</c>, after curl 8.21.0's two <c>-v</c> lines
    /// (ADR-0108).
    /// </summary>
    private async ValueTask<TransferResult> RefuseActiveAddressAsync(FtpPortArgument argument)
    {
        if (argument.UsesControlAddress)
        {
            return await QuitAndFailAsync(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort).ConfigureAwait(false);
        }

        string couldNotResolve = FtpTransferMessages.CouldNotResolveHost(argument.Address);
        context.Events.ReportInfo(couldNotResolve);
        context.Events.ReportInfo(FtpTransferMessages.PortAddressNotResolved(argument.Address));
        return TransferResult.Failure(CurlExitCode.CouldntResolveHost, couldNotResolve);
    }

    /// <summary>
    /// Binds the active-mode listening port; a bind failure is the listener's exit code and
    /// message after <c>QUIT</c>, as curl 8.21.0 ends <c>bind() failed, ran out of ports</c>.
    /// A <c>-P</c> address that is not local is reported with <c>-v</c> and bound once more on
    /// the control connection's address, as curl 8.21.0's <c>ftp_port_bind_socket</c> does;
    /// the <c>-P</c> address is still the one announced (BL-464, ADR-0107).
    /// </summary>
    private async ValueTask<TransferResult?> ListenAsync(IPAddress address, FtpPortArgument argument)
    {
        announcedAddress = address;
        ListenResult listening = await BindAsync(address, argument).ConfigureAwait(false);
        if (IsNonLocalBindFailure(listening) && !argument.UsesControlAddress && ControlAddress is { } controlAddress)
        {
            context.Events.ReportInfo(listening.ErrorMessage!);
            listening = await BindAsync(controlAddress, argument).ConfigureAwait(false);
        }

        pendingConnection = listening.PendingConnection;
        if (pendingConnection is null)
        {
            return await QuitAndFailLeavingConnectionIntactAsync(listening.ExitCode, FtpTransferMessages.BindFailed(listening.ErrorMessage!)).ConfigureAwait(false);
        }

        trace.ActivePortListening();
        return null;
    }

    private static bool IsNonLocalBindFailure(ListenResult listening) =>
        listening.ErrorMessage?.Contains(FtpTransferMessages.NonLocalBindFailed, StringComparison.Ordinal) == true;

    private ValueTask<ListenResult> BindAsync(IPAddress address, FtpPortArgument argument) =>
        connections.Listener.ListenAsync(new ListenTarget(address, argument.LowPort, argument.HighPort), context.CancellationToken);

    private async ValueTask ReleasePendingConnectionAsync()
    {
        await pendingConnection!.DisposeAsync().ConfigureAwait(false);
        pendingConnection = null;
    }

    /// <summary>
    /// Makes the data connection ready once the transfer command is answered: in active mode
    /// accepts the server's connection, then runs the TLS handshake over it after an accepted
    /// <c>PROT P</c>. Nothing to do in passive mode, whose handshake ran right after the
    /// connect (<see cref="ConnectDataAsync" />).
    /// </summary>
    private async ValueTask<TransferResult?> ReadyDataConnectionAsync() =>
        pendingConnection is { } pending
            ? await AcceptDataConnectionAsync(pending).ConfigureAwait(false)
                ?? await SecureDataConnectionAsync().ConfigureAwait(false)
            : null;

    /// <summary>
    /// Waits up to 60 seconds for the server to connect to the active-mode port: exit 12
    /// after <c>QUIT</c> when it does not, and a failed accept's exit code after <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult?> AcceptDataConnectionAsync(IPendingConnection pending)
    {
        context.Events.ReportInfo(FtpTransferMessages.DataConnectionNotAvailable);
        trace.AcceptPending();
        context.Events.ReportInfo(FtpTransferMessages.ReadyToAccept);
        using var timeout = new CancellationTokenSource(AcceptTimeout, context.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeout.Token);
        ConnectResult accepted;
        try
        {
            accepted = await pending.AcceptAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return await QuitAndFailAsync(CurlExitCode.FtpAcceptTimeout, FtpTransferMessages.AcceptTimeout).ConfigureAwait(false);
        }

        dataConnection = accepted.Connection;
        if (dataConnection is null)
        {
            return await QuitAndFailAsync(accepted.ExitCode, accepted.ErrorMessage!).ConfigureAwait(false);
        }

        ReportAccepted(dataConnection.RemoteEndPoint, (IPEndPoint)pending.LocalEndPoint);
        log.ActiveDataAccepted();
        return null;
    }

    /// <summary>
    /// Reports curl 8.21.0's <c>-v</c> lines for an accepted active-mode data connection: the
    /// connection line only when the server's end is known.
    /// </summary>
    private void ReportAccepted(EndPoint? remoteEndPoint, IPEndPoint listening)
    {
        context.Events.ReportInfo(FtpTransferMessages.ConnectionAccepted);
        if (remoteEndPoint is IPEndPoint remote)
        {
            context.Events.ReportInfo(FtpTransferMessages.SecondConnectionEstablished(remote, listening));
        }
    }

    /// <summary>
    /// Runs the TLS handshake over the data connection after an accepted <c>PROT P</c>, right
    /// after a passive connect or an active accept, reporting it to the transfer's events, which writes curl's <c>schannel:</c> lines and no
    /// second <c>Established</c> line (measured, BL-1084); a failed one ends the transfer with
    /// its exit code and no <c>QUIT</c>, as a failed passive connect does.
    /// </summary>
    private async ValueTask<TransferResult?> SecureDataConnectionAsync()
    {
        if (!protectData)
        {
            return null;
        }

        ConnectResult secured = await connections.TlsProvider
            .AuthenticateAsClientAsync(dataConnection!, context.Url.IdnHost, context.Events, context.CancellationToken)
            .ConfigureAwait(false);
        dataConnection = secured.Connection;
        if (dataConnection is null)
        {
            return TransferResult.Failure(secured.ExitCode, secured.ErrorMessage!, bytesTransferred);
        }

        log.DataSecured();
        return null;
    }

    /// <summary>
    /// Opens a passive data connection: <c>EPSV</c> first unless <c>--disable-epsv</c>, then
    /// <c>PASV</c> when <c>EPSV</c> was skipped or answered with anything but <c>229</c>.
    /// </summary>
    /// <remarks>
    /// curl 8.21.0's <c>-v</c> says <see cref="FtpTransferMessages.ConnectDataStreamPassively" />
    /// once the first of them is sent, and <see cref="FtpTransferMessages.EpsvFailed" /> when
    /// <c>EPSV</c> is refused (BL-931). Over IPv6 there is no <c>PASV</c>: <c>EPSV</c> is sent
    /// even under <c>--disable-epsv</c>, and a refusal ends the transfer with exit 8 and no
    /// <c>QUIT</c>, as curl 8.21.0 does (BL-903).
    /// </remarks>
    private async ValueTask<TransferResult?> OpenPassiveDataConnectionAsync()
    {
        if (context.FtpDisableEpsv && !controlPeerIsIPv6)
        {
            return await EnterPassiveModeAsync(FtpTransferMessages.ConnectDataStreamPassively).ConfigureAwait(false);
        }

        FtpReply epsv = await ExchangeAsync("EPSV", FtpTransferMessages.ConnectDataStreamPassively).ConfigureAwait(false);
        if (epsv.Code != 229)
        {
            return await AnswerRefusedEpsvAsync(epsv.Code).ConfigureAwait(false);
        }

        if (!FtpPassiveReply.TryParseEpsvPort(epsv.LastLine, out int epsvPort, out string unreadable))
        {
            return await QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, unreadable).ConfigureAwait(false);
        }

        TransferResult? dialFailure = await DialDataAsync(context.Url.IdnHost, controlPeerAddress, epsvPort).ConfigureAwait(false);
        return dialFailure is null
            ? await SecureDataConnectionAsync().ConfigureAwait(false)
            : await AnswerFailedEpsvDialAsync(dialFailure).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a data connection to the <c>229</c>'s port that could not be made: over IPv6
    /// the transfer ends with exit 8 and the dial's message, no <c>QUIT</c>; otherwise curl
    /// 8.21.0's <c>-v</c> line and <c>PASV</c>, whose port carries the transfer (measured, BL-1250).
    /// </summary>
    private async ValueTask<TransferResult?> AnswerFailedEpsvDialAsync(TransferResult dialFailure)
    {
        if (controlPeerIsIPv6)
        {
            context.Events.ReportInfo(FtpTransferMessages.EpsvFailedOverIPv6);
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, dialFailure.ErrorMessage!, bytesTransferred);
        }

        context.Events.ReportInfo(FtpTransferMessages.EpsvFailed);
        log.EpsvDataConnectFailed();
        return await EnterPassiveModeAsync(afterSent: null).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers an <c>EPSV</c> refused with anything but <c>229</c>: over IPv6 the transfer
    /// ends with exit 8 and no <c>QUIT</c>; otherwise curl 8.21.0's <c>-v</c> line and <c>PASV</c>.
    /// </summary>
    private async ValueTask<TransferResult?> AnswerRefusedEpsvAsync(int code)
    {
        if (controlPeerIsIPv6)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, FtpTransferMessages.EpsvFailedOverIPv6, bytesTransferred);
        }

        context.Events.ReportInfo(FtpTransferMessages.EpsvFailed);
        trace.EpsvRefused();
        log.EpsvRefused(code);
        return await EnterPassiveModeAsync(afterSent: null).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>PASV</c> and connects to the port its <c>227</c> reply names, on the control
    /// connection's host, or under <c>--no-ftp-skip-pasv-ip</c> on the address it names.
    /// </summary>
    /// <param name="afterSent">The <c>-v</c> line to report once <c>PASV</c> is sent, or <see langword="null" /> for none.</param>
    private async ValueTask<TransferResult?> EnterPassiveModeAsync(string? afterSent)
    {
        FtpReply pasv = await ExchangeAsync("PASV", afterSent).ConfigureAwait(false);
        if (pasv.Code != 227)
        {
            return await QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, FtpTransferMessages.BadPassiveReply(pasv.Code)).ConfigureAwait(false);
        }

        return FtpPassiveReply.TryParsePasv(pasv.LastLine, out string address, out int pasvPort)
            ? await ConnectToPassiveAddressAsync(address, pasvPort).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.FtpWeird227Format, FtpTransferMessages.Weird227Reply);
    }

    /// <summary>
    /// Connects to the port a <c>227</c> reply named: on the address it named under
    /// <c>--no-ftp-skip-pasv-ip</c>, otherwise on the control connection's host, after curl
    /// 8.21.0's <c>-v</c> line that says so.
    /// </summary>
    private async ValueTask<TransferResult?> ConnectToPassiveAddressAsync(string address, int port)
    {
        if (!context.FtpSkipPasvIp)
        {
            return await ConnectDataAsync(address, address, port).ConfigureAwait(false);
        }

        context.Events.ReportInfo(FtpTransferMessages.SkipPassiveAddress(address, context.Url.IdnHost));
        log.PassiveAddressSkipped(address, context.Url.IdnHost);
        return await ConnectDataAsync(context.Url.IdnHost, controlPeerAddress, port).ConfigureAwait(false);
    }

    /// <summary>
    /// Dials the passive data connection, after curl 8.21.0's <c>-v</c> line naming
    /// <paramref name="shownHost" /> and the port. A dial that fails names the control
    /// connection and then <paramref name="shownHost" /> after <c>via</c>, as curl 8.21.0's does (BL-904).
    /// After an accepted <c>PROT P</c> the TLS handshake runs at once, before <c>TYPE</c>, so
    /// its <c>schannel:</c> lines follow the <c>Trying</c> line, as curl 8.21.0 writes them
    /// (measured, BL-1084; BL-1091).
    /// </summary>
    private async ValueTask<TransferResult?> ConnectDataAsync(string host, string shownHost, int port) =>
        await DialDataAsync(host, shownHost, port).ConfigureAwait(false)
            ?? await SecureDataConnectionAsync().ConfigureAwait(false);

    /// <summary>
    /// Dials the passive data connection without its TLS handshake: <see langword="null" />
    /// once it is open, otherwise the failure, its message rewritten as curl 8.21.0 names it.
    /// </summary>
    private async ValueTask<TransferResult?> DialDataAsync(string host, string shownHost, int port)
    {
        context.Events.ReportInfo(FtpTransferMessages.ConnectingTo(shownHost, port));
        trace.DoPhaseComplete();
        var failure = new FtpDataConnectFailure(host, shownHost, port, controlName);
        if (port == 0)
        {
            // A ConnectTarget carries ports 1 to 65535; curl 8.21.0 dials a 229's port 0 and
            // the dial fails at once, writing its -v failure line (measured, BL-1240, BL-1250).
            string message = failure.Rewrite($"Failed to connect to {host}:0 after 0 ms: Could not connect to server");
            context.Events.ReportInfo(message);
            return TransferResult.Failure(CurlExitCode.CouldntConnect, message);
        }

        var target = new ConnectTarget(host, port, false)
        {
            Proxy = context.Proxy,
            Events = new FtpDataConnectEvents(context.Events, failure, controlName.Host, trace),
            DiagnosticLog = context.DiagnosticLog,
            TcpIoTrace = protectData ? null : FtpTcpIoTraces.Data,
        };
        ConnectResult connected = await connections.DataConnector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        dataConnection = connected.Connection;
        if (dataConnection is null)
        {
            return new TransferResult(connected.ExitCode, 0, failure.Rewrite(connected.ErrorMessage!)) { IsConnectionRefused = connected.IsConnectionRefused };
        }

        log.PassiveDataConnected(host, port);
        return null;
    }

    /// <summary>Sends <c>TYPE A</c> for a listing or an ASCII transfer, <c>TYPE I</c> otherwise.</summary>
    private async ValueTask<TransferResult?> SetTypeAsync(bool ascii)
    {
        FtpReply type = await ExchangeAsync(ascii ? "TYPE A" : "TYPE I").ConfigureAwait(false);
        return type.IsCompletion
            ? null
            : await QuitAndFailAsync(CurlExitCode.FtpCouldntSetType, FtpTransferMessages.CouldNotSetType).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> ReadSizeAsync(string fileName, bool listing)
    {
        if (listing)
        {
            return null;
        }

        FtpReply size = await ExchangeAsync("SIZE " + fileName).ConfigureAwait(false);
        if (size.Code == 550)
        {
            return await QuitAndFailAsync(CurlExitCode.RemoteFileNotFound, FtpTransferMessages.FileDoesNotExist).ConfigureAwait(false);
        }

        fileSize = SizeOf(size);
        return null;
    }

    /// <summary>
    /// Reads the byte count a <c>213</c> reply to <c>SIZE</c> carries, or
    /// <see langword="null" /> for any other reply or an unreadable count.
    /// </summary>
    private static long? SizeOf(FtpReply size) =>
        size.Code == 213 && long.TryParse(size.LastLine.AsSpan(3), System.Globalization.NumberStyles.AllowLeadingWhite, System.Globalization.CultureInfo.InvariantCulture, out long bytes)
            ? bytes
            : null;

    /// <summary>
    /// Ends the download with exit 63 when the <c>SIZE</c> count, the whole file's whatever
    /// <c>-C</c> or <c>-r</c> asks for, is larger than <c>--max-filesize</c>: <c>ABOR</c> for
    /// a range, then <c>QUIT</c>, and no <c>REST</c> or <c>RETR</c>, as curl 8.21.0 does.
    /// </summary>
    private async ValueTask<TransferResult?> RefuseOversizedFileAsync() =>
        fileSize > maxFileSize
            ? await EndAndFailAsync(CurlExitCode.FilesizeExceeded, FtpTransferMessages.MaxFileSizeExceeded).ConfigureAwait(false)
            : null;

    /// <summary>
    /// Applies the window before <c>RETR</c>: works out how many bytes to expect and, for
    /// a non-zero offset, sends <c>REST</c>, failing with exit 31 and no <c>QUIT</c> when
    /// it is answered with anything but <c>350</c>.
    /// </summary>
    private async ValueTask<TransferResult?> PositionAsync()
    {
        if (window.Offset == 0)
        {
            expectedSize = LimitToWindow(fileSize);
            return null;
        }

        if (fileSize is { } size)
        {
            return await PositionWithinSizeAsync(size).ConfigureAwait(false);
        }

        context.Events.ReportInfo(FtpTransferMessages.SizeNotSupported);
        return await RestartAtAsync(window.Offset).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the window against the <c>SIZE</c> count: exit 36 when it starts past the
    /// end, a success with nothing transferred when nothing is left, otherwise
    /// <c>REST</c> at the resolved offset.
    /// </summary>
    private async ValueTask<TransferResult?> PositionWithinSizeAsync(long size)
    {
        if (ResolveRemaining(size) is not { } remaining)
        {
            return await EndAndFailAsync(CurlExitCode.BadDownloadResume, FtpTransferMessages.OffsetBeyondFileSize(window.Offset, size)).ConfigureAwait(false);
        }

        if (remaining == 0)
        {
            context.Events.ReportInfo(FtpTransferMessages.AlreadyCompletelyDownloaded);
            return await EndAndSucceedAsync().ConfigureAwait(false);
        }

        expectedSize = LimitToWindow(remaining);
        return await RestartAtAsync(size - remaining).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> RestartAtAsync(long offset)
    {
        context.Events.ReportInfo(FtpTransferMessages.ResumingFrom(offset));
        FtpReply rest = await ExchangeAsync("REST " + offset.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
        return rest.Code == 350 ? null : TransferResult.Failure(CurlExitCode.FtpCouldntUseRest, FtpTransferMessages.CouldNotUseRest);
    }

    /// <summary>
    /// The bytes from the window's offset to the end of a file of <paramref name="size" />
    /// bytes, or <see langword="null" /> when the offset lies past its end, or a suffix is
    /// longer than it.
    /// </summary>
    private long? ResolveRemaining(long size)
    {
        long remaining = window.Offset < 0 ? -window.Offset : size - window.Offset;
        return remaining < 0 || remaining > size ? null : remaining;
    }

    private long? LimitToWindow(long? count) =>
        count is { } known && window.MaxDownload is { } max ? Math.Min(known, max) : count;

    private async ValueTask<TransferResult> RetrieveAsync(string command, bool listing)
    {
        FtpReply opened = await ExchangeAsync(command).ConfigureAwait(false);
        if (opened.Code is 125 or 150)
        {
            log.TransferStarted(command);
            context.Events.ReportInfo(FtpTransferMessages.MaxDownload(window.MaxDownload));
            if (!listing)
            {
                context.Events.ReportInfo(FtpTransferMessages.GettingFile(expectedSize));
            }

            if (await ReadyDataConnectionAsync().ConfigureAwait(false) is { } notReady)
            {
                return notReady;
            }

            trace.TransferInitiated();
            return await CopyDataAsync().ConfigureAwait(false)
                ?? await (window.MaxDownload is null ? ReadTransferCompleteAsync() : EndRangeAsync()).ConfigureAwait(false);
        }

        return await RefuseRetrieveAsync(opened.Code, listing).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends a transfer whose <c>RETR</c>, <c>LIST</c> or <c>NLST</c> was not answered with
    /// 125 or 150: a <c>450</c> to a listing is an empty listing, a <c>550</c> to <c>RETR</c>
    /// exit 78, anything else exit 19. <c>QUIT</c> is sent in every case.
    /// </summary>
    private async ValueTask<TransferResult> RefuseRetrieveAsync(int code, bool listing)
    {
        if (listing && code == 450)
        {
            return await QuitAndSucceedAsync().ConfigureAwait(false);
        }

        CurlExitCode exitCode = !listing && code == 550 ? CurlExitCode.RemoteFileNotFound : CurlExitCode.FtpCouldntRetrFile;
        return await QuitAndFailAsync(exitCode, FtpTransferMessages.RetrieveRefused(code)).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> CopyDataAsync()
    {
        IConnection data = dataConnection!;
        byte[] buffer = new byte[DataReadBufferSize];
        while (!IsWindowRead() && !IsExpectedSizeRead())
        {
            int read;
            try
            {
                read = await data.ReadAsync(buffer.AsMemory(0, NextDataReadLength()), context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException failure)
            {
                return TransferResult.Failure(CurlExitCode.RecvError, FtpTransferMessages.ReceiveFailed(failure), bytesTransferred);
            }

            int wanted = CountWithinWindow(read);
            int allowed = CountWithinMaxFileSize(wanted);
            context.Events.ReportDataReceived(buffer.AsSpan(0, allowed));
            if (read == 0)
            {
                return null;
            }

            if (await WriteOutputAsync(buffer.AsMemory(0, allowed)).ConfigureAwait(false) is { } writeFailed)
            {
                return writeFailed;
            }

            bytesTransferred += allowed;
            context.Progress.ReportDownloaded(bytesTransferred, expectedSize);
            if (allowed < wanted)
            {
                return TransferResult.Failure(CurlExitCode.FilesizeExceeded, FtpTransferMessages.MaxFileSizeExceededWhileReading(maxFileSize!.Value, bytesTransferred), bytesTransferred);
            }
        }

        // Read to the announced size, curl shuts the data connection down and writes the empty
        // block, { [0 bytes data], an end of data would have written (measured, BL-1259 Notes).
        if (!IsWindowRead())
        {
            context.Events.ReportDataReceived([]);
        }

        return null;
    }

    /// <summary>
    /// Writes <paramref name="bytes" /> to the output: nothing at all, not an empty write,
    /// when there are none; exit 23 when the output refuses them.
    /// </summary>
    private async ValueTask<TransferResult?> WriteOutputAsync(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return null;
        }

        try
        {
            await context.Output.WriteAsync(bytes, context.CancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException exception)
        {
            int accepted = exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
            return TransferResult.Failure(CurlExitCode.WriteError, FtpTransferMessages.OutputWriteFailed(bytes.Length, accepted), bytesTransferred);
        }
    }

    /// <summary>
    /// The bytes of <paramref name="count" /> that still fit under <c>--max-filesize</c>,
    /// counted from this transfer's first byte: the whole count when there is no limit.
    /// </summary>
    private int CountWithinMaxFileSize(int count) =>
        maxFileSize is { } max ? (int)Math.Min(count, max - bytesTransferred) : count;

    /// <summary>
    /// Gets whether the window's byte limit has been written, so curl stops reading the
    /// data connection.
    /// </summary>
    private bool IsWindowRead() => window.MaxDownload is { } max && bytesTransferred >= max;

    /// <summary>
    /// Whether the size <c>SIZE</c> announced has been read: curl 8.21.0 reads no further, and a
    /// server that sends more is cut off there (measured, BL-1259 Notes).
    /// </summary>
    private bool IsExpectedSizeRead() => expectedSize is { } expected && bytesTransferred >= expected;

    /// <summary>
    /// The length of the next data read: the bytes still expected, at most curl's 102400-byte
    /// buffer, or the whole buffer when the size is unknown, as curl 8.21.0 sizes its reads and
    /// writes them under <c>--trace-config tcp</c> (measured, BL-1259 Notes).
    /// </summary>
    private int NextDataReadLength() =>
        expectedSize is { } expected ? (int)Math.Min(DataReadBufferSize, expected - bytesTransferred) : DataReadBufferSize;

    /// <summary>The bytes of a read of <paramref name="read" /> bytes that lie within the window.</summary>
    private int CountWithinWindow(int read) =>
        window.MaxDownload is { } max ? (int)Math.Min(read, max - bytesTransferred) : read;

    /// <summary>
    /// Ends a download that had a byte limit: exit 18 with no <c>QUIT</c> when the data
    /// fell short of what was expected, otherwise <c>ABOR</c>, whose reply curl reads
    /// without checking it, the post-transfer quotes and <c>QUIT</c>. As curl 8.21.0 does,
    /// the quotes run before the <c>--trace-config ftp</c> <c>done</c> line, which carries
    /// their result, and the <c>-v</c> line saying the connection is shut down (BL-1201).
    /// </summary>
    private async ValueTask<TransferResult> EndRangeAsync()
    {
        if (expectedSize is { } expected && bytesTransferred < expected)
        {
            return TransferResult.Failure(CurlExitCode.PartialFile, FtpTransferMessages.EndOfResponseWithBytesMissing(expected - bytesTransferred), bytesTransferred);
        }

        context.Events.ReportInfo(FtpTransferMessages.RememberingDirectory(rememberedDirectory));
        await AbortRangeAsync().ConfigureAwait(false);
        context.Events.ReportInfo(FtpTransferMessages.PartialDownloadClosing);
        (FtpQuoteCommand Command, int Code)? refused = await RunPostQuotesAsync().ConfigureAwait(false);
        context.Events.ReportInfo(FtpTransferMessages.ShuttingDownConnection(controlName.Number));
        return await QuitAfterPostQuotesAsync(refused).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the end-of-transfer reply, after curl 8.21.0's <c>-v</c> line naming the directory
    /// it remembers, and ends the transfer: exit 18 for missing bytes; exit 70 for <c>552</c>
    /// and exit 18 for any other reply but <c>226</c> or <c>250</c>, each after the <c>-v</c>
    /// line saying the control connection is left intact and <c>QUIT</c>; otherwise the
    /// post-transfer quotes, <c>QUIT</c> and, for a success, that left-intact line.
    /// </summary>
    private async ValueTask<TransferResult> ReadTransferCompleteAsync()
    {
        context.Events.ReportInfo(FtpTransferMessages.RememberingDirectory(rememberedDirectory));
        trace.ClosingDataConnection();
        FtpReply complete = await ReadReplyAsync(FtpTransferMessages.ControlConnectionLooksDead).ConfigureAwait(false);
        trace.TransferReplyRead(complete);
        if (expectedSize is { } expected && bytesTransferred < expected)
        {
            return TransferResult.Failure(CurlExitCode.PartialFile, FtpTransferMessages.ClosedWithBytesRemaining(expected - bytesTransferred), bytesTransferred);
        }

        if (complete.Code is not (226 or 250))
        {
            (CurlExitCode exitCode, string message) = DescribeTransferNotOk(complete.Code);
            return await QuitAndFailLeavingConnectionIntactAsync(exitCode, message).ConfigureAwait(false);
        }

        return await QuitAndKeepConnectionAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The exit code and message of curl 8.21.0's <c>ftp_done</c> for an end-of-transfer
    /// reply other than <c>226</c> or <c>250</c>: exit 70 for <c>552</c>, otherwise exit 18.
    /// </summary>
    private static (CurlExitCode ExitCode, string Message) DescribeTransferNotOk(int code) =>
        code == 552
            ? (CurlExitCode.RemoteDiskFull, FtpTransferMessages.StorageAllocationExceeded)
            : (CurlExitCode.PartialFile, FtpTransferMessages.TransferNotOk(code));

    /// <summary>
    /// Ends a transfer the server reported complete as <see cref="QuitAndSucceedAsync" /> does,
    /// then, for a success, reports curl 8.21.0's <c>-v</c> line saying the control connection
    /// is left intact.
    /// </summary>
    private async ValueTask<TransferResult> QuitAndKeepConnectionAsync()
    {
        TransferResult ended = await QuitAndSucceedAsync().ConfigureAwait(false);
        if (ended.IsSuccess)
        {
            context.Events.ReportInfo(FtpTransferMessages.ConnectionLeftIntact(controlName.Number, controlName.Host, controlName.Port));
        }

        return ended;
    }

    /// <summary>
    /// Sends <c>QUIT</c> and fails the transfer after curl 8.21.0's <c>-v</c> lines naming the
    /// directory it remembers and saying the control connection is left intact, as measured
    /// for exits 13, 17, 30, 36, 63 and 78 (BL-1240, BL-1251). Every caller passes a failure
    /// curl's <c>ftp_done</c> says leaves the control connection "alive fine": exits 9, 10,
    /// 12, 13, 17, 18, 19, 23, 25, 30, 36, 63 and 78; any other failure marks it invalid,
    /// and its <c>-v</c> lines are not these.
    /// </summary>
    private async ValueTask<TransferResult> QuitAndFailAsync(CurlExitCode exitCode, string message)
    {
        context.Events.ReportInfo(FtpTransferMessages.RememberingDirectory(rememberedDirectory));
        return await QuitAndFailLeavingConnectionIntactAsync(exitCode, message).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>QUIT</c> and fails the transfer after curl 8.21.0's <c>-v</c> line saying the
    /// control connection is left intact, with no line naming the directory: after a refused
    /// <c>CWD</c> and after an active-mode port that cannot be bound, for which curl names no
    /// directory, and after an end-of-transfer reply other than <c>226</c> or <c>250</c>,
    /// whose directory line came before the reply (measured for exits 9, 18, 30 and 70, BL-1251).
    /// </summary>
    private async ValueTask<TransferResult> QuitAndFailLeavingConnectionIntactAsync(CurlExitCode exitCode, string message)
    {
        ReportUnalignedUpload();
        context.Events.ReportInfo(FtpTransferMessages.ConnectionLeftIntact(controlName.Number, controlName.Host, controlName.Port));
        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Failure(exitCode, message, bytesTransferred);
    }

    /// <summary>
    /// Reports curl 8.21.0's <c>Uploaded unaligned file size</c> <c>-v</c> line, which its
    /// <c>ftp_done_check_partial</c> writes for an upload of a known size whose sent bytes
    /// differ from that size - or, under <c>--crlf</c>, which may add bytes, fall short of it
    /// (BL-1395). Every failure that ends here is one <c>ftp_done</c> maps to OK first.
    /// </summary>
    private void ReportUnalignedUpload()
    {
        if (uploadSize is { } size && (context.ConvertLineEndings ? bytesTransferred < size : bytesTransferred != size))
        {
            context.Events.ReportInfo(FtpTransferMessages.UploadedUnalignedFileSize(bytesTransferred, size));
        }
    }

    /// <summary>
    /// Ends a transfer that succeeded: the post-transfer quotes, then <c>QUIT</c>. A refused
    /// quote is exit 21, <c>QUIT</c> still sent.
    /// </summary>
    private async ValueTask<TransferResult> QuitAndSucceedAsync() =>
        await QuitAfterPostQuotesAsync(await RunPostQuotesAsync().ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Sends the post-transfer quotes and writes the <c>--trace-config ftp</c> <c>done</c>
    /// line with their result: 21 when one was refused, otherwise 0.
    /// </summary>
    /// <returns>The refused quote and its reply's code, or <see langword="null" /> when none was refused.</returns>
    private async ValueTask<(FtpQuoteCommand Command, int Code)?> RunPostQuotesAsync()
    {
        (FtpQuoteCommand Command, int Code)? refused = await FindRefusedQuoteAsync(quotes.AfterTransfer, FtpQuoteStage.AfterTransfer).ConfigureAwait(false);
        trace.Ended(refused is null ? CurlExitCode.Ok : CurlExitCode.QuoteError);
        return refused;
    }

    /// <summary>
    /// Sends <c>QUIT</c> and ends the transfer: exit 21 naming <paramref name="refused" />
    /// when a post-transfer quote was refused, otherwise a success.
    /// </summary>
    private async ValueTask<TransferResult> QuitAfterPostQuotesAsync((FtpQuoteCommand Command, int Code)? refused)
    {
        await QuitAsync().ConfigureAwait(false);
        return refused is { } quote
            ? TransferResult.Failure(CurlExitCode.QuoteError, FtpTransferMessages.QuoteNotAccepted(quote.Command.Command), bytesTransferred)
            : TransferResult.Success(bytesTransferred);
    }

    /// <summary>
    /// Sends <c>ABOR</c> when the window has a byte limit, as curl does once the outcome is
    /// decided.
    /// </summary>
    private async ValueTask AbortRangeAsync()
    {
        if (window.MaxDownload is not null && await SendIgnoringReplyAsync("ABOR", trace.ClosingDataConnection).ConfigureAwait(false) is { } aborted)
        {
            trace.TransferReplyRead(aborted);
            lastReplyCode = aborted.Code;
        }
    }

    private async ValueTask<TransferResult> EndAndSucceedAsync()
    {
        await AbortRangeAsync().ConfigureAwait(false);
        return await QuitAndSucceedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Fails a download refused once its size is known (exit 36 or 63). Without a byte limit
    /// it ends as <see cref="QuitAndFailAsync" /> does; with one, as curl 8.21.0 was measured to
    /// (BL-1251): the <c>-v</c> line naming the directory it remembers, <c>ABOR</c>, the lines
    /// saying the partial download closes and shuts down the connection, then <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult> EndAndFailAsync(CurlExitCode exitCode, string message)
    {
        if (window.MaxDownload is null)
        {
            return await QuitAndFailAsync(exitCode, message).ConfigureAwait(false);
        }

        context.Events.ReportInfo(FtpTransferMessages.RememberingDirectory(rememberedDirectory));
        await AbortRangeAsync().ConfigureAwait(false);
        context.Events.ReportInfo(FtpTransferMessages.PartialDownloadClosing);
        context.Events.ReportInfo(FtpTransferMessages.ShuttingDownConnection(controlName.Number));
        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Failure(exitCode, message, bytesTransferred);
    }

    /// <summary>
    /// Sends the quotes due before the transfer: the first answered with 400 or more whose
    /// failure is not ignored ends the transfer with exit 21 and no <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult?> SendQuotesAsync(IReadOnlyList<FtpQuoteCommand> commands, FtpQuoteStage stage) =>
        await FindRefusedQuoteAsync(commands, stage).ConfigureAwait(false) is { } refused
            ? TransferResult.Failure(CurlExitCode.QuoteError, FtpTransferMessages.QuoteCommandFailed(refused.Code), bytesTransferred)
            : null;

    /// <summary>
    /// Sends each of <paramref name="commands" /> in turn, stopping at the first answered
    /// with 400 or more whose failure is not ignored.
    /// </summary>
    /// <returns>That command and its reply's code, or <see langword="null" /> when none was refused.</returns>
    private async ValueTask<(FtpQuoteCommand Command, int Code)?> FindRefusedQuoteAsync(IReadOnlyList<FtpQuoteCommand> commands, FtpQuoteStage stage)
    {
        foreach (FtpQuoteCommand quote in commands)
        {
            await SendAsync(quote.Command).ConfigureAwait(false);
            trace.QuoteSent(stage);
            FtpReply reply = await ReadReplyAsync().ConfigureAwait(false);
            trace.QuoteReplyRead(stage, reply);
            if (reply.Code >= 400)
            {
                if (!quote.IgnoreFailure)
                {
                    return (quote, reply.Code);
                }

                log.QuoteFailureIgnored(quote.Command, reply.Code);
            }
        }

        return null;
    }

    /// <summary>
    /// Sends <c>QUIT</c>, whose reply curl 8.21.0 never reports as <c>%{response_code}</c>;
    /// neither it nor its reply is reported to <c>-v</c> or <c>--trace</c> (BL-930).
    /// </summary>
    private async ValueTask QuitAsync()
    {
        control.StopReporting();
        await SendIgnoringReplyAsync("QUIT").ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <paramref name="command" /> and reads one reply, ignoring whatever goes wrong:
    /// the transfer's outcome is already decided.
    /// </summary>
    /// <returns>
    /// The reply, or <see langword="null" /> when the command could not be sent or no
    /// complete reply of a readable size arrived.
    /// </returns>
    private async ValueTask<FtpReply?> SendIgnoringReplyAsync(string command, Action? afterSent = null)
    {
        if (await control.SendAsync(command).ConfigureAwait(false) is not null)
        {
            return null;
        }

        afterSent?.Invoke();

        try
        {
            return await control.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            // An oversized reply to ABOR or QUIT changes nothing.
            return null;
        }
        catch (FtpReplyNulByteException)
        {
            // Nor does one holding a NUL byte: curl's ftp_quit ends the connection on any read error.
            return null;
        }
        catch (FtpReplyLineWriteException)
        {
            // Nor does ABOR's reply refused by the -D stream; QUIT's is never written there.
            return null;
        }
    }

    private ValueTask<FtpReply> ExchangeAsync(string command) => ExchangeAsync(command, afterSent: null);

    /// <summary>
    /// Sends <paramref name="command" />, reports <paramref name="afterSent" /> as a <c>-v</c>
    /// line before the reply is read, as curl 8.21.0 does after <c>EPSV</c>, and reads the reply.
    /// </summary>
    private async ValueTask<FtpReply> ExchangeAsync(string command, string? afterSent)
    {
        await SendAsync(command).ConfigureAwait(false);
        trace.Sent(command);
        ReportInfo(afterSent);
        trace.AwaitingReply();
        return await ReadReplyAsync().ConfigureAwait(false);
    }

    /// <summary>Sends <paramref name="command" />, ending the conversation with exit 55 when it cannot be sent.</summary>
    private async ValueTask SendAsync(string command)
    {
        if (await control.SendAsync(command).ConfigureAwait(false) is { } failure)
        {
            throw Failed(CurlExitCode.SendError, FtpTransferMessages.SendFailed(failure));
        }
    }

    /// <summary>
    /// Reads the next reply, ending the conversation for a closed connection (exit 56), an
    /// oversized line (exit 100), a line holding a NUL byte (exit 8), or a <c>421</c>, which curl reports as exit 28 with
    /// <paramref name="closingMessage" /> and no <c>QUIT</c>.
    /// </summary>
    private async ValueTask<FtpReply> ReadReplyAsync(string closingMessage = FtpTransferMessages.TimeoutReached)
    {
        FtpReply? reply;
        try
        {
            reply = await control.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (FtpReplyNulByteException)
        {
            throw Failed(CurlExitCode.WeirdServerReply, FtpTransferMessages.NulByteInReplyLine);
        }
        catch (InvalidDataException)
        {
            throw Failed(CurlExitCode.TooLarge, FtpTransferMessages.ReplyLineTooLarge);
        }
        catch (FtpReplyLineWriteException refused)
        {
            throw Failed(CurlExitCode.WriteError, refused.Message);
        }

        if (reply is null)
        {
            throw Failed(CurlExitCode.RecvError, FtpTransferMessages.ResponseReadingFailed);
        }

        lastReplyCode = reply.Code;
        if (reply.Code == 421)
        {
            context.Events.ReportInfo(FtpTransferMessages.Got421Timeout);
            throw Failed(CurlExitCode.OperationTimedOut, closingMessage);
        }

        return reply;
    }

    private FtpControlConversationFailedException Failed(CurlExitCode exitCode, string message) =>
        new(TransferResult.Failure(exitCode, message, bytesTransferred));
}
