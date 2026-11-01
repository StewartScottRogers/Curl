using System.Globalization;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Telnet;

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
    /// How long the telnet loop waits for the server before it sends the upload's next
    /// buffer: curl 8.22.0's <c>telnet_do</c> polls the socket for 100 ms between reads of an
    /// upload (<c>wait_timeout</c> on Windows, <c>interval_ms</c> elsewhere).
    /// </summary>
    private static readonly TimeSpan UploadInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The most bytes read from the server at once: curl 8.21.0's telnet reads the socket
    /// at most 4096 bytes at a time, so no single output write is ever larger.
    /// </summary>
    private const int ReceiveBufferSize = 4096;

    private const string BadFunctionArgumentMessage = "A libcurl function was given a bad argument";

    /// <summary>The user name or password held a byte below 0x20 (exit 43), curl 8.22.0's text (BL-2179).</summary>
    private const string CredentialsHaveControlCode = "control code detected in credentials";

    /// <summary>
    /// The exit 55 message curl falls back to for a failed send with no socket error:
    /// <c>curl_easy_strerror(CURLE_SEND_ERROR)</c>.
    /// </summary>
    private const string SendFailedMessage = "Failed sending data to the peer";

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
        if (RefuseControlBytes(context.Credentials) is { } credentialsRefused)
        {
            log.Failed(credentialsRefused);
            return credentialsRefused;
        }

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

    /// <summary>
    /// The exit 43 curl 8.22.0 refuses <paramref name="credentials" /> with before it connects when the
    /// user name or password holds a byte below 0x20, or <see langword="null" /> when neither does (BL-2179).
    /// </summary>
    private static TransferResult? RefuseControlBytes(System.Net.NetworkCredential? credentials) =>
        credentials is not null && (credentials.UserName.Any(character => character < ' ') || credentials.Password.Any(character => character < ' '))
            ? TransferResult.Failure(CurlExitCode.BadFunctionArgument, CredentialsHaveControlCode)
            : null;

    private static async Task<TransferResult> RunSessionAsync(
        IConnection connection,
        ITransferContext context,
        TelnetReceiver receiver,
        TelnetOutbox outbox,
        long startedAt)
    {
        using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        TelnetUploadReader? upload = context.Upload is { } source ? new TelnetUploadReader(source, uploadCancellation.Token) : null;
        try
        {
            return await ReceiveUntilClosedAsync(connection, context, receiver, outbox, upload, startedAt)
                .ConfigureAwait(false);
        }
        finally
        {
            uploadCancellation.Cancel();
            upload?.Abandon();
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

    /// <summary>
    /// Runs curl 8.22.0's telnet loop (<c>telnet_do</c>): each turn waits for the server's next
    /// read, or with an upload at most <see cref="UploadInterval" /> for it, handles what the
    /// read brought and sends every reply it calls for, and only then sends the next buffer of
    /// the upload. Everything goes out from this one loop, so upload bytes never land between
    /// the writes of a negotiation reply and the upload follows the replies to the server's
    /// opening negotiation, as curl's does (BL-3536, CGR-007).
    /// </summary>
    private static async Task<TransferResult> ReceiveUntilClosedAsync(
        IConnection connection,
        ITransferContext context,
        TelnetReceiver receiver,
        TelnetOutbox outbox,
        TelnetUploadReader? upload,
        long startedAt)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        var buffer = new byte[ReceiveBufferSize];
        var data = new List<byte>();
        long bytesWritten = 0;
        Task<int>? read = null;

        while (true)
        {
            read ??= ReadOrClosedAsync(connection, buffer, cancellationToken);
            if (await ReadCompletedAsync(read, upload, context).ConfigureAwait(false))
            {
                Task<int> completed = read;
                read = null;
                (TransferResult? ended, bytesWritten) = await HandleReadAsync(
                    completed, buffer, data, connection, context, receiver, outbox, bytesWritten, startedAt).ConfigureAwait(false);
                if (ended is not null)
                {
                    await SendReadyUploadAsync(ended, upload, connection, cancellationToken).ConfigureAwait(false);
                    return ended;
                }
            }

            if (await TrySendUploadAsync(upload, connection, cancellationToken).ConfigureAwait(false) is { } uploadFailure)
            {
                ObserveFault(read ?? Task.CompletedTask);
                return SendFailure(uploadFailure, bytesWritten);
            }
        }
    }

    /// <summary>
    /// Waits for <paramref name="read" />, or, while an upload still has bytes to send, at
    /// most <see cref="UploadInterval" />: curl's telnet loop polls the socket that long
    /// before it reads the upload again. Returns whether the read completed.
    /// </summary>
    private static async Task<bool> ReadCompletedAsync(Task<int> read, TelnetUploadReader? upload, ITransferContext context)
    {
        if (upload is null || upload.Finished)
        {
            await ((Task)read).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            return true;
        }

        Task interval = Task.Delay(UploadInterval, context.TimeProvider, context.CancellationToken);
        return await Task.WhenAny(read, interval).ConfigureAwait(false) == read;
    }

    /// <summary>
    /// Handles one completed read: the server closing ends the session with exit 0, the
    /// <c>-m</c> watchdog cancelling it with exit 28; otherwise its data is written and its
    /// replies sent. Returns the result the session ends with, or <see langword="null" /> when
    /// it goes on, and the bytes written so far.
    /// </summary>
    private static async Task<(TransferResult? Ended, long BytesWritten)> HandleReadAsync(
        Task<int> read,
        byte[] buffer,
        List<byte> data,
        IConnection connection,
        ITransferContext context,
        TelnetReceiver receiver,
        TelnetOutbox outbox,
        long bytesWritten,
        long startedAt)
    {
        int count;
        try
        {
            count = await read.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (MaxTimePassed(context, startedAt))
        {
            return (new TransferResult(CurlExitCode.OperationTimedOut, bytesWritten, TimedOutMessage), bytesWritten);
        }

        if (count == 0)
        {
            return (TransferResult.Success(bytesWritten), bytesWritten);
        }

        data.Clear();
        TelnetReceiveError error = receiver.Receive(buffer.AsSpan(0, count), data, outbox);
        if (await WriteReceivedDataAsync(context, data, bytesWritten).ConfigureAwait(false) is { } writeFailure)
        {
            return (writeFailure, bytesWritten);
        }

        bytesWritten += data.Count;
        context.Progress.ReportDownloaded(bytesWritten, null);
        if (await TrySendRepliesAsync(outbox, connection, context.CancellationToken).ConfigureAwait(false) is { } sendFailure)
        {
            return (SendFailure(sendFailure, bytesWritten), bytesWritten);
        }

        return (error == TelnetReceiveError.None ? null : ToFailure(error, bytesWritten), bytesWritten);
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

    /// <summary>
    /// The exit 55 result for a failed send: the socket filter's <c>Send failure:</c> text
    /// (<see cref="CurlSocketErrorText" />) for a socket error, and curl's fallback text for
    /// anything else.
    /// </summary>
    private static TransferResult SendFailure(IOException failure, long bytesWritten) =>
        new(CurlExitCode.SendError, bytesWritten, CurlSocketErrorText.SendFailure(failure) ?? SendFailedMessage);

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

    /// <summary>
    /// Sends the upload's next buffer, IAC doubled, when its read has completed, returning
    /// <see langword="null" /> when it went or there was none to send, or the failure when
    /// the connection did not take it.
    /// </summary>
    private static async Task<IOException?> TrySendUploadAsync(
        TelnetUploadReader? upload,
        IConnection connection,
        CancellationToken cancellationToken) =>
        upload?.TakeCompletedBuffer() is { } bytes
            ? await TrySendAsync(connection, EscapeInterpretAsCommand(bytes), cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>
    /// When the server closed (<paramref name="ended" /> is exit 0), sends every buffer of the
    /// upload whose read has already completed, a failed write ending it quietly: curl 8.21.0
    /// sent standard input to a telnet server that closed without a byte (BL-316).
    /// </summary>
    private static async Task SendReadyUploadAsync(
        TransferResult ended,
        TelnetUploadReader? upload,
        IConnection connection,
        CancellationToken cancellationToken)
    {
        while (ended.ExitCode == CurlExitCode.Ok
            && upload?.TakeCompletedBuffer() is { } bytes
            && await TrySendAsync(connection, EscapeInterpretAsCommand(bytes), cancellationToken).ConfigureAwait(false) is null)
        {
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
    /// a socket error is reported in curl's words for it and the session goes on, as curl's
    /// do (BL-1307, BL-1312). Returns <see langword="null" /> when every write went, or the
    /// failure of a write that fails otherwise, which still ends the session with exit 55.
    /// </summary>
    private static async Task<IOException?> TrySendRepliesAsync(
        TelnetOutbox outbox,
        IConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await outbox.SendPendingAsync(reply => SendReplyAsync(connection, reply, cancellationToken)).ConfigureAwait(false);
            return null;
        }
        catch (IOException failure)
        {
            return failure;
        }
    }

    /// <summary>
    /// Sends one reply, returning <see langword="null" /> when the connection took it, or
    /// the <see cref="SocketException" /> inside its <see cref="IOException" /> when it failed
    /// with one. Any other <see cref="IOException" /> is thrown.
    /// </summary>
    private static async Task<SocketException?> SendReplyAsync(
        IConnection connection,
        byte[] reply,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(reply, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException failure) when (failure.InnerException is SocketException socketError)
        {
            return socketError;
        }
    }

    /// <summary>
    /// Sends bytes to the server, returning <see langword="null" /> when the connection took
    /// them, or the failure when it did not.
    /// </summary>
    private static async Task<IOException?> TrySendAsync(
        IConnection connection,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException failure)
        {
            return failure;
        }
    }

    /// <summary>
    /// Reads the upload one buffer at a time without the telnet loop ever waiting on it: a
    /// read is started and its buffer taken only once it has completed, so an upload that
    /// never yields (a terminal's standard input) leaves the session receiving. An upload
    /// whose read fails ends there and the session goes on, as before.
    /// </summary>
    /// <param name="source">The upload.</param>
    /// <param name="cancellationToken">Cancels a read still waiting when the session ends.</param>
    private sealed class TelnetUploadReader(Stream source, CancellationToken cancellationToken)
    {
        private readonly byte[] buffer = new byte[BufferSize];

        private Task<int>? read;

        /// <summary>Gets a value indicating whether the upload has ended or failed.</summary>
        public bool Finished { get; private set; }

        /// <summary>
        /// Takes the bytes of the upload's read when it has completed, starting the next one
        /// first when none is waiting; returns <see langword="null" /> while the read waits,
        /// and from the end of the upload or a failed read on.
        /// </summary>
        /// <returns>The bytes read, or <see langword="null" />.</returns>
        public byte[]? TakeCompletedBuffer()
        {
            if (Finished)
            {
                return null;
            }

            read ??= ReadAsync();
            if (!read.IsCompleted)
            {
                return null;
            }

            Task<int> completed = read;
            read = null;
            int count = completed.IsCompletedSuccessfully ? completed.GetAwaiter().GetResult() : 0;
            ObserveFault(completed);
            Finished = count == 0;
            return Finished ? null : buffer.AsSpan(0, count).ToArray();
        }

        /// <summary>Observes the fault of a read still waiting when the session ends.</summary>
        public void Abandon() => ObserveFault(read ?? Task.CompletedTask);

        /// <summary>Reads the upload, a read that throws at once ending as a faulted task.</summary>
        private async Task<int> ReadAsync() => await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
