using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// One <c>ftp://</c> download or upload over an open control connection: the conversation
/// curl 8.21.0 holds, from the greeting to <c>QUIT</c>.
/// </summary>
/// <param name="connector">Opens the data connection to the port the server offers.</param>
/// <param name="control">The control connection, already open.</param>
/// <param name="context">The transfer being performed.</param>
/// <remarks>
/// <para>
/// The conversation is <c>USER</c>, <c>PASS</c> (skipped when <c>USER</c> is answered
/// with a 2xx), <c>PWD</c>, one <c>CWD</c> per directory in the path, <c>EPSV</c> (and
/// <c>PASV</c> when <c>EPSV</c> is refused), <c>TYPE I</c>, <c>SIZE</c>, <c>RETR</c> and
/// <c>QUIT</c>. A path ending in <c>/</c> sends <c>TYPE A</c> and <c>LIST</c> instead of
/// <c>TYPE I</c>, <c>SIZE</c> and <c>RETR</c>, and copies the listing.
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
internal sealed class FtpSession(IConnector connector, FtpControlChannel control, ITransferContext context)
    : IAsyncDisposable
{
    private const string AnonymousUser = "anonymous";

    private const string AnonymousPassword = "ftp@example.com";

    private const int ReadBufferSize = 16384;

    private readonly FtpQuoteCommands quotes = FtpQuoteCommands.Parse(context.QuoteCommands);

    private IConnection? dataConnection;

    private FtpDownloadWindow window;

    private long? fileSize;

    private long? expectedSize;

    private long bytesTransferred;

    /// <summary>
    /// Holds the whole conversation. The data connection it opens stays open until the
    /// session is disposed.
    /// </summary>
    /// <returns>The transfer's outcome.</returns>
    public async ValueTask<TransferResult> RunAsync()
    {
        try
        {
            return await GreetAndLogInAsync().ConfigureAwait(false)
                ?? await TransferPathAsync().ConfigureAwait(false);
        }
        catch (FtpControlConversationFailedException lost)
        {
            return lost.Result;
        }
    }

    /// <summary>
    /// Disposes the data connection, if one was opened. The control connection belongs to
    /// the caller.
    /// </summary>
    /// <returns>A task that completes when the data connection is closed.</returns>
    public ValueTask DisposeAsync() => dataConnection?.DisposeAsync() ?? ValueTask.CompletedTask;

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
            220 => await LogInAsync().ConfigureAwait(false),
            _ => TransferResult.Failure(CurlExitCode.WeirdServerReply, FtpTransferMessages.UnexpectedGreeting(greeting.Code)),
        };
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
        await ExchangeAsync("PWD").ConfigureAwait(false);
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

        context.Progress.ReportTransferStarted();
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
    /// Answers <c>-I</c> as curl does, with no data connection: a directory URL sends
    /// nothing more; a file sends <c>MDTM</c>, <c>TYPE I</c>, <c>SIZE</c> and
    /// <c>REST 0</c>, writing a <c>Last-Modified</c>, <c>Content-Length</c> and
    /// <c>Accept-ranges</c> line for each that succeeded.
    /// </summary>
    private async ValueTask<TransferResult> ReportHeadAsync(string fileName)
    {
        if (fileName.Length == 0)
        {
            return await SendQuotesAsync(quotes.BeforeTransfer).ConfigureAwait(false)
                ?? await QuitAndSucceedAsync().ConfigureAwait(false);
        }

        FtpReply modified = await ExchangeAsync("MDTM " + fileName).ConfigureAwait(false);
        return await WriteHeaderAsync(FtpHeadHeaderLines.LastModified(modified)).ConfigureAwait(false)
            ?? await SetTypeAsync(false).ConfigureAwait(false)
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
    /// Opens the data connection: <c>EPSV</c> first unless <c>--disable-epsv</c>, then
    /// <c>PASV</c> when <c>EPSV</c> was skipped or answered with anything but <c>229</c>.
    /// </summary>
    private async ValueTask<TransferResult?> OpenDataConnectionAsync()
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
        ConnectResult connected = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
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
            context.Progress.ReportTransferStarted();
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
            try
            {
                await context.Output.WriteAsync(buffer.AsMemory(0, wanted), context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                int accepted = exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
                return TransferResult.Failure(CurlExitCode.WriteError, FtpTransferMessages.OutputWriteFailed(wanted, accepted), bytesTransferred);
            }

            bytesTransferred += wanted;
            context.Progress.ReportDownloaded(bytesTransferred, expectedSize);
        }

        return null;
    }

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
        if (window.MaxDownload is not null)
        {
            await SendIgnoringReplyAsync("ABOR").ConfigureAwait(false);
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

    private ValueTask QuitAsync() => SendIgnoringReplyAsync("QUIT");

    /// <summary>
    /// Sends <paramref name="command" /> and reads one reply, ignoring whatever goes wrong:
    /// the transfer's outcome is already decided.
    /// </summary>
    private async ValueTask SendIgnoringReplyAsync(string command)
    {
        if (!await control.TrySendAsync(command).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await control.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            // An oversized reply to ABOR or QUIT changes nothing.
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

        return reply.Code == 421 ? throw Failed(CurlExitCode.OperationTimedOut, closingMessage) : reply;
    }

    private FtpControlConversationFailedException Failed(CurlExitCode exitCode, string message) =>
        new(TransferResult.Failure(exitCode, message, bytesTransferred));
}
