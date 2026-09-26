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
/// ends it with exit 43 (<see cref="CurlExitCode.BadFunctionArgument" />), because no
/// <c>-t</c> option supplied one, and a malformed subnegotiation ends it with exit 56
/// (<see cref="CurlExitCode.RecvError" />).
/// </para>
/// <para>
/// <see cref="ITransferContext.TelnetOptions" /> is not read yet.
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
            return await RunSessionAsync(connection, context).ConfigureAwait(false);
        }
    }

    private static async Task<TransferResult> RunSessionAsync(IConnection connection, ITransferContext context)
    {
        var sendLock = new SemaphoreSlim(1, 1);
        using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        Task upload = context.Upload is { } source
            ? SendUploadAsync(source, connection, sendLock, uploadCancellation.Token)
            : Task.CompletedTask;

        try
        {
            return await ReceiveUntilClosedAsync(connection, context, sendLock).ConfigureAwait(false);
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
        SemaphoreSlim sendLock)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        var receiver = new TelnetReceiver();
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
        error == TelnetReceiveError.SubnegotiationValueMissing
            ? new TransferResult(CurlExitCode.BadFunctionArgument, bytesWritten, BadFunctionArgumentMessage)
            : new TransferResult(CurlExitCode.RecvError, bytesWritten, SuboptionErrorMessage);

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
