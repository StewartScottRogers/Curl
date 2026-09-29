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
/// <param name="context">The transfer being performed.</param>
/// <param name="implicitTls">
/// <see langword="true" /> for <c>ftps://</c>, whose control connection is TLS from its first
/// byte, so no <c>AUTH</c> is sent.
/// </param>
/// <param name="connectPhase">
/// Holds the greeting, the login and <c>PWD</c> to <c>--connect-timeout</c>, as curl holds its
/// states before <c>DO</c> (BL-512); the caller owns it.
/// </param>
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
/// with no <c>QUIT</c> under <c>--ssl-reqd</c>, plaintext data otherwise.
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
/// with a 2xx), <c>PWD</c>, one <c>CWD</c> per directory in the path, <c>EPSV</c> (and
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
/// sends nothing more. ADR-0093's BL-438 addendum records the measurements.
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
/// other than <c>226</c> or <c>250</c> exit 18. ADR-0093's BL-439 addendum records the
/// measurements.
/// </para>
/// <para>
/// The FTP control options change that conversation as ADR-0093's BL-436 addendum records:
/// <c>--disable-epsv</c> goes straight to <c>PASV</c>; <c>--no-ftp-skip-pasv-ip</c>
/// connects to the address a <c>227</c> reply names; <c>--ftp-method</c> picks the
/// <c>CWD</c>s through <see cref="FtpUrlPath" />; <c>--ftp-create-dirs</c> answers a refused
/// <c>CWD</c> with <c>MKD</c> and one more <c>CWD</c>; <c>-l</c> lists with <c>NLST</c>,
/// even for a file URL; and <c>-Q</c> commands are sent after <c>PWD</c>, after
/// <c>TYPE</c> (<c>+</c>) or after a successful transfer (<c>-</c>), as
/// <see cref="FtpQuoteCommands" /> sorts them. A refused quote is exit 21: before the
/// transfer with no <c>QUIT</c>, after it with <c>QUIT</c>.
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
    ITransferContext context,
    bool implicitTls,
    FtpConnectPhaseLimit connectPhase)
    : IAsyncDisposable
{
    private const string AnonymousUser = "anonymous";

    private const string AnonymousPassword = "ftp@example.com";

    private const int ReadBufferSize = 16384;

    /// <summary>
    /// How long curl 8.21.0 waits for the server to open an active-mode data connection,
    /// measured with <c>-P -</c> whatever <c>--connect-timeout</c> says.
    /// </summary>
    private static readonly TimeSpan AcceptTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The <c>AUTH</c> mechanisms curl 8.21.0 offers, in the order it offers them.</summary>
    private static readonly string[] AuthMechanisms = ["SSL", "TLS"];

    private readonly FtpQuoteCommands quotes = FtpQuoteCommands.Parse(context.QuoteCommands);

    private readonly FtpTlsRequirement tlsRequirement = FtpTlsRequirements.Of(context);

    /// <summary>The control connection's own address, which <c>-P -</c> listens on.</summary>
    private readonly EndPoint? controlLocalEndPoint = control.Connection.LocalEndPoint;

    /// <summary>Whether the control connection is TLS: from the start for <c>ftps://</c>, or after <c>AUTH</c>.</summary>
    private bool controlSecured = implicitTls;

    /// <summary>The control connection <c>AUTH</c> upgraded to TLS, which the session owns.</summary>
    private IConnection? securedControl;

    /// <summary>Whether an accepted <c>PROT P</c> makes every data connection TLS.</summary>
    private bool protectData;

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
            return await GreetAndLogInAsync().ConfigureAwait(false)
                ?? await ProtectDataAsync().ConfigureAwait(false)
                ?? await ReadEntryPathAsync().ConfigureAwait(false);
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
    /// knows no server system yet (BL-782).
    /// </summary>
    private async ValueTask<TransferResult?> ReadEntryPathAsync(bool askSystemForRelativePath)
    {
        if (!FtpEntryPath.TryRead(await ExchangeAsync("PWD").ConfigureAwait(false), out entryPath))
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, FtpTransferMessages.WeirdServerReply);
        }

        return askSystemForRelativePath && entryPath?.StartsWith('/') == false
            ? await AskServerSystemAsync().ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Sends <c>SYST</c>, whose refusal curl carries on past. A <c>215</c> naming
    /// <c>OS/400</c> sends <c>SITE NAMEFMT 1</c>, and a 2xx to that sends <c>PWD</c> again
    /// for the entry path in the new name format, with no second <c>SYST</c>.
    /// </summary>
    private async ValueTask<TransferResult?> AskServerSystemAsync()
    {
        FtpReply system = await ExchangeAsync("SYST").ConfigureAwait(false);
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
        FtpReply greeting = await ReadReplyAsync().ConfigureAwait(false);
        return greeting.Code switch
        {
            230 => null,
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

        return tlsRequirement == FtpTlsRequirement.Try
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, FtpTransferMessages.RequestedSslLevelFailed);
    }

    /// <summary>
    /// Runs the TLS handshake over the control connection and carries on over the secured
    /// connection; a failed handshake ends the session with its exit code and no <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult?> UpgradeControlAsync()
    {
        ConnectResult secured = await connections.TlsProvider
            .AuthenticateAsClientAsync(control.Connection, context.Url.IdnHost, control.CancellationToken)
            .ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            return TransferResult.Failure(secured.ExitCode, secured.ErrorMessage!);
        }

        securedControl = connection;
        control.SwitchTo(connection);
        controlSecured = true;
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
        return prot.IsCompletion || tlsRequirement != FtpTlsRequirement.AllConnections
            ? null
            : TransferResult.Failure(CurlExitCode.UseSslFailed, FtpTransferMessages.RequestedSslLevelFailed);
    }

    private async ValueTask<TransferResult?> LogInAsync()
    {
        FtpReply user = await ExchangeAsync("USER " + (context.Credentials?.UserName ?? AnonymousUser)).ConfigureAwait(false);
        if (user.IsCompletion)
        {
            return null;
        }

        return user.Code == 331
            ? await SendPasswordAsync().ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.LoginDenied, FtpTransferMessages.AccessDenied(user.Code));
    }

    private async ValueTask<TransferResult?> SendPasswordAsync()
    {
        FtpReply pass = await ExchangeAsync("PASS " + (context.Credentials?.Password ?? AnonymousPassword)).ConfigureAwait(false);
        if (pass.IsCompletion)
        {
            return null;
        }

        return TransferResult.Failure(
            CurlExitCode.LoginDenied,
            pass.Code == 332 ? FtpTransferMessages.AccountRequested : FtpTransferMessages.AccessDenied(pass.Code));
    }

    private async ValueTask<TransferResult> TransferPathAsync()
    {
        if (FtpUrlPath.Parse(context.Url.AbsolutePath, context.FtpFileMethod) is not { } path)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, FtpTransferMessages.PathHasControlCharacters);
        }

        return context.Upload is { } upload
            ? await UploadAsync(path, upload).ConfigureAwait(false)
            : await RetrieveFromPathAsync(path).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> RetrieveFromPathAsync(FtpUrlPath path)
    {
        return await SendQuotesAsync(quotes.AfterLogin).ConfigureAwait(false)
            ?? await ChangeDirectoriesAsync(path.Directories).ConfigureAwait(false)
            ?? await CheckModificationTimeAsync(path.FileName).ConfigureAwait(false)
            ?? (context.NoBody
                ? await ReportHeadAsync(path.FileName).ConfigureAwait(false)
                : await DownloadAsync(path).ConfigureAwait(false));
    }

    /// <summary>
    /// Downloads the file, or the listing when the path names a directory or <c>-l</c> asks
    /// for one. The window <c>-r</c> or <c>-C</c> asks for applies to a file only.
    /// </summary>
    private async ValueTask<TransferResult> DownloadAsync(FtpUrlPath path)
    {
        bool listing = path.FileName.Length == 0 || context.ListOnly;
        window = listing ? default : FtpDownloadWindow.Of(context);
        return await OpenDataConnectionAsync().ConfigureAwait(false)
            ?? await SetTypeAsync(listing).ConfigureAwait(false)
            ?? await SendQuotesAsync(quotes.BeforeTransfer).ConfigureAwait(false)
            ?? await ReadSizeAsync(path.FileName, listing).ConfigureAwait(false)
            ?? await RefuseOversizedFileAsync().ConfigureAwait(false)
            ?? await PositionAsync().ConfigureAwait(false)
            ?? await RetrieveAsync(listing ? ListCommand(path) : "RETR " + path.FileName, listing).ConfigureAwait(false);
    }

    /// <summary>
    /// The listing command: <c>NLST</c> under <c>-l</c>, <c>LIST</c> otherwise, with the
    /// directory as its argument under <c>--ftp-method nocwd</c>.
    /// </summary>
    private string ListCommand(FtpUrlPath path)
    {
        string verb = context.ListOnly ? "NLST" : "LIST";
        return path.ListArgument is { } argument ? verb + " " + argument : verb;
    }

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

        return await SendQuotesAsync(quotes.AfterLogin).ConfigureAwait(false)
            ?? await ChangeDirectoriesAsync(path.Directories).ConfigureAwait(false)
            ?? await CheckModificationTimeAsync(path.FileName).ConfigureAwait(false)
            ?? await OpenDataConnectionAsync().ConfigureAwait(false)
            ?? await SetTypeAsync(false).ConfigureAwait(false)
            ?? await SendQuotesAsync(quotes.BeforeTransfer).ConfigureAwait(false)
            ?? await StoreAsync(path.FileName, upload).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>STOR</c> from offset 0, or skips the <c>-C</c> offset and sends <c>APPE</c>;
    /// an offset that covers the whole upload sends nothing more than <c>QUIT</c> and succeeds.
    /// </summary>
    private async ValueTask<TransferResult> StoreAsync(string fileName, Stream upload)
    {
        long offset = context.ResumeUploadFromUnknownOffset
            ? await ReadRemoteSizeAsync(fileName).ConfigureAwait(false)
            : context.ResumeFrom ?? 0;
        if (offset <= 0)
        {
            return await SendUploadAsync("STOR " + fileName, upload).ConfigureAwait(false);
        }

        return FtpUploadOffset.TrySkip(upload, offset)
            ? await SendUploadAsync("APPE " + fileName, upload).ConfigureAwait(false)
            : await QuitAndSucceedAsync().ConfigureAwait(false);
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
        FtpReply opened = await ExchangeAsync(command).ConfigureAwait(false);
        if (opened.Code >= 400)
        {
            return await QuitAndFailAsync(CurlExitCode.UploadFailed, FtpTransferMessages.UploadRefused(opened.Code)).ConfigureAwait(false);
        }

        if (await ReadyDataConnectionAsync().ConfigureAwait(false) is { } notReady)
        {
            return notReady;
        }

        return await CopyUploadAsync(upload).ConfigureAwait(false)
            ?? await ReadTransferCompleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Copies the upload to the data connection and closes it, which tells the server the
    /// file has ended. A failed read ends the upload as the end of the source does, as curl
    /// takes it; a failed write is exit 55.
    /// </summary>
    private async ValueTask<TransferResult?> CopyUploadAsync(Stream upload)
    {
        IConnection data = dataConnection!;
        long? expected = upload.CanSeek ? Math.Max(0, upload.Length - upload.Position) : null;
        byte[] buffer = new byte[ReadBufferSize];
        int read;
        while ((read = await ReadUploadAsync(upload, buffer).ConfigureAwait(false)) > 0)
        {
            try
            {
                await data.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken).ConfigureAwait(false);
                await data.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.SendError, FtpTransferMessages.SendFailed, bytesTransferred);
            }

            bytesTransferred += read;
            context.Progress.ReportUploaded(bytesTransferred, expected);
        }

        dataConnection = null;
        await data.DisposeAsync().ConfigureAwait(false);
        return null;
    }

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
            return await SendQuotesAsync(quotes.BeforeTransfer).ConfigureAwait(false)
                ?? await QuitAndSucceedAsync().ConfigureAwait(false);
        }

        return await SetTypeAsync(false).ConfigureAwait(false)
            ?? await ReadSizeAsync(fileName, false).ConfigureAwait(false)
            ?? await WriteHeaderAsync(fileSize is { } size ? FtpHeadHeaderLines.ContentLength(size) : null).ConfigureAwait(false)
            ?? await ReportRestAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> ReportRestAsync()
    {
        FtpReply rest = await ExchangeAsync("REST 0").ConfigureAwait(false);
        return await WriteHeaderAsync(rest.Code == 350 ? FtpHeadHeaderLines.AcceptRanges : null).ConfigureAwait(false)
            ?? await SendQuotesAsync(quotes.BeforeTransfer).ConfigureAwait(false)
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
                return await QuitAndFailAsync(CurlExitCode.RemoteAccessDenied, FtpTransferMessages.ChangeDirectoryDenied).ConfigureAwait(false);
            }
        }

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
        return (await ExchangeAsync("CWD " + directory).ConfigureAwait(false)).IsCompletion;
    }

    /// <summary>
    /// Prepares the data connection: under <c>-P</c> a listening port announced with
    /// <c>EPRT</c> or <c>PORT</c>, otherwise a passive connection.
    /// </summary>
    private async ValueTask<TransferResult?> OpenDataConnectionAsync() =>
        context.FtpPort is { } ftpPort
            ? await AnnounceActivePortAsync(FtpPortArgument.Parse(ftpPort)).ConfigureAwait(false)
            : await OpenPassiveDataConnectionAsync().ConfigureAwait(false);

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
                return null;
            }

            await ReleasePendingConnectionAsync().ConfigureAwait(false);
        }

        return ipv6
            ? await QuitAndFailAsync(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort).ConfigureAwait(false)
            : await ListenAsync(address, argument).ConfigureAwait(false) ?? await SendPortAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> SendPortAsync() =>
        (await ExchangeAsync(FtpActiveCommand.Port(ListeningEndPoint)).ConfigureAwait(false)).IsCompletion
            ? null
            : await QuitAndFailAsync(CurlExitCode.FtpPortFailed, FtpTransferMessages.FailedToDoPort).ConfigureAwait(false);

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
        return pendingConnection is null
            ? await QuitAndFailAsync(listening.ExitCode, FtpTransferMessages.BindFailed(listening.ErrorMessage!)).ConfigureAwait(false)
            : null;
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
    /// Makes the data connection ready once the transfer command is answered: accepts the
    /// server's connection in active mode, then runs the TLS handshake over it after an
    /// accepted <c>PROT P</c>.
    /// </summary>
    private async ValueTask<TransferResult?> ReadyDataConnectionAsync() =>
        await AcceptDataConnectionAsync().ConfigureAwait(false)
            ?? await SecureDataConnectionAsync().ConfigureAwait(false);

    /// <summary>
    /// Waits up to 60 seconds for the server to connect to the active-mode port: exit 12
    /// after <c>QUIT</c> when it does not, and a failed accept's exit code after <c>QUIT</c>.
    /// Nothing to do in passive mode.
    /// </summary>
    private async ValueTask<TransferResult?> AcceptDataConnectionAsync()
    {
        if (pendingConnection is not { } pending)
        {
            return null;
        }

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
        return dataConnection is null
            ? await QuitAndFailAsync(accepted.ExitCode, accepted.ErrorMessage!).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Runs the TLS handshake over the data connection after an accepted <c>PROT P</c>; a
    /// failed one ends the transfer with its exit code and no <c>QUIT</c>, as a failed
    /// passive connect does.
    /// </summary>
    private async ValueTask<TransferResult?> SecureDataConnectionAsync()
    {
        if (!protectData)
        {
            return null;
        }

        ConnectResult secured = await connections.TlsProvider
            .AuthenticateAsClientAsync(dataConnection!, context.Url.IdnHost, context.CancellationToken)
            .ConfigureAwait(false);
        dataConnection = secured.Connection;
        return dataConnection is null
            ? TransferResult.Failure(secured.ExitCode, secured.ErrorMessage!, bytesTransferred)
            : null;
    }

    /// <summary>
    /// Opens a passive data connection: <c>EPSV</c> first unless <c>--disable-epsv</c>, then
    /// <c>PASV</c> when <c>EPSV</c> was skipped or answered with anything but <c>229</c>.
    /// </summary>
    private async ValueTask<TransferResult?> OpenPassiveDataConnectionAsync()
    {
        if (!context.FtpDisableEpsv && await ExchangeAsync("EPSV").ConfigureAwait(false) is { Code: 229 } epsv)
        {
            return FtpPassiveReply.TryParseEpsvPort(epsv.LastLine, out int epsvPort)
                ? await ConnectDataAsync(context.Url.IdnHost, epsvPort).ConfigureAwait(false)
                : await QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, FtpTransferMessages.WeirdEpsvReply).ConfigureAwait(false);
        }

        return await EnterPassiveModeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>PASV</c> and connects to the port its <c>227</c> reply names, on the control
    /// connection's host, or under <c>--no-ftp-skip-pasv-ip</c> on the address it names.
    /// </summary>
    private async ValueTask<TransferResult?> EnterPassiveModeAsync()
    {
        FtpReply pasv = await ExchangeAsync("PASV").ConfigureAwait(false);
        if (pasv.Code != 227)
        {
            return await QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, FtpTransferMessages.BadPassiveReply(pasv.Code)).ConfigureAwait(false);
        }

        return FtpPassiveReply.TryParsePasv(pasv.LastLine, out string address, out int pasvPort)
            ? await ConnectDataAsync(context.FtpSkipPasvIp ? context.Url.IdnHost : address, pasvPort).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.FtpWeird227Format, FtpTransferMessages.Weird227Reply);
    }

    private async ValueTask<TransferResult?> ConnectDataAsync(string host, int port)
    {
        var target = new ConnectTarget(host, port, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
        };
        ConnectResult connected = await connections.Connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        dataConnection = connected.Connection;
        return dataConnection is null
            ? new TransferResult(connected.ExitCode, 0, connected.ErrorMessage) { IsConnectionRefused = connected.IsConnectionRefused }
            : null;
    }

    private async ValueTask<TransferResult?> SetTypeAsync(bool listing)
    {
        FtpReply type = await ExchangeAsync(listing ? "TYPE A" : "TYPE I").ConfigureAwait(false);
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

        return await (fileSize is { } size ? PositionWithinSizeAsync(size) : RestartAtAsync(window.Offset)).ConfigureAwait(false);
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
            return await EndAndSucceedAsync().ConfigureAwait(false);
        }

        expectedSize = LimitToWindow(remaining);
        return await RestartAtAsync(size - remaining).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> RestartAtAsync(long offset)
    {
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
            if (await ReadyDataConnectionAsync().ConfigureAwait(false) is { } notReady)
            {
                return notReady;
            }

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
        byte[] buffer = new byte[ReadBufferSize];
        while (!IsWindowRead())
        {
            int read;
            try
            {
                read = await data.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.RecvError, FtpTransferMessages.ReceiveFailed, bytesTransferred);
            }

            if (read == 0)
            {
                return null;
            }

            int wanted = CountWithinWindow(read);
            int allowed = CountWithinMaxFileSize(wanted);
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

    /// <summary>The bytes of a read of <paramref name="read" /> bytes that lie within the window.</summary>
    private int CountWithinWindow(int read) =>
        window.MaxDownload is { } max ? (int)Math.Min(read, max - bytesTransferred) : read;

    /// <summary>
    /// Ends a download that had a byte limit: exit 18 with no <c>QUIT</c> when the data
    /// fell short of what was expected, otherwise <c>ABOR</c>, whose reply curl reads
    /// without checking it, the post-transfer quotes and <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult> EndRangeAsync()
    {
        if (expectedSize is { } expected && bytesTransferred < expected)
        {
            return TransferResult.Failure(CurlExitCode.PartialFile, FtpTransferMessages.EndOfResponseWithBytesMissing(expected - bytesTransferred), bytesTransferred);
        }

        return await EndAndSucceedAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> ReadTransferCompleteAsync()
    {
        FtpReply complete = await ReadReplyAsync(FtpTransferMessages.ControlConnectionLooksDead).ConfigureAwait(false);
        if (expectedSize is { } expected && bytesTransferred < expected)
        {
            return TransferResult.Failure(CurlExitCode.PartialFile, FtpTransferMessages.ClosedWithBytesRemaining(expected - bytesTransferred), bytesTransferred);
        }

        if (complete.Code is not (226 or 250))
        {
            return await QuitAndFailAsync(CurlExitCode.PartialFile, FtpTransferMessages.TransferNotOk(complete.Code)).ConfigureAwait(false);
        }

        return await QuitAndSucceedAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> QuitAndFailAsync(CurlExitCode exitCode, string message)
    {
        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Failure(exitCode, message, bytesTransferred);
    }

    /// <summary>
    /// Ends a transfer that succeeded: the post-transfer quotes, then <c>QUIT</c>. A refused
    /// quote is exit 21, <c>QUIT</c> still sent.
    /// </summary>
    private async ValueTask<TransferResult> QuitAndSucceedAsync()
    {
        (FtpQuoteCommand Command, int Code)? refused = await FindRefusedQuoteAsync(quotes.AfterTransfer).ConfigureAwait(false);
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
        if (window.MaxDownload is not null && await SendIgnoringReplyAsync("ABOR").ConfigureAwait(false) is { } aborted)
        {
            lastReplyCode = aborted.Code;
        }
    }

    private async ValueTask<TransferResult> EndAndSucceedAsync()
    {
        await AbortRangeAsync().ConfigureAwait(false);
        return await QuitAndSucceedAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> EndAndFailAsync(CurlExitCode exitCode, string message)
    {
        await AbortRangeAsync().ConfigureAwait(false);
        return await QuitAndFailAsync(exitCode, message).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the quotes due before the transfer: the first answered with 400 or more whose
    /// failure is not ignored ends the transfer with exit 21 and no <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult?> SendQuotesAsync(IReadOnlyList<FtpQuoteCommand> commands) =>
        await FindRefusedQuoteAsync(commands).ConfigureAwait(false) is { } refused
            ? TransferResult.Failure(CurlExitCode.QuoteError, FtpTransferMessages.QuoteCommandFailed(refused.Code), bytesTransferred)
            : null;

    /// <summary>
    /// Sends each of <paramref name="commands" /> in turn, stopping at the first answered
    /// with 400 or more whose failure is not ignored.
    /// </summary>
    /// <returns>That command and its reply's code, or <see langword="null" /> when none was refused.</returns>
    private async ValueTask<(FtpQuoteCommand Command, int Code)?> FindRefusedQuoteAsync(IReadOnlyList<FtpQuoteCommand> commands)
    {
        foreach (FtpQuoteCommand quote in commands)
        {
            FtpReply reply = await ExchangeAsync(quote.Command).ConfigureAwait(false);
            if (reply.Code >= 400 && !quote.IgnoreFailure)
            {
                return (quote, reply.Code);
            }
        }

        return null;
    }

    /// <summary>
    /// Sends <c>QUIT</c>, whose reply curl 8.21.0 never reports as <c>%{response_code}</c>.
    /// </summary>
    private async ValueTask QuitAsync() => await SendIgnoringReplyAsync("QUIT").ConfigureAwait(false);

    /// <summary>
    /// Sends <paramref name="command" /> and reads one reply, ignoring whatever goes wrong:
    /// the transfer's outcome is already decided.
    /// </summary>
    /// <returns>
    /// The reply, or <see langword="null" /> when the command could not be sent or no
    /// complete reply of a readable size arrived.
    /// </returns>
    private async ValueTask<FtpReply?> SendIgnoringReplyAsync(string command)
    {
        if (!await control.TrySendAsync(command).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return await control.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            // An oversized reply to ABOR or QUIT changes nothing.
            return null;
        }
    }

    private async ValueTask<FtpReply> ExchangeAsync(string command)
    {
        if (!await control.TrySendAsync(command).ConfigureAwait(false))
        {
            throw Failed(CurlExitCode.SendError, FtpTransferMessages.SendFailed);
        }

        return await ReadReplyAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the next reply, ending the conversation for a closed connection (exit 56), an
    /// oversized line (exit 100), or a <c>421</c>, which curl reports as exit 28 with
    /// <paramref name="closingMessage" /> and no <c>QUIT</c>.
    /// </summary>
    private async ValueTask<FtpReply> ReadReplyAsync(string closingMessage = FtpTransferMessages.TimeoutReached)
    {
        FtpReply? reply;
        try
        {
            reply = await control.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            throw Failed(CurlExitCode.TooLarge, FtpTransferMessages.ReplyLineTooLarge);
        }

        if (reply is null)
        {
            throw Failed(CurlExitCode.RecvError, FtpTransferMessages.ResponseReadingFailed);
        }

        lastReplyCode = reply.Code;
        return reply.Code == 421 ? throw Failed(CurlExitCode.OperationTimedOut, closingMessage) : reply;
    }

    private FtpControlConversationFailedException Failed(CurlExitCode exitCode, string message) =>
        new(TransferResult.Failure(exitCode, message, bytesTransferred));
}
