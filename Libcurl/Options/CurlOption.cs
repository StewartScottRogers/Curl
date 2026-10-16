namespace Libcurl.Options;

/// <summary>
/// libcurl's <c>CURLoption</c>: every <c>CURLOPT_*</c> of curl 8.22.0's <c>lib/easyoptions.c</c>
/// table, with its number from <c>include/curl/curl.h</c> (the <c>CURLOPTTYPE_*</c> base plus the
/// option's own number). Each name is the upstream name without <c>CURLOPT_</c>, one capital per
/// underscore-separated word, as <see cref="Curl.Api.Codes.CurlCode"/> names <c>CURLE_*</c>.
/// </summary>
/// <remarks>
/// An alias (<c>CURLOPT_FILE</c>, <c>CURLOPT_WRITEHEADER</c> and the rest flagged
/// <c>CURLOT_FLAG_ALIAS</c>) has the number of the option it stands for, as upstream's
/// <c>#define</c> does.
/// </remarks>
public enum CurlOption
{
    /// <summary><c>CURLOPT_ABSTRACT_UNIX_SOCKET</c> (10264), a <c>CURLOT_STRING</c> option.</summary>
    AbstractUnixSocket = 10264,

    /// <summary><c>CURLOPT_ACCEPTTIMEOUT_MS</c> (212), a <c>CURLOT_LONG</c> option.</summary>
    AccepttimeoutMs = 212,

    /// <summary><c>CURLOPT_ACCEPT_ENCODING</c> (10102), a <c>CURLOT_STRING</c> option.</summary>
    AcceptEncoding = 10102,

    /// <summary><c>CURLOPT_ADDRESS_SCOPE</c> (171), a <c>CURLOT_LONG</c> option.</summary>
    AddressScope = 171,

    /// <summary><c>CURLOPT_ALTSVC</c> (10287), a <c>CURLOT_STRING</c> option.</summary>
    Altsvc = 10287,

    /// <summary><c>CURLOPT_ALTSVC_CTRL</c> (286), a <c>CURLOT_LONG</c> option.</summary>
    AltsvcCtrl = 286,

    /// <summary><c>CURLOPT_APPEND</c> (50), a <c>CURLOT_LONG</c> option.</summary>
    Append = 50,

    /// <summary><c>CURLOPT_AUTOREFERER</c> (58), a <c>CURLOT_LONG</c> option.</summary>
    Autoreferer = 58,

    /// <summary><c>CURLOPT_AWS_SIGV4</c> (10305), a <c>CURLOT_STRING</c> option.</summary>
    AwsSigv4 = 10305,

    /// <summary><c>CURLOPT_BUFFERSIZE</c> (98), a <c>CURLOT_LONG</c> option.</summary>
    Buffersize = 98,

    /// <summary><c>CURLOPT_CAINFO</c> (10065), a <c>CURLOT_STRING</c> option.</summary>
    Cainfo = 10065,

    /// <summary><c>CURLOPT_CAINFO_BLOB</c> (40309), a <c>CURLOT_BLOB</c> option.</summary>
    CainfoBlob = 40309,

    /// <summary><c>CURLOPT_CAPATH</c> (10097), a <c>CURLOT_STRING</c> option.</summary>
    Capath = 10097,

    /// <summary><c>CURLOPT_CA_CACHE_TIMEOUT</c> (321), a <c>CURLOT_LONG</c> option.</summary>
    CaCacheTimeout = 321,

    /// <summary><c>CURLOPT_CERTINFO</c> (172), a <c>CURLOT_LONG</c> option.</summary>
    Certinfo = 172,

    /// <summary><c>CURLOPT_CHUNK_BGN_FUNCTION</c> (20198), a <c>CURLOT_FUNCTION</c> option.</summary>
    ChunkBgnFunction = 20198,

    /// <summary><c>CURLOPT_CHUNK_DATA</c> (10201), a <c>CURLOT_CBPTR</c> option.</summary>
    ChunkData = 10201,

    /// <summary><c>CURLOPT_CHUNK_END_FUNCTION</c> (20199), a <c>CURLOT_FUNCTION</c> option.</summary>
    ChunkEndFunction = 20199,

    /// <summary><c>CURLOPT_CLOSESOCKETDATA</c> (10209), a <c>CURLOT_CBPTR</c> option.</summary>
    Closesocketdata = 10209,

    /// <summary><c>CURLOPT_CLOSESOCKETFUNCTION</c> (20208), a <c>CURLOT_FUNCTION</c> option.</summary>
    Closesocketfunction = 20208,

    /// <summary><c>CURLOPT_CONNECTTIMEOUT</c> (78), a <c>CURLOT_LONG</c> option.</summary>
    Connecttimeout = 78,

    /// <summary><c>CURLOPT_CONNECTTIMEOUT_MS</c> (156), a <c>CURLOT_LONG</c> option.</summary>
    ConnecttimeoutMs = 156,

    /// <summary><c>CURLOPT_CONNECT_ONLY</c> (141), a <c>CURLOT_LONG</c> option.</summary>
    ConnectOnly = 141,

    /// <summary><c>CURLOPT_CONNECT_TO</c> (10243), a <c>CURLOT_SLIST</c> option.</summary>
    ConnectTo = 10243,

    /// <summary><c>CURLOPT_CONV_FROM_NETWORK_FUNCTION</c> (20142), a <c>CURLOT_FUNCTION</c> option.</summary>
    ConvFromNetworkFunction = 20142,

    /// <summary><c>CURLOPT_CONV_FROM_UTF8_FUNCTION</c> (20144), a <c>CURLOT_FUNCTION</c> option.</summary>
    ConvFromUtf8Function = 20144,

    /// <summary><c>CURLOPT_CONV_TO_NETWORK_FUNCTION</c> (20143), a <c>CURLOT_FUNCTION</c> option.</summary>
    ConvToNetworkFunction = 20143,

    /// <summary><c>CURLOPT_COOKIE</c> (10022), a <c>CURLOT_STRING</c> option.</summary>
    Cookie = 10022,

    /// <summary><c>CURLOPT_COOKIEFILE</c> (10031), a <c>CURLOT_STRING</c> option.</summary>
    Cookiefile = 10031,

    /// <summary><c>CURLOPT_COOKIEJAR</c> (10082), a <c>CURLOT_STRING</c> option.</summary>
    Cookiejar = 10082,

    /// <summary><c>CURLOPT_COOKIELIST</c> (10135), a <c>CURLOT_STRING</c> option.</summary>
    Cookielist = 10135,

    /// <summary><c>CURLOPT_COOKIESESSION</c> (96), a <c>CURLOT_LONG</c> option.</summary>
    Cookiesession = 96,

    /// <summary><c>CURLOPT_COPYPOSTFIELDS</c> (10165), a <c>CURLOT_OBJECT</c> option.</summary>
    Copypostfields = 10165,

    /// <summary><c>CURLOPT_CRLF</c> (27), a <c>CURLOT_LONG</c> option.</summary>
    Crlf = 27,

    /// <summary><c>CURLOPT_CRLFILE</c> (10169), a <c>CURLOT_STRING</c> option.</summary>
    Crlfile = 10169,

    /// <summary><c>CURLOPT_CURLU</c> (10282), a <c>CURLOT_OBJECT</c> option.</summary>
    Curlu = 10282,

    /// <summary><c>CURLOPT_CUSTOMREQUEST</c> (10036), a <c>CURLOT_STRING</c> option.</summary>
    Customrequest = 10036,

