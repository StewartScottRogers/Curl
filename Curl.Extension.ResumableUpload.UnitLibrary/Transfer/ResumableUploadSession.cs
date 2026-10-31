using Curl.Extension.ResumableUpload.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Sends a resumable upload's requests through curl's connector, keeping a connection open while the server lets it
/// persist (RFC 9112 §9.3) and the next request goes to the same host and port, and opening another otherwise.
/// </summary>
/// <param name="connector">Curl's connector.</param>
/// <param name="context">The transfer: its proxy, <c>-H</c> lines and cancellation.</param>
internal sealed class ResumableUploadSession(IConnector connector, ITransferContext context) : IAsyncDisposable
{
    private ConnectTarget? target;
    private IConnection? connection;
    private ResumableUploadHttpClientConnection? client;

    /// <summary>Sends one request and reads its final response whole.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="uri">Where the request goes.</param>
    /// <param name="headers">The flow's header fields; <c>Host</c> and <c>-H</c>'s are added.</param>
    /// <param name="body">The whole upload, seekable, or <see langword="null" /> for a request without a body.</param>
    /// <param name="offset">Where in <paramref name="body" /> the request's content starts.</param>
    /// <returns>The request and its final response.</returns>
    /// <exception cref="ResumableUploadTransferException">The connection failed or broke, or the response was malformed.</exception>
    public async Task<ResumableUploadExchange> SendAsync(
        string method, Uri uri, IReadOnlyList<KeyValuePair<string, string>> headers, Stream? body = null, long offset = 0)
    {
        var http = await ConnectAsync(uri);
        var request = new ResumableUploadHttpRequest(
            method, uri.PathAndQuery, ResumableUploadRequestHeaders.For(uri, headers, context), body, body is null ? 0 : body.Length - offset, offset);
        body?.Position = offset;
        ResumableUploadTransferException failure;
        try
        {
            return await ExchangeAsync(http, request, uri);
        }
        catch (ResumableUploadHttpException exception)
        {
            failure = exception.Failure is ResumableUploadHttpFailure.ClosedEarly
                ? new ResumableUploadTransferException(CurlExitCode.RecvError, $"Recv failure: {exception.Message}", canResume: true)
                : new ResumableUploadTransferException(CurlExitCode.WeirdServerReply, exception.Message, canResume: false);
        }
        catch (ResumableUploadTransferException exception)
        {
            failure = exception;
        }

        await CloseAsync();
        throw failure;
    }

    private async Task<ResumableUploadExchange> ExchangeAsync(ResumableUploadHttpClientConnection http, ResumableUploadHttpRequest request, Uri uri)
    {
        var response = await http.SendAsync(request, context.CancellationToken);
        var content = new MemoryStream();
        await response.Body.CopyToAsync(content, context.CancellationToken);
        if (!response.ConnectionPersists)
        {
            await CloseAsync();
        }

        return new ResumableUploadExchange(request.Method, uri, response.StatusCode, response.ReasonPhrase, response.Headers, content.ToArray());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => CloseAsync();

    private async Task<ResumableUploadHttpClientConnection> ConnectAsync(Uri uri)
    {
        var wanted = new ConnectTarget(uri.IdnHost, uri.Port, uri.Scheme == Uri.UriSchemeHttps) { Proxy = context.Proxy };
        if (client is not null && wanted == target)
        {
            return client;
        }

        await CloseAsync();
        var result = await connector.ConnectAsync(wanted, context.CancellationToken);
        if (result.Connection is not { } opened)
        {
            throw new ResumableUploadTransferException(result.ExitCode, result.ErrorMessage!, canResume: false);
        }

        connection = new ResumableUploadInterruptibleConnection(opened);
        target = wanted;
        return client = new ResumableUploadHttpClientConnection(connection);
    }

    private async ValueTask CloseAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        connection = null;
        client = null;
        target = null;
    }
}
