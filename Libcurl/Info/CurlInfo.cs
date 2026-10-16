namespace Libcurl.Info;

/// <summary>
/// libcurl's <c>CURLINFO</c>: every <c>CURLINFO_*</c> of curl 8.22.0's <c>include/curl/curl.h</c>,
/// with its number (the <c>CURLINFO_STRING</c>, <c>_LONG</c>, <c>_DOUBLE</c>, <c>_SLIST</c>/<c>_PTR</c>,
/// <c>_SOCKET</c> or <c>_OFF_T</c> base plus the info's own number). Each name is the upstream name
/// without <c>CURLINFO_</c>, one capital per underscore-separated word, as <see cref="Options.CurlOption"/>
/// names <c>CURLOPT_*</c>.
/// </summary>
/// <remarks>
/// <see cref="Easy.CurlEasy.GetInfo"/> answers each with the .NET value of its base: a
/// <see cref="string"/> (or <see langword="null"/>) for <c>_STRING</c>, a <see cref="long"/> for
/// <c>_LONG</c>, <c>_SOCKET</c> and <c>_OFF_T</c>, a <see cref="double"/> for <c>_DOUBLE</c>, and for
/// <c>_SLIST</c>/<c>_PTR</c> an <see cref="IReadOnlyList{T}"/> of <see cref="string"/>
/// (<see cref="SslEngines"/>, <see cref="Cookielist"/>), one of such lists (<see cref="Certinfo"/>), a
/// <see cref="CurlTlsSessionInfo"/> (<see cref="TlsSslPtr"/>, <see cref="TlsSession"/>) or the
/// application's own value (<see cref="Private"/>, a <c>_STRING</c> upstream that holds a pointer).
/// </remarks>
public enum CurlInfo
{
    /// <summary><c>CURLINFO_EFFECTIVE_URL</c> (0x100001), a string.</summary>
    EffectiveUrl = 0x100001,

    /// <summary><c>CURLINFO_RESPONSE_CODE</c> (0x200002), a long.</summary>
    ResponseCode = 0x200002,

    /// <summary><c>CURLINFO_HTTP_CODE</c>, upstream's old alias of <see cref="ResponseCode"/>.</summary>
    HttpCode = ResponseCode,

    /// <summary><c>CURLINFO_TOTAL_TIME</c> (0x300003), a double.</summary>
    TotalTime = 0x300003,

    /// <summary><c>CURLINFO_NAMELOOKUP_TIME</c> (0x300004), a double.</summary>
    NamelookupTime = 0x300004,

    /// <summary><c>CURLINFO_CONNECT_TIME</c> (0x300005), a double.</summary>
    ConnectTime = 0x300005,

    /// <summary><c>CURLINFO_PRETRANSFER_TIME</c> (0x300006), a double.</summary>
    PretransferTime = 0x300006,

    /// <summary><c>CURLINFO_SIZE_UPLOAD</c> (0x300007), a double; deprecated upstream for <see cref="SizeUploadT"/>.</summary>
    SizeUpload = 0x300007,

    /// <summary><c>CURLINFO_SIZE_UPLOAD_T</c> (0x600007), an off_t.</summary>
    SizeUploadT = 0x600007,

    /// <summary><c>CURLINFO_SIZE_DOWNLOAD</c> (0x300008), a double; deprecated upstream for <see cref="SizeDownloadT"/>.</summary>
    SizeDownload = 0x300008,

    /// <summary><c>CURLINFO_SIZE_DOWNLOAD_T</c> (0x600008), an off_t.</summary>
    SizeDownloadT = 0x600008,

    /// <summary><c>CURLINFO_SPEED_DOWNLOAD</c> (0x300009), a double; deprecated upstream for <see cref="SpeedDownloadT"/>.</summary>
    SpeedDownload = 0x300009,

    /// <summary><c>CURLINFO_SPEED_DOWNLOAD_T</c> (0x600009), an off_t.</summary>
    SpeedDownloadT = 0x600009,

    /// <summary><c>CURLINFO_SPEED_UPLOAD</c> (0x30000A), a double; deprecated upstream for <see cref="SpeedUploadT"/>.</summary>
    SpeedUpload = 0x30000A,

    /// <summary><c>CURLINFO_SPEED_UPLOAD_T</c> (0x60000A), an off_t.</summary>
    SpeedUploadT = 0x60000A,

    /// <summary><c>CURLINFO_HEADER_SIZE</c> (0x20000B), a long.</summary>
    HeaderSize = 0x20000B,

    /// <summary><c>CURLINFO_REQUEST_SIZE</c> (0x20000C), a long.</summary>
    RequestSize = 0x20000C,

    /// <summary><c>CURLINFO_SSL_VERIFYRESULT</c> (0x20000D), a long.</summary>
    SslVerifyresult = 0x20000D,

