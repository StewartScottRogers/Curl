namespace Curl.Api.Info;

/// <summary>
/// What <c>curl_easy_getinfo</c> reports about an easy handle's transfer (ADR-0506, plan section
/// "CURLINFO"): one property per <c>CURLINFO_*</c> except <c>CURLINFO_PRIVATE</c>, which is the
/// caller's own value and stays with <c>Libcurl</c>. Every default is what libcurl 8.22.0 answers
/// on a fresh handle, before any transfer. A time is one <see cref="TimeSpan"/> for both the
/// double-seconds and the <c>_T</c> microsecond variants; a size or speed is one <see cref="long"/>
/// for both the double and the <c>_T</c> variants; <c>Libcurl</c> converts.
/// </summary>
public sealed record TransferInfo
{
    /// <summary>
    /// <c>CURLINFO_EFFECTIVE_URL</c>: the last URL used. Default empty: libcurl answers <c>""</c>
    /// on a handle with no URL.
    /// </summary>
    public string EffectiveUrl { get; init; } = "";

    /// <summary>
    /// <c>CURLINFO_EFFECTIVE_METHOD</c>: the last request method used. Default <c>GET</c>: the
    /// method a fresh handle would send.
    /// </summary>
    public string EffectiveMethod { get; init; } = "GET";

    /// <summary>
    /// <c>CURLINFO_REDIRECT_URL</c>: the URL a redirect would take the transfer to, when it was
    /// not followed. Default <see langword="null"/>: no redirect.
    /// </summary>
    public string? RedirectUrl { get; init; }

    /// <summary>
    /// <c>CURLINFO_CONTENT_TYPE</c>: the received <c>Content-Type</c>. Default
    /// <see langword="null"/>: none received.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// <c>CURLINFO_PRIMARY_IP</c>: the IP address of the last connection's peer. Default empty:
    /// no connection.
    /// </summary>
    public string PrimaryIp { get; init; } = "";

    /// <summary>
    /// <c>CURLINFO_LOCAL_IP</c>: the local IP address of the last connection. Default empty: no
    /// connection.
    /// </summary>
    public string LocalIp { get; init; } = "";

    /// <summary>
    /// <c>CURLINFO_FTP_ENTRY_PATH</c>: the path the FTP server put the login in. Default
    /// <see langword="null"/>: no FTP login.
    /// </summary>
    public string? FtpEntryPath { get; init; }

    /// <summary>
    /// <c>CURLINFO_RTSP_SESSION_ID</c>: the RTSP session ID the server gave. Default
    /// <see langword="null"/>: none.
    /// </summary>
    public string? RtspSessionId { get; init; }

    /// <summary>
    /// <c>CURLINFO_SCHEME</c>: the URL scheme of the last connection. Default
    /// <see langword="null"/>: no connection.
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// <c>CURLINFO_REFERER</c>: the <c>Referer</c> header last sent. Default
    /// <see langword="null"/>: none sent.
    /// </summary>
    public string? Referer { get; init; }

    /// <summary>
    /// <c>CURLINFO_CAINFO</c>: the build's default CA bundle file. Default
    /// <see langword="null"/>: the Schannel build has none.
    /// </summary>
    public string? CaInfo { get; init; }

    /// <summary>
    /// <c>CURLINFO_CAPATH</c>: the build's default CA certificate directory. Default
    /// <see langword="null"/>: the Schannel build has none.
    /// </summary>
    public string? CaPath { get; init; }

    /// <summary>
    /// <c>CURLINFO_RESPONSE_CODE</c>: the last response code (HTTP, FTP, SMTP and the like).
    /// Default 0: no response.
    /// </summary>
    public long ResponseCode { get; init; }

    /// <summary>
    /// <c>CURLINFO_HTTP_CONNECTCODE</c>: the proxy's response code to the last <c>CONNECT</c>.
    /// Default 0: no <c>CONNECT</c>.
    /// </summary>
    public long HttpConnectCode { get; init; }

    /// <summary>
    /// <c>CURLINFO_HTTP_VERSION</c>: the HTTP version of the last connection, a
    /// <c>CURL_HTTP_VERSION_*</c> value. Default 0: no HTTP connection.
    /// </summary>
    public long HttpVersion { get; init; }