    /// <summary><c>CURLOPT_DEBUGDATA</c> (10095), a <c>CURLOT_CBPTR</c> option.</summary>
    Debugdata = 10095,

    /// <summary><c>CURLOPT_DEBUGFUNCTION</c> (20094), a <c>CURLOT_FUNCTION</c> option.</summary>
    Debugfunction = 20094,

    /// <summary><c>CURLOPT_DEFAULT_PROTOCOL</c> (10238), a <c>CURLOT_STRING</c> option.</summary>
    DefaultProtocol = 10238,

    /// <summary><c>CURLOPT_DIRLISTONLY</c> (48), a <c>CURLOT_LONG</c> option.</summary>
    Dirlistonly = 48,

    /// <summary><c>CURLOPT_DISALLOW_USERNAME_IN_URL</c> (278), a <c>CURLOT_LONG</c> option.</summary>
    DisallowUsernameInUrl = 278,

    /// <summary><c>CURLOPT_DNS_CACHE_TIMEOUT</c> (92), a <c>CURLOT_LONG</c> option.</summary>
    DnsCacheTimeout = 92,

    /// <summary><c>CURLOPT_DNS_INTERFACE</c> (10221), a <c>CURLOT_STRING</c> option.</summary>
    DnsInterface = 10221,

    /// <summary><c>CURLOPT_DNS_LOCAL_IP4</c> (10222), a <c>CURLOT_STRING</c> option.</summary>
    DnsLocalIp4 = 10222,

    /// <summary><c>CURLOPT_DNS_LOCAL_IP6</c> (10223), a <c>CURLOT_STRING</c> option.</summary>
    DnsLocalIp6 = 10223,

    /// <summary><c>CURLOPT_DNS_SERVERS</c> (10211), a <c>CURLOT_STRING</c> option.</summary>
    DnsServers = 10211,

    /// <summary><c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c> (275), a <c>CURLOT_LONG</c> option.</summary>
    DnsShuffleAddresses = 275,

    /// <summary><c>CURLOPT_DNS_USE_GLOBAL_CACHE</c> (91), a <c>CURLOT_LONG</c> option.</summary>
    DnsUseGlobalCache = 91,

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYHOST</c> (307), a <c>CURLOT_LONG</c> option.</summary>
    DohSslVerifyhost = 307,

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYPEER</c> (306), a <c>CURLOT_LONG</c> option.</summary>
    DohSslVerifypeer = 306,

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYSTATUS</c> (308), a <c>CURLOT_LONG</c> option.</summary>
    DohSslVerifystatus = 308,

    /// <summary><c>CURLOPT_DOH_URL</c> (10279), a <c>CURLOT_STRING</c> option.</summary>
    DohUrl = 10279,

    /// <summary><c>CURLOPT_ECH</c> (10325), a <c>CURLOT_STRING</c> option.</summary>
    Ech = 10325,

    /// <summary><c>CURLOPT_EGDSOCKET</c> (10077), a <c>CURLOT_STRING</c> option.</summary>
    Egdsocket = 10077,

    /// <summary><c>CURLOPT_ENCODING</c>: an alias of <c>CURLOPT_ACCEPT_ENCODING</c> (10102), a <c>CURLOT_STRING</c> option.</summary>
    Encoding = AcceptEncoding,

    /// <summary><c>CURLOPT_ERRORBUFFER</c> (10010), a <c>CURLOT_OBJECT</c> option.</summary>
    Errorbuffer = 10010,

    /// <summary><c>CURLOPT_EXPECT_100_TIMEOUT_MS</c> (227), a <c>CURLOT_LONG</c> option.</summary>
    Expect100TimeoutMs = 227,

    /// <summary><c>CURLOPT_FAILONERROR</c> (45), a <c>CURLOT_LONG</c> option.</summary>
    Failonerror = 45,

    /// <summary><c>CURLOPT_FILE</c>: an alias of <c>CURLOPT_WRITEDATA</c> (10001), a <c>CURLOT_CBPTR</c> option.</summary>
    File = Writedata,

    /// <summary><c>CURLOPT_FILETIME</c> (69), a <c>CURLOT_LONG</c> option.</summary>
    Filetime = 69,

    /// <summary><c>CURLOPT_FNMATCH_DATA</c> (10202), a <c>CURLOT_CBPTR</c> option.</summary>
    FnmatchData = 10202,

    /// <summary><c>CURLOPT_FNMATCH_FUNCTION</c> (20200), a <c>CURLOT_FUNCTION</c> option.</summary>
    FnmatchFunction = 20200,

    /// <summary><c>CURLOPT_FOLLOWLOCATION</c> (52), a <c>CURLOT_LONG</c> option.</summary>
    Followlocation = 52,

    /// <summary><c>CURLOPT_FORBID_REUSE</c> (75), a <c>CURLOT_LONG</c> option.</summary>
    ForbidReuse = 75,

    /// <summary><c>CURLOPT_FRESH_CONNECT</c> (74), a <c>CURLOT_LONG</c> option.</summary>
    FreshConnect = 74,

    /// <summary><c>CURLOPT_FTPAPPEND</c>: an alias of <c>CURLOPT_APPEND</c> (50), a <c>CURLOT_LONG</c> option.</summary>
    Ftpappend = Append,

    /// <summary><c>CURLOPT_FTPLISTONLY</c>: an alias of <c>CURLOPT_DIRLISTONLY</c> (48), a <c>CURLOT_LONG</c> option.</summary>
    Ftplistonly = Dirlistonly,

    /// <summary><c>CURLOPT_FTPPORT</c> (10017), a <c>CURLOT_STRING</c> option.</summary>
    Ftpport = 10017,

    /// <summary><c>CURLOPT_FTPSSLAUTH</c> (129), a <c>CURLOT_VALUES</c> option.</summary>
    Ftpsslauth = 129,

    /// <summary><c>CURLOPT_FTP_ACCOUNT</c> (10134), a <c>CURLOT_STRING</c> option.</summary>
    FtpAccount = 10134,

    /// <summary><c>CURLOPT_FTP_ALTERNATIVE_TO_USER</c> (10147), a <c>CURLOT_STRING</c> option.</summary>
    FtpAlternativeToUser = 10147,

    /// <summary><c>CURLOPT_FTP_CREATE_MISSING_DIRS</c> (110), a <c>CURLOT_LONG</c> option.</summary>
    FtpCreateMissingDirs = 110,

    /// <summary><c>CURLOPT_FTP_FILEMETHOD</c> (138), a <c>CURLOT_VALUES</c> option.</summary>
    FtpFilemethod = 138,

    /// <summary><c>CURLOPT_FTP_RESPONSE_TIMEOUT</c>: an alias of <c>CURLOPT_SERVER_RESPONSE_TIMEOUT</c> (112), a <c>CURLOT_LONG</c> option.</summary>
    FtpResponseTimeout = ServerResponseTimeout,

    /// <summary><c>CURLOPT_FTP_SKIP_PASV_IP</c> (137), a <c>CURLOT_LONG</c> option.</summary>
    FtpSkipPasvIp = 137,

    /// <summary><c>CURLOPT_FTP_SSL</c>: an alias of <c>CURLOPT_USE_SSL</c> (119), a <c>CURLOT_VALUES</c> option.</summary>
    FtpSsl = UseSsl,

