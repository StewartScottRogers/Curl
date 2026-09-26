using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Serves the <c>http</c> and <c>https</c> schemes over HTTP/1.1: connects through the
/// injected <see cref="IConnector" />, sends the request head, writes the response head to
/// <see cref="ITransferContext.HeaderOutput" /> and the body to
/// <see cref="ITransferContext.Output" />, and reports what it learned in a
/// <see cref="TransferReport" />.
/// </summary>
/// <param name="connector">
/// Opens the connection for each transfer. <c>https</c> asks it for TLS with
/// <see cref="ConnectTarget.UseTls" />; the handler does no TLS of its own (ADR-0005).
/// </param>
/// <param name="authenticator">
/// Answers authentication challenges. Held for the authentication task (BL-181); this
/// handler sends no <c>Authorization</c> header of its own yet.
/// </param>
/// <param name="cookieStore">
/// The cookies to send and store, or <see langword="null" /> when cookies are off. Held for
/// the cookie task (BL-182); this handler sends and stores no cookie yet.
/// </param>
/// <remarks>
/// <para>
/// The port is the URL's, or 80 for <c>http</c> and 443 for <c>https</c> when it names none.
/// A connect failure (exit 6, 7, 35, 60 and the like) is returned with the connector's exit
/// code and message unchanged. Every other failure while the response is read is returned
/// with the exit code and message curl 8.21.0 reports, and with a report of whatever was
/// learned before it; only cancellation leaves this handler as an exception.
/// </para>
/// <para>
/// A body in <see cref="HttpRequestOptions.Body" /> makes the request a POST unless <c>-X</c>
/// names another method, and is sent after the head as <see cref="HttpRequestFraming" />
/// decides: at once, or after up to one second's wait for <c>100 Continue</c>, and not at all
/// when a final status arrives during that wait. A body stream of known length that fails a
/// read ends the transfer with exit 26.
/// </para>
/// <para>
/// Every response head read, 1xx heads included, is written to the header output as one
/// write, exactly as received with continuation lines folded, before any body byte; a
/// chunked body's trailers follow the body. A header output that fails a write ends the
/// transfer with exit 23.
/// </para>
/// <para>
/// <see cref="ITransferContext.NoBody" /> (<c>-I</c>) sends HEAD unless <c>-X</c> names
/// another method, and reads and writes no body whatever the method. A final status of 400 or
/// above ends the transfer with exit 22 for <c>-f</c>, after the head is written and before
/// any body is read, and for <c>--fail-with-body</c> after the body is written, as curl
/// 8.21.0 does (BL-176 Notes).
/// </para>
/// <para>
/// A 3xx response's <c>Location</c> is resolved against the request URL into
/// <see cref="TransferReport.RedirectUrl" /> (<see cref="HttpRedirectLocation" />), with or
/// without <c>-L</c>. Under <see cref="HttpRequestOptions.FollowRedirects" /> the body of a
/// response that has one is read and discarded, counted in the download size as curl counts
/// it, while its head and trailers are still written to the header output (BL-179 Notes).
/// </para>
/// <para>
/// <see cref="HttpRequestOptions.Compressed" /> sends <c>Accept-Encoding: deflate, gzip, br</c>
/// (ADR-0020) and, unless <see cref="HttpRequestOptions.Raw" /> is set, decodes the body as
/// its Content-Encoding says (<see cref="HttpContentDecoder" />, BL-177 Notes).
/// </para>
/// </remarks>
public sealed class HttpProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    ICookieStore? cookieStore = null) : IProtocolHandler
{
    private static readonly string[] Schemes = ["http", "https"];

    private readonly IConnector connector = connector ?? throw new ArgumentNullException(nameof(connector));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <summary>
    /// Gets the authenticator the handler was given.
    /// </summary>
    internal IHttpAuthenticator Authenticator { get; } =
        authenticator ?? throw new ArgumentNullException(nameof(authenticator));