    /// <summary><c>CURLINFO_FILETIME</c> (0x20000E), a long.</summary>
    Filetime = 0x20000E,

    /// <summary><c>CURLINFO_FILETIME_T</c> (0x60000E), an off_t.</summary>
    FiletimeT = 0x60000E,

    /// <summary><c>CURLINFO_CONTENT_LENGTH_DOWNLOAD</c> (0x30000F), a double; deprecated upstream for <see cref="ContentLengthDownloadT"/>.</summary>
    ContentLengthDownload = 0x30000F,

    /// <summary><c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c> (0x60000F), an off_t.</summary>
    ContentLengthDownloadT = 0x60000F,

    /// <summary><c>CURLINFO_CONTENT_LENGTH_UPLOAD</c> (0x300010), a double; deprecated upstream for <see cref="ContentLengthUploadT"/>.</summary>
    ContentLengthUpload = 0x300010,

    /// <summary><c>CURLINFO_CONTENT_LENGTH_UPLOAD_T</c> (0x600010), an off_t.</summary>
    ContentLengthUploadT = 0x600010,

    /// <summary><c>CURLINFO_STARTTRANSFER_TIME</c> (0x300011), a double.</summary>
    StarttransferTime = 0x300011,

    /// <summary><c>CURLINFO_CONTENT_TYPE</c> (0x100012), a string.</summary>
    ContentType = 0x100012,

    /// <summary><c>CURLINFO_REDIRECT_TIME</c> (0x300013), a double.</summary>
    RedirectTime = 0x300013,

    /// <summary><c>CURLINFO_REDIRECT_COUNT</c> (0x200014), a long.</summary>
    RedirectCount = 0x200014,

    /// <summary><c>CURLINFO_PRIVATE</c> (0x100015): the <c>CURLOPT_PRIVATE</c> value, a pointer upstream.</summary>
    Private = 0x100015,

    /// <summary><c>CURLINFO_HTTP_CONNECTCODE</c> (0x200016), a long.</summary>
    HttpConnectcode = 0x200016,

    /// <summary><c>CURLINFO_HTTPAUTH_AVAIL</c> (0x200017), a long.</summary>
    HttpauthAvail = 0x200017,

    /// <summary><c>CURLINFO_PROXYAUTH_AVAIL</c> (0x200018), a long.</summary>
    ProxyauthAvail = 0x200018,

    /// <summary><c>CURLINFO_OS_ERRNO</c> (0x200019), a long.</summary>
    OsErrno = 0x200019,

    /// <summary><c>CURLINFO_NUM_CONNECTS</c> (0x20001A), a long.</summary>
    NumConnects = 0x20001A,

    /// <summary><c>CURLINFO_SSL_ENGINES</c> (0x40001B), a string list.</summary>
    SslEngines = 0x40001B,

    /// <summary><c>CURLINFO_COOKIELIST</c> (0x40001C), a string list.</summary>
    Cookielist = 0x40001C,

    /// <summary><c>CURLINFO_LASTSOCKET</c> (0x20001D), a long; deprecated upstream for <see cref="Activesocket"/>.</summary>
    Lastsocket = 0x20001D,

    /// <summary><c>CURLINFO_FTP_ENTRY_PATH</c> (0x10001E), a string.</summary>
    FtpEntryPath = 0x10001E,

    /// <summary><c>CURLINFO_REDIRECT_URL</c> (0x10001F), a string.</summary>
    RedirectUrl = 0x10001F,

    /// <summary><c>CURLINFO_PRIMARY_IP</c> (0x100020), a string.</summary>
    PrimaryIp = 0x100020,

    /// <summary><c>CURLINFO_APPCONNECT_TIME</c> (0x300021), a double.</summary>
    AppconnectTime = 0x300021,

    /// <summary><c>CURLINFO_CERTINFO</c> (0x400022), a <c>struct curl_certinfo</c>: one string list per certificate.</summary>
    Certinfo = 0x400022,

    /// <summary><c>CURLINFO_CONDITION_UNMET</c> (0x200023), a long.</summary>
    ConditionUnmet = 0x200023,

    /// <summary><c>CURLINFO_RTSP_SESSION_ID</c> (0x100024), a string.</summary>
    RtspSessionId = 0x100024,

    /// <summary><c>CURLINFO_RTSP_CLIENT_CSEQ</c> (0x200025), a long.</summary>
    RtspClientCseq = 0x200025,

    /// <summary><c>CURLINFO_RTSP_SERVER_CSEQ</c> (0x200026), a long.</summary>
    RtspServerCseq = 0x200026,

    /// <summary><c>CURLINFO_RTSP_CSEQ_RECV</c> (0x200027), a long.</summary>
    RtspCseqRecv = 0x200027,

    /// <summary><c>CURLINFO_PRIMARY_PORT</c> (0x200028), a long.</summary>
    PrimaryPort = 0x200028,