    /// <summary><c>CURLOPT_FTP_SSL_CCC</c> (154), a <c>CURLOT_LONG</c> option.</summary>
    FtpSslCcc = 154,

    /// <summary><c>CURLOPT_FTP_USE_EPRT</c> (106), a <c>CURLOT_LONG</c> option.</summary>
    FtpUseEprt = 106,

    /// <summary><c>CURLOPT_FTP_USE_EPSV</c> (85), a <c>CURLOT_LONG</c> option.</summary>
    FtpUseEpsv = 85,

    /// <summary><c>CURLOPT_FTP_USE_PRET</c> (188), a <c>CURLOT_LONG</c> option.</summary>
    FtpUsePret = 188,

    /// <summary><c>CURLOPT_GSSAPI_DELEGATION</c> (210), a <c>CURLOT_VALUES</c> option.</summary>
    GssapiDelegation = 210,

    /// <summary><c>CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS</c> (271), a <c>CURLOT_LONG</c> option.</summary>
    HappyEyeballsTimeoutMs = 271,

    /// <summary><c>CURLOPT_HAPROXYPROTOCOL</c> (274), a <c>CURLOT_LONG</c> option.</summary>
    Haproxyprotocol = 274,

    /// <summary><c>CURLOPT_HAPROXY_CLIENT_IP</c> (10323), a <c>CURLOT_STRING</c> option.</summary>
    HaproxyClientIp = 10323,

    /// <summary><c>CURLOPT_HEADER</c> (42), a <c>CURLOT_LONG</c> option.</summary>
    Header = 42,

    /// <summary><c>CURLOPT_HEADERDATA</c> (10029), a <c>CURLOT_CBPTR</c> option.</summary>
    Headerdata = 10029,

    /// <summary><c>CURLOPT_HEADERFUNCTION</c> (20079), a <c>CURLOT_FUNCTION</c> option.</summary>
    Headerfunction = 20079,

    /// <summary><c>CURLOPT_HEADEROPT</c> (229), a <c>CURLOT_VALUES</c> option.</summary>
    Headeropt = 229,

    /// <summary><c>CURLOPT_HSTS</c> (10300), a <c>CURLOT_STRING</c> option.</summary>
    Hsts = 10300,

    /// <summary><c>CURLOPT_HSTSREADDATA</c> (10302), a <c>CURLOT_CBPTR</c> option.</summary>
    Hstsreaddata = 10302,

    /// <summary><c>CURLOPT_HSTSREADFUNCTION</c> (20301), a <c>CURLOT_FUNCTION</c> option.</summary>
    Hstsreadfunction = 20301,

    /// <summary><c>CURLOPT_HSTSWRITEDATA</c> (10304), a <c>CURLOT_CBPTR</c> option.</summary>
    Hstswritedata = 10304,

    /// <summary><c>CURLOPT_HSTSWRITEFUNCTION</c> (20303), a <c>CURLOT_FUNCTION</c> option.</summary>
    Hstswritefunction = 20303,

    /// <summary><c>CURLOPT_HSTS_CTRL</c> (299), a <c>CURLOT_LONG</c> option.</summary>
    HstsCtrl = 299,

    /// <summary><c>CURLOPT_HTTP09_ALLOWED</c> (285), a <c>CURLOT_LONG</c> option.</summary>
    Http09Allowed = 285,

    /// <summary><c>CURLOPT_HTTP200ALIASES</c> (10104), a <c>CURLOT_SLIST</c> option.</summary>
    Http200aliases = 10104,

    /// <summary><c>CURLOPT_HTTPAUTH</c> (107), a <c>CURLOT_VALUES</c> option.</summary>
    Httpauth = 107,

    /// <summary><c>CURLOPT_HTTPGET</c> (80), a <c>CURLOT_LONG</c> option.</summary>
    Httpget = 80,

    /// <summary><c>CURLOPT_HTTPHEADER</c> (10023), a <c>CURLOT_SLIST</c> option.</summary>
    Httpheader = 10023,

    /// <summary><c>CURLOPT_HTTPPOST</c> (10024), a <c>CURLOT_OBJECT</c> option.</summary>
    Httppost = 10024,

    /// <summary><c>CURLOPT_HTTPPROXYTUNNEL</c> (61), a <c>CURLOT_LONG</c> option.</summary>
    Httpproxytunnel = 61,

    /// <summary><c>CURLOPT_HTTPSIG_ALGORITHM</c> (329), a <c>CURLOT_VALUES</c> option.</summary>
    HttpsigAlgorithm = 329,

    /// <summary><c>CURLOPT_HTTPSIG_HEADERS</c> (10332), a <c>CURLOT_STRING</c> option.</summary>
    HttpsigHeaders = 10332,

    /// <summary><c>CURLOPT_HTTPSIG_KEY</c> (10330), a <c>CURLOT_STRING</c> option.</summary>
    HttpsigKey = 10330,

    /// <summary><c>CURLOPT_HTTPSIG_KEYID</c> (10331), a <c>CURLOT_STRING</c> option.</summary>
    HttpsigKeyid = 10331,

    /// <summary><c>CURLOPT_HTTP_CONTENT_DECODING</c> (158), a <c>CURLOT_LONG</c> option.</summary>
    HttpContentDecoding = 158,

    /// <summary><c>CURLOPT_HTTP_TRANSFER_DECODING</c> (157), a <c>CURLOT_LONG</c> option.</summary>
    HttpTransferDecoding = 157,

    /// <summary><c>CURLOPT_HTTP_VERSION</c> (84), a <c>CURLOT_VALUES</c> option.</summary>
    HttpVersion = 84,

    /// <summary><c>CURLOPT_IGNORE_CONTENT_LENGTH</c> (136), a <c>CURLOT_LONG</c> option.</summary>
    IgnoreContentLength = 136,

    /// <summary><c>CURLOPT_INFILE</c>: an alias of <c>CURLOPT_READDATA</c> (10009), a <c>CURLOT_CBPTR</c> option.</summary>
    Infile = Readdata,

    /// <summary><c>CURLOPT_INFILESIZE</c> (14), a <c>CURLOT_LONG</c> option.</summary>
    Infilesize = 14,

    /// <summary><c>CURLOPT_INFILESIZE_LARGE</c> (30115), a <c>CURLOT_OFF_T</c> option.</summary>
    InfilesizeLarge = 30115,

    /// <summary><c>CURLOPT_INTERFACE</c> (10062), a <c>CURLOT_STRING</c> option.</summary>
    Interface = 10062,

    /// <summary><c>CURLOPT_INTERLEAVEDATA</c> (10195), a <c>CURLOT_CBPTR</c> option.</summary>
    Interleavedata = 10195,

    /// <summary><c>CURLOPT_INTERLEAVEFUNCTION</c> (20196), a <c>CURLOT_FUNCTION</c> option.</summary>
    Interleavefunction = 20196,

    /// <summary><c>CURLOPT_IOCTLDATA</c> (10131), a <c>CURLOT_CBPTR</c> option.</summary>
    Ioctldata = 10131,

    /// <summary><c>CURLOPT_IOCTLFUNCTION</c> (20130), a <c>CURLOT_FUNCTION</c> option.</summary>
    Ioctlfunction = 20130,

    /// <summary><c>CURLOPT_IPRESOLVE</c> (113), a <c>CURLOT_VALUES</c> option.</summary>
    Ipresolve = 113,

