using System.Collections.Frozen;
using System.ComponentModel;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The one place the message for a failed TLS handshake is written: the text curl prints
/// after <c>curl: (NN) </c> for exit 35, exit 43, exit 58, exit 60 and exit 77, in the two builds ADR-0009
/// reproduces, the Schannel build of curl on Windows and the OpenSSL build elsewhere.
/// </summary>
/// <remarks>
/// Every exit 60 is followed, in both builds, by the <c>More details here</c> help block;
/// curl's command-line tool prints it, not libcurl, so it is not part of these messages.
/// </remarks>
internal static class TlsFailureMessages
{
    private const string SchannelUntrustedRoot =
        "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.";

    private const string SchannelCaCertificateFileAnchorsNothing =
        "schannel: the certificate or certificate chain is based on an untrusted root";

    private const string SchannelNameMismatch = "schannel: CertFindExtension() returned no extension.";

    // What curl's Schannel build reports when the connection closes during the handshake,
    // where there is no security status to name.
    private const string SchannelHandshakeNotReceived = "schannel: failed to receive handshake, SSL/TLS connection failed";

    private const string OpenSslErrorStringPrefix = "error:";

    // The SEC_E_* names curl's Schannel build prints for a security status; any other is
    // "Unknown error", as curl's own table falls back to.
    private static readonly FrozenDictionary<int, string> SecurityStatusNames = new Dictionary<int, string>
    {
        [unchecked((int)0x80090300)] = "SEC_E_INSUFFICIENT_MEMORY",
        [unchecked((int)0x80090301)] = "SEC_E_INVALID_HANDLE",
        [unchecked((int)0x80090302)] = "SEC_E_UNSUPPORTED_FUNCTION",
        [unchecked((int)0x80090303)] = "SEC_E_TARGET_UNKNOWN",
        [unchecked((int)0x80090304)] = "SEC_E_INTERNAL_ERROR",
        [unchecked((int)0x80090308)] = "SEC_E_INVALID_TOKEN",
        [unchecked((int)0x8009030C)] = "SEC_E_LOGON_DENIED",
        [unchecked((int)0x8009030E)] = "SEC_E_NO_CREDENTIALS",
        [unchecked((int)0x8009030F)] = "SEC_E_MESSAGE_ALTERED",
        [unchecked((int)0x80090318)] = "SEC_E_INCOMPLETE_MESSAGE",
        [unchecked((int)0x80090321)] = "SEC_E_BUFFER_TOO_SMALL",
        [unchecked((int)0x80090322)] = "SEC_E_WRONG_PRINCIPAL",
        [unchecked((int)0x80090325)] = "SEC_E_UNTRUSTED_ROOT",
        [unchecked((int)0x80090326)] = "SEC_E_ILLEGAL_MESSAGE",
        [unchecked((int)0x80090327)] = "SEC_E_CERT_UNKNOWN",
        [unchecked((int)0x80090328)] = "SEC_E_CERT_EXPIRED",
        [unchecked((int)0x80090330)] = "SEC_E_DECRYPT_FAILURE",
        [unchecked((int)0x80090331)] = "SEC_E_ALGORITHM_MISMATCH",
    }.ToFrozenDictionary();

    /// <summary>
    /// The Schannel build's message for exit 60: the server certificate or its host name
    /// did not verify.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="hasCaCertificateFile">
    /// <see langword="true" /> when <c>--cacert</c> replaced the system store.
    /// </param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelPeerFailedVerification(SslPolicyErrors errors, bool hasCaCertificateFile)
    {
        if (errors == SslPolicyErrors.RemoteCertificateNameMismatch)
        {
            return SchannelNameMismatch;
        }

        return hasCaCertificateFile ? SchannelCaCertificateFileAnchorsNothing : SchannelUntrustedRoot;
    }

    /// <summary>
    /// The OpenSSL build's message for exit 60: the chain did not verify, reported as
    /// OpenSSL's verify result, or it did and the host name did not match.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chain">The chain built for the server certificate, if one was presented.</param>
    /// <param name="targetHost">The host the certificate was checked against.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslPeerFailedVerification(SslPolicyErrors errors, X509Chain? chain, string targetHost)
    {
        if (errors == SslPolicyErrors.RemoteCertificateNameMismatch)
        {
            var subjectName = chain!.ChainElements[0].Certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            return $"SSL: certificate subject name '{subjectName}' does not match target hostname '{targetHost}'";
        }

        return $"SSL certificate OpenSSL verify result: {OpenSslVerifyError(chain)}";
    }

    /// <summary>
    /// The Schannel build's message for exit 35: the handshake failed for a reason other
    /// than verification, named by the security status Schannel returned.
    /// </summary>
    /// <param name="exception">What the handshake threw.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelSslConnectError(Exception exception)
    {
        var securityStatus = FindInnerException<Win32Exception>(exception);
        if (securityStatus is null)
        {
            return SchannelHandshakeNotReceived;
        }

        var code = securityStatus.NativeErrorCode;
        var name = SecurityStatusNames.GetValueOrDefault(code, "Unknown error");
        return $"schannel: next InitializeSecurityContext failed: {name} (0x{code:X8}) - {securityStatus.Message}";
    }