    /// <summary>
    /// <c>CURLINFO_FILETIME</c> (and <c>CURLINFO_FILETIME_T</c>): the remote file's modification
    /// time in seconds since 1970-01-01 UTC. Default -1: unknown.
    /// </summary>
    public long FileTime { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_REDIRECT_COUNT</c>: how many redirects were followed. Default 0.
    /// </summary>
    public long RedirectCount { get; init; }

    /// <summary>
    /// <c>CURLINFO_HEADER_SIZE</c>: the bytes of all headers received. Default 0.
    /// </summary>
    public long HeaderSize { get; init; }

    /// <summary>
    /// <c>CURLINFO_REQUEST_SIZE</c>: the bytes of all requests sent. Default 0.
    /// </summary>
    public long RequestSize { get; init; }

    /// <summary>
    /// <c>CURLINFO_SSL_VERIFYRESULT</c>: the result of the server certificate's verification.
    /// Default 0: success, or not verified.
    /// </summary>
    public long SslVerifyResult { get; init; }

    /// <summary>
    /// <c>CURLINFO_PROXY_SSL_VERIFYRESULT</c>: the result of the HTTPS proxy certificate's
    /// verification. Default 0: success, or not verified.
    /// </summary>
    public long ProxySslVerifyResult { get; init; }

    /// <summary>
    /// <c>CURLINFO_HTTPAUTH_AVAIL</c>: the <c>CURLAUTH_*</c> bits the server offered. Default 0:
    /// none.
    /// </summary>
    public long HttpAuthAvailable { get; init; }

    /// <summary>
    /// <c>CURLINFO_PROXYAUTH_AVAIL</c>: the <c>CURLAUTH_*</c> bits the proxy offered. Default 0:
    /// none.
    /// </summary>
    public long ProxyAuthAvailable { get; init; }

    /// <summary>
    /// <c>CURLINFO_HTTPAUTH_USED</c>: the <c>CURLAUTH_*</c> bit used with the server. Default 0:
    /// none.
    /// </summary>
    public long HttpAuthUsed { get; init; }

    /// <summary>
    /// <c>CURLINFO_PROXYAUTH_USED</c>: the <c>CURLAUTH_*</c> bit used with the proxy. Default 0:
    /// none.
    /// </summary>
    public long ProxyAuthUsed { get; init; }

    /// <summary>
    /// <c>CURLINFO_OS_ERRNO</c>: the operating system's error number from the last failed
    /// connect. Default 0.
    /// </summary>
    public long OsErrno { get; init; }

    /// <summary>
    /// <c>CURLINFO_NUM_CONNECTS</c>: how many new connections the last transfer made. Default 0.
    /// </summary>
    public long NumConnects { get; init; }

    /// <summary>
    /// <c>CURLINFO_PRIMARY_PORT</c>: the peer port of the last connection. Default 0: no
    /// connection.
    /// </summary>
    public long PrimaryPort { get; init; }

    /// <summary>
    /// <c>CURLINFO_LOCAL_PORT</c>: the local port of the last connection. Default 0: no
    /// connection.
    /// </summary>
    public long LocalPort { get; init; }

    /// <summary>
    /// <c>CURLINFO_CONDITION_UNMET</c>: 1 when the time condition stopped the transfer. Default 0.
    /// </summary>
    public long ConditionUnmet { get; init; }

    /// <summary>
    /// <c>CURLINFO_RTSP_CLIENT_CSEQ</c>: the next RTSP client CSeq. Default 0.
    /// </summary>
    public long RtspClientCSeq { get; init; }

    /// <summary>
    /// <c>CURLINFO_RTSP_SERVER_CSEQ</c>: the next expected RTSP server CSeq. Default 0.
    /// </summary>
    public long RtspServerCSeq { get; init; }

    /// <summary>
    /// <c>CURLINFO_RTSP_CSEQ_RECV</c>: the last RTSP CSeq received. Default 0.
    /// </summary>
    public long RtspCSeqReceived { get; init; }

    /// <summary>
    /// <c>CURLINFO_PROTOCOL</c>: the <c>CURLPROTO_*</c> bit of the last connection. Default 0: no
    /// connection.
    /// </summary>
    public long Protocol { get; init; }

    /// <summary>
    /// <c>CURLINFO_PROXY_ERROR</c>: the <c>CURLPX_*</c> detail of the last proxy failure. Default
    /// 0: <c>CURLPX_OK</c>.
    /// </summary>
    public long ProxyError { get; init; }

    /// <summary>
    /// <c>CURLINFO_USED_PROXY</c>: 1 when the last transfer went through a proxy. Default 0.
    /// </summary>
    public long UsedProxy { get; init; }

    /// <summary>
    /// <c>CURLINFO_XFER_ID</c>: the transfer's ID. Default -1: no transfer yet.
    /// </summary>
    public long TransferId { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_CONN_ID</c>: the last connection's ID. Default -1: no connection.
    /// </summary>
    public long ConnectionId { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_LASTSOCKET</c>: the last connection's socket. Default -1: no socket.
    /// </summary>
    public long LastSocket { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_ACTIVESOCKET</c>: the active connection's socket. Default -1
    /// (<c>CURL_SOCKET_BAD</c>): no socket.
    /// </summary>
    public long ActiveSocket { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_TOTAL_TIME</c> (and <c>_T</c>): the whole transfer's time. Default zero.
    /// </summary>
    public TimeSpan TotalTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_NAMELOOKUP_TIME</c> (and <c>_T</c>): the time to the end of name resolution.
    /// Default zero.
    /// </summary>
    public TimeSpan NameLookupTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_CONNECT_TIME</c> (and <c>_T</c>): the time to the end of the connect. Default
    /// zero.
    /// </summary>
    public TimeSpan ConnectTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_APPCONNECT_TIME</c> (and <c>_T</c>): the time to the end of the TLS or SSH
    /// handshake. Default zero.
    /// </summary>
    public TimeSpan AppConnectTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_PRETRANSFER_TIME</c> (and <c>_T</c>): the time until the transfer is about to
    /// start. Default zero.
    /// </summary>
    public TimeSpan PreTransferTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_POSTTRANSFER_TIME_T</c>: the time until the last request byte was sent.
    /// Default zero.
    /// </summary>
    public TimeSpan PostTransferTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_STARTTRANSFER_TIME</c> (and <c>_T</c>): the time to the first response byte.
    /// Default zero.
    /// </summary>
    public TimeSpan StartTransferTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_REDIRECT_TIME</c> (and <c>_T</c>): the time all redirect steps took. Default
    /// zero.
    /// </summary>
    public TimeSpan RedirectTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_QUEUE_TIME_T</c>: the time the transfer waited in the queue before it started.
    /// Default zero.
    /// </summary>
    public TimeSpan QueueTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_RETRY_AFTER</c>: the server's <c>Retry-After</c>, in whole seconds. Default
    /// zero: none given.
    /// </summary>
    public TimeSpan RetryAfter { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLINFO_EARLYDATA_SENT_T</c>: the bytes sent as TLS early data. Default 0. (A size, not
    /// a time, though the plan lists it with the times.)
    /// </summary>
    public long EarlyDataSent { get; init; }

    /// <summary>
    /// <c>CURLINFO_SIZE_UPLOAD</c> (and <c>_T</c>): the bytes uploaded. Default 0.
    /// </summary>
    public long SizeUpload { get; init; }

    /// <summary>
    /// <c>CURLINFO_SIZE_DOWNLOAD</c> (and <c>_T</c>): the bytes downloaded. Default 0.
    /// </summary>
    public long SizeDownload { get; init; }

    /// <summary>
    /// <c>CURLINFO_SPEED_UPLOAD</c> (and <c>_T</c>): the average upload speed in bytes per
    /// second. Default 0.
    /// </summary>
    public long SpeedUpload { get; init; }

    /// <summary>
    /// <c>CURLINFO_SPEED_DOWNLOAD</c> (and <c>_T</c>): the average download speed in bytes per
    /// second. Default 0.
    /// </summary>
    public long SpeedDownload { get; init; }

    /// <summary>
    /// <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD</c> (and <c>_T</c>): the download's declared size.
    /// Default -1: unknown.
    /// </summary>
    public long ContentLengthDownload { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_CONTENT_LENGTH_UPLOAD</c> (and <c>_T</c>): the upload's declared size. Default
    /// -1: unknown.
    /// </summary>
    public long ContentLengthUpload { get; init; } = -1;

    /// <summary>
    /// <c>CURLINFO_SSL_ENGINES</c>: the TLS engines the build supports. Default empty: the
    /// Schannel build has none.
    /// </summary>
    public IReadOnlyList<string> SslEngines { get; init; } = [];

    /// <summary>
    /// <c>CURLINFO_COOKIELIST</c>: every known cookie as a Netscape cookie-file line. Default
    /// empty: no cookies.
    /// </summary>
    public IReadOnlyList<string> CookieList { get; init; } = [];

    /// <summary>
    /// <c>CURLINFO_CERTINFO</c>: the server's certificate chain, each certificate a list of
    /// <c>name:value</c> strings. Default empty: no chain gathered.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> CertInfo { get; init; } = [];

    /// <summary>
    /// <c>CURLINFO_TLS_SSL_PTR</c>: the <c>curl_sslbackend</c> value of the build's TLS backend.
    /// Default 8 (<c>CURLSSLBACKEND_SCHANNEL</c>), the Schannel build's answer; there is no native
    /// TLS pointer to give, so the <c>internals</c> half is always <see langword="null"/>.
    /// </summary>
    public long TlsSslBackend { get; init; } = 8;
}