    /// <summary><c>CURLOPT_ISSUERCERT</c> (10170), a <c>CURLOT_STRING</c> option.</summary>
    Issuercert = 10170,

    /// <summary><c>CURLOPT_ISSUERCERT_BLOB</c> (40295), a <c>CURLOT_BLOB</c> option.</summary>
    IssuercertBlob = 40295,

    /// <summary><c>CURLOPT_KEEP_SENDING_ON_ERROR</c> (245), a <c>CURLOT_LONG</c> option.</summary>
    KeepSendingOnError = 245,

    /// <summary><c>CURLOPT_KEYPASSWD</c> (10026), a <c>CURLOT_STRING</c> option.</summary>
    Keypasswd = 10026,

    /// <summary><c>CURLOPT_KRB4LEVEL</c>: an alias of <c>CURLOPT_KRBLEVEL</c> (10063), a <c>CURLOT_STRING</c> option.</summary>
    Krb4level = Krblevel,

    /// <summary><c>CURLOPT_KRBLEVEL</c> (10063), a <c>CURLOT_STRING</c> option.</summary>
    Krblevel = 10063,

    /// <summary><c>CURLOPT_LOCALPORT</c> (139), a <c>CURLOT_LONG</c> option.</summary>
    Localport = 139,

    /// <summary><c>CURLOPT_LOCALPORTRANGE</c> (140), a <c>CURLOT_LONG</c> option.</summary>
    Localportrange = 140,

    /// <summary><c>CURLOPT_LOGIN_OPTIONS</c> (10224), a <c>CURLOT_STRING</c> option.</summary>
    LoginOptions = 10224,

    /// <summary><c>CURLOPT_LOW_SPEED_LIMIT</c> (19), a <c>CURLOT_LONG</c> option.</summary>
    LowSpeedLimit = 19,

    /// <summary><c>CURLOPT_LOW_SPEED_TIME</c> (20), a <c>CURLOT_LONG</c> option.</summary>
    LowSpeedTime = 20,

    /// <summary><c>CURLOPT_MAIL_AUTH</c> (10217), a <c>CURLOT_STRING</c> option.</summary>
    MailAuth = 10217,

    /// <summary><c>CURLOPT_MAIL_FROM</c> (10186), a <c>CURLOT_STRING</c> option.</summary>
    MailFrom = 10186,

    /// <summary><c>CURLOPT_MAIL_RCPT</c> (10187), a <c>CURLOT_SLIST</c> option.</summary>
    MailRcpt = 10187,

    /// <summary><c>CURLOPT_MAIL_RCPT_ALLLOWFAILS</c>: an alias of <c>CURLOPT_MAIL_RCPT_ALLOWFAILS</c> (290), a <c>CURLOT_LONG</c> option.</summary>
    MailRcptAlllowfails = MailRcptAllowfails,

    /// <summary><c>CURLOPT_MAIL_RCPT_ALLOWFAILS</c> (290), a <c>CURLOT_LONG</c> option.</summary>
    MailRcptAllowfails = 290,

    /// <summary><c>CURLOPT_MAXAGE_CONN</c> (288), a <c>CURLOT_LONG</c> option.</summary>
    MaxageConn = 288,

    /// <summary><c>CURLOPT_MAXCONNECTS</c> (71), a <c>CURLOT_LONG</c> option.</summary>
    Maxconnects = 71,

    /// <summary><c>CURLOPT_MAXFILESIZE</c> (114), a <c>CURLOT_LONG</c> option.</summary>
    Maxfilesize = 114,

    /// <summary><c>CURLOPT_MAXFILESIZE_LARGE</c> (30117), a <c>CURLOT_OFF_T</c> option.</summary>
    MaxfilesizeLarge = 30117,

    /// <summary><c>CURLOPT_MAXLIFETIME_CONN</c> (314), a <c>CURLOT_LONG</c> option.</summary>
    MaxlifetimeConn = 314,

    /// <summary><c>CURLOPT_MAXREDIRS</c> (68), a <c>CURLOT_LONG</c> option.</summary>
    Maxredirs = 68,

    /// <summary><c>CURLOPT_MAX_RECV_SPEED_LARGE</c> (30146), a <c>CURLOT_OFF_T</c> option.</summary>
    MaxRecvSpeedLarge = 30146,

    /// <summary><c>CURLOPT_MAX_SEND_SPEED_LARGE</c> (30145), a <c>CURLOT_OFF_T</c> option.</summary>
    MaxSendSpeedLarge = 30145,

    /// <summary><c>CURLOPT_MIMEPOST</c> (10269), a <c>CURLOT_OBJECT</c> option.</summary>
    Mimepost = 10269,

    /// <summary><c>CURLOPT_MIME_OPTIONS</c> (315), a <c>CURLOT_LONG</c> option.</summary>
    MimeOptions = 315,

    /// <summary><c>CURLOPT_NETRC</c> (51), a <c>CURLOT_VALUES</c> option.</summary>
    Netrc = 51,

    /// <summary><c>CURLOPT_NETRC_FILE</c> (10118), a <c>CURLOT_STRING</c> option.</summary>
    NetrcFile = 10118,

    /// <summary><c>CURLOPT_NEW_DIRECTORY_PERMS</c> (160), a <c>CURLOT_LONG</c> option.</summary>
    NewDirectoryPerms = 160,

    /// <summary><c>CURLOPT_NEW_FILE_PERMS</c> (159), a <c>CURLOT_LONG</c> option.</summary>
    NewFilePerms = 159,

    /// <summary><c>CURLOPT_NOBODY</c> (44), a <c>CURLOT_LONG</c> option.</summary>
    Nobody = 44,

    /// <summary><c>CURLOPT_NOPROGRESS</c> (43), a <c>CURLOT_LONG</c> option.</summary>
    Noprogress = 43,

    /// <summary><c>CURLOPT_NOPROXY</c> (10177), a <c>CURLOT_STRING</c> option.</summary>
    Noproxy = 10177,

    /// <summary><c>CURLOPT_NOSIGNAL</c> (99), a <c>CURLOT_LONG</c> option.</summary>
    Nosignal = 99,

    /// <summary><c>CURLOPT_OPENSOCKETDATA</c> (10164), a <c>CURLOT_CBPTR</c> option.</summary>
    Opensocketdata = 10164,

    /// <summary><c>CURLOPT_OPENSOCKETFUNCTION</c> (20163), a <c>CURLOT_FUNCTION</c> option.</summary>
    Opensocketfunction = 20163,

    /// <summary><c>CURLOPT_PASSWORD</c> (10174), a <c>CURLOT_STRING</c> option.</summary>
    Password = 10174,

    /// <summary><c>CURLOPT_PATH_AS_IS</c> (234), a <c>CURLOT_LONG</c> option.</summary>
    PathAsIs = 234,

    /// <summary><c>CURLOPT_PINNEDPUBLICKEY</c> (10230), a <c>CURLOT_STRING</c> option.</summary>
    Pinnedpublickey = 10230,

    /// <summary><c>CURLOPT_PIPEWAIT</c> (237), a <c>CURLOT_LONG</c> option.</summary>
    Pipewait = 237,

    /// <summary><c>CURLOPT_PORT</c> (3), a <c>CURLOT_LONG</c> option.</summary>
    Port = 3,

    /// <summary><c>CURLOPT_POST</c> (47), a <c>CURLOT_LONG</c> option.</summary>
    Post = 47,

