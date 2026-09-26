namespace Curl.Networking;

/// <summary>
/// The one place the message for a failed TLS handshake is written, so the text curl
/// prints for exit 35, exit 60 and exit 77 can be settled (BL-064) by changing only this type.
/// </summary>
internal static class TlsFailureMessages
{
    /// <summary>
    /// The message for exit 60: the server certificate or its host name did not verify.
    /// </summary>
    /// <param name="targetHost">The host the certificate was checked against.</param>
    /// <returns>The message curl prints.</returns>
    public static string PeerFailedVerification(string targetHost) =>
        $"SSL certificate problem: the certificate presented by {targetHost} could not be verified";

    /// <summary>
    /// The message for exit 35: the handshake failed for a reason other than verification.
    /// </summary>
    /// <param name="exception">What the handshake threw.</param>
    /// <returns>The message curl prints.</returns>
    public static string SslConnectError(Exception exception) =>
        $"TLS connect error: {exception.Message}";

    /// <summary>
    /// The message for exit 77: the <c>--cacert</c> file could not be read as PEM
    /// certificates.
    /// </summary>
    /// <param name="caCertificateFile">The path given to <c>--cacert</c>.</param>
    /// <param name="exception">What reading or parsing the file threw.</param>
    /// <returns>The message curl prints.</returns>
    public static string CaCertificateFileUnusable(string caCertificateFile, Exception exception) =>
        $"failed to read CA file '{caCertificateFile}': {exception.Message}";
}
