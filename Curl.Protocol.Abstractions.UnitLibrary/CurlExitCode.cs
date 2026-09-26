namespace Curl.Protocol.Abstractions;

/// <summary>
/// The process exit codes curl reports, mirroring libcurl's <c>CURLE_</c> values.
/// </summary>
/// <remarks>
/// <para>
/// Drop-in compatibility means returning the same number in the same circumstance as
/// upstream, so these values are fixed by curl and are not ours to choose. Taken from
/// <see href="https://curl.se/libcurl/c/libcurl-errors.html" />, current as of
/// curl 8.21.0.
/// </para>
/// <para>
/// The numbering has gaps: curl retired codes such as 20, 24 and 29 and never reuses
/// a number. Do not fill them in.
/// </para>
/// </remarks>
public enum CurlExitCode
{
    /// <summary>The transfer succeeded.</summary>
    /// <remarks>curl reports this as 0.</remarks>
    Ok = 0,

    /// <summary>The URL used a scheme this build does not support.</summary>
    /// <remarks>curl reports this as 1.</remarks>
    UnsupportedProtocol = 1,

    /// <summary>Initialisation failed.</summary>
    /// <remarks>curl reports this as 2.</remarks>
    FailedInit = 2,

    /// <summary>The URL was not formatted correctly.</summary>
    /// <remarks>curl reports this as 3.</remarks>
    UrlMalformat = 3,

    /// <summary>A requested feature was not compiled in.</summary>
    /// <remarks>curl reports this as 4.</remarks>
    NotBuiltIn = 4,

    /// <summary>The proxy host could not be resolved.</summary>
    /// <remarks>curl reports this as 5.</remarks>
    CouldntResolveProxy = 5,

    /// <summary>The remote host could not be resolved.</summary>
    /// <remarks>curl reports this as 6.</remarks>
    CouldntResolveHost = 6,

    /// <summary>The connection to the host or proxy failed.</summary>
    /// <remarks>curl reports this as 7.</remarks>
    CouldntConnect = 7,

    /// <summary>The server sent a reply this implementation could not parse.</summary>
    /// <remarks>curl reports this as 8.</remarks>
    WeirdServerReply = 8,

    /// <summary>Access to the remote resource was denied.</summary>
    /// <remarks>curl reports this as 9.</remarks>
    RemoteAccessDenied = 9,

    /// <summary>The server rejected an FTP data connection.</summary>
    /// <remarks>curl reports this as 10.</remarks>
    FtpAcceptFailed = 10,

    /// <summary>The FTP server sent an unexpected reply to PASS.</summary>
    /// <remarks>curl reports this as 11.</remarks>
    FtpWeirdPassReply = 11,

    /// <summary>Timed out waiting for an FTP data connection.</summary>
    /// <remarks>curl reports this as 12.</remarks>
    FtpAcceptTimeout = 12,

    /// <summary>The FTP server sent an unexpected reply to PASV.</summary>
    /// <remarks>curl reports this as 13.</remarks>
    FtpWeirdPasvReply = 13,

    /// <summary>An FTP 227 reply could not be parsed.</summary>
    /// <remarks>curl reports this as 14.</remarks>
    FtpWeird227Format = 14,

    /// <summary>The host named in an FTP 227 reply could not be used.</summary>
    /// <remarks>curl reports this as 15.</remarks>
    FtpCantGetHost = 15,

    /// <summary>An HTTP/2 framing or protocol error occurred.</summary>
    /// <remarks>curl reports this as 16.</remarks>
    Http2 = 16,

    /// <summary>The FTP transfer type could not be set.</summary>
    /// <remarks>curl reports this as 17.</remarks>
    FtpCouldntSetType = 17,

    /// <summary>The transfer ended with fewer bytes than announced.</summary>
    /// <remarks>curl reports this as 18.</remarks>
    PartialFile = 18,

    /// <summary>The FTP server could not send the requested file.</summary>
    /// <remarks>curl reports this as 19.</remarks>
    FtpCouldntRetrFile = 19,

    /// <summary>A quote command returned an error.</summary>
    /// <remarks>curl reports this as 21.</remarks>
    QuoteError = 21,

    /// <summary>The server returned an error status and --fail was in effect.</summary>
    /// <remarks>curl reports this as 22.</remarks>
    HttpReturnedError = 22,

    /// <summary>Writing received data to its destination failed.</summary>
    /// <remarks>curl reports this as 23.</remarks>
    WriteError = 23,

    /// <summary>The upload was rejected.</summary>
    /// <remarks>curl reports this as 25.</remarks>
    UploadFailed = 25,

    /// <summary>Reading the local file to upload failed.</summary>
    /// <remarks>curl reports this as 26.</remarks>
    ReadError = 26,

