using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Serves the <c>gopher</c> and <c>gophers</c> schemes by sending the URL's selector and
/// writing the server's reply to the output unaltered, as curl 8.21.0 does.
/// </summary>
/// <remarks>
/// <para>
/// The connection comes from the injected <see cref="IConnector" /> (ADR-0005): port 70
/// for both schemes unless the URL names one, and <c>gophers</c> differs only in asking
/// for TLS. No <see cref="System.Net.Sockets.Socket" /> or <c>SslStream</c> is
/// constructed here.
/// </para>
/// <para>
/// The selector is built by <see cref="GopherSelector" /> and sent followed by CRLF. The
/// reply is copied to <see cref="ITransferContext.Output" /> until the server closes the
/// connection, which ends the transfer with exit 0, even when nothing was received. A
/// selector that decodes to a NUL byte is exit 3 (<see cref="CurlExitCode.UrlMalformat" />),
/// found after connecting as in curl. A failed connect is returned unchanged; a failed
/// send is exit 55, a failed receive exit 56 and a failed output write exit 23, all
/// returned rather than thrown. The exit 23 message names the size of the read that failed
/// to write (at most 16384 bytes, as curl 8.21.0 reads gopher) and the bytes of it the
/// output accepted, from <see cref="OutputWriteFailedException.BytesAccepted" />.
/// Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class GopherProtocolHandler : IProtocolHandler
{
    private const int DefaultPort = 70;

    private const int ReadBufferSize = 16384;

    /// <summary>
    /// The two schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol
    /// list names them.
    /// </summary>
    private static readonly string[] Schemes = ["gopher", "gophers"];

    private static readonly byte[] LineEnd = "\r\n"u8.ToArray();

    private readonly IConnector connector;

    /// <summary>
    /// Initializes a new instance of the <see cref="GopherProtocolHandler" /> class.
    /// </summary>
    /// <param name="connector">The connector each transfer's connection comes from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connector" /> is <see langword="null" />.</exception>
    public GopherProtocolHandler(IConnector connector)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

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

        ConnectResult connected = await connector
            .ConnectAsync(CreateTarget(context), context.CancellationToken)
            .ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new TransferResult(connected.ExitCode, 0, connected.ErrorMessage) { IsConnectionRefused = connected.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            if (GopherSelector.FromUrl(context.Url) is not { } selector)
            {
                return TransferResult.Failure(CurlExitCode.UrlMalformat, GopherTransferMessages.SelectorMalformed);
            }

            if (!await TrySendAsync(connection, selector, context.CancellationToken).ConfigureAwait(false))
            {
                return TransferResult.Failure(CurlExitCode.SendError, GopherTransferMessages.SendFailed);
            }

            return await CopyReplyAsync(connection, context.Output, context.Progress, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static ConnectTarget CreateTarget(ITransferContext context)
    {
        CurlUrl url = context.Url;
        return new(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, url.Scheme == "gophers")
        {
            Proxy = context.Proxy,
        };
    }

    private static async ValueTask<bool> TrySendAsync(
        IConnection connection,
        byte[] selector,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(selector, cancellationToken).ConfigureAwait(false);
            await connection.WriteAsync(LineEnd, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async ValueTask<TransferResult> CopyReplyAsync(
        IConnection connection,
        Stream output,
        ITransferProgress progress,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[ReadBufferSize];
        long bytesWritten = 0;
        while (true)
        {
            int read;
            try
            {
                read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return new TransferResult(CurlExitCode.RecvError, bytesWritten, GopherTransferMessages.ReceiveFailed);
            }

            if (read == 0)
            {
                return TransferResult.Success(bytesWritten);
            }

            try
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                return new TransferResult(
                    CurlExitCode.WriteError,
                    bytesWritten,
                    GopherTransferMessages.OutputWriteFailed(read, BytesAcceptedBy(exception)));
            }

            bytesWritten += read;
            progress.ReportDownloaded(bytesWritten, null);
        }
    }

    /// <summary>
    /// How many bytes of a failed write the output accepted: the count an
    /// <see cref="OutputWriteFailedException" /> carries, and 0 for any other
    /// <see cref="IOException" />.
    /// </summary>
    private static int BytesAcceptedBy(IOException exception) =>
        exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
}
