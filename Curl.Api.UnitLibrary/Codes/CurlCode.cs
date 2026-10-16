namespace Curl.Api.Codes;

/// <summary>
/// The result of a libcurl easy function: libcurl's <c>CURLcode</c> (<c>CURLE_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every member shares its name and number with the <see cref="Curl.Protocol.Abstractions.CurlExitCode"/>
/// member for the same outcome, so <see cref="CurlCodeConversion"/> converts between them by number
/// (ADR-0506). <c>CurlExitCode</c> has no member for the numbers upstream keeps only as
/// <c>CURLE_OBSOLETE*</c> placeholders; this enum keeps those too, so every number round-trips.
/// </para>
/// </remarks>
public enum CurlCode
{
    /// <summary><c>CURLE_OK</c> (0): the function succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURLE_UNSUPPORTED_PROTOCOL</c> (1): the URL used a scheme this build does not support.</summary>
    UnsupportedProtocol = 1,

    /// <summary><c>CURLE_FAILED_INIT</c> (2): initialisation failed.</summary>
    FailedInit = 2,

    /// <summary><c>CURLE_URL_MALFORMAT</c> (3): the URL was not formatted correctly.</summary>
    UrlMalformat = 3,

    /// <summary><c>CURLE_NOT_BUILT_IN</c> (4): a requested feature was not compiled in.</summary>
    NotBuiltIn = 4,

    /// <summary><c>CURLE_COULDNT_RESOLVE_PROXY</c> (5): the proxy host could not be resolved.</summary>
    CouldntResolveProxy = 5,

    /// <summary><c>CURLE_COULDNT_RESOLVE_HOST</c> (6): the remote host could not be resolved.</summary>
    CouldntResolveHost = 6,

    /// <summary><c>CURLE_COULDNT_CONNECT</c> (7): the connection to the host or proxy failed.</summary>
    CouldntConnect = 7,

    /// <summary><c>CURLE_WEIRD_SERVER_REPLY</c> (8): the server sent a reply that could not be parsed.</summary>
    WeirdServerReply = 8,

    /// <summary><c>CURLE_REMOTE_ACCESS_DENIED</c> (9): access to the remote resource was denied.</summary>
    RemoteAccessDenied = 9,

    /// <summary><c>CURLE_FTP_ACCEPT_FAILED</c> (10): the server rejected an FTP data connection.</summary>
    FtpAcceptFailed = 10,

    /// <summary><c>CURLE_FTP_WEIRD_PASS_REPLY</c> (11): the FTP server sent an unexpected reply to PASS.</summary>
    FtpWeirdPassReply = 11,

    /// <summary><c>CURLE_FTP_ACCEPT_TIMEOUT</c> (12): timed out waiting for an FTP data connection.</summary>
    FtpAcceptTimeout = 12,

    /// <summary><c>CURLE_FTP_WEIRD_PASV_REPLY</c> (13): the FTP server sent an unexpected reply to PASV.</summary>
    FtpWeirdPasvReply = 13,

    /// <summary><c>CURLE_FTP_WEIRD_227_FORMAT</c> (14): an FTP 227 reply could not be parsed.</summary>
    FtpWeird227Format = 14,

    /// <summary><c>CURLE_FTP_CANT_GET_HOST</c> (15): the host named in an FTP 227 reply could not be used.</summary>
    FtpCantGetHost = 15,

    /// <summary><c>CURLE_HTTP2</c> (16): an HTTP/2 framing or protocol error occurred.</summary>
    Http2 = 16,

    /// <summary><c>CURLE_FTP_COULDNT_SET_TYPE</c> (17): the FTP transfer type could not be set.</summary>
    FtpCouldntSetType = 17,

    /// <summary><c>CURLE_PARTIAL_FILE</c> (18): the transfer ended with fewer bytes than announced.</summary>
    PartialFile = 18,

    /// <summary><c>CURLE_FTP_COULDNT_RETR_FILE</c> (19): the FTP server could not send the requested file.</summary>
    FtpCouldntRetrFile = 19,

    /// <summary><c>CURLE_OBSOLETE20</c> (20): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete20 = 20,

    /// <summary><c>CURLE_QUOTE_ERROR</c> (21): a quote command returned an error.</summary>
    QuoteError = 21,