    /// <summary>
    /// The OpenSSL build's message for exit 35: the handshake failed for a reason other
    /// than verification, named by OpenSSL's error string.
    /// </summary>
    /// <param name="exception">What the handshake threw.</param>
    /// <returns>
    /// The message curl prints; when no OpenSSL error string is in the exception, the
    /// innermost exception's message stands in for it.
    /// </returns>
    public static string OpenSslSslConnectError(Exception exception)
    {
        var innermost = exception;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.StartsWith(OpenSslErrorStringPrefix, StringComparison.Ordinal))
            {
                return $"TLS connect error: {current.Message}";
            }

            innermost = current;
        }

        return $"TLS connect error: {innermost.Message}";
    }

    /// <summary>
    /// The Schannel build's message for exit 77: the <c>--cacert</c> file could not be
    /// opened.
    /// </summary>
    /// <param name="caCertificateFile">The path given to <c>--cacert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelCaCertificateFileUnusable(string caCertificateFile) =>
        $"schannel: failed to open CA file '{caCertificateFile}'";

    /// <summary>
    /// The OpenSSL build's message for exit 77: the <c>--cacert</c> file could not be
    /// read, or holds no certificate, or holds one that does not parse.
    /// </summary>
    /// <param name="caCertificateFile">The path given to <c>--cacert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslCaCertificateFileUnusable(string caCertificateFile) =>
        $"error adding trust anchors from file: {caCertificateFile}";

    /// <summary>
    /// The Schannel build's message for exit 58 when the <c>--cert</c> file cannot be opened:
    /// it is missing, or is a directory.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelClientCertificateNotFound(string clientCertificateFile) =>
        $"schannel: Failed to get certificate location or file for {clientCertificateFile}";

    /// <summary>
    /// The Schannel build's message for exit 58 when the <c>--cert</c> file is empty.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelClientCertificateNotRead(string clientCertificateFile) =>
        $"schannel: Failed to read cert file {clientCertificateFile}";

    /// <summary>
    /// The Schannel build's message for exit 58 when the <c>--cert</c> file is not PKCS#12,
    /// such as a PEM or DER certificate: <c>CRYPT_E_BAD_ENCODE</c>.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelClientCertificateNotImported(string clientCertificateFile) =>
        $"schannel: Failed to import cert file {clientCertificateFile}, last error is 0x80092002";

    /// <summary>
    /// The Schannel build's message for exit 58 when the PKCS#12 <c>--cert</c> file does not
    /// open with the passphrase given, or with none.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelClientCertificatePasswordBad(string clientCertificateFile) =>
        $"schannel: Failed to import cert file {clientCertificateFile}, password is bad";

    /// <summary>OpenSSL's error string for a file that does not exist.</summary>
    public const string OpenSslNoSuchFile = "error:80000002:system library::No such file or directory";

    /// <summary>
    /// OpenSSL's error string for a file with no PEM block in it, which is also what a
    /// directory, an empty file and a DER or PKCS#12 file give.
    /// </summary>
    public const string OpenSslNoStartLine = "error:0480006C:PEM routines::no start line";

    /// <summary>
    /// The OpenSSL build's message for exit 58: the <c>--cert</c> file did not load as a PEM
    /// certificate.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <param name="openSslError">
    /// <see cref="OpenSslNoSuchFile" /> or <see cref="OpenSslNoStartLine" />.
    /// </param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslClientCertificateNotLoaded(string clientCertificateFile, string openSslError) =>
        $"could not load PEM client certificate from {clientCertificateFile}, OpenSSL error {openSslError}, (no key found, wrong passphrase, or wrong file format?)";

    /// <summary>
    /// The OpenSSL build's message for exit 43: the private key file is missing, holds no
    /// key, holds one that does not match the certificate, or is encrypted and the
    /// passphrase does not open it.
    /// </summary>
    /// <param name="privateKeyFile">The <c>--key</c> file, or the certificate file without one.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslPrivateKeyUnusable(string privateKeyFile) =>
        $"unable to set private key file: '{privateKeyFile}' type PEM";

    // OpenSSL reports a failed trust before a failed validity period.
    private static string OpenSslVerifyError(X509Chain? chain)
    {
        if (chain is null || chain.ChainElements.Count == 0)
        {
            return "unable to get local issuer certificate (20)";
        }

        var status = chain.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, next) => all | next.Status);
        return status == X509ChainStatusFlags.NotTimeValid ? OpenSslValidityError(chain) : OpenSslTrustError(chain);
    }

    private static string OpenSslTrustError(X509Chain chain)
    {
        var elements = chain.ChainElements;
        var last = elements[^1].Certificate;
        if (!last.SubjectName.RawData.AsSpan().SequenceEqual(last.IssuerName.RawData))
        {
            return "unable to get local issuer certificate (20)";
        }

        return elements.Count == 1 ? "self-signed certificate (18)" : "self-signed certificate in certificate chain (19)";
    }

    private static string OpenSslValidityError(X509Chain chain)
    {
        var outOfDate = chain.ChainElements
            .First(element => element.ChainElementStatus.Any(status => status.Status == X509ChainStatusFlags.NotTimeValid))
            .Certificate;
        return outOfDate.NotBefore > chain.ChainPolicy.VerificationTime
            ? "certificate is not yet valid (9)"
            : "certificate has expired (10)";
    }

    private static T? FindInnerException<T>(Exception exception)
        where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is T found)
            {
                return found;
            }
        }

        return null;
    }
}
