using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// One <c>ftp://</c> download over an open control connection: the conversation curl
/// 8.21.0 holds, from the greeting to <c>QUIT</c>.
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
/// Each failure ends the session with curl's exit code and message, and sends
/// <c>QUIT</c> first exactly where curl was measured to: not after a refused login, a
/// bad greeting, a path with a control character, an unreadable <c>227</c> reply, or a
/// short transfer. A lost control connection is exit 56, a failed send exit 55, and a
/// reply line of 65536 bytes or more exit 100. The user name and password are sent as
/// given, CR and LF included, as curl sends them.
/// </para>
/// </remarks>
internal sealed class FtpDownloadSession(IConnector connector, FtpControlChannel control, ITransferContext context)
    : IAsyncDisposable
{
    private const string AnonymousUser = "anonymous";

    private const string AnonymousPassword = "ftp@example.com";

    private const int ReadBufferSize = 16384;

    private IConnection? dataConnection;

    private FtpDownloadWindow window;

    private long? fileSize;

    private long? expectedSize;

    private long bytesWritten;

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
                ?? await RetrieveFromPathAsync().ConfigureAwait(false);
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

    private async ValueTask<TransferResult> RetrieveFromPathAsync()
    {
        await ExchangeAsync("PWD").ConfigureAwait(false);
        if (FtpUrlPath.Parse(context.Url.AbsolutePath) is not { } path)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, FtpTransferMessages.PathHasControlCharacters);
        }

        return await ChangeDirectoriesAsync(path.Directories).ConfigureAwait(false)
            ?? (context.NoBody
                ? await ReportHeadAsync(path.FileName).ConfigureAwait(false)
                : await DownloadAsync(path.FileName).ConfigureAwait(false));
    }

    /// <summary>
    /// Downloads the file, or the listing when <paramref name="fileName" /> is empty. The
    /// window <c>-r</c> or <c>-C</c> asks for applies to a file only.
    /// </summary>
    private async ValueTask<TransferResult> DownloadAsync(string fileName)
    {
        bool listing = fileName.Length == 0;
        window = listing ? default : FtpDownloadWindow.Of(context);
        return await OpenDataConnectionAsync().ConfigureAwait(false)
            ?? await SetTypeAsync(listing).ConfigureAwait(false)
            ?? await ReadSizeAsync(fileName, listing).ConfigureAwait(false)
            ?? await PositionAsync().ConfigureAwait(false)
            ?? await RetrieveAsync(listing ? "LIST" : "RETR " + fileName, listing).ConfigureAwait(false);
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
            return await QuitAndSucceedAsync().ConfigureAwait(false);
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
            FtpReply changed = await ExchangeAsync("CWD " + directory).ConfigureAwait(false);
            if (!changed.IsCompletion)
            {
                return await QuitAndFailAsync(CurlExitCode.RemoteAccessDenied, FtpTransferMessages.ChangeDirectoryDenied).ConfigureAwait(false);
            }
        }

        return null;
    }

    private async ValueTask<TransferResult?> OpenDataConnectionAsync()
    {
        FtpReply epsv = await ExchangeAsync("EPSV").ConfigureAwait(false);
        if (epsv.Code == 229)
        {
            return FtpPassiveReply.TryParseEpsvPort(epsv.LastLine, out int epsvPort)
                ? await ConnectDataAsync(epsvPort).ConfigureAwait(false)
                : await QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, FtpTransferMessages.WeirdEpsvReply).ConfigureAwait(false);
        }

        FtpReply pasv = await ExchangeAsync("PASV").ConfigureAwait(false);
        if (pasv.Code != 227)
        {
            return await QuitAndFailAsync(CurlExitCode.FtpWeirdPasvReply, FtpTransferMessages.BadPassiveReply(pasv.Code)).ConfigureAwait(false);
        }

        return FtpPassiveReply.TryParsePasvPort(pasv.LastLine, out int pasvPort)
            ? await ConnectDataAsync(pasvPort).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.FtpWeird227Format, FtpTransferMessages.Weird227Reply);
    }

    private async ValueTask<TransferResult?> ConnectDataAsync(int port)
    {
        var target = new ConnectTarget(context.Url.IdnHost, port, false)
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

        if (size.Code == 213 && long.TryParse(size.LastLine.AsSpan(3), System.Globalization.NumberStyles.AllowLeadingWhite, System.Globalization.CultureInfo.InvariantCulture, out long bytes))
        {
            fileSize = bytes;
        }

        return null;
    }

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
            await EndAsync().ConfigureAwait(false);
            return TransferResult.Success(0);
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
    /// Ends a transfer whose <c>RETR</c> or <c>LIST</c> was not answered with 125 or 150:
    /// a <c>450</c> to <c>LIST</c> is an empty listing, a <c>550</c> to <c>RETR</c> exit 78,
    /// anything else exit 19. <c>QUIT</c> is sent first in every case.
    /// </summary>
    private async ValueTask<TransferResult> RefuseRetrieveAsync(int code, bool listing)
    {
        await QuitAsync().ConfigureAwait(false);
        if (listing && code == 450)
        {
            return TransferResult.Success(0);
        }

        CurlExitCode exitCode = !listing && code == 550 ? CurlExitCode.RemoteFileNotFound : CurlExitCode.FtpCouldntRetrFile;
        return TransferResult.Failure(exitCode, FtpTransferMessages.RetrieveRefused(code));
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
                return TransferResult.Failure(CurlExitCode.RecvError, FtpTransferMessages.ReceiveFailed, bytesWritten);
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
                return TransferResult.Failure(CurlExitCode.WriteError, FtpTransferMessages.OutputWriteFailed(wanted, accepted), bytesWritten);
            }

            bytesWritten += wanted;
            context.Progress.ReportDownloaded(bytesWritten, expectedSize);
        }

        return null;
    }

    /// <summary>
    /// Gets whether the window's byte limit has been written, so curl stops reading the
    /// data connection.
    /// </summary>
    private bool IsWindowRead() => window.MaxDownload is { } max && bytesWritten >= max;

    /// <summary>The bytes of a read of <paramref name="read" /> bytes that lie within the window.</summary>
    private int CountWithinWindow(int read) =>
        window.MaxDownload is { } max ? (int)Math.Min(read, max - bytesWritten) : read;

    /// <summary>
    /// Ends a download that had a byte limit: exit 18 with no <c>QUIT</c> when the data
    /// fell short of what was expected, otherwise <c>ABOR</c>, whose reply curl reads
    /// without checking it, and <c>QUIT</c>.
    /// </summary>
    private async ValueTask<TransferResult> EndRangeAsync()
    {
        if (expectedSize is { } expected && bytesWritten < expected)
        {
            return TransferResult.Failure(CurlExitCode.PartialFile, FtpTransferMessages.EndOfResponseWithBytesMissing(expected - bytesWritten), bytesWritten);
        }

        await EndAsync().ConfigureAwait(false);
        return TransferResult.Success(bytesWritten);
    }

    private async ValueTask<TransferResult> ReadTransferCompleteAsync()
    {
        FtpReply complete = await ReadReplyAsync(FtpTransferMessages.ControlConnectionLooksDead).ConfigureAwait(false);
        if (expectedSize is { } expected && bytesWritten < expected)
        {
            return TransferResult.Failure(CurlExitCode.PartialFile, FtpTransferMessages.ClosedWithBytesRemaining(expected - bytesWritten), bytesWritten);
        }

        if (complete.Code is not (226 or 250))
        {
            return await QuitAndFailAsync(CurlExitCode.PartialFile, FtpTransferMessages.TransferNotOk(complete.Code)).ConfigureAwait(false);
        }

        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Success(bytesWritten);
    }

    private async ValueTask<TransferResult> QuitAndFailAsync(CurlExitCode exitCode, string message)
    {
        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Failure(exitCode, message, bytesWritten);
    }

    private async ValueTask<TransferResult> QuitAndSucceedAsync()
    {
        await QuitAsync().ConfigureAwait(false);
        return TransferResult.Success(0);
    }

    /// <summary>
    /// Ends the conversation as curl does once the outcome is decided: <c>ABOR</c> first
    /// when the window has a byte limit, then <c>QUIT</c>.
    /// </summary>
    private async ValueTask EndAsync()
    {
        if (window.MaxDownload is not null)
        {
            await SendIgnoringReplyAsync("ABOR").ConfigureAwait(false);
        }

        await QuitAsync().ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> EndAndFailAsync(CurlExitCode exitCode, string message)
    {
        await EndAsync().ConfigureAwait(false);
        return TransferResult.Failure(exitCode, message, bytesWritten);
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
        new(TransferResult.Failure(exitCode, message, bytesWritten));
}