    /// <summary><c>CURLE_HTTP_RETURNED_ERROR</c> (22): the server returned an error status and failing on errors was asked for.</summary>
    HttpReturnedError = 22,

    /// <summary><c>CURLE_WRITE_ERROR</c> (23): writing received data to its destination failed.</summary>
    WriteError = 23,

    /// <summary><c>CURLE_OBSOLETE24</c> (24): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete24 = 24,

    /// <summary><c>CURLE_UPLOAD_FAILED</c> (25): the upload was rejected.</summary>
    UploadFailed = 25,

    /// <summary><c>CURLE_READ_ERROR</c> (26): reading the data to upload failed.</summary>
    ReadError = 26,

    /// <summary><c>CURLE_OUT_OF_MEMORY</c> (27): a memory allocation failed.</summary>
    OutOfMemory = 27,

    /// <summary><c>CURLE_OPERATION_TIMEDOUT</c> (28): the operation exceeded its timeout.</summary>
    OperationTimedOut = 28,

    /// <summary><c>CURLE_OBSOLETE29</c> (29): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete29 = 29,

    /// <summary><c>CURLE_FTP_PORT_FAILED</c> (30): the FTP PORT command failed.</summary>
    FtpPortFailed = 30,

    /// <summary><c>CURLE_FTP_COULDNT_USE_REST</c> (31): the FTP REST command failed, so the transfer cannot resume.</summary>
    FtpCouldntUseRest = 31,

    /// <summary><c>CURLE_OBSOLETE32</c> (32): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete32 = 32,

    /// <summary><c>CURLE_RANGE_ERROR</c> (33): the server does not support byte ranges.</summary>
    RangeError = 33,

    /// <summary><c>CURLE_OBSOLETE34</c> (34): unused since 7.56.0 (was <c>CURLE_HTTP_POST_ERROR</c>); upstream keeps the number so it is never reused.</summary>
    Obsolete34 = 34,

    /// <summary><c>CURLE_SSL_CONNECT_ERROR</c> (35): the TLS handshake failed.</summary>
    SslConnectError = 35,

    /// <summary><c>CURLE_BAD_DOWNLOAD_RESUME</c> (36): the download could not be resumed from the requested offset.</summary>
    BadDownloadResume = 36,

    /// <summary><c>CURLE_FILE_COULDNT_READ_FILE</c> (37): the local file named by a file:// URL could not be read.</summary>
    FileCouldntReadFile = 37,

    /// <summary><c>CURLE_LDAP_CANNOT_BIND</c> (38): the LDAP bind operation failed.</summary>
    LdapCannotBind = 38,

    /// <summary><c>CURLE_LDAP_SEARCH_FAILED</c> (39): the LDAP search failed.</summary>
    LdapSearchFailed = 39,

    /// <summary><c>CURLE_OBSOLETE40</c> (40): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete40 = 40,

    /// <summary><c>CURLE_OBSOLETE41</c> (41): unused since 7.53.0 (was <c>CURLE_FUNCTION_NOT_FOUND</c>); upstream keeps the number so it is never reused.</summary>
    Obsolete41 = 41,

    /// <summary><c>CURLE_ABORTED_BY_CALLBACK</c> (42): a callback aborted the transfer.</summary>
    AbortedByCallback = 42,

    /// <summary><c>CURLE_BAD_FUNCTION_ARGUMENT</c> (43): an argument was rejected as invalid.</summary>
    BadFunctionArgument = 43,

    /// <summary><c>CURLE_OBSOLETE44</c> (44): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete44 = 44,

    /// <summary><c>CURLE_INTERFACE_FAILED</c> (45): the requested outgoing interface could not be used.</summary>
    InterfaceFailed = 45,

    /// <summary><c>CURLE_OBSOLETE46</c> (46): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete46 = 46,

    /// <summary><c>CURLE_TOO_MANY_REDIRECTS</c> (47): the redirect limit was reached.</summary>
    TooManyRedirects = 47,

    /// <summary><c>CURLE_UNKNOWN_OPTION</c> (48): an option was not recognised.</summary>
    UnknownOption = 48,

