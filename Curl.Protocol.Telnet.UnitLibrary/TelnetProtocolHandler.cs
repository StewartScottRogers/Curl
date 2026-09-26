using System.Globalization;
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
/// fails, as a reset does, ends the session like a close, with exit 0. A send it fails,
/// of the upload or of a negotiation reply, ends it with exit 55
/// (<see cref="CurlExitCode.SendError" />), and an output that refuses a write with
/// exit 23 (<see cref="CurlExitCode.WriteError" />).
/// </para>
/// <para>
/// The <see cref="ITransferContext.Credentials" /> user name and
/// <see cref="ITransferContext.TelnetOptions" /> are read by <see cref="TelnetOptionParser" />
/// once connected, as curl reads them: the user name is sent as the NEW-ENVIRON variable
/// <c>USER</c>, a non-ASCII one ends the transfer with exit 43, and a bad option with
/// exit 48 or 49, before a byte is sent.
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

        Uri url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, false);
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage);
        }

        await using (connection.ConfigureAwait(false))
        {
            var optionValues = new TelnetOptionValues();
            TransferResult? optionFailure = TelnetOptionParser.Parse(
                context.Credentials?.UserName,
                context.TelnetOptions,
                optionValues);
            return optionFailure
                ?? await RunSessionAsync(connection, context, optionValues).ConfigureAwait(false);
        }
    }

    private static async Task<TransferResult> RunSessionAsync(
        IConnection connection,
        ITransferContext context,
        TelnetOptionValues optionValues)
    {
        var sendLock = new SemaphoreSlim(1, 1);
        var uploadSendFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        Task upload = context.Upload is { } source
            ? SendUploadAsync(source, connection, sendLock, uploadSendFailed, uploadCancellation.Token)
            : Task.CompletedTask;

        try
        {
            return await ReceiveUntilClosedAsync(connection, context, optionValues, sendLock, uploadSendFailed.Task)
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
        TelnetOptionValues optionValues,
        SemaphoreSlim sendLock,
        Task uploadSendFailed)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        var receiver = new TelnetReceiver(optionValues);
        var buffer = new byte[ReceiveBufferSize];
        var data = new List<byte>();
        var replies = new List<byte>();
        long bytesWritten = 0;

        while (true)
        {
            Task<int> read = ReadOrClosedAsync(connection, buffer, cancellationToken);
            if (await Task.WhenAny(uploadSendFailed, read).ConfigureAwait(false) == uploadSendFailed)
            {
                ObserveFault(read);
                return SendFailure(bytesWritten);
            }

            int count = await read.ConfigureAwait(false);
            if (count == 0)
            {
                return TransferResult.Success(bytesWritten);
            }

            data.Clear();
            replies.Clear();
            TelnetReceiveError error = receiver.Receive(buffer.AsSpan(0, count), data, replies);
            if (await WriteOutputAsync(context.Output, data, cancellationToken).ConfigureAwait(false) is { } accepted)
            {
                return WriteFailure(data.Count, accepted, bytesWritten);
            }

            bytesWritten += data.Count;
            if (!await TrySendAsync(connection, sendLock, replies.ToArray(), cancellationToken).ConfigureAwait(false))
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

    /// <summary>Sends bytes to the server, reporting whether the connection took them.</summary>
    private static async Task<bool> TrySendAsync(
        IConnection connection,
        SemaphoreSlim sendLock,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (bytes.Length == 0)
        {
            return true;
        }

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