    /// <summary>A memory allocation failed.</summary>
    /// <remarks>curl reports this as 27.</remarks>
    OutOfMemory = 27,

    /// <summary>The operation exceeded its timeout.</summary>
    /// <remarks>curl reports this as 28.</remarks>
    OperationTimedOut = 28,

    /// <summary>The FTP PORT command failed.</summary>
    /// <remarks>curl reports this as 30.</remarks>
    FtpPortFailed = 30,

    /// <summary>The FTP REST command failed, so the transfer cannot resume.</summary>
    /// <remarks>curl reports this as 31.</remarks>
    FtpCouldntUseRest = 31,

    /// <summary>The server does not support byte ranges.</summary>
    /// <remarks>curl reports this as 33.</remarks>
    RangeError = 33,

    /// <summary>The TLS handshake failed.</summary>
    /// <remarks>curl reports this as 35.</remarks>
    SslConnectError = 35,

    /// <summary>The download could not be resumed from the requested offset.</summary>
    /// <remarks>curl reports this as 36.</remarks>
    BadDownloadResume = 36,

    /// <summary>The local file named by a file:// URL could not be read.</summary>
    /// <remarks>curl reports this as 37.</remarks>
    FileCouldntReadFile = 37,

    /// <summary>The LDAP bind operation failed.</summary>
    /// <remarks>curl reports this as 38.</remarks>
    LdapCannotBind = 38,

    /// <summary>The LDAP search failed.</summary>
    /// <remarks>curl reports this as 39.</remarks>
    LdapSearchFailed = 39,

    /// <summary>A callback aborted the transfer.</summary>
    /// <remarks>curl reports this as 42.</remarks>
    AbortedByCallback = 42,

    /// <summary>An argument was rejected as invalid.</summary>
    /// <remarks>curl reports this as 43.</remarks>
    BadFunctionArgument = 43,

    /// <summary>The requested outgoing interface could not be used.</summary>
    /// <remarks>curl reports this as 45.</remarks>
    InterfaceFailed = 45,

    /// <summary>The redirect limit was reached.</summary>
    /// <remarks>curl reports this as 47.</remarks>
    TooManyRedirects = 47,

    /// <summary>An option was not recognised.</summary>
    /// <remarks>curl reports this as 48.</remarks>
    UnknownOption = 48,

    /// <summary>An option value was syntactically invalid.</summary>
    /// <remarks>curl reports this as 49.</remarks>
    SetoptOptionSyntax = 49,

    /// <summary>The server closed the connection without sending anything.</summary>
    /// <remarks>curl reports this as 52.</remarks>
    GotNothing = 52,

    /// <summary>The requested TLS crypto engine was not found.</summary>
    /// <remarks>curl reports this as 53.</remarks>
    SslEngineNotFound = 53,

    /// <summary>The TLS crypto engine could not be selected.</summary>
    /// <remarks>curl reports this as 54.</remarks>
    SslEngineSetFailed = 54,

    /// <summary>Sending data to the peer failed.</summary>
    /// <remarks>curl reports this as 55.</remarks>
    SendError = 55,

    /// <summary>Receiving data from the peer failed.</summary>
    /// <remarks>curl reports this as 56.</remarks>
    RecvError = 56,

    /// <summary>The local client certificate could not be used.</summary>
    /// <remarks>curl reports this as 58.</remarks>
    SslCertProblem = 58,

    /// <summary>No cipher could be agreed with the peer.</summary>
    /// <remarks>curl reports this as 59.</remarks>
    SslCipher = 59,

    /// <summary>The peer certificate could not be verified.</summary>
    /// <remarks>curl reports this as 60.</remarks>
    PeerFailedVerification = 60,

    /// <summary>The response used a content encoding that could not be decoded.</summary>
    /// <remarks>curl reports this as 61.</remarks>
    BadContentEncoding = 61,

    /// <summary>The transfer exceeded the maximum size allowed.</summary>
    /// <remarks>curl reports this as 63.</remarks>
    FilesizeExceeded = 63,

    /// <summary>The requested TLS upgrade was refused.</summary>
    /// <remarks>curl reports this as 64.</remarks>
    UseSslFailed = 64,

    /// <summary>The upload stream could not be rewound to retry.</summary>
    /// <remarks>curl reports this as 65.</remarks>
    SendFailRewind = 65,

    /// <summary>The TLS crypto engine failed to initialise.</summary>
    /// <remarks>curl reports this as 66.</remarks>
    SslEngineInitFailed = 66,

    /// <summary>The credentials were rejected.</summary>
    /// <remarks>curl reports this as 67.</remarks>
    LoginDenied = 67,

    /// <summary>The TFTP server reported the file was not found.</summary>
    /// <remarks>curl reports this as 68.</remarks>
    TftpNotFound = 68,