    /// <summary><c>CURLE_SETOPT_OPTION_SYNTAX</c> (49): an option value was syntactically invalid.</summary>
    SetoptOptionSyntax = 49,

    /// <summary><c>CURLE_OBSOLETE50</c> (50): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete50 = 50,

    /// <summary><c>CURLE_OBSOLETE51</c> (51): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete51 = 51,

    /// <summary><c>CURLE_GOT_NOTHING</c> (52): the server closed the connection without sending anything.</summary>
    GotNothing = 52,

    /// <summary><c>CURLE_SSL_ENGINE_NOTFOUND</c> (53): the requested TLS crypto engine was not found.</summary>
    SslEngineNotFound = 53,

    /// <summary><c>CURLE_SSL_ENGINE_SETFAILED</c> (54): the TLS crypto engine could not be selected.</summary>
    SslEngineSetFailed = 54,

    /// <summary><c>CURLE_SEND_ERROR</c> (55): sending data to the peer failed.</summary>
    SendError = 55,

    /// <summary><c>CURLE_RECV_ERROR</c> (56): receiving data from the peer failed.</summary>
    RecvError = 56,

    /// <summary><c>CURLE_OBSOLETE57</c> (57): unused; upstream keeps the number so it is never reused.</summary>
    Obsolete57 = 57,

    /// <summary><c>CURLE_SSL_CERTPROBLEM</c> (58): the local client certificate could not be used.</summary>
    SslCertProblem = 58,

    /// <summary><c>CURLE_SSL_CIPHER</c> (59): no cipher could be agreed with the peer.</summary>
    SslCipher = 59,

    /// <summary><c>CURLE_PEER_FAILED_VERIFICATION</c> (60): the peer certificate could not be verified.</summary>
    PeerFailedVerification = 60,

    /// <summary><c>CURLE_BAD_CONTENT_ENCODING</c> (61): the response used a content encoding that could not be decoded.</summary>
    BadContentEncoding = 61,

    /// <summary><c>CURLE_OBSOLETE62</c> (62): unused since 7.82.0; upstream keeps the number so it is never reused.</summary>
    Obsolete62 = 62,

    /// <summary><c>CURLE_FILESIZE_EXCEEDED</c> (63): the transfer exceeded the maximum size allowed.</summary>
    FilesizeExceeded = 63,

    /// <summary><c>CURLE_USE_SSL_FAILED</c> (64): the requested TLS upgrade was refused.</summary>
    UseSslFailed = 64,

    /// <summary><c>CURLE_SEND_FAIL_REWIND</c> (65): the upload stream could not be rewound to retry.</summary>
    SendFailRewind = 65,

    /// <summary><c>CURLE_SSL_ENGINE_INITFAILED</c> (66): the TLS crypto engine failed to initialise.</summary>
    SslEngineInitFailed = 66,

    /// <summary><c>CURLE_LOGIN_DENIED</c> (67): the credentials were rejected.</summary>
    LoginDenied = 67,

    /// <summary><c>CURLE_TFTP_NOTFOUND</c> (68): the TFTP server reported the file was not found.</summary>
    TftpNotFound = 68,

    /// <summary><c>CURLE_TFTP_PERM</c> (69): the TFTP server reported a permission problem.</summary>
    TftpPerm = 69,

    /// <summary><c>CURLE_REMOTE_DISK_FULL</c> (70): the remote peer ran out of storage.</summary>
    RemoteDiskFull = 70,

    /// <summary><c>CURLE_TFTP_ILLEGAL</c> (71): the TFTP server rejected the operation as illegal.</summary>
    TftpIllegal = 71,

    /// <summary><c>CURLE_TFTP_UNKNOWNID</c> (72): the TFTP transfer identifier was unknown.</summary>
    TftpUnknownId = 72,

    /// <summary><c>CURLE_REMOTE_FILE_EXISTS</c> (73): the remote file already exists.</summary>
    RemoteFileExists = 73,

    /// <summary><c>CURLE_TFTP_NOSUCHUSER</c> (74): the TFTP user was unknown.</summary>
    TftpNoSuchUser = 74,

    /// <summary><c>CURLE_OBSOLETE75</c> (75): unused since 7.82.0; upstream keeps the number so it is never reused.</summary>
    Obsolete75 = 75,

