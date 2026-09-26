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
/// <see cref="ITransferContext.TelnetOptions" /> is read by <see cref="TelnetOptionParser" />
/// once connected, as curl reads it: a bad option ends the transfer with exit 48 or 49
/// before a byte is sent.
/// </para>
/// </remarks>
public sealed class TelnetProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The port a <c>telnet</c> URL without one connects to.</summary>
    private const int DefaultPort = 23;

    /// <summary>The most bytes read from the server or the upload at once.</summary>
    private const int BufferSize = 16384;

    private const string BadFunctionArgumentMessage = "A libcurl function was given a bad argument";

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
            TransferResult? optionFailure = TelnetOptionParser.Parse(context.TelnetOptions, optionValues);
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
        using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        Task upload = context.Upload is { } source
            ? SendUploadAsync(source, connection, sendLock, uploadCancellation.Token)
            : Task.CompletedTask;

        try
        {
            return await ReceiveUntilClosedAsync(connection, context, optionValues, sendLock).ConfigureAwait(false);
        }
        finally
        {
            uploadCancellation.Cancel();
            _ = upload.ContinueWith(
                static finished => finished.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private static async Task<TransferResult> ReceiveUntilClosedAsync(
        IConnection connection,
        ITransferContext context,
        TelnetOptionValues optionValues,
        SemaphoreSlim sendLock)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        var receiver = new TelnetReceiver(optionValues);
        var buffer = new byte[BufferSize];
        var data = new List<byte>();
        var replies = new List<byte>();
        long bytesWritten = 0;

        while (true)
        {
            int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return TransferResult.Success(bytesWritten);
            }

            data.Clear();
            replies.Clear();
            TelnetReceiveError error = receiver.Receive(buffer.AsSpan(0, read), data, replies);
            await context.Output.WriteAsync(data.ToArray(), cancellationToken).ConfigureAwait(false);
            bytesWritten += data.Count;
            await SendAsync(connection, sendLock, replies.ToArray(), cancellationToken).ConfigureAwait(false);

            if (error != TelnetReceiveError.None)
            {
                return ToFailure(error, bytesWritten);
            }
        }
    }

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
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await upload.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            byte[] escaped = EscapeInterpretAsCommand(buffer.AsSpan(0, read));
            await SendAsync(connection, sendLock, escaped, cancellationToken).ConfigureAwait(false);
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

    private static async Task SendAsync(
        IConnection connection,
        SemaphoreSlim sendLock,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            sendLock.Release();
        }
    }
}