    /// <summary>The TFTP server reported a permission problem.</summary>
    /// <remarks>curl reports this as 69.</remarks>
    TftpPerm = 69,

    /// <summary>The remote peer ran out of storage.</summary>
    /// <remarks>curl reports this as 70.</remarks>
    RemoteDiskFull = 70,

    /// <summary>The TFTP server rejected the operation as illegal.</summary>
    /// <remarks>curl reports this as 71.</remarks>
    TftpIllegal = 71,

    /// <summary>The TFTP transfer identifier was unknown.</summary>
    /// <remarks>curl reports this as 72.</remarks>
    TftpUnknownId = 72,

    /// <summary>The remote file already exists.</summary>
    /// <remarks>curl reports this as 73.</remarks>
    RemoteFileExists = 73,

    /// <summary>The TFTP user was unknown.</summary>
    /// <remarks>curl reports this as 74.</remarks>
    TftpNoSuchUser = 74,

    /// <summary>The certificate authority bundle could not be read.</summary>
    /// <remarks>curl reports this as 77.</remarks>
    SslCacertBadfile = 77,

    /// <summary>The remote resource does not exist.</summary>
    /// <remarks>curl reports this as 78.</remarks>
    RemoteFileNotFound = 78,

    /// <summary>An unspecified SSH layer error occurred.</summary>
    /// <remarks>curl reports this as 79.</remarks>
    Ssh = 79,

    /// <summary>The TLS shutdown exchange failed.</summary>
    /// <remarks>curl reports this as 80.</remarks>
    SslShutdownFailed = 80,

    /// <summary>The socket is not ready; retry the operation.</summary>
    /// <remarks>curl reports this as 81.</remarks>
    Again = 81,

    /// <summary>The certificate revocation list could not be read.</summary>
    /// <remarks>curl reports this as 82.</remarks>
    SslCrlBadfile = 82,

    /// <summary>The certificate issuer check failed.</summary>
    /// <remarks>curl reports this as 83.</remarks>
    SslIssuerError = 83,

    /// <summary>The FTP PRET command failed.</summary>
    /// <remarks>curl reports this as 84.</remarks>
    FtpPretFailed = 84,

    /// <summary>An RTSP CSeq number did not match what was expected.</summary>
    /// <remarks>curl reports this as 85.</remarks>
    RtspCseqError = 85,

    /// <summary>An RTSP session identifier did not match.</summary>
    /// <remarks>curl reports this as 86.</remarks>
    RtspSessionError = 86,

    /// <summary>An FTP file list could not be parsed.</summary>
    /// <remarks>curl reports this as 87.</remarks>
    FtpBadFileList = 87,

    /// <summary>A chunked transfer callback reported an error.</summary>
    /// <remarks>curl reports this as 88.</remarks>
    ChunkFailed = 88,

    /// <summary>No connection was available from the pool.</summary>
    /// <remarks>curl reports this as 89.</remarks>
    NoConnectionAvailable = 89,

    /// <summary>The peer public key did not match the pinned value.</summary>
    /// <remarks>curl reports this as 90.</remarks>
    SslPinnedPubKeyNotMatch = 90,

    /// <summary>The certificate status reported by the server was invalid.</summary>
    /// <remarks>curl reports this as 91.</remarks>
    SslInvalidCertStatus = 91,

    /// <summary>An error occurred on an HTTP/2 stream.</summary>
    /// <remarks>curl reports this as 92.</remarks>
    Http2Stream = 92,

    /// <summary>An API function was called recursively.</summary>
    /// <remarks>curl reports this as 93.</remarks>
    RecursiveApiCall = 93,

    /// <summary>An authentication function returned an error.</summary>
    /// <remarks>curl reports this as 94.</remarks>
    AuthError = 94,

    /// <summary>An HTTP/3 layer error occurred.</summary>
    /// <remarks>curl reports this as 95.</remarks>
    Http3 = 95,

    /// <summary>The QUIC connection failed.</summary>
    /// <remarks>curl reports this as 96.</remarks>
    QuicConnectError = 96,

    /// <summary>The proxy handshake failed.</summary>
    /// <remarks>curl reports this as 97.</remarks>
    Proxy = 97,

    /// <summary>The client certificate was required but unusable.</summary>
    /// <remarks>curl reports this as 98.</remarks>
    SslClientCert = 98,

    /// <summary>Polling the underlying socket failed unrecoverably.</summary>
    /// <remarks>curl reports this as 99.</remarks>
    UnrecoverablePoll = 99,

    /// <summary>A value exceeded the maximum size allowed.</summary>
    /// <remarks>curl reports this as 100.</remarks>
    TooLarge = 100,

    /// <summary>Encrypted Client Hello was required but not used.</summary>
    /// <remarks>curl reports this as 101.</remarks>
    EchRequired = 101,
}
