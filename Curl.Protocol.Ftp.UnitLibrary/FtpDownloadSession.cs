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

        bool listing = path.FileName.Length == 0;
        return await ChangeDirectoriesAsync(path.Directories).ConfigureAwait(false)
            ?? await OpenDataConnectionAsync().ConfigureAwait(false)
            ?? await SetTypeAsync(listing).ConfigureAwait(false)
            ?? await ReadSizeAsync(path.FileName, listing).ConfigureAwait(false)
            ?? await RetrieveAsync(listing ? "LIST" : "RETR " + path.FileName, listing).ConfigureAwait(false);
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
            expectedSize = bytes;
        }

        return null;
    }

    private async ValueTask<TransferResult> RetrieveAsync(string command, bool listing)
    {
        FtpReply opened = await ExchangeAsync(command).ConfigureAwait(false);
        if (opened.Code is 125 or 150)
        {
            context.Progress.ReportTransferStarted();
            return await CopyDataAsync().ConfigureAwait(false) ?? await ReadTransferCompleteAsync().ConfigureAwait(false);
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
        while (true)
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

            try
            {
                await context.Output.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                int accepted = exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
                return TransferResult.Failure(CurlExitCode.WriteError, FtpTransferMessages.OutputWriteFailed(read, accepted), bytesWritten);
            }

            bytesWritten += read;
            context.Progress.ReportDownloaded(bytesWritten, expectedSize);
        }
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

    /// <summary>
    /// Sends <c>QUIT</c> and reads the reply, ignoring whatever goes wrong: the transfer's
    /// outcome is already decided.
    /// </summary>
    private async ValueTask QuitAsync()
    {
        if (!await control.TrySendAsync("QUIT").ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await control.ReadReplyAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            // An oversized reply to QUIT changes nothing.
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
