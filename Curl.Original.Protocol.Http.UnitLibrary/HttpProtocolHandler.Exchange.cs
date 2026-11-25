using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that defines HttpExchange, the parts of one exchange.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// What one exchange has learned so far, turned into a <see cref="TransferReport" /> when
    /// the transfer ends, successfully or not.
    /// </summary>
    /// <param name="connect">The connect that opened the connection.</param>
    /// <param name="connection">The connection the exchange runs on.</param>
    /// <param name="method">The request's method.</param>
    /// <param name="headSize">The request head's size in bytes.</param>
    /// <param name="upload">The writer that sends the request body.</param>
    /// <param name="earlier">
    /// The report of the exchange a retry answers, whose request and header sizes and
    /// connections this one's report adds to, as curl 8.21.0's <c>%{size_request}</c>,
    /// <c>%{size_header}</c> and <c>%{num_connects}</c> do (BL-181 and BL-260 Notes);
    /// <see langword="null" /> for the first exchange.
    /// </param>
    /// <param name="newConnection">
    /// <see langword="true" /> when this exchange opened its connection, rather than reusing
    /// the one <paramref name="earlier" /> used.
    /// </param>
    private sealed class HttpExchange(
        ConnectResult connect,
        IConnection connection,
        string method,
        int headSize,
        HttpRequestBodyWriter upload,
        TransferReport? earlier,
        bool newConnection)
    {
        /// <summary>
        /// Gets or sets the final response head, <see langword="null" /> until it is read.
        /// </summary>
        internal HttpResponseHead? Head { get; set; }

        /// <summary>
        /// Gets a value indicating whether the transfer goes through a proxy, forwarded or
        /// tunnelled, the report's <see cref="TransferReport.UsedProxy" />.
        /// </summary>
        internal bool UsedProxy { get; init; }

        /// <summary>
        /// Gets or sets the final head's <c>Location</c> resolved against the request URL,
        /// <see langword="null" /> until the head is read or when it names none.
        /// </summary>
        internal string? RedirectUrl { get; set; }

        /// <summary>
        /// Gets the moment the transfer began, the report's <see cref="TransferTimings.Started" />.
        /// </summary>
        internal required long Started { get; init; }

        /// <summary>
        /// Gets the clock every timestamp of the exchange is taken on.
        /// </summary>
        internal required TimeProvider TimeProvider { get; init; }

        /// <summary>
        /// Gets the connection the response is read through, which records when its first
        /// byte arrived.
        /// </summary>
        internal required HttpFirstByteTimingConnection ResponseConnection { get; init; }

        /// <summary>
        /// Gets how many resends after a 417 this transfer made before this exchange, the
        /// report's <see cref="TransferReport.RedirectCount" /> (BL-396 Notes).
        /// </summary>
        internal int RedirectCount { get; init; }

        /// <summary>
        /// Gets the scheme a server picked for the exchange's request, the report's
        /// <see cref="TransferReport.AuthSchemePicked" /> (BL-1819).
        /// </summary>
        internal HttpAuthSchemes AuthSchemePicked { get; init; }

        /// <summary>
        /// Gets the scheme the exchange's request answered a proxy's challenge with, the
        /// report's <see cref="TransferReport.ProxyAuthSchemePicked" /> (BL-2367).
        /// </summary>
        internal HttpAuthSchemes ProxyAuthSchemePicked { get; init; }

        /// <summary>
        /// Gets or sets every scheme a 401 offered during the transfer up to this exchange's
        /// response, the report's <see cref="TransferReport.AuthSchemesOffered" /> (BL-3256).
        /// </summary>
        internal HttpAuthSchemes AuthSchemesOffered { get; set; }

        /// <summary>
        /// Gets or sets every scheme a 407 offered during the transfer up to this exchange's
        /// response, the report's <see cref="TransferReport.ProxyAuthSchemesOffered" /> (BL-3256).
        /// </summary>
        internal HttpAuthSchemes ProxyAuthSchemesOffered { get; set; }

        /// <summary>
        /// Gets the Digest value the exchange's request was sent with, the report's
        /// <see cref="TransferReport.DigestAuthorizationSent" /> (BL-2252).
        /// </summary>
        internal string? DigestAuthorizationSent { get; init; }

        /// <summary>
        /// Gets a value indicating whether the transfer held a Digest nonce before the exchange's
        /// response (<see cref="HttpRequestPlan.HoldsDigestNonce" />), from which the report's
        /// <see cref="TransferReport.DigestStaysAvailable" /> is decided (BL-2820).
        /// </summary>
        internal bool HeldDigestNonce { get; init; }

        /// <summary>
        /// Gets a value indicating whether the exchange's request was sent with an NTLM
        /// <c>Authorization</c> value, whose handshake a bare <c>NTLM</c> challenge in the response
        /// refuses (<see cref="TransferReport.DigestStaysAvailable" />, BL-2823).
        /// </summary>
        internal bool SentNtlmAuthorization { get; init; }

        /// <summary>
        /// Gets or sets the moment the first request byte was about to be sent,
        /// <see langword="null" /> until then.
        /// </summary>
        internal long? RequestReady { get; set; }

        /// <summary>
        /// Gets or sets the moment the last request byte was sent, <see langword="null" />
        /// until then.
        /// </summary>
        internal long? RequestSent { get; set; }

        /// <summary>
        /// Builds the report: what the connect and request told, their timings with the
        /// transfer's end taken now, and what the final head told once it was read. The request
        /// size counts the body bytes sent, chunk framing included, as well as the head once the request was ready to send (a request refused before it, as an HTTP/1.0 upload of unknown size is, counts none: CGR-263), as curl 8.21.0's
        /// <c>%{size_request}</c> does (BL-175 Notes); the upload size counts the chunk framing too, as
        /// libcurl 8.22.0's <c>CURLINFO_SIZE_UPLOAD_T</c> does (BL-3320).
        /// </summary>
        /// <param name="body">
        /// The body reader, whose bytes accepted before and after content decoding are
        /// <c>%{size_download}</c> and <c>%{size_delivered}</c> (BL-516).
        /// </param>
        /// <returns>The report.</returns>
        internal TransferReport Report(HttpResponseBodyReader body)
        {
            TransferReport report = new()
            {
                Method = method,
                RequestSize = RequestSizeSoFar() + HeadSizeSent() + upload.BytesSent,
                UploadSize = upload.BytesSent,
                DownloadSize = body.BytesWritten,
                DeliveredSize = body.BytesDelivered,
                HeaderSize = earlier?.HeaderSize ?? connect.ProxyConnectHeaderBytes,
                ConnectionCount = ConnectionCountSoFar() + (newConnection ? 1 : 0),
                OsErrno = earlier?.OsErrno ?? 0,
                RedirectCount = RedirectCount,
                ResponseHeadersStored = body.HeadersStored,
                AuthSchemePicked = AuthSchemePicked,
                ProxyAuthSchemePicked = ProxyAuthSchemePicked,
                AuthSchemesOffered = AuthSchemesOffered,
                ProxyAuthSchemesOffered = ProxyAuthSchemesOffered,
                DigestAuthorizationSent = DigestAuthorizationSent,
                ProxyConnectResponseCode = connect.ProxyConnectResponseCode,
                UsedProxy = UsedProxy,
                LocalEndPoint = connect.LocalEndPoint ?? connection.LocalEndPoint as IPEndPoint,
                PeerCertificates = connect.PeerCertificates,
                RemoteEndPoint = connection.RemoteEndPoint as IPEndPoint,
                Timings = TimingsNow(),
            };
            return Head is null ? report : WithHead(report, Head, body.StreamTrailers) with { TrailerCount = body.StreamTrailers.Count + body.ChunkedTrailerCount, RedirectUrl = RedirectUrl, DigestStaysAvailable = DigestStaysAvailableAfter(HeldDigestNonce, SentNtlmAuthorization, Head) };
        }

        private long RequestSizeSoFar() => earlier?.RequestSize ?? 0;

        private int HeadSizeSent() => RequestReady is null ? 0 : headSize;

        private int ConnectionCountSoFar() => earlier?.ConnectionCount ?? 0;

        private TransferTimings TimingsNow() =>
            new(
                Started,
                connect.Timings,
                RequestReady,
                RequestSent,
                ResponseConnection.FirstByteReceived,
                TimeProvider.GetTimestamp());

        private static TransferReport WithHead(TransferReport report, HttpResponseHead head, IReadOnlyList<KeyValuePair<string, string>> streamTrailers) =>
            report with
            {
                ResponseCode = head.StatusLine.StatusCode,
                HttpVersion = head.StatusLine.Version,
                // HTTP/2 and HTTP/3 trailers stay out of the headers, so %header{} and %{header_json} leave them out as curl does (BL-5392); their count is TrailerCount.
                ResponseHeaders = [.. head.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value))],
                TrailerCount = streamTrailers.Count,
                ContentType = head.Headers.LastOrDefault(IsContentType)?.Value,
                HeaderSize = report.HeaderSize + head.HeadBytes.Length,
            };

        private static bool IsContentType(HttpResponseHeader header) =>
            string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase);
    }
}