    /// <summary><c>CURLOPT_POST301</c>: an alias of <c>CURLOPT_POSTREDIR</c> (161), a <c>CURLOT_VALUES</c> option.</summary>
    Post301 = Postredir,

    /// <summary><c>CURLOPT_POSTFIELDS</c> (10015), a <c>CURLOT_OBJECT</c> option.</summary>
    Postfields = 10015,

    /// <summary><c>CURLOPT_POSTFIELDSIZE</c> (60), a <c>CURLOT_LONG</c> option.</summary>
    Postfieldsize = 60,

    /// <summary><c>CURLOPT_POSTFIELDSIZE_LARGE</c> (30120), a <c>CURLOT_OFF_T</c> option.</summary>
    PostfieldsizeLarge = 30120,

    /// <summary><c>CURLOPT_POSTQUOTE</c> (10039), a <c>CURLOT_SLIST</c> option.</summary>
    Postquote = 10039,

    /// <summary><c>CURLOPT_POSTREDIR</c> (161), a <c>CURLOT_VALUES</c> option.</summary>
    Postredir = 161,

    /// <summary><c>CURLOPT_PREQUOTE</c> (10093), a <c>CURLOT_SLIST</c> option.</summary>
    Prequote = 10093,

    /// <summary><c>CURLOPT_PREREQDATA</c> (10313), a <c>CURLOT_CBPTR</c> option.</summary>
    Prereqdata = 10313,

    /// <summary><c>CURLOPT_PREREQFUNCTION</c> (20312), a <c>CURLOT_FUNCTION</c> option.</summary>
    Prereqfunction = 20312,

    /// <summary><c>CURLOPT_PRE_PROXY</c> (10262), a <c>CURLOT_STRING</c> option.</summary>
    PreProxy = 10262,

    /// <summary><c>CURLOPT_PRIVATE</c> (10103), a <c>CURLOT_OBJECT</c> option.</summary>
    Private = 10103,

    /// <summary><c>CURLOPT_PROGRESSDATA</c>: an alias of <c>CURLOPT_XFERINFODATA</c> (10057), a <c>CURLOT_CBPTR</c> option.</summary>
    Progressdata = Xferinfodata,

    /// <summary><c>CURLOPT_PROGRESSFUNCTION</c> (20056), a <c>CURLOT_FUNCTION</c> option.</summary>
    Progressfunction = 20056,

    /// <summary><c>CURLOPT_PROTOCOLS</c> (181), a <c>CURLOT_LONG</c> option.</summary>
    Protocols = 181,

    /// <summary><c>CURLOPT_PROTOCOLS_STR</c> (10318), a <c>CURLOT_STRING</c> option.</summary>
    ProtocolsStr = 10318,

    /// <summary><c>CURLOPT_PROXY</c> (10004), a <c>CURLOT_STRING</c> option.</summary>
    Proxy = 10004,

    /// <summary><c>CURLOPT_PROXYAUTH</c> (111), a <c>CURLOT_VALUES</c> option.</summary>
    Proxyauth = 111,

    /// <summary><c>CURLOPT_PROXYHEADER</c> (10228), a <c>CURLOT_SLIST</c> option.</summary>
    Proxyheader = 10228,

    /// <summary><c>CURLOPT_PROXYPASSWORD</c> (10176), a <c>CURLOT_STRING</c> option.</summary>
    Proxypassword = 10176,

    /// <summary><c>CURLOPT_PROXYPORT</c> (59), a <c>CURLOT_LONG</c> option.</summary>
    Proxyport = 59,

    /// <summary><c>CURLOPT_PROXYTYPE</c> (101), a <c>CURLOT_VALUES</c> option.</summary>
    Proxytype = 101,

    /// <summary><c>CURLOPT_PROXYUSERNAME</c> (10175), a <c>CURLOT_STRING</c> option.</summary>
    Proxyusername = 10175,

    /// <summary><c>CURLOPT_PROXYUSERPWD</c> (10006), a <c>CURLOT_STRING</c> option.</summary>
    Proxyuserpwd = 10006,

    /// <summary><c>CURLOPT_PROXY_CAINFO</c> (10246), a <c>CURLOT_STRING</c> option.</summary>
    ProxyCainfo = 10246,

    /// <summary><c>CURLOPT_PROXY_CAINFO_BLOB</c> (40310), a <c>CURLOT_BLOB</c> option.</summary>
    ProxyCainfoBlob = 40310,

    /// <summary><c>CURLOPT_PROXY_CAPATH</c> (10247), a <c>CURLOT_STRING</c> option.</summary>
    ProxyCapath = 10247,

    /// <summary><c>CURLOPT_PROXY_CRLFILE</c> (10260), a <c>CURLOT_STRING</c> option.</summary>
    ProxyCrlfile = 10260,

    /// <summary><c>CURLOPT_PROXY_ISSUERCERT</c> (10296), a <c>CURLOT_STRING</c> option.</summary>
    ProxyIssuercert = 10296,

    /// <summary><c>CURLOPT_PROXY_ISSUERCERT_BLOB</c> (40297), a <c>CURLOT_BLOB</c> option.</summary>
    ProxyIssuercertBlob = 40297,

    /// <summary><c>CURLOPT_PROXY_KEYPASSWD</c> (10258), a <c>CURLOT_STRING</c> option.</summary>
    ProxyKeypasswd = 10258,

    /// <summary><c>CURLOPT_PROXY_PINNEDPUBLICKEY</c> (10263), a <c>CURLOT_STRING</c> option.</summary>
    ProxyPinnedpublickey = 10263,

    /// <summary><c>CURLOPT_PROXY_SERVICE_NAME</c> (10235), a <c>CURLOT_STRING</c> option.</summary>
    ProxyServiceName = 10235,

    /// <summary><c>CURLOPT_PROXY_SSLCERT</c> (10254), a <c>CURLOT_STRING</c> option.</summary>
    ProxySslcert = 10254,

    /// <summary><c>CURLOPT_PROXY_SSLCERTTYPE</c> (10255), a <c>CURLOT_STRING</c> option.</summary>
    ProxySslcerttype = 10255,

    /// <summary><c>CURLOPT_PROXY_SSLCERT_BLOB</c> (40293), a <c>CURLOT_BLOB</c> option.</summary>
    ProxySslcertBlob = 40293,

    /// <summary><c>CURLOPT_PROXY_SSLKEY</c> (10256), a <c>CURLOT_STRING</c> option.</summary>
    ProxySslkey = 10256,

    /// <summary><c>CURLOPT_PROXY_SSLKEYTYPE</c> (10257), a <c>CURLOT_STRING</c> option.</summary>
    ProxySslkeytype = 10257,

    /// <summary><c>CURLOPT_PROXY_SSLKEY_BLOB</c> (40294), a <c>CURLOT_BLOB</c> option.</summary>
    ProxySslkeyBlob = 40294,

    /// <summary><c>CURLOPT_PROXY_SSLVERSION</c> (250), a <c>CURLOT_VALUES</c> option.</summary>
    ProxySslversion = 250,

    /// <summary><c>CURLOPT_PROXY_SSL_CIPHER_LIST</c> (10259), a <c>CURLOT_STRING</c> option.</summary>
    ProxySslCipherList = 10259,

    /// <summary><c>CURLOPT_PROXY_SSL_OPTIONS</c> (261), a <c>CURLOT_LONG</c> option.</summary>
    ProxySslOptions = 261,

