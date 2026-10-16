using Curl.Api.Codes;

namespace Curl.Api.Errors;

/// <summary>
/// <c>curl_easy_strerror</c>: the text libcurl gives each <see cref="CurlCode"/>, read from curl
/// 8.22.0's <c>libcurl-4.dll</c> (mingw, Schannel) on 2026-10-16 (BL-2498 Notes).
/// </summary>
public static class CurlCodeText
{
    /// <summary>The text libcurl gives a number it does not name, including every <c>CURLE_OBSOLETE*</c> number.</summary>
    public const string UnknownError = "Unknown error";

    // Indexed by the code's number; null marks a CURLE_OBSOLETE* number.
    private static readonly string?[] Texts =
    [
        "No error",
        "Unsupported protocol",
        "Failed initialization",
        "URL using bad/illegal format or missing URL",
        "A requested feature, protocol or option was not found built-in in this libcurl due to a build-time decision.",
        "Could not resolve proxy name",
        "Could not resolve hostname",
        "Could not connect to server",
        "Weird server reply",
        "Access denied to remote resource",
        "FTP: The server failed to connect to data port",
        "FTP: unknown PASS reply",
        "FTP: Accepting server connect has timed out",
        "FTP: unknown PASV reply",
        "FTP: unknown 227 response format",
        "FTP: cannot figure out the host in the PASV response",
        "Error in the HTTP2 framing layer",
        "FTP: could not set file type",
        "Transferred a partial file",
        "FTP: could not retrieve (RETR failed) the specified file",
        null,
        "Quote command returned error",
        "HTTP response code said error",
        "Failed writing received data to disk/application",
        null,
        "Upload failed (at start/before it took off)",
        "Failed to open/read local data from file/application",
        "Out of memory",
        "Timeout was reached",
        null,
        "FTP: command PORT failed",
        "FTP: command REST failed",
        null,
        "Requested range was not delivered by the server",
        null,
        "SSL connect error",
        "Could not resume download",
        "Could not read a file:// file",
        "LDAP: cannot bind",
        "LDAP: search failed",
        null,
        null,
        "Operation was aborted by an application callback",
        "A libcurl function was given a bad argument",
        null,
        "Failed binding local connection end",
        null,
        "Number of redirects hit maximum amount",
        "An unknown option was passed in to libcurl",
        "Malformed option provided in a setopt",
        null,
        null,
        "Server returned nothing (no headers, no data)",
        "SSL crypto engine not found",
        "Can not set SSL crypto engine as default",
        "Failed sending data to the peer",
        "Failure when receiving data from the peer",
        null,
        "Problem with the local SSL certificate",
        "Could not use specified SSL cipher",
        "SSL peer certificate or SSH remote key was not OK",
        "Unrecognized or bad HTTP Content or Transfer-Encoding",
        null,
        "Maximum file size exceeded",
        "Requested SSL level failed",
        "Send failed since rewinding of the data stream failed",
        "Failed to initialize SSL crypto engine",
        "Login denied",
        "TFTP: File Not Found",
        "TFTP: Access Violation",
        "Disk full or allocation exceeded",
        "TFTP: Illegal operation",
        "TFTP: Unknown transfer ID",
        "Remote file already exists",
        "TFTP: No such user",
        null,
        null,
        "Problem with the SSL CA cert (path? access rights?)",
        "Remote file not found",
        "Error in the SSH layer",
        "Failed to shut down the SSL connection",
        "Socket not ready for send/recv",
        "Failed to load CRL file (path? access rights?, format?)",
        "Issuer check against peer certificate failed",
        "FTP: The server did not accept the PRET command.",
        "RTSP CSeq mismatch or invalid CSeq",
        "RTSP session error",
        "Unable to parse FTP file list",
        "Chunk callback failed",
        "The max connection limit is reached",
        "SSL public key does not match pinned public key",
        "SSL server certificate status verification FAILED",
        "Stream error in the HTTP/2 framing layer",
        "API function called from within callback",
        "An authentication function returned an error",
        "HTTP/3 error",
        "QUIC connection error",
        "proxy handshake error",
        "SSL Client Certificate required",
        "Unrecoverable error in select/poll",
        "A value or data field grew larger than allowed",
        "ECH attempted but failed",
    ];

    /// <summary>The text <c>curl_easy_strerror</c> gives <paramref name="code"/>.</summary>
    /// <param name="code">The libcurl result.</param>
    /// <returns>libcurl's text for the code, or <see cref="UnknownError"/> for a number it does not name.</returns>
    public static string Of(CurlCode code)
    {
        int number = (int)code;
        return (uint)number < (uint)Texts.Length ? Texts[number] ?? UnknownError : UnknownError;
    }
}
