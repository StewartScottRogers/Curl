using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Dict;

/// <summary>
/// Serves the <c>dict</c> scheme: sends the lookup the URL's path names and writes the
/// server's whole reply to the output, unaltered, until the server closes the connection.
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port (ADR-0005). No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <remarks>
/// Matches curl 8.21.0, measured against a loopback listener. The port defaults to 2628.
/// The request, encoded by <see cref="DictRequest" />, is sent whole without waiting for
/// the server's greeting. A server that closes without sending anything ends the transfer
/// with exit 0 and nothing written. A connect failure is returned as the connector
/// reported it. A path that decodes to a control character is refused after connecting,
/// with exit 3 (<see cref="CurlExitCode.UrlMalformat" />), nothing sent and nothing written.
/// </remarks>
public sealed class DictProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The port a <c>dict</c> URL without one connects to.</summary>
    private const int DefaultPort = 2628;

    /// <summary>The most bytes read from the server at once.</summary>
    private const int BufferSize = 16384;

    private const string UrlMalformatMessage = "URL using bad/illegal format or missing URL";

    /// <summary>
    /// The one scheme this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names it.
    /// </summary>
    private static readonly string[] Schemes = ["dict"];

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
            if (!DictRequest.TryEncode(url.AbsolutePath, out byte[] request))
            {
                return TransferResult.Failure(CurlExitCode.UrlMalformat, UrlMalformatMessage);
            }

            await connection.WriteAsync(request, context.CancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            return await CopyReplyAsync(connection, context).ConfigureAwait(false);
        }
    }

    private static async Task<TransferResult> CopyReplyAsync(IConnection connection, ITransferContext context)
    {
        var buffer = new byte[BufferSize];
        long bytesWritten = 0;
        int read;
        while ((read = await connection.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false)) > 0)
        {
            await context.Output.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken).ConfigureAwait(false);
            bytesWritten += read;
        }

        return TransferResult.Success(bytesWritten);
    }
}