    /// <summary><c>CURLOPT_PROXY_SSL_VERIFYHOST</c> (249), a <c>CURLOT_LONG</c> option.</summary>
    ProxySslVerifyhost = 249,

    /// <summary><c>CURLOPT_PROXY_SSL_VERIFYPEER</c> (248), a <c>CURLOT_LONG</c> option.</summary>
    ProxySslVerifypeer = 248,

    /// <summary><c>CURLOPT_PROXY_TLS13_CIPHERS</c> (10277), a <c>CURLOT_STRING</c> option.</summary>
    ProxyTls13Ciphers = 10277,

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_PASSWORD</c> (10252), a <c>CURLOT_STRING</c> option.</summary>
    ProxyTlsauthPassword = 10252,

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_TYPE</c> (10253), a <c>CURLOT_STRING</c> option.</summary>
    ProxyTlsauthType = 10253,

    /// <summary><c>CURLOPT_PROXY_TLSAUTH_USERNAME</c> (10251), a <c>CURLOT_STRING</c> option.</summary>
    ProxyTlsauthUsername = 10251,

    /// <summary><c>CURLOPT_PROXY_TRANSFER_MODE</c> (166), a <c>CURLOT_LONG</c> option.</summary>
    ProxyTransferMode = 166,

    /// <summary><c>CURLOPT_PUT</c> (54), a <c>CURLOT_LONG</c> option.</summary>
    Put = 54,

    /// <summary><c>CURLOPT_QUICK_EXIT</c> (322), a <c>CURLOT_LONG</c> option.</summary>
    QuickExit = 322,

    /// <summary><c>CURLOPT_QUOTE</c> (10028), a <c>CURLOT_SLIST</c> option.</summary>
    Quote = 10028,

    /// <summary><c>CURLOPT_RANDOM_FILE</c> (10076), a <c>CURLOT_STRING</c> option.</summary>
    RandomFile = 10076,

    /// <summary><c>CURLOPT_RANGE</c> (10007), a <c>CURLOT_STRING</c> option.</summary>
    Range = 10007,

    /// <summary><c>CURLOPT_READDATA</c> (10009), a <c>CURLOT_CBPTR</c> option.</summary>
    Readdata = 10009,

    /// <summary><c>CURLOPT_READFUNCTION</c> (20012), a <c>CURLOT_FUNCTION</c> option.</summary>
    Readfunction = 20012,

    /// <summary><c>CURLOPT_REDIR_PROTOCOLS</c> (182), a <c>CURLOT_LONG</c> option.</summary>
    RedirProtocols = 182,

    /// <summary><c>CURLOPT_REDIR_PROTOCOLS_STR</c> (10319), a <c>CURLOT_STRING</c> option.</summary>
    RedirProtocolsStr = 10319,

    /// <summary><c>CURLOPT_REFERER</c> (10016), a <c>CURLOT_STRING</c> option.</summary>
    Referer = 10016,

    /// <summary><c>CURLOPT_REQUEST_TARGET</c> (10266), a <c>CURLOT_STRING</c> option.</summary>
    RequestTarget = 10266,

    /// <summary><c>CURLOPT_RESOLVE</c> (10203), a <c>CURLOT_SLIST</c> option.</summary>
    Resolve = 10203,

    /// <summary><c>CURLOPT_RESOLVER_START_DATA</c> (10273), a <c>CURLOT_CBPTR</c> option.</summary>
    ResolverStartData = 10273,

    /// <summary><c>CURLOPT_RESOLVER_START_FUNCTION</c> (20272), a <c>CURLOT_FUNCTION</c> option.</summary>
    ResolverStartFunction = 20272,

    /// <summary><c>CURLOPT_RESUME_FROM</c> (21), a <c>CURLOT_LONG</c> option.</summary>
    ResumeFrom = 21,

    /// <summary><c>CURLOPT_RESUME_FROM_LARGE</c> (30116), a <c>CURLOT_OFF_T</c> option.</summary>
    ResumeFromLarge = 30116,

    /// <summary><c>CURLOPT_RTSPHEADER</c>: an alias of <c>CURLOPT_HTTPHEADER</c> (10023), a <c>CURLOT_SLIST</c> option.</summary>
    Rtspheader = Httpheader,

    /// <summary><c>CURLOPT_RTSP_CLIENT_CSEQ</c> (193), a <c>CURLOT_LONG</c> option.</summary>
    RtspClientCseq = 193,

    /// <summary><c>CURLOPT_RTSP_REQUEST</c> (189), a <c>CURLOT_VALUES</c> option.</summary>
    RtspRequest = 189,

    /// <summary><c>CURLOPT_RTSP_SERVER_CSEQ</c> (194), a <c>CURLOT_LONG</c> option.</summary>
    RtspServerCseq = 194,

    /// <summary><c>CURLOPT_RTSP_SESSION_ID</c> (10190), a <c>CURLOT_STRING</c> option.</summary>
    RtspSessionId = 10190,

    /// <summary><c>CURLOPT_RTSP_STREAM_URI</c> (10191), a <c>CURLOT_STRING</c> option.</summary>
    RtspStreamUri = 10191,

    /// <summary><c>CURLOPT_RTSP_TRANSPORT</c> (10192), a <c>CURLOT_STRING</c> option.</summary>
    RtspTransport = 10192,

    /// <summary><c>CURLOPT_SASL_AUTHZID</c> (10289), a <c>CURLOT_STRING</c> option.</summary>
    SaslAuthzid = 10289,

    /// <summary><c>CURLOPT_SASL_IR</c> (218), a <c>CURLOT_LONG</c> option.</summary>
    SaslIr = 218,

    /// <summary><c>CURLOPT_SEEKDATA</c> (10168), a <c>CURLOT_CBPTR</c> option.</summary>
    Seekdata = 10168,

    /// <summary><c>CURLOPT_SEEKFUNCTION</c> (20167), a <c>CURLOT_FUNCTION</c> option.</summary>
    Seekfunction = 20167,

    /// <summary><c>CURLOPT_SERVER_RESPONSE_TIMEOUT</c> (112), a <c>CURLOT_LONG</c> option.</summary>
    ServerResponseTimeout = 112,

    /// <summary><c>CURLOPT_SERVER_RESPONSE_TIMEOUT_MS</c> (324), a <c>CURLOT_LONG</c> option.</summary>
    ServerResponseTimeoutMs = 324,

    /// <summary><c>CURLOPT_SERVICE_NAME</c> (10236), a <c>CURLOT_STRING</c> option.</summary>
    ServiceName = 10236,

    /// <summary><c>CURLOPT_SHARE</c> (10100), a <c>CURLOT_OBJECT</c> option.</summary>
    Share = 10100,

    /// <summary><c>CURLOPT_SOCKOPTDATA</c> (10149), a <c>CURLOT_CBPTR</c> option.</summary>
    Sockoptdata = 10149,

    /// <summary><c>CURLOPT_SOCKOPTFUNCTION</c> (20148), a <c>CURLOT_FUNCTION</c> option.</summary>
    Sockoptfunction = 20148,

    /// <summary><c>CURLOPT_SOCKS5_AUTH</c> (267), a <c>CURLOT_LONG</c> option.</summary>
    Socks5Auth = 267,

    /// <summary><c>CURLOPT_SOCKS5_GSSAPI_NEC</c> (180), a <c>CURLOT_LONG</c> option.</summary>
    Socks5GssapiNec = 180,