    /// <summary>
    /// Gets the cookie store the handler was given, or <see langword="null" /> when cookies are off.
    /// </summary>
    internal ICookieStore? CookieStore { get; } = cookieStore;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ConnectResult connect = await connector.ConnectAsync(TargetOf(context.Url), context.CancellationToken)
            .ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return TransferResult.Failure(connect.ExitCode, connect.ErrorMessage!);
        }

        await using (connection.ConfigureAwait(false))
        {
            return await ExchangeAsync(context, connect, connection).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the connect target for <paramref name="url" />: its host without IPv6
    /// brackets, its port, and TLS for <c>https</c>.
    /// </summary>
    private static ConnectTarget TargetOf(Uri url) =>
        new(url.DnsSafeHost, url.Port, string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sends the request on <paramref name="connection" /> and reads the response into the
    /// transfer's outputs.
    /// </summary>
    private static async ValueTask<TransferResult> ExchangeAsync(
        ITransferContext context,
        ConnectResult connect,
        IConnection connection)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        HttpRequestFraming framing = HttpRequestFraming.Of(options, [.. options.Headers.Select(HttpCustomHeader.Parse)], context.NoBody);
        byte[] request = HttpRequestHeadFormatter.Format(context.Url, options, context.NoBody);
        await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);

        HttpRequestBodyWriter upload = new(connection);
        HttpExchange exchange = new(connect, connection, framing.Method, request.Length, upload);
        IConnection responseConnection = framing.AwaitsContinue ? new HttpContinueWaitConnection(connection) : connection;
        HttpResponseBodyReader body = new(responseConnection);
        try
        {
            await SendBodyAsync(context, framing, responseConnection, upload).ConfigureAwait(false);
            exchange.Head = await new HttpResponseHeadReader(responseConnection).ReadAsync(cancellationToken).ConfigureAwait(false);
            exchange.RedirectUrl = HttpRedirectLocation.Find(context.Url, exchange.Head);
            await WriteHeadersAsync(context.HeaderOutput, exchange.Head.HeadBytes, cancellationToken).ConfigureAwait(false);
            ThrowIfFailing(options.Fail, HttpFailMode.Fail, exchange.Head);
            bool followsRedirect = options.FollowRedirects && exchange.RedirectUrl is not null;
            Stream bodyOutput = followsRedirect ? Stream.Null : context.Output;
            await body.CopyAsync(exchange.Head, context.NoBody, bodyOutput, DecodesContent(options, followsRedirect), cancellationToken)
                .ConfigureAwait(false);
            await WriteHeadersAsync(context.HeaderOutput, body.TrailerBytes, cancellationToken).ConfigureAwait(false);
            ThrowIfFailing(options.Fail, HttpFailMode.FailWithBody, exchange.Head);
        }
        catch (HttpTransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message, body.BytesWritten)
                with
            { Report = exchange.Report(body.BytesWritten) };
        }

        return TransferResult.Success(body.BytesWritten) with { Report = exchange.Report(body.BytesWritten) };
    }

    /// <summary>
    /// Decides whether the body is decoded: for <c>--compressed</c> without <c>--raw</c>, and
    /// not for a 3xx body that <c>-L</c> reads and discards, which curl 8.21.0 does not
    /// decode (measured, BL-177 Notes).
    /// </summary>
    private static bool DecodesContent(HttpRequestOptions options, bool followsRedirect) =>
        options.Compressed && !options.Raw && !followsRedirect;

    /// <summary>
    /// Sends the request body, if there is one: at once, or once
    /// <paramref name="responseConnection" /> has waited for <c>100 Continue</c> and not been
    /// answered with a final status instead.
    /// </summary>
    private static async ValueTask SendBodyAsync(
        ITransferContext context,
        HttpRequestFraming framing,
        IConnection responseConnection,
        HttpRequestBodyWriter upload)
    {
        if (framing.Body is not { } requestBody)
        {
            return;
        }

        if (responseConnection is HttpContinueWaitConnection waiting
            && !await waiting.WaitForContinueAsync(context.TimeProvider, context.CancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await upload.WriteAsync(requestBody, framing.IsChunked, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the transfer with exit 22 when <paramref name="fail" /> is
    /// <paramref name="failAt" /> and the final status is 400 or above: before the body for
    /// <c>-f</c>, after it for <c>--fail-with-body</c>. The head is written first either way.
    /// </summary>
    /// <exception cref="HttpTransferException">The status fails the transfer (exit 22).</exception>
    private static void ThrowIfFailing(HttpFailMode fail, HttpFailMode failAt, HttpResponseHead head)
    {
        int statusCode = head.StatusLine.StatusCode;
        if (fail == failAt && statusCode >= 400)
        {
            throw new HttpTransferException(CurlExitCode.HttpReturnedError, HttpTransferMessages.RequestedUrlReturnedError(statusCode));
        }
    }

    /// <summary>
    /// Writes head or trailer bytes to the header output as one write, or nothing when there
    /// is no header output or no bytes.
    /// </summary>
    /// <exception cref="HttpTransferException">The header output failed the write (exit 23).</exception>
    private static async ValueTask WriteHeadersAsync(Stream? headerOutput, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (headerOutput is null || bytes.IsEmpty)
        {
            return;
        }

        try
        {
            await headerOutput.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OutputWriteFailedException failure)
        {
            throw WriteFailed(bytes.Length, failure.BytesAccepted);
        }
        catch (IOException)
        {
            throw WriteFailed(bytes.Length, 0);
        }
    }

    private static HttpTransferException WriteFailed(int passed, int returned) =>
        new(CurlExitCode.WriteError, HttpTransferMessages.OutputWriteFailed(passed, returned));

    /// <summary>
    /// What one exchange has learned so far, turned into a <see cref="TransferReport" /> when
    /// the transfer ends, successfully or not.
    /// </summary>
    private sealed class HttpExchange(
        ConnectResult connect,
        IConnection connection,
        string method,
        int headSize,
        HttpRequestBodyWriter upload)
    {
        /// <summary>
        /// Gets or sets the final response head, <see langword="null" /> until it is read.
        /// </summary>
        internal HttpResponseHead? Head { get; set; }

        /// <summary>
        /// Gets or sets the final head's <c>Location</c> resolved against the request URL,
        /// <see langword="null" /> until the head is read or when it names none.
        /// </summary>
        internal string? RedirectUrl { get; set; }

        /// <summary>
        /// Builds the report: what the connect and request told, and what the final head told
        /// once it was read. The request size counts the body bytes sent as well as the head,
        /// as curl 8.21.0's <c>%{size_request}</c> does (BL-175 Notes).
        /// </summary>
        /// <param name="downloadSize">The body bytes the output accepted.</param>
        /// <returns>The report.</returns>
        internal TransferReport Report(long downloadSize)
        {
            TransferReport report = new()
            {
                Method = method,
                RequestSize = headSize + upload.BytesWritten,
                UploadSize = upload.BytesWritten,
                DownloadSize = downloadSize,
                ConnectionCount = 1,
                ProxyConnectResponseCode = connect.ProxyConnectResponseCode,
                LocalEndPoint = connect.LocalEndPoint,
                RemoteEndPoint = connection.RemoteEndPoint as IPEndPoint,
            };
            return Head is null ? report : WithHead(report, Head) with { RedirectUrl = RedirectUrl };
        }

        private static TransferReport WithHead(TransferReport report, HttpResponseHead head) =>
            report with
            {
                ResponseCode = head.StatusLine.StatusCode,
                HttpVersion = head.StatusLine.Version,
                ResponseHeaders = [.. head.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value))],
                ContentType = head.Headers.LastOrDefault(IsContentType)?.Value,
                HeaderSize = head.HeadBytes.Length,
            };

        private static bool IsContentType(HttpResponseHeader header) =>
            string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase);
    }
}
