using System.Collections.ObjectModel;
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
    /// Gets a value indicating whether the transfer was set to go through a proxy, forwarded
    /// or tunnelled, even when connecting to it failed, the source of <c>%{proxy_used}</c>;
    /// <see langword="false" /> for a direct transfer (ADR-0058).
    /// </summary>
    public bool UsedProxy { get; init; }

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
    /// Gets how many trailer fields an HTTP/2 response ended with, already in <see cref="ResponseHeaders" />;
    /// zero when there were none. When above zero, <c>%{num_headers}</c> is this count alone, as curl 8.18.0
    /// with nghttp2 1.68 replaces the head's header count with the trailers' (BL-4025).
    /// </summary>
    public int TrailerCount { get; init; }

    /// <summary>
    /// Gets every header of every response before the last one in a <c>-L</c> chain, hop by
    /// hop and in the order received, duplicates kept; empty when no redirect was followed.
    /// <c>%header{name:all:separator}</c> writes these values before the last response's.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> EarlierResponseHeaders { get; init; } = [];

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
    /// <c>%{num_redirects}</c>; from the HTTP handler, the resends it made itself, after a 417 or
    /// in answer to an authentication challenge (a 401 or 407 retry); curl 8.22.0 counts those
    /// resends against <c>--max-redirs</c> too, so <c>--max-redirs 0</c> with <c>--digest</c> or
    /// <c>--anyauth</c> ends with exit 47 (BL-396, BL-3256).
    /// </summary>
    public int RedirectCount { get; init; }

    /// <summary>
    /// Gets the <c>Referer</c> the last request was sent with, set by the redirect follower,
    /// the source of <c>%{referer}</c>; <see langword="null" /> from a handler, meaning
    /// <see cref="HttpRequestOptions.Referer" />.
    /// </summary>
    public string? Referer { get; init; }

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
    /// Gets the body bytes delivered to the output after content decoding, the source of
    /// <c>%{size_delivered}</c>; <see langword="null" /> when nothing was decoded, meaning
    /// <see cref="DownloadSize" />. Under <c>--compressed</c> a gzip body of 51 bytes that
    /// decodes to 501 downloads 51 and delivers 501 (measured, BL-516 Notes).
    /// </summary>
    public long? DeliveredSize { get; init; }

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
    /// <c>%{local_port}</c>; <see langword="null" /> when unknown. A handler that leaves both
    /// end points <see langword="null" /> has them filled in by the composition from the
    /// first connection the transfer opened (ADR-0119).
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; init; }

    /// <summary>
    /// Gets the address and port of the peer, the proxy's when a proxy is used, the
    /// source of <c>%{remote_ip}</c> and <c>%{remote_port}</c>; <see langword="null" />
    /// when unknown.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the text curl 8.21.0 prints as <c>%{remote_ip}</c> for a connection made through a
    /// Unix domain socket, in place of <see cref="RemoteEndPoint" />, which such a connection
    /// leaves <see langword="null" />: the socket's path cut to 45 characters, or empty for an
    /// abstract name (measured, BL-793); <see langword="null" /> for any other connection.
    /// The composition fills it in, as it does the end points (ADR-0119).
    /// </summary>
    public string? UnixSocketRemoteIp { get; init; }

    /// <summary>
    /// Gets the transfer's timestamps, the source of the <c>%{time_*}</c> variables;
    /// <see langword="null" /> when the handler recorded none.
    /// </summary>
    public TransferTimings? Timings { get; init; }

    /// <summary>
    /// Gets the DER encoding of every certificate the server sent in the TLS handshake,
    /// its own first, copied from <see cref="ConnectResult.PeerCertificates" />, the source
    /// of <c>%{certs}</c> and <c>%{num_certs}</c> (ADR-0054); empty for a transfer without
    /// TLS.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> PeerCertificates { get; init; } = [];

    /// <summary>
    /// Gets the certificate verify code last reported for the origin's TLS handshake
    /// (<see cref="ITransferEvents.ReportCertificateVerifyResult" />), the source of
    /// <c>CURLINFO_SSL_VERIFYRESULT</c>; <c>0</c> without TLS, and always in the Schannel build.
    /// </summary>
    public long SslVerifyResult { get; init; }

    /// <summary>
    /// Gets the certificate verify code last reported for an HTTPS proxy's TLS handshake, the
    /// source of <c>CURLINFO_PROXY_SSL_VERIFYRESULT</c>; <c>0</c> without an HTTPS proxy.
    /// </summary>
    public long ProxySslVerifyResult { get; init; }

    /// <summary>
    /// Gets the operating system's error number of the transfer's last failed connect attempt,
    /// the source of <c>CURLINFO_OS_ERRNO</c> (BL-2374): the Winsock code on Windows, <c>errno</c>
    /// elsewhere; <c>0</c> when no connect attempt failed.
    /// </summary>
    public long OsErrno { get; init; }

    /// <summary>
    /// Gets why the transfer's SOCKS proxy handshake failed, copied from
    /// <see cref="ConnectResult.ProxyError" />, the source of <c>CURLINFO_PROXY_ERROR</c> (BL-2375);
    /// <see cref="CurlProxyCode.Ok" /> when no SOCKS handshake failed.
    /// </summary>
    public CurlProxyCode ProxyError { get; init; }

    /// <summary>
    /// Gets the directory the FTP server's <c>257</c> reply to <c>PWD</c> named at login,
    /// its doubled quotes undoubled, the source of <c>%{ftp_entry_path}</c>;
    /// <see langword="null" /> for a transfer that is not FTP, or when the reply named no
    /// directory (BL-514).
    /// </summary>
    public string? FtpEntryPath { get; init; }

    /// <summary>
    /// Gets how many response headers the transfer had stored when this exchange ended: the
    /// earlier exchanges' and hops' and this exchange's heads and trailers, which curl 8.21.0
    /// counts toward its limit of 5000 (measured, BL-1448 Notes). 0 for a transfer that is not HTTP.
    /// </summary>
    public int ResponseHeadersStored { get; init; }

    /// <summary>
    /// Gets the HTTP authentication scheme the transfer's last request to the origin was sent
    /// with after a server picked it - by answering its challenge, or inherited from an earlier
    /// hop's <see cref="HttpRequestOptions.AuthSchemePicked" /> - which a followed redirect
    /// carries to its next hop (upstream test1088, BL-1819). <see cref="HttpAuthSchemes.None" />
    /// when no server picked one, or for a transfer that is not HTTP.
    /// </summary>
    public HttpAuthSchemes AuthSchemePicked { get; init; }

    /// <summary>
    /// Gets the HTTP authentication scheme the transfer's last request through a forward proxy
    /// answered the proxy's <c>Proxy-Authenticate</c> challenge with, the source of
    /// <c>CURLINFO_PROXYAUTH_USED</c> (BL-2367); <see cref="HttpAuthSchemes.None" /> when no
    /// proxy challenge was answered, or for a transfer that is not HTTP.
    /// </summary>
    public HttpAuthSchemes ProxyAuthSchemePicked { get; init; }

    /// <summary>
    /// Gets every HTTP authentication scheme a server offered in a 401's <c>WWW-Authenticate</c>
    /// challenges during the transfer, across its own authentication retries, as libcurl ORs each
    /// challenge into <c>data->info.httpauthavail</c> (BL-3256); <see cref="HttpAuthSchemes.None" />
    /// when no server offered one, or for a transfer that is not HTTP.
    /// </summary>
    public HttpAuthSchemes AuthSchemesOffered { get; init; }

    /// <summary>
    /// Gets every HTTP authentication scheme a forward proxy offered in a 407's
    /// <c>Proxy-Authenticate</c> challenges during the transfer, across its own authentication
    /// retries, as libcurl ORs each challenge into <c>data->info.proxyauthavail</c> (BL-3256);
    /// <see cref="HttpAuthSchemes.None" /> when no proxy offered one, or for a transfer that is not HTTP.
    /// </summary>
    public HttpAuthSchemes ProxyAuthSchemesOffered { get; init; }

    /// <summary>
    /// Gets the RTSP session ID the server's first <c>Session</c> header gave, the source of
    /// <c>CURLINFO_RTSP_SESSION_ID</c>; <see langword="null" /> before any, or for a transfer
    /// that is not RTSP (BL-2367).
    /// </summary>
    public string? RtspSessionId { get; init; }

    /// <summary>
    /// Gets the <c>CSeq</c> the transfer's next RTSP request would be sent with, the source of
    /// <c>CURLINFO_RTSP_CLIENT_CSEQ</c>; <c>0</c> for a transfer that is not RTSP (BL-2367).
    /// </summary>
    public long RtspClientSequenceNumber { get; init; }

    /// <summary>
    /// Gets the <c>CSeq</c> the transfer expects on the server's next request to it, the source
    /// of <c>CURLINFO_RTSP_SERVER_CSEQ</c>. Only libcurl's <c>RECEIVE</c> request moves it, and
    /// Curl's RTSP handler sends <c>OPTIONS</c> only, so it stays <c>0</c> (BL-2367).
    /// </summary>
    public long RtspServerSequenceNumber { get; init; }

    /// <summary>
    /// Gets the <c>CSeq</c> the last RTSP reply carried, the source of
    /// <c>CURLINFO_RTSP_CSEQ_RECV</c>; <c>0</c> when it carried none, or for a transfer that is
    /// not RTSP (BL-2367).
    /// </summary>
    public long RtspSequenceNumberReceived { get; init; }

    /// <summary>
    /// Gets the Digest <c>Authorization</c> value the transfer's last request to the origin was
    /// sent with, which a followed redirect to a hop that may be sent credentials carries as
    /// <see cref="HttpRequestOptions.KeptDigestAuthorization" /> (upstream test1286, BL-2252);
    /// <see langword="null" /> when that request sent no Digest value, or for a transfer that is
    /// not HTTP.
    /// </summary>
    public string? DigestAuthorizationSent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transfer ended with Digest still marked available: its
    /// last response was a 401 offering Digest that set libcurl 8.22.0's <c>authproblem</c>, from
    /// either of two sources: a Digest challenge without <c>stale=true</c> after the transfer
    /// already held a Digest nonce, kept from the easy handle's previous transfer
    /// (<see cref="HttpRequestOptions.KeptDigestChallenges" />) or offered by an earlier 401 of its
    /// own, which <c>Curl_input_digest</c> fails (upstream test2027, BL-2820); or a bare
    /// <c>NTLM</c> challenge to a request sent with an NTLM <c>Authorization</c>, which
    /// <c>Curl_input_ntlm</c> fails as a rejected handshake (upstream test2030, BL-2823). Either
    /// way <c>Curl_http_auth_act</c> skips <c>pickoneauth</c> and <c>authhost.avail</c> keeps
    /// <c>CURLAUTH_DIGEST</c> into the handle's next transfer, which the easy handle carries as
    /// <see cref="HttpRequestOptions.DigestStaysAvailable" /> (ADR-0709);
    /// <see langword="false" /> otherwise, or for a transfer that is not HTTP.
    /// </summary>
    public bool DigestStaysAvailable { get; init; }

    /// <summary>
    /// Gets the write-out values an extension's transfer hook gave this transfer, by variable name
    /// (ADR-1130): <c>-w</c> prints the value given under <c>name</c> for <c>%{name}</c> only when
    /// curl itself has no variable of that name, so an extension can add variables but never
    /// change one of curl's. Empty, the default, for every transfer no extension reported on.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExtensionWriteOutValues { get; init; } = ReadOnlyDictionary<string, string>.Empty;
}