    /// <summary><c>CURLOPT_SOCKS5_GSSAPI_SERVICE</c> (10179), a <c>CURLOT_STRING</c> option.</summary>
    Socks5GssapiService = 10179,

    /// <summary><c>CURLOPT_SSH_AUTH_TYPES</c> (151), a <c>CURLOT_VALUES</c> option.</summary>
    SshAuthTypes = 151,

    /// <summary><c>CURLOPT_SSH_COMPRESSION</c> (268), a <c>CURLOT_LONG</c> option.</summary>
    SshCompression = 268,

    /// <summary><c>CURLOPT_SSH_HOSTKEYDATA</c> (10317), a <c>CURLOT_CBPTR</c> option.</summary>
    SshHostkeydata = 10317,

    /// <summary><c>CURLOPT_SSH_HOSTKEYFUNCTION</c> (20316), a <c>CURLOT_FUNCTION</c> option.</summary>
    SshHostkeyfunction = 20316,

    /// <summary><c>CURLOPT_SSH_HOST_PUBLIC_KEY_MD5</c> (10162), a <c>CURLOT_STRING</c> option.</summary>
    SshHostPublicKeyMd5 = 10162,

    /// <summary><c>CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256</c> (10311), a <c>CURLOT_STRING</c> option.</summary>
    SshHostPublicKeySha256 = 10311,

    /// <summary><c>CURLOPT_SSH_KEYDATA</c> (10185), a <c>CURLOT_CBPTR</c> option.</summary>
    SshKeydata = 10185,

    /// <summary><c>CURLOPT_SSH_KEYFUNCTION</c> (20184), a <c>CURLOT_FUNCTION</c> option.</summary>
    SshKeyfunction = 20184,

    /// <summary><c>CURLOPT_SSH_KNOWNHOSTS</c> (10183), a <c>CURLOT_STRING</c> option.</summary>
    SshKnownhosts = 10183,

    /// <summary><c>CURLOPT_SSH_PRIVATE_KEYFILE</c> (10153), a <c>CURLOT_STRING</c> option.</summary>
    SshPrivateKeyfile = 10153,

    /// <summary><c>CURLOPT_SSH_PUBLIC_KEYFILE</c> (10152), a <c>CURLOT_STRING</c> option.</summary>
    SshPublicKeyfile = 10152,

    /// <summary><c>CURLOPT_SSLCERT</c> (10025), a <c>CURLOT_STRING</c> option.</summary>
    Sslcert = 10025,

    /// <summary><c>CURLOPT_SSLCERTPASSWD</c>: an alias of <c>CURLOPT_KEYPASSWD</c> (10026), a <c>CURLOT_STRING</c> option.</summary>
    Sslcertpasswd = Keypasswd,

    /// <summary><c>CURLOPT_SSLCERTTYPE</c> (10086), a <c>CURLOT_STRING</c> option.</summary>
    Sslcerttype = 10086,

    /// <summary><c>CURLOPT_SSLCERT_BLOB</c> (40291), a <c>CURLOT_BLOB</c> option.</summary>
    SslcertBlob = 40291,

    /// <summary><c>CURLOPT_SSLENGINE</c> (10089), a <c>CURLOT_STRING</c> option.</summary>
    Sslengine = 10089,

    /// <summary><c>CURLOPT_SSLENGINE_DEFAULT</c> (90), a <c>CURLOT_LONG</c> option.</summary>
    SslengineDefault = 90,

    /// <summary><c>CURLOPT_SSLKEY</c> (10087), a <c>CURLOT_STRING</c> option.</summary>
    Sslkey = 10087,

    /// <summary><c>CURLOPT_SSLKEYPASSWD</c>: an alias of <c>CURLOPT_KEYPASSWD</c> (10026), a <c>CURLOT_STRING</c> option.</summary>
    Sslkeypasswd = Keypasswd,

    /// <summary><c>CURLOPT_SSLKEYTYPE</c> (10088), a <c>CURLOT_STRING</c> option.</summary>
    Sslkeytype = 10088,

    /// <summary><c>CURLOPT_SSLKEY_BLOB</c> (40292), a <c>CURLOT_BLOB</c> option.</summary>
    SslkeyBlob = 40292,

    /// <summary><c>CURLOPT_SSLVERSION</c> (32), a <c>CURLOT_VALUES</c> option.</summary>
    Sslversion = 32,

    /// <summary><c>CURLOPT_SSL_CIPHER_LIST</c> (10083), a <c>CURLOT_STRING</c> option.</summary>
    SslCipherList = 10083,

    /// <summary><c>CURLOPT_SSL_CTX_DATA</c> (10109), a <c>CURLOT_CBPTR</c> option.</summary>
    SslCtxData = 10109,

    /// <summary><c>CURLOPT_SSL_CTX_FUNCTION</c> (20108), a <c>CURLOT_FUNCTION</c> option.</summary>
    SslCtxFunction = 20108,

    /// <summary><c>CURLOPT_SSL_EC_CURVES</c> (10298), a <c>CURLOT_STRING</c> option.</summary>
    SslEcCurves = 10298,

    /// <summary><c>CURLOPT_SSL_ENABLE_ALPN</c> (226), a <c>CURLOT_LONG</c> option.</summary>
    SslEnableAlpn = 226,

    /// <summary><c>CURLOPT_SSL_ENABLE_NPN</c> (225), a <c>CURLOT_LONG</c> option.</summary>
    SslEnableNpn = 225,

    /// <summary><c>CURLOPT_SSL_FALSESTART</c> (233), a <c>CURLOT_LONG</c> option.</summary>
    SslFalsestart = 233,

    /// <summary><c>CURLOPT_SSL_OPTIONS</c> (216), a <c>CURLOT_VALUES</c> option.</summary>
    SslOptions = 216,

    /// <summary><c>CURLOPT_SSL_SESSIONID_CACHE</c> (150), a <c>CURLOT_LONG</c> option.</summary>
    SslSessionidCache = 150,

    /// <summary><c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c> (10328), a <c>CURLOT_STRING</c> option.</summary>
    SslSignatureAlgorithms = 10328,

    /// <summary><c>CURLOPT_SSL_VERIFYHOST</c> (81), a <c>CURLOT_LONG</c> option.</summary>
    SslVerifyhost = 81,

    /// <summary><c>CURLOPT_SSL_VERIFYPEER</c> (64), a <c>CURLOT_LONG</c> option.</summary>
    SslVerifypeer = 64,

    /// <summary><c>CURLOPT_SSL_VERIFYSTATUS</c> (232), a <c>CURLOT_LONG</c> option.</summary>
    SslVerifystatus = 232,

    /// <summary><c>CURLOPT_STDERR</c> (10037), a <c>CURLOT_OBJECT</c> option.</summary>
    Stderr = 10037,

    /// <summary><c>CURLOPT_STREAM_DEPENDS</c> (10240), a <c>CURLOT_OBJECT</c> option.</summary>
    StreamDepends = 10240,

    /// <summary><c>CURLOPT_STREAM_DEPENDS_E</c> (10241), a <c>CURLOT_OBJECT</c> option.</summary>
    StreamDependsE = 10241,

    /// <summary><c>CURLOPT_STREAM_WEIGHT</c> (239), a <c>CURLOT_LONG</c> option.</summary>
    StreamWeight = 239,

