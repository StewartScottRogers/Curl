using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// What one transfer learned beyond its exit code and byte count: the facts the
/// <c>-w</c>/<c>--write-out</c> renderer prints and the <c>-L</c>/<c>--location</c>
/// follower decides on. Every member defaults to "not known" (<c>0</c>,
/// <see langword="null" /> or empty), so a handler sets only what it learned; see
/// ADR-0015.
/// </summary>
public sealed record TransferReport
{
    /// <summary>
    /// Gets the status code of the last response, the source of <c>%{response_code}</c>
    /// and <c>%{http_code}</c>; <c>0</c> before any response.
    /// </summary>
    public int ResponseCode { get; init; }

    /// <summary>
    /// Gets the status code of the proxy's reply to the CONNECT that opened a tunnel,
    /// copied from <see cref="ConnectResult.ProxyConnectResponseCode" />, the source of
    /// <c>%{http_connect}</c>; <c>0</c> when there was no CONNECT.
    /// </summary>
    public int ProxyConnectResponseCode { get; init; }

    /// <summary>
    /// Gets the protocol version of the last response's status line, the source of
    /// <c>%{http_version}</c>; <see langword="null" /> for a non-HTTP transfer or before
    /// any response.
    /// </summary>
    public Version? HttpVersion { get; init; }

    /// <summary>
    /// Gets the request method as sent, the source of <c>%{method}</c>;
    /// <see langword="null" /> when none was sent.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Gets every header of the last response, name and value as received, in the order
    /// received, duplicates kept; empty when there was none.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders { get; init; } = [];

    /// <summary>
    /// Gets every header line a handler synthesised for the header stream that curl's
    /// header API does not hold, such as the <c>Content-Length</c>,
    /// <c>Accept-ranges</c> and <c>Last-Modified</c> lines of a <c>file://</c> transfer,
    /// name and value in the order written; empty when there was none. They count
    /// towards <c>%{num_headers}</c> but, unlike <see cref="ResponseHeaders" />, are
    /// never found by <c>%header{}</c>. See ADR-0052.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> PseudoHeaders { get; init; } = [];

    /// <summary>
    /// Gets the last response's <c>Content-Type</c> value, the source of
    /// <c>%{content_type}</c>; <see langword="null" /> when absent.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the last response's <c>Location</c>, resolved against the request URL whether
    /// or not <c>-L</c> was given, the source of <c>%{redirect_url}</c>;
    /// <see langword="null" /> when there is none.
    /// </summary>
    public string? RedirectUrl { get; init; }

    /// <summary>
    /// Gets the URL of the last request made, set by the redirect follower, the source of
    /// <c>%{url_effective}</c>; <see langword="null" /> from a handler, meaning
    /// <see cref="ITransferContext.Url" />.
    /// </summary>
    public string? EffectiveUrl { get; init; }

    /// <summary>
    /// Gets the number of redirects followed, set by the redirect follower, the source of
    /// <c>%{num_redirects}</c>; <c>0</c> from a handler.
    /// </summary>
    public int RedirectCount { get; init; }

    /// <summary>
    /// Gets the bytes of every response header block received, status lines and blank
    /// lines included, the source of <c>%{size_header}</c>.
    /// </summary>
    public long HeaderSize { get; init; }

    /// <summary>
    /// Gets the bytes of every request sent, each request header block and the body bytes
    /// sent after it, the source of <c>%{size_request}</c>.
    /// </summary>
    public long RequestSize { get; init; }

    /// <summary>
    /// Gets the body bytes received, after transfer decoding and before content decoding,
    /// the source of <c>%{size_download}</c> and <c>%{speed_download}</c>.
    /// </summary>
    public long DownloadSize { get; init; }

    /// <summary>
    /// Gets the body bytes sent, the source of <c>%{size_upload}</c> and
    /// <c>%{speed_upload}</c>.
    /// </summary>
    public long UploadSize { get; init; }

    /// <summary>
    /// Gets the number of connections the transfer opened, <c>0</c> when it reused one,
    /// the source of <c>%{num_connects}</c>.
    /// </summary>
    public int ConnectionCount { get; init; }

    /// <summary>
    /// Gets the local address and port of the connection, copied from
    /// <see cref="ConnectResult.LocalEndPoint" />, the source of <c>%{local_ip}</c> and
    /// <c>%{local_port}</c>; <see langword="null" /> when unknown.
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; init; }

    /// <summary>
    /// Gets the address and port of the peer, the proxy's when a proxy is used, the
    /// source of <c>%{remote_ip}</c> and <c>%{remote_port}</c>; <see langword="null" />
    /// when unknown.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the transfer's timestamps, the source of the <c>%{time_*}</c> variables;
    /// <see langword="null" /> when the handler recorded none.
    /// </summary>
    public TransferTimings? Timings { get; init; }

    /// <summary>
    /// Gets the DER encoding of every certificate the server sent in the TLS handshake,
    /// its own first, copied from <see cref="ConnectResult.PeerCertificates" />, the source
    /// of <c>%{certs}</c> and <c>%{num_certs}</c> (ADR-0053); empty for a transfer without
    /// TLS.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> PeerCertificates { get; init; } = [];
}