    /// <summary><c>CURLE_OBSOLETE76</c> (76): unused since 7.82.0; upstream keeps the number so it is never reused.</summary>
    Obsolete76 = 76,

    /// <summary><c>CURLE_SSL_CACERT_BADFILE</c> (77): the certificate authority bundle could not be read.</summary>
    SslCacertBadfile = 77,

    /// <summary><c>CURLE_REMOTE_FILE_NOT_FOUND</c> (78): the remote resource does not exist.</summary>
    RemoteFileNotFound = 78,

    /// <summary><c>CURLE_SSH</c> (79): an unspecified SSH layer error occurred.</summary>
    Ssh = 79,

    /// <summary><c>CURLE_SSL_SHUTDOWN_FAILED</c> (80): the TLS shutdown exchange failed.</summary>
    SslShutdownFailed = 80,

    /// <summary><c>CURLE_AGAIN</c> (81): the socket is not ready; retry the operation.</summary>
    Again = 81,

    /// <summary><c>CURLE_SSL_CRL_BADFILE</c> (82): the certificate revocation list could not be read.</summary>
    SslCrlBadfile = 82,

    /// <summary><c>CURLE_SSL_ISSUER_ERROR</c> (83): the certificate issuer check failed.</summary>
    SslIssuerError = 83,

    /// <summary><c>CURLE_FTP_PRET_FAILED</c> (84): the FTP PRET command failed.</summary>
    FtpPretFailed = 84,

    /// <summary><c>CURLE_RTSP_CSEQ_ERROR</c> (85): an RTSP CSeq number did not match what was expected.</summary>
    RtspCseqError = 85,

    /// <summary><c>CURLE_RTSP_SESSION_ERROR</c> (86): an RTSP session identifier did not match.</summary>
    RtspSessionError = 86,

    /// <summary><c>CURLE_FTP_BAD_FILE_LIST</c> (87): an FTP file list could not be parsed.</summary>
    FtpBadFileList = 87,

    /// <summary><c>CURLE_CHUNK_FAILED</c> (88): a chunked transfer callback reported an error.</summary>
    ChunkFailed = 88,

    /// <summary><c>CURLE_NO_CONNECTION_AVAILABLE</c> (89): no connection was available from the pool.</summary>
    NoConnectionAvailable = 89,

    /// <summary><c>CURLE_SSL_PINNEDPUBKEYNOTMATCH</c> (90): the peer public key did not match the pinned value.</summary>
    SslPinnedPubKeyNotMatch = 90,

    /// <summary><c>CURLE_SSL_INVALIDCERTSTATUS</c> (91): the certificate status reported by the server was invalid.</summary>
    SslInvalidCertStatus = 91,

    /// <summary><c>CURLE_HTTP2_STREAM</c> (92): an error occurred on an HTTP/2 stream.</summary>
    Http2Stream = 92,

    /// <summary><c>CURLE_RECURSIVE_API_CALL</c> (93): an API function was called from inside a callback.</summary>
    RecursiveApiCall = 93,

    /// <summary><c>CURLE_AUTH_ERROR</c> (94): an authentication function returned an error.</summary>
    AuthError = 94,

    /// <summary><c>CURLE_HTTP3</c> (95): an HTTP/3 layer error occurred.</summary>
    Http3 = 95,

    /// <summary><c>CURLE_QUIC_CONNECT_ERROR</c> (96): the QUIC connection failed.</summary>
    QuicConnectError = 96,

    /// <summary><c>CURLE_PROXY</c> (97): the proxy handshake failed.</summary>
    Proxy = 97,

    /// <summary><c>CURLE_SSL_CLIENTCERT</c> (98): the client certificate was required but unusable.</summary>
    SslClientCert = 98,

    /// <summary><c>CURLE_UNRECOVERABLE_POLL</c> (99): polling the underlying socket failed unrecoverably.</summary>
    UnrecoverablePoll = 99,

    /// <summary><c>CURLE_TOO_LARGE</c> (100): a value exceeded the maximum size allowed.</summary>
    TooLarge = 100,

    /// <summary><c>CURLE_ECH_REQUIRED</c> (101): Encrypted Client Hello was required but not used.</summary>
    EchRequired = 101,
}