    /// <summary><c>CURLINFO_LOCAL_IP</c> (0x100029), a string.</summary>
    LocalIp = 0x100029,

    /// <summary><c>CURLINFO_LOCAL_PORT</c> (0x20002A), a long.</summary>
    LocalPort = 0x20002A,

    /// <summary><c>CURLINFO_TLS_SESSION</c> (0x40002B), a <c>struct curl_tlssessioninfo</c>; deprecated upstream for <see cref="TlsSslPtr"/>.</summary>
    TlsSession = 0x40002B,

    /// <summary><c>CURLINFO_ACTIVESOCKET</c> (0x50002C), a socket.</summary>
    Activesocket = 0x50002C,

    /// <summary><c>CURLINFO_TLS_SSL_PTR</c> (0x40002D), a <c>struct curl_tlssessioninfo</c>.</summary>
    TlsSslPtr = 0x40002D,

    /// <summary><c>CURLINFO_HTTP_VERSION</c> (0x20002E), a long.</summary>
    HttpVersion = 0x20002E,

    /// <summary><c>CURLINFO_PROXY_SSL_VERIFYRESULT</c> (0x20002F), a long.</summary>
    ProxySslVerifyresult = 0x20002F,

    /// <summary><c>CURLINFO_PROTOCOL</c> (0x200030), a long; deprecated upstream for <see cref="Scheme"/>.</summary>
    Protocol = 0x200030,

    /// <summary><c>CURLINFO_SCHEME</c> (0x100031), a string.</summary>
    Scheme = 0x100031,

    /// <summary><c>CURLINFO_TOTAL_TIME_T</c> (0x600032), an off_t in microseconds.</summary>
    TotalTimeT = 0x600032,

    /// <summary><c>CURLINFO_NAMELOOKUP_TIME_T</c> (0x600033), an off_t in microseconds.</summary>
    NamelookupTimeT = 0x600033,

    /// <summary><c>CURLINFO_CONNECT_TIME_T</c> (0x600034), an off_t in microseconds.</summary>
    ConnectTimeT = 0x600034,

    /// <summary><c>CURLINFO_PRETRANSFER_TIME_T</c> (0x600035), an off_t in microseconds.</summary>
    PretransferTimeT = 0x600035,

    /// <summary><c>CURLINFO_STARTTRANSFER_TIME_T</c> (0x600036), an off_t in microseconds.</summary>
    StarttransferTimeT = 0x600036,

    /// <summary><c>CURLINFO_REDIRECT_TIME_T</c> (0x600037), an off_t in microseconds.</summary>
    RedirectTimeT = 0x600037,

    /// <summary><c>CURLINFO_APPCONNECT_TIME_T</c> (0x600038), an off_t in microseconds.</summary>
    AppconnectTimeT = 0x600038,

    /// <summary><c>CURLINFO_RETRY_AFTER</c> (0x600039), an off_t in seconds.</summary>
    RetryAfter = 0x600039,

    /// <summary><c>CURLINFO_EFFECTIVE_METHOD</c> (0x10003A), a string.</summary>
    EffectiveMethod = 0x10003A,

    /// <summary><c>CURLINFO_PROXY_ERROR</c> (0x20003B), a long.</summary>
    ProxyError = 0x20003B,

    /// <summary><c>CURLINFO_REFERER</c> (0x10003C), a string.</summary>
    Referer = 0x10003C,

    /// <summary><c>CURLINFO_CAINFO</c> (0x10003D), a string.</summary>
    Cainfo = 0x10003D,

    /// <summary><c>CURLINFO_CAPATH</c> (0x10003E), a string.</summary>
    Capath = 0x10003E,

    /// <summary><c>CURLINFO_XFER_ID</c> (0x60003F), an off_t.</summary>
    XferId = 0x60003F,

    /// <summary><c>CURLINFO_CONN_ID</c> (0x600040), an off_t.</summary>
    ConnId = 0x600040,

    /// <summary><c>CURLINFO_QUEUE_TIME_T</c> (0x600041), an off_t in microseconds.</summary>
    QueueTimeT = 0x600041,

    /// <summary><c>CURLINFO_USED_PROXY</c> (0x200042), a long.</summary>
    UsedProxy = 0x200042,

    /// <summary><c>CURLINFO_POSTTRANSFER_TIME_T</c> (0x600043), an off_t in microseconds.</summary>
    PosttransferTimeT = 0x600043,

    /// <summary><c>CURLINFO_EARLYDATA_SENT_T</c> (0x600044), an off_t.</summary>
    EarlydataSentT = 0x600044,

    /// <summary><c>CURLINFO_HTTPAUTH_USED</c> (0x200045), a long.</summary>
    HttpauthUsed = 0x200045,

    /// <summary><c>CURLINFO_PROXYAUTH_USED</c> (0x200046), a long.</summary>
    ProxyauthUsed = 0x200046,
}
