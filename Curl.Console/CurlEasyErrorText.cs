using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The text libcurl's <c>curl_easy_strerror</c> gives each exit code, read from curl 8.21.0's
/// <c>libcurl-4.dll</c> (mingw, Schannel) on 2026-09-27 (BL-440 Notes). The tool prints it when a
/// transfer ends with a code but no message of its own, as when a <c>-T</c> glob match that cannot
/// be opened reports the transfer before it.
/// </summary>
internal static class CurlEasyErrorText
{
    /// <summary>The text for a code libcurl does not name.</summary>
    public const string UnknownError = "Unknown error";

    private static readonly Dictionary<CurlExitCode, string> Texts = new()
    {
        [CurlExitCode.Ok] = "No error",
        [CurlExitCode.UnsupportedProtocol] = "Unsupported protocol",
        [CurlExitCode.FailedInit] = "Failed initialization",
        [CurlExitCode.UrlMalformat] = "URL using bad/illegal format or missing URL",
        [CurlExitCode.NotBuiltIn] = "A requested feature, protocol or option was not found built-in in this libcurl due to a build-time decision.",
        [CurlExitCode.CouldntResolveProxy] = "Could not resolve proxy name",
        [CurlExitCode.CouldntResolveHost] = "Could not resolve hostname",
        [CurlExitCode.CouldntConnect] = "Could not connect to server",
        [CurlExitCode.WeirdServerReply] = "Weird server reply",
        [CurlExitCode.RemoteAccessDenied] = "Access denied to remote resource",
        [CurlExitCode.FtpAcceptFailed] = "FTP: The server failed to connect to data port",
        [CurlExitCode.FtpWeirdPassReply] = "FTP: unknown PASS reply",
        [CurlExitCode.FtpAcceptTimeout] = "FTP: Accepting server connect has timed out",
        [CurlExitCode.FtpWeirdPasvReply] = "FTP: unknown PASV reply",
        [CurlExitCode.FtpWeird227Format] = "FTP: unknown 227 response format",
        [CurlExitCode.FtpCantGetHost] = "FTP: cannot figure out the host in the PASV response",
        [CurlExitCode.Http2] = "Error in the HTTP2 framing layer",
        [CurlExitCode.FtpCouldntSetType] = "FTP: could not set file type",
        [CurlExitCode.PartialFile] = "Transferred a partial file",
        [CurlExitCode.FtpCouldntRetrFile] = "FTP: could not retrieve (RETR failed) the specified file",
        [CurlExitCode.QuoteError] = "Quote command returned error",
        [CurlExitCode.HttpReturnedError] = "HTTP response code said error",
        [CurlExitCode.WriteError] = "Failed writing received data to disk/application",
        [CurlExitCode.UploadFailed] = "Upload failed (at start/before it took off)",
        [CurlExitCode.ReadError] = "Failed to open/read local data from file/application",
        [CurlExitCode.OutOfMemory] = "Out of memory",
        [CurlExitCode.OperationTimedOut] = "Timeout was reached",
        [CurlExitCode.FtpPortFailed] = "FTP: command PORT failed",
        [CurlExitCode.FtpCouldntUseRest] = "FTP: command REST failed",
        [CurlExitCode.RangeError] = "Requested range was not delivered by the server",
        [CurlExitCode.SslConnectError] = "SSL connect error",
        [CurlExitCode.BadDownloadResume] = "Could not resume download",
        [CurlExitCode.FileCouldntReadFile] = "Could not read a file:// file",
        [CurlExitCode.LdapCannotBind] = "LDAP: cannot bind",
        [CurlExitCode.LdapSearchFailed] = "LDAP: search failed",
        [CurlExitCode.AbortedByCallback] = "Operation was aborted by an application callback",
        [CurlExitCode.BadFunctionArgument] = "A libcurl function was given a bad argument",
        [CurlExitCode.InterfaceFailed] = "Failed binding local connection end",
        [CurlExitCode.TooManyRedirects] = "Number of redirects hit maximum amount",
        [CurlExitCode.UnknownOption] = "An unknown option was passed in to libcurl",
        [CurlExitCode.SetoptOptionSyntax] = "Malformed option provided in a setopt",
        [CurlExitCode.GotNothing] = "Server returned nothing (no headers, no data)",
        [CurlExitCode.SslEngineNotFound] = "SSL crypto engine not found",
        [CurlExitCode.SslEngineSetFailed] = "Can not set SSL crypto engine as default",
        [CurlExitCode.SendError] = "Failed sending data to the peer",
        [CurlExitCode.RecvError] = "Failure when receiving data from the peer",
        [CurlExitCode.SslCertProblem] = "Problem with the local SSL certificate",
        [CurlExitCode.SslCipher] = "Could not use specified SSL cipher",
        [CurlExitCode.PeerFailedVerification] = "SSL peer certificate or SSH remote key was not OK",
        [CurlExitCode.BadContentEncoding] = "Unrecognized or bad HTTP Content or Transfer-Encoding",
        [CurlExitCode.FilesizeExceeded] = "Maximum file size exceeded",
        [CurlExitCode.UseSslFailed] = "Requested SSL level failed",
        [CurlExitCode.SendFailRewind] = "Send failed since rewinding of the data stream failed",
        [CurlExitCode.SslEngineInitFailed] = "Failed to initialize SSL crypto engine",
        [CurlExitCode.LoginDenied] = "Login denied",
        [CurlExitCode.TftpNotFound] = "TFTP: File Not Found",
        [CurlExitCode.TftpPerm] = "TFTP: Access Violation",
        [CurlExitCode.RemoteDiskFull] = "Disk full or allocation exceeded",
        [CurlExitCode.TftpIllegal] = "TFTP: Illegal operation",
        [CurlExitCode.TftpUnknownId] = "TFTP: Unknown transfer ID",
        [CurlExitCode.RemoteFileExists] = "Remote file already exists",
        [CurlExitCode.TftpNoSuchUser] = "TFTP: No such user",
        [CurlExitCode.SslCacertBadfile] = "Problem with the SSL CA cert (path? access rights?)",
        [CurlExitCode.RemoteFileNotFound] = "Remote file not found",
        [CurlExitCode.Ssh] = "Error in the SSH layer",
        [CurlExitCode.SslShutdownFailed] = "Failed to shut down the SSL connection",
        [CurlExitCode.Again] = "Socket not ready for send/recv",
        [CurlExitCode.SslCrlBadfile] = "Failed to load CRL file (path? access rights?, format?)",
        [CurlExitCode.SslIssuerError] = "Issuer check against peer certificate failed",
        [CurlExitCode.FtpPretFailed] = "FTP: The server did not accept the PRET command.",
        [CurlExitCode.RtspCseqError] = "RTSP CSeq mismatch or invalid CSeq",
        [CurlExitCode.RtspSessionError] = "RTSP session error",
        [CurlExitCode.FtpBadFileList] = "Unable to parse FTP file list",
        [CurlExitCode.ChunkFailed] = "Chunk callback failed",
        [CurlExitCode.NoConnectionAvailable] = "The max connection limit is reached",
        [CurlExitCode.SslPinnedPubKeyNotMatch] = "SSL public key does not match pinned public key",
        [CurlExitCode.SslInvalidCertStatus] = "SSL server certificate status verification FAILED",
        [CurlExitCode.Http2Stream] = "Stream error in the HTTP/2 framing layer",
        [CurlExitCode.RecursiveApiCall] = "API function called from within callback",
        [CurlExitCode.AuthError] = "An authentication function returned an error",
        [CurlExitCode.Http3] = "HTTP/3 error",
        [CurlExitCode.QuicConnectError] = "QUIC connection error",
        [CurlExitCode.Proxy] = "proxy handshake error",
        [CurlExitCode.SslClientCert] = "SSL Client Certificate required",
        [CurlExitCode.UnrecoverablePoll] = "Unrecoverable error in select/poll",
        [CurlExitCode.TooLarge] = "A value or data field grew larger than allowed",
        [CurlExitCode.EchRequired] = "ECH attempted but failed",
    };

    /// <summary>
    /// The text <c>curl_easy_strerror</c> gives <paramref name="exitCode" />.
    /// </summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>libcurl's text for the code, or <see cref="UnknownError" /> for a code it does not name.</returns>
    public static string Of(CurlExitCode exitCode) =>
        Texts.TryGetValue(exitCode, out string? text) ? text : UnknownError;
}