    /// <summary><c>CURLOPT_SUPPRESS_CONNECT_HEADERS</c> (265), a <c>CURLOT_LONG</c> option.</summary>
    SuppressConnectHeaders = 265,

    /// <summary><c>CURLOPT_TCP_FASTOPEN</c> (244), a <c>CURLOT_LONG</c> option.</summary>
    TcpFastopen = 244,

    /// <summary><c>CURLOPT_TCP_KEEPALIVE</c> (213), a <c>CURLOT_LONG</c> option.</summary>
    TcpKeepalive = 213,

    /// <summary><c>CURLOPT_TCP_KEEPCNT</c> (326), a <c>CURLOT_LONG</c> option.</summary>
    TcpKeepcnt = 326,

    /// <summary><c>CURLOPT_TCP_KEEPIDLE</c> (214), a <c>CURLOT_LONG</c> option.</summary>
    TcpKeepidle = 214,

    /// <summary><c>CURLOPT_TCP_KEEPINTVL</c> (215), a <c>CURLOT_LONG</c> option.</summary>
    TcpKeepintvl = 215,

    /// <summary><c>CURLOPT_TCP_NODELAY</c> (121), a <c>CURLOT_LONG</c> option.</summary>
    TcpNodelay = 121,

    /// <summary><c>CURLOPT_TELNETOPTIONS</c> (10070), a <c>CURLOT_SLIST</c> option.</summary>
    Telnetoptions = 10070,

    /// <summary><c>CURLOPT_TFTP_BLKSIZE</c> (178), a <c>CURLOT_LONG</c> option.</summary>
    TftpBlksize = 178,

    /// <summary><c>CURLOPT_TFTP_NO_OPTIONS</c> (242), a <c>CURLOT_LONG</c> option.</summary>
    TftpNoOptions = 242,

    /// <summary><c>CURLOPT_TIMECONDITION</c> (33), a <c>CURLOT_VALUES</c> option.</summary>
    Timecondition = 33,

    /// <summary><c>CURLOPT_TIMEOUT</c> (13), a <c>CURLOT_LONG</c> option.</summary>
    Timeout = 13,

    /// <summary><c>CURLOPT_TIMEOUT_MS</c> (155), a <c>CURLOT_LONG</c> option.</summary>
    TimeoutMs = 155,

    /// <summary><c>CURLOPT_TIMEVALUE</c> (34), a <c>CURLOT_LONG</c> option.</summary>
    Timevalue = 34,

    /// <summary><c>CURLOPT_TIMEVALUE_LARGE</c> (30270), a <c>CURLOT_OFF_T</c> option.</summary>
    TimevalueLarge = 30270,

    /// <summary><c>CURLOPT_TLS13_CIPHERS</c> (10276), a <c>CURLOT_STRING</c> option.</summary>
    Tls13Ciphers = 10276,

    /// <summary><c>CURLOPT_TLSAUTH_PASSWORD</c> (10205), a <c>CURLOT_STRING</c> option.</summary>
    TlsauthPassword = 10205,

    /// <summary><c>CURLOPT_TLSAUTH_TYPE</c> (10206), a <c>CURLOT_STRING</c> option.</summary>
    TlsauthType = 10206,

    /// <summary><c>CURLOPT_TLSAUTH_USERNAME</c> (10204), a <c>CURLOT_STRING</c> option.</summary>
    TlsauthUsername = 10204,

    /// <summary><c>CURLOPT_TRAILERDATA</c> (10284), a <c>CURLOT_CBPTR</c> option.</summary>
    Trailerdata = 10284,

    /// <summary><c>CURLOPT_TRAILERFUNCTION</c> (20283), a <c>CURLOT_FUNCTION</c> option.</summary>
    Trailerfunction = 20283,

    /// <summary><c>CURLOPT_TRANSFERTEXT</c> (53), a <c>CURLOT_LONG</c> option.</summary>
    Transfertext = 53,

    /// <summary><c>CURLOPT_TRANSFER_ENCODING</c> (207), a <c>CURLOT_LONG</c> option.</summary>
    TransferEncoding = 207,

    /// <summary><c>CURLOPT_UNIX_SOCKET_PATH</c> (10231), a <c>CURLOT_STRING</c> option.</summary>
    UnixSocketPath = 10231,

    /// <summary><c>CURLOPT_UNRESTRICTED_AUTH</c> (105), a <c>CURLOT_LONG</c> option.</summary>
    UnrestrictedAuth = 105,

    /// <summary><c>CURLOPT_UPKEEP_INTERVAL_MS</c> (281), a <c>CURLOT_LONG</c> option.</summary>
    UpkeepIntervalMs = 281,

    /// <summary><c>CURLOPT_UPLOAD</c> (46), a <c>CURLOT_LONG</c> option.</summary>
    Upload = 46,

    /// <summary><c>CURLOPT_UPLOAD_BUFFERSIZE</c> (280), a <c>CURLOT_LONG</c> option.</summary>
    UploadBuffersize = 280,

    /// <summary><c>CURLOPT_UPLOAD_FLAGS</c> (327), a <c>CURLOT_LONG</c> option.</summary>
    UploadFlags = 327,

    /// <summary><c>CURLOPT_URL</c> (10002), a <c>CURLOT_STRING</c> option.</summary>
    Url = 10002,

    /// <summary><c>CURLOPT_USERAGENT</c> (10018), a <c>CURLOT_STRING</c> option.</summary>
    Useragent = 10018,

    /// <summary><c>CURLOPT_USERNAME</c> (10173), a <c>CURLOT_STRING</c> option.</summary>
    Username = 10173,

    /// <summary><c>CURLOPT_USERPWD</c> (10005), a <c>CURLOT_STRING</c> option.</summary>
    Userpwd = 10005,

    /// <summary><c>CURLOPT_USE_SSL</c> (119), a <c>CURLOT_VALUES</c> option.</summary>
    UseSsl = 119,

    /// <summary><c>CURLOPT_VERBOSE</c> (41), a <c>CURLOT_LONG</c> option.</summary>
    Verbose = 41,

    /// <summary><c>CURLOPT_WILDCARDMATCH</c> (197), a <c>CURLOT_LONG</c> option.</summary>
    Wildcardmatch = 197,

    /// <summary><c>CURLOPT_WRITEDATA</c> (10001), a <c>CURLOT_CBPTR</c> option.</summary>
    Writedata = 10001,

    /// <summary><c>CURLOPT_WRITEFUNCTION</c> (20011), a <c>CURLOT_FUNCTION</c> option.</summary>
    Writefunction = 20011,

    /// <summary><c>CURLOPT_WRITEHEADER</c>: an alias of <c>CURLOPT_HEADERDATA</c> (10029), a <c>CURLOT_CBPTR</c> option.</summary>
    Writeheader = Headerdata,

    /// <summary><c>CURLOPT_WS_OPTIONS</c> (320), a <c>CURLOT_LONG</c> option.</summary>
    WsOptions = 320,

    /// <summary><c>CURLOPT_XFERINFODATA</c> (10057), a <c>CURLOT_CBPTR</c> option.</summary>
    Xferinfodata = 10057,

    /// <summary><c>CURLOPT_XFERINFOFUNCTION</c> (20219), a <c>CURLOT_FUNCTION</c> option.</summary>
    Xferinfofunction = 20219,

    /// <summary><c>CURLOPT_XOAUTH2_BEARER</c> (10220), a <c>CURLOT_STRING</c> option.</summary>
    Xoauth2Bearer = 10220,
}
