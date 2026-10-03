using System.Globalization;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Serves the <c>telnet</c> scheme: sends the transfer's upload to the server while
/// writing what the server sends to the output, until the server closes the connection.
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port (ADR-0005). No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <remarks>
/// <para>
/// Matches curl 8.21.0, measured against a loopback listener. The port defaults to 23.
/// The upload, which is standard input when the caller composes it so, is sent as read,
/// with no line-ending conversion; the one change is that each <c>0xFF</c> byte is sent
/// doubled, as <c>IAC IAC</c>, as curl sends it. Received bytes go through
/// <see cref="TelnetReceiver" />, which removes command sequences and answers option
/// negotiation.
/// </para>
/// <para>
/// Sending and receiving run concurrently. The session ends when the server closes, with
/// exit 0, whether or not the upload is exhausted; an upload still waiting for input then
/// is abandoned rather than awaited, so a console that is never typed into cannot hold
/// the session open. A subnegotiation asking for a terminal type or X display location
/// that no <c>-t</c> option supplied ends it with exit 43
/// (<see cref="CurlExitCode.BadFunctionArgument" />), one whose value is over 1000
/// characters with exit 55 (<see cref="CurlExitCode.SendError" />), and a malformed
/// subnegotiation with exit 56 (<see cref="CurlExitCode.RecvError" />).
/// </para>
/// <para>
/// A connection failure is met as curl 8.21.0 on Windows meets it. A read the connection
/// fails, as a reset does, ends the session like a close, with exit 0. A send of the
/// upload it fails ends it with exit 55 (<see cref="CurlExitCode.SendError" />), and an
/// output that refuses a write with exit 23 (<see cref="CurlExitCode.WriteError" />). Each
/// negotiation reply is its own write, as curl sends it; one the connection fails with a
/// socket error is reported as curl's <c>Sending data failed (N)</c> line and the session
/// goes on, as curl's does (BL-1307), while any other failure of one ends it with exit 55.
/// </para>
/// <para>
/// Received data meets <see cref="ITransferContext.NoBody" /> and
/// <see cref="ITransferContext.MaxFileSize" /> as curl 8.21.0's download writer meets them
/// (measured, BL-1306): under <c>-I</c> the first data ends the session with exit 8
/// (<see cref="CurlExitCode.WeirdServerReply" />), unwritten; data past the limit is cut to
/// it and the session ends with exit 63 (<see cref="CurlExitCode.FilesizeExceeded" />).
/// </para>
/// <para>
/// The <see cref="ITransferContext.Credentials" /> user name and
/// <see cref="ITransferContext.TelnetOptions" /> are read by <see cref="TelnetOptionParser" />
/// once connected, as curl reads them: the user name is sent as the NEW-ENVIRON variable
/// <c>USER</c>, a non-ASCII one ends the transfer with exit 43, and a bad option with
/// exit 48 or 49, before a byte is sent.
/// </para>
/// <para>
/// After connecting, <see cref="ITransferContext.Events" /> gets what curl 8.21.0's
/// <c>-v</c> and <c>--trace</c> show (measured, BL-935), through
/// <see cref="TelnetTraceReporter" />: each negotiation and subnegotiation received and
/// sent, each run of output data as data received, and the line the connection ends with.
/// </para>
/// </remarks>
public sealed class TelnetProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The port a <c>telnet</c> URL without one connects to.</summary>
    private const int DefaultPort = 23;

    /// <summary>The most bytes read from the upload at once.</summary>
    private const int BufferSize = 16384;

    /// <summary>
    /// The most bytes read from the server at once: curl 8.21.0's telnet reads the socket
    /// at most 4096 bytes at a time, so no single output write is ever larger.
    /// </summary>
    private const int ReceiveBufferSize = 4096;

    private const string BadFunctionArgumentMessage = "A libcurl function was given a bad argument";

    /// <summary>
    /// The exit 55 message for a send the connection fails, as curl 8.21.0 on Windows words
    /// a send to a reset connection.
    /// </summary>
    private const string SendFailureMessage = "Send failure: Connection was reset";

    private const string SuboptionErrorMessage = "telnet: suboption error";

    private const string TerminalTypeTooLongMessage = "Too long telnet TTYPE";

    private const string XDisplayLocationTooLongMessage = "Too long telnet XDISPLOC";

    /// <summary>
    /// The exit 28 message for a session <c>-m</c> ends: curl 8.21.0's telnet loop checks
    /// <c>-m</c> itself and words it so, not as the multi loop's <c>Operation timed out</c>
    /// (measured 2026-09-28, BL-511 Notes).
    /// </summary>
    private const string TimedOutMessage = "Time-out";

    /// <summary>
    /// The one scheme this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names it.
    /// </summary>
    private static readonly string[] Schemes = ["telnet"];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long sessionStarted = context.TimeProvider.GetTimestamp();
        long startedAt = context.OperationStarted ?? sessionStarted;
        var log = new TelnetDiagnosticLog(context.DiagnosticLog);
        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            var failed = new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
            log.Failed(failed);
            return failed;
        }

        log.SessionStarted(target.Host, target.Port);
        context.Progress.ReportTransferStarted();
        var outbox = new TelnetOutbox(context.Events);
        var trace = new TelnetTraceReporter(outbox);
        await using (connection.ConfigureAwait(false))
        {
            var optionValues = new TelnetOptionValues();
            TransferResult result = TelnetOptionParser.Parse(
                context.Credentials?.UserName,
                context.TelnetOptions,
                optionValues)
                ?? await RunSessionAsync(connection, context, new TelnetReceiver(optionValues, log, trace), outbox, startedAt).ConfigureAwait(false);
            log.SessionEnded(result, context.TimeProvider.GetElapsedTime(sessionStarted));
            trace.ConnectionEnded(result, connect.ConnectionNumber);
            outbox.ReportPending();
            return result;
        }
    }

    private static async Task<TransferResult> RunSessionAsync(
        IConnection connection,
        ITransferContext context,
        TelnetReceiver receiver,
        TelnetOutbox outbox,
        long startedAt)
    {
        var sendLock = new SemaphoreSlim(1, 1);
        var uploadSendFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        Task upload = context.Upload is { } source
            ? SendUploadAsync(source, connection, sendLock, uploadSendFailed, uploadCancellation.Token)
            : Task.CompletedTask;

        try
        {
            return await ReceiveUntilClosedAsync(connection, context, receiver, outbox, sendLock, uploadSendFailed.Task, startedAt)
                .ConfigureAwait(false);
        }
        finally
        {
            uploadCancellation.Cancel();
            ObserveFault(upload);
        }
    }

    /// <summary>
    /// Observes a task that is abandoned rather than awaited, so a fault it ends with later
    /// is never reported as unobserved.
    /// </summary>
    private static void ObserveFault(Task abandoned) =>
        _ = abandoned.ContinueWith(
            static finished => finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static async Task<TransferResult> ReceiveUntilClosedAsync(
        IConnection connection,
        ITransferContext context,
        TelnetReceiver receiver,
        TelnetOutbox outbox,
        SemaphoreSlim sendLock,
        Task uploadSendFailed,
        long startedAt)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        var buffer = new byte[ReceiveBufferSize];
        var data = new List<byte>();
        long bytesWritten = 0;

        while (true)
        {
            Task<int> read = ReadOrClosedAsync(connection, buffer, cancellationToken);
            if (await Task.WhenAny(uploadSendFailed, read).ConfigureAwait(false) == uploadSendFailed)
            {
                ObserveFault(read);
                return SendFailure(bytesWritten);
            }

            int count;
            try
            {
                count = await read.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (MaxTimePassed(context, startedAt))
            {
                return new TransferResult(CurlExitCode.OperationTimedOut, bytesWritten, TimedOutMessage);
            }

            if (count == 0)
            {
                return TransferResult.Success(bytesWritten);
            }

            data.Clear();
            TelnetReceiveError error = receiver.Receive(buffer.AsSpan(0, count), data, outbox);
            if (await WriteReceivedDataAsync(context, data, bytesWritten).ConfigureAwait(false) is { } writeFailure)
            {
                return writeFailure;
            }

            bytesWritten += data.Count;
            context.Progress.ReportDownloaded(bytesWritten, null);
            if (!await TrySendRepliesAsync(outbox, connection, sendLock, cancellationToken).ConfigureAwait(false))
            {
                return SendFailure(bytesWritten);
            }

            if (error != TelnetReceiveError.None)
            {
                return ToFailure(error, bytesWritten);
            }
        }
    }

    /// <summary>
    /// Tells whether <c>-m</c> is set and has passed on the transfer's clock since
    /// <paramref name="startedAt" />, so a read the runner's <c>-m</c> watchdog cancelled ends with
    /// curl's telnet message rather than the multi loop's (ADR-0117, Decision 4).
    /// </summary>
    private static bool MaxTimePassed(ITransferContext context, long startedAt) =>
        context.MaxTime is { } limit && limit > TimeSpan.Zero && context.TimeProvider.GetElapsedTime(startedAt) >= limit;

    /// <summary>
    /// Reads from the server, taking a read the connection fails with
    /// <see cref="IOException" /> as the server closing: curl 8.21.0 on Windows ends a
    /// telnet session whose connection is reset with exit 0, the reset arriving as the
    /// socket's close event.
    /// </summary>
    private static async Task<int> ReadOrClosedAsync(
        IConnection connection,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Writes one read's data to the output as curl 8.21.0's download writer
    /// (<c>cw_download_write</c>, <c>lib/sendf.c</c>) does, returning <see langword="null" />
    /// when it all went out: under <c>-I</c> any data ends the session with exit 8 and is not
    /// written; data past <c>--max-filesize</c> (0 is no limit) is cut, the part under the
    /// limit written, and the session ended with exit 63; an output that refuses the write
    /// ends it with exit 23. The limit counts across reads through
    /// <paramref name="bytesWritten" />.
    /// </summary>
    private static async Task<TransferResult?> WriteReceivedDataAsync(
        ITransferContext context,
        List<byte> data,
        long bytesWritten)
    {
        if (data.Count > 0 && context.NoBody)
        {
            return new TransferResult(CurlExitCode.WeirdServerReply, bytesWritten, TelnetTraceReporter.WeirdServerReplyMessage);
        }

        long? limit = context.MaxFileSize is > 0 ? context.MaxFileSize : null;
        int allowed = AllowedBytes(limit, bytesWritten, data.Count);
        bool cut = allowed < data.Count;
        data.RemoveRange(allowed, data.Count - allowed);
        if (await WriteOutputAsync(context.Output, data, context.CancellationToken).ConfigureAwait(false) is { } accepted)
        {
            return WriteFailure(data.Count, accepted, bytesWritten);
        }

        return cut ? FileSizeExceeded(limit!.Value, bytesWritten + allowed) : null;
    }

    /// <summary>
    /// How many of a read's <paramref name="count" /> bytes fit under <c>--max-filesize</c>
    /// <paramref name="limit" /> (<see langword="null" /> for none) after
    /// <paramref name="bytesWritten" />.
    /// </summary>
    private static int AllowedBytes(long? limit, long bytesWritten, int count) =>
        limit is { } max ? (int)Math.Clamp(max - bytesWritten, 0, count) : count;

    private static TransferResult FileSizeExceeded(long limit, long bytesWritten) =>
        new(
            CurlExitCode.FilesizeExceeded,
            bytesWritten,
            string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({limit}) with {bytesWritten} bytes"));

    /// <summary>
    /// Writes received data to the output, returning <see langword="null" /> when the output
    /// took it, or how many bytes of the write it accepted when it failed: the
    /// <see cref="OutputWriteFailedException.BytesAccepted" /> of one, and 0 for any other
    /// <see cref="IOException" />.
    /// </summary>
    private static async Task<int?> WriteOutputAsync(
        Stream output,
        List<byte> data,
        CancellationToken cancellationToken)
    {
        if (data.Count == 0)
        {
            return null;
        }

        try
        {
            await output.WriteAsync(data.ToArray(), cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (OutputWriteFailedException failure)
        {
            return failure.BytesAccepted;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static TransferResult SendFailure(long bytesWritten) =>
        new(CurlExitCode.SendError, bytesWritten, SendFailureMessage);

    private static TransferResult WriteFailure(int passed, int accepted, long bytesWritten) =>
        new(
            CurlExitCode.WriteError,
            bytesWritten,
            string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {accepted}"));

    private static TransferResult ToFailure(TelnetReceiveError error, long bytesWritten) =>
        error switch
        {
            TelnetReceiveError.SubnegotiationValueMissing =>
                new TransferResult(CurlExitCode.BadFunctionArgument, bytesWritten, BadFunctionArgumentMessage),
            TelnetReceiveError.TerminalTypeTooLong =>
                new TransferResult(CurlExitCode.SendError, bytesWritten, TerminalTypeTooLongMessage),
            TelnetReceiveError.XDisplayLocationTooLong =>
                new TransferResult(CurlExitCode.SendError, bytesWritten, XDisplayLocationTooLongMessage),
            _ => new TransferResult(CurlExitCode.RecvError, bytesWritten, SuboptionErrorMessage),
        };

    private static async Task SendUploadAsync(
        Stream upload,
        IConnection connection,
        SemaphoreSlim sendLock,
        TaskCompletionSource sendFailed,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await upload.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            byte[] escaped = EscapeInterpretAsCommand(buffer.AsSpan(0, read));
            if (!await TrySendAsync(connection, sendLock, escaped, cancellationToken).ConfigureAwait(false))
            {
                sendFailed.SetResult();
                return;
            }
        }
    }

    private static byte[] EscapeInterpretAsCommand(ReadOnlySpan<byte> bytes)
    {
        var escaped = new List<byte>(bytes.Length);
        foreach (byte value in bytes)
        {
            escaped.Add(value);
            if (value == TelnetByte.InterpretAsCommand)
            {
                escaped.Add(value);
            }
        }

        return [.. escaped];
    }

    /// <summary>
    /// Sends the replies a read called for, one write each, as curl 8.21.0's
    /// <c>send_negotiation</c> and <c>sendsuboption</c> do: a write the connection fails with
    /// a socket error is reported as <c>Sending data failed (N)</c> and the session goes on,
    /// as curl's do (BL-1307). Returns <see langword="false" /> for a write that fails
    /// otherwise, which still ends the session with exit 55.
    /// </summary>
    private static async Task<bool> TrySendRepliesAsync(
        TelnetOutbox outbox,
        IConnection connection,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        try
        {
            await outbox.SendPendingAsync(reply => SendReplyAsync(connection, sendLock, reply, cancellationToken)).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sends one reply, returning <see langword="null" /> when the connection took it, or
    /// the socket error number - the WSA code on Windows, the errno elsewhere, as curl's
    /// <c>SOCKERRNO</c> is - when it failed with a <see cref="SocketException" /> inside its
    /// <see cref="IOException" />. Any other <see cref="IOException" /> is thrown.
    /// </summary>
    private static async Task<int?> SendReplyAsync(
        IConnection connection,
        SemaphoreSlim sendLock,
        byte[] reply,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.WriteAsync(reply, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException failure) when (failure.InnerException is SocketException socketError)
        {
            return socketError.NativeErrorCode;
        }
        finally
        {
            sendLock.Release();
        }
    }

    /// <summary>Sends bytes to the server, reporting whether the connection took them.</summary>
    private static async Task<bool> TrySendAsync(
        IConnection connection,
        SemaphoreSlim sendLock,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            sendLock.Release();
        }
    }
}
