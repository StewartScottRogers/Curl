using System.Collections.Frozen;
using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The one place the message for a failed TLS handshake is written: the text curl prints
/// after <c>curl: (NN) </c> for exit 35, exit 43, exit 58, exit 59, exit 60, exit 77, exit 82 and exit 90, in the two builds ADR-0009
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

    // With --cacert curl's Schannel build builds the chain itself and names the first of
    // these trust errors it finds, in this order (BL-150, measured; BL-368).
    private const string SchannelChainNotTimeValid =
        "schannel: this certificate or one of the certificates in the certificate chain is not time valid";

    private const string SchannelChainIncomplete = "schannel: the certificate chain is incomplete";

    private const string SchannelRevocationStatusUnknown = "schannel: the revocation status is unknown";

    // The Schannel build with --cacert checks the name itself (BL-150, measured): an IP
    // literal against a certificate with no subjectAltName extension, an IP literal against
    // one with it (no message of its own, so libcurl's text for exit 60), and a host name.
    private const string SchannelIpAddressWithoutAlternativeNames = "schannel: CertFindExtension() returned no extension.";

    private const string SchannelIpAddressNotAmongAlternativeNames = "SSL peer certificate or SSH remote key was not OK";

    // Without --cacert, Schannel checks the name and says so with its own status.
    private const string SchannelWrongPrincipal =
        "schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.";

    // What curl's Schannel build reports when the connection closes during the handshake,
    // where there is no security status to name.
    private const string SchannelHandshakeNotReceived = "schannel: failed to receive handshake, SSL/TLS connection failed";

    private const string OpenSslErrorStringPrefix = "error:";

    // What the OpenSSL build reports when the server closes mid-handshake (BL-150, measured);
    // .NET sees the end of the stream before OpenSSL does, so no exception carries it.
    private const string OpenSslUnexpectedEof = "error:0A000126:SSL routines::unexpected eof while reading";

    /// <summary>
    /// The Schannel build's exit 56 message for a read that finds the connection ended
    /// without <c>close_notify</c> (BL-819, measured; ADR-0221).
    /// </summary>
    internal const string SchannelMissingCloseNotify = "schannel: server closed abruptly (missing close_notify)";

    /// <summary>
    /// The OpenSSL build's exit 56 message for a read that finds the connection ended
    /// without <c>close_notify</c>: <c>SSL_read</c>'s error string, which curl prefixes
    /// with the OpenSSL version of the reference build, <c>curlimages/curl:8.21.0</c>
    /// (BL-819; ADR-0221).
    /// </summary>
    internal const string OpenSslMissingCloseNotify =
        "OpenSSL SSL_read: OpenSSL/3.5.7: " + OpenSslUnexpectedEof + ", errno 0";

    /// <summary>
    /// The exit 56 message of the build being matched for a read that finds the connection
    /// ended without <c>close_notify</c>.
    /// </summary>
    /// <param name="matchesSchannelBuild"><see langword="true" /> for curl's Schannel build.</param>
    /// <returns><see cref="SchannelMissingCloseNotify" /> or <see cref="OpenSslMissingCloseNotify" />.</returns>
    internal static string MissingCloseNotify(bool matchesSchannelBuild) =>
        matchesSchannelBuild ? SchannelMissingCloseNotify : OpenSslMissingCloseNotify;

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

    // A socket error during the handshake is curl's "Recv failure: " and the error as each
    // build names it: its own Winsock table in the Schannel build, strerror in the OpenSSL
    // build (BL-369, measured; ADR-0088). Any other socket error's own message stands in.
    private static readonly FrozenDictionary<SocketError, string> SchannelSocketErrorTexts = new Dictionary<SocketError, string>
    {
        [SocketError.ConnectionReset] = "Connection was reset",
        [SocketError.ConnectionAborted] = "Connection was aborted",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<SocketError, string> OpenSslSocketErrorTexts = new Dictionary<SocketError, string>
    {
        [SocketError.ConnectionReset] = "Connection reset by peer",
    }.ToFrozenDictionary();

    // OpenSSL's X509_verify_cert_error_string for each --crlfile refusal. 3, 8 and 23 were
    // measured with curl 8.18.0's OpenSSL 3.5.5 build (BL-609); the others are OpenSSL's table.
    private static readonly FrozenDictionary<long, string> OpenSslRevocationListErrorTexts = new Dictionary<long, string>
    {
        [OpenSslVerifyResult.UnableToGetCertificateRevocationList] = "unable to get certificate CRL",
        [OpenSslVerifyResult.CertificateRevocationListSignatureFailure] = "CRL signature failure",
        [OpenSslVerifyResult.CertificateRevocationListNotYetValid] = "CRL is not yet valid",
        [OpenSslVerifyResult.CertificateRevocationListHasExpired] = "CRL has expired",
        [OpenSslVerifyResult.CertificateRevoked] = "certificate revoked",
        [OpenSslVerifyResult.KeyUsageDoesNotIncludeCrlSigning] = "key usage does not include CRL signing",
    }.ToFrozenDictionary();

    // What curl's Schannel build reports when the server sends a fatal alert during the
    // handshake (measured against a handshake_failure alert, ADR-0140).
    private const string SchannelFatalAlertReceived =
        "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.";

    // OpenSSL 3's reason strings for an alert, each its reason code 1000 plus the alert
    // (ssl/ssl_err.c): handshake_failure (ADR-0140), protocol_version (BL-502) and
    // unknown_psk_identity (BL-712, an SRP server that does not know the user) measured.
    private static readonly FrozenDictionary<TlsAlertDescription, string> OpenSslAlertReasons = new Dictionary<TlsAlertDescription, string>
    {
        [TlsAlertDescription.UnexpectedMessage] = "sslv3 alert unexpected message",
        [TlsAlertDescription.BadRecordMac] = "sslv3 alert bad record mac",
        [TlsAlertDescription.RecordOverflow] = "tlsv1 alert record overflow",
        [TlsAlertDescription.HandshakeFailure] = "ssl/tls alert handshake failure",
        [TlsAlertDescription.BadCertificate] = "ssl/tls alert bad certificate",
        [TlsAlertDescription.UnsupportedCertificate] = "sslv3 alert unsupported certificate",
        [TlsAlertDescription.CertificateRevoked] = "sslv3 alert certificate revoked",
        [TlsAlertDescription.CertificateExpired] = "sslv3 alert certificate expired",
        [TlsAlertDescription.CertificateUnknown] = "sslv3 alert certificate unknown",
        [TlsAlertDescription.IllegalParameter] = "sslv3 alert illegal parameter",
        [TlsAlertDescription.UnknownCa] = "tlsv1 alert unknown ca",
        [TlsAlertDescription.AccessDenied] = "tlsv1 alert access denied",
        [TlsAlertDescription.DecodeError] = "tlsv1 alert decode error",
        [TlsAlertDescription.DecryptError] = "tlsv1 alert decrypt error",
        [TlsAlertDescription.ProtocolVersion] = "tlsv1 alert protocol version",
        [TlsAlertDescription.InsufficientSecurity] = "tlsv1 alert insufficient security",
        [TlsAlertDescription.InternalError] = "tlsv1 alert internal error",
        [TlsAlertDescription.InappropriateFallback] = "tlsv1 alert inappropriate fallback",
        [TlsAlertDescription.UserCanceled] = "tlsv1 alert user cancelled",
        [TlsAlertDescription.MissingExtension] = "tlsv13 alert missing extension",
        [TlsAlertDescription.UnsupportedExtension] = "tlsv1 unsupported extension",
        [TlsAlertDescription.UnrecognizedName] = "tlsv1 unrecognized name",
        [TlsAlertDescription.UnknownPskIdentity] = "tlsv1 alert unknown psk identity",
        [TlsAlertDescription.BadCertificateStatusResponse] = "tlsv1 bad certificate status response",
        [TlsAlertDescription.CertificateRequired] = "tlsv13 alert certificate required",
        [TlsAlertDescription.NoApplicationProtocol] = "tlsv1 alert no application protocol",
    }.ToFrozenDictionary();

    /// <summary>
    /// The Schannel build's message for exit 35 when the hand-built client's handshake fails
    /// for a reason other than verification (ADR-0140, "Failures and text"): the server
    /// closing, or any range of only TLS 1.0 and 1.1 (as <see cref="SchannelSslConnectError(Exception, bool)" />
    /// reports it), is <c>failed to receive handshake</c>; an alert either side sent is the
    /// line Schannel's curl prints for a fatal alert.
    /// </summary>
    /// <param name="failure">Why the hand-built handshake failed.</param>
    /// <param name="offersOnlyVersionsBelowTls12"><see langword="true" /> when the ceiling was TLS 1.0 or TLS 1.1.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelHandBuiltHandshakeFailure(TlsHandshakeFailure failure, bool offersOnlyVersionsBelowTls12) =>
        failure.Origin == TlsHandshakeFailureOrigin.TransportClosed || offersOnlyVersionsBelowTls12
            ? SchannelHandshakeNotReceived
            : SchannelFatalAlertReceived;

    /// <summary>
    /// The message for exit 101 when the server did not accept the hand-built client's ECH
    /// offer: the OpenSSL build's <c>ECH required:</c> and OpenSSL's error string for
    /// <c>SSL_R_ECH_REQUIRED</c>, on every platform (measured 2026-10-02 with curl 8.21.0 and
    /// OpenSSL 4.0.0, ADR-0359).
    /// </summary>
    public const string EchRequired = "ECH required: error:0A0001A8:SSL routines::ech required";

    /// <summary>
    /// The <c>-v</c> line written before exit 101 when the server that refused the ECH offer sent
    /// no <c>retry_configs</c> (curl 8.21.0's <c>ossl_trace_ech_retry_configs</c>, measured).
    /// </summary>
    public const string EchNoRetryConfigsLine = "ECH: no retry_configs (rv = 1)";

    /// <summary>
    /// The OpenSSL build's message for exit 35 when the hand-built client's handshake fails
    /// for a reason other than verification: the server closing is OpenSSL's unexpected-EOF
    /// error string, and an alert is OpenSSL's error string for it, reason code 1000 plus the
    /// alert, named as OpenSSL 3 names it, or <c>reason(N)</c> as OpenSSL prints a reason it
    /// has no string for.
    /// </summary>
    /// <param name="failure">Why the hand-built handshake failed.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslHandBuiltHandshakeFailure(TlsHandshakeFailure failure)
    {
        if (failure.Origin == TlsHandshakeFailureOrigin.TransportClosed)
        {
            return $"TLS connect error: {OpenSslUnexpectedEof}";
        }

        var reason = 1000 + (int)failure.Alert;
        var text = OpenSslAlertReasons.GetValueOrDefault(failure.Alert, $"reason({reason})");
        return $"TLS connect error: error:{0x0A000000 | reason:X8}:SSL routines::{text}";
    }

    /// <summary>
    /// The Schannel build's message for exit 60: the server certificate or its host name
    /// did not verify.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chain">The chain built for the server certificate, if one was presented.</param>
    /// <param name="targetHost">The host the certificate was checked against, IPv6 without brackets.</param>
    /// <param name="hasCaCertificateFile">
    /// <see langword="true" /> when <c>--cacert</c> replaced the system store.
    /// </param>
    /// <returns>The message curl prints.</returns>
    /// <remarks>
    /// A chain failure is reported before a name mismatch. With <c>--cacert</c> it names the
    /// first of a certificate out of its validity period, an incomplete chain, an untrusted
    /// root and an unknown revocation status; without it, every chain failure that reaches
    /// exit 60 is an untrusted root (see <see cref="IsSchannelCertificateExpired" /> for the
    /// one that does not). Without <c>--cacert</c> a name mismatch is Schannel's
    /// <c>SEC_E_WRONG_PRINCIPAL</c>; with it, curl's own check names the host, or for an IP
    /// literal whether the certificate has a subjectAltName extension.
    /// </remarks>
    public static string SchannelPeerFailedVerification(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost,
        bool hasCaCertificateFile)
    {
        if (errors != SslPolicyErrors.RemoteCertificateNameMismatch)
        {
            return hasCaCertificateFile ? SchannelCaCertificateFileChainError(chain) : SchannelUntrustedRoot;
        }

        if (!hasCaCertificateFile)
        {
            return SchannelWrongPrincipal;
        }

        if (ToIpAddressFamily(targetHost) is null)
        {
            return $"schannel: CertGetNameString() failed to match connection hostname ({targetHost}) against server certificate names";
        }

        return FindAlternativeNames(chain!.ChainElements[0].Certificate) is null
            ? SchannelIpAddressWithoutAlternativeNames
            : SchannelIpAddressNotAmongAlternativeNames;
    }

    /// <summary>
    /// The Schannel build's message for exit 35 when the system store's check finds the
    /// server certificate out of its validity period, measured against
    /// <c>https://expired.badssl.com/</c>: Schannel fails the handshake itself with
    /// <c>SEC_E_CERT_EXPIRED</c>, which it also returns for a certificate not yet valid.
    /// </summary>
    public const string SchannelCertificateExpired =
        "schannel: next InitializeSecurityContext failed: SEC_E_CERT_EXPIRED (0x80090328) - The received certificate has expired.";

    /// <summary>
    /// Whether the Schannel build, checking against the system store, fails the handshake
    /// with <see cref="SchannelCertificateExpired" /> (exit 35) rather than exit 60: the
    /// chain is trusted and the host name matches, but a certificate in it is out of its
    /// validity period.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chain">The chain built for the server certificate, if one was presented.</param>
    /// <returns><see langword="true" /> when being out of date is the only thing wrong.</returns>
    public static bool IsSchannelCertificateExpired(SslPolicyErrors errors, X509Chain? chain) =>
        errors == SslPolicyErrors.RemoteCertificateChainErrors && ChainStatus(chain) == X509ChainStatusFlags.NotTimeValid;

    /// <summary>
    /// The OpenSSL build's message for exit 60: the host name did not match, or it did and
    /// the chain did not verify, reported as OpenSSL's verify result.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chain">The chain built for the server certificate, if one was presented.</param>
    /// <param name="targetHost">The host the certificate was checked against, IPv6 without brackets.</param>
    /// <returns>The message curl prints.</returns>
    /// <remarks>
    /// curl checks the name first, so a name mismatch is reported even when the chain also
    /// failed (BL-150, measured). A certificate with DNS or IP subjectAltName entries is
    /// matched against those alone, and the message names the kind of target; one without
    /// is matched against its common name.
    /// </remarks>
    public static string OpenSslPeerFailedVerification(SslPolicyErrors errors, X509Chain? chain, string targetHost)
    {
        if (!errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return $"SSL certificate OpenSSL verify result: {OpenSslVerifyError(chain)}";
        }

        var certificate = chain!.ChainElements[0].Certificate;
        var shownHost = ToIpAddressFamily(targetHost) == AddressFamily.InterNetworkV6 ? $"[{targetHost}]" : targetHost;
        if (HasDnsOrIpAlternativeNames(certificate))
        {
            return $"SSL: no alternative certificate subject name matches target {DescribeTarget(targetHost)} '{shownHost}'";
        }

        var subjectName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        return $"SSL: certificate subject name '{subjectName}' does not match target hostname '{shownHost}'";
    }

    /// <summary>
    /// The Schannel build's message for exit 35: the handshake failed for a reason other
    /// than verification, named by the security status Schannel returned, or by the socket
    /// error when the connection failed under it.
    /// </summary>
    /// <param name="exception">What the handshake threw.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelSslConnectError(Exception exception) =>
        SchannelSslConnectError(exception, offersOnlyVersionsBelowTls12: false);

    /// <summary>
    /// The Schannel build's message for exit 35, as <see cref="SchannelSslConnectError(Exception)" />
    /// gives it, except that when the handshake offered only TLS 1.0 or TLS 1.1, a security
    /// status is not named: the message is <c>failed to receive handshake</c>, as curl's
    /// Schannel build reported every such range measured against a TLS 1.2 server
    /// (<c>--tls-max 1.1</c>, <c>--tls-max 1.0</c>, <c>--tlsv1.0 --tls-max 1.1</c>,
    /// <c>--tlsv1.1 --tls-max 1.1</c>, <c>--tlsv1.0 --tls-max 1.0</c>; curl 8.21.0, 2026-09-28,
    /// BL-502), where <see cref="System.Net.Security.SslStream" /> returns
    /// <c>SEC_E_UNSUPPORTED_FUNCTION</c> for a range Windows 11 will not offer and
    /// <c>SEC_E_ILLEGAL_MESSAGE</c> for the server's refusal of one it will.
    /// </summary>
    /// <param name="exception">What the handshake threw.</param>
    /// <param name="offersOnlyVersionsBelowTls12">
    /// <see langword="true" /> when the ceiling was TLS 1.0 or TLS 1.1.
    /// </param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelSslConnectError(Exception exception, bool offersOnlyVersionsBelowTls12)
    {
        if (RecvFailure(exception, SchannelSocketErrorTexts) is { } recvFailure)
        {
            return recvFailure;
        }

        var securityStatus = FindInnerException<Win32Exception>(exception);
        if (securityStatus is null || offersOnlyVersionsBelowTls12)
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
    /// The message curl prints. A socket error, such as the server resetting the connection,
    /// is curl's <c>Recv failure</c> line. Otherwise, when no OpenSSL error string is in the
    /// exception, a bare <see cref="IOException" /> innermost, which is how
    /// <see cref="SslStream" /> reports the server closing mid-handshake, is OpenSSL's
    /// unexpected-EOF error string; any other innermost exception's message stands in for one.
    /// </returns>
    public static string OpenSslSslConnectError(Exception exception)
    {
        if (RecvFailure(exception, OpenSslSocketErrorTexts) is { } recvFailure)
        {
            return recvFailure;
        }

        var innermost = exception;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.StartsWith(OpenSslErrorStringPrefix, StringComparison.Ordinal))
            {
                return $"TLS connect error: {current.Message}";
            }

            innermost = current;
        }

        return innermost.GetType() == typeof(IOException)
            ? $"TLS connect error: {OpenSslUnexpectedEof}"
            : $"TLS connect error: {innermost.Message}";
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
    /// The OpenSSL build's message for exit 82: the <c>--crlfile</c> could not be read, or
    /// holds no PEM certificate revocation list, or one that does not decode (measured with
    /// curl 8.18.0's OpenSSL build for garbage, an empty file, a DER list and a directory, BL-609).
    /// </summary>
    /// <param name="revocationListFile">The path given to <c>--crlfile</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslRevocationListFileUnusable(string revocationListFile) =>
        $"error loading CRL file: {revocationListFile}";

    /// <summary>
    /// The OpenSSL build's message for exit 60 when a <c>--crlfile</c> check refuses the chain,
    /// e.g. <c>SSL certificate OpenSSL verify result: certificate revoked (23)</c> (BL-609).
    /// </summary>
    /// <param name="verifyResult">The refusal's <see cref="OpenSslVerifyResult" /> code.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslRevocationListRefusal(long verifyResult) =>
        $"SSL certificate OpenSSL verify result: {OpenSslRevocationListErrorTexts[verifyResult]} ({verifyResult})";

    /// <summary>
    /// The Schannel build's message for exit 58 when the <c>--cert</c> file cannot be opened:
    /// it is missing, or is a directory.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelClientCertificateNotFound(string clientCertificateFile) =>
        $"schannel: Failed to get certificate location or file for {clientCertificateFile}";

    /// <summary>
    /// The Schannel build's message for exit 58 when the store a <c>--cert</c> store path
    /// names does not open, measured 2026-09-27 for <c>CurrentUser\NOSUCHSTORE\…</c>.
    /// </summary>
    /// <param name="storeLocationFlag">The location's <c>CERT_SYSTEM_STORE_*</c> flag, printed in lowercase hex.</param>
    /// <param name="storeName">The store name as written.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelCertificateStoreNotOpened(int storeLocationFlag, string storeName) =>
        $"schannel: Failed to open cert store {storeLocationFlag:x} {storeName}, last error is 0x00000002";

    /// <summary>
    /// The Schannel build's message for exit 58 when no certificate in the store has the
    /// <c>--cert</c> store path's thumbprint, measured 2026-09-27.
    /// </summary>
    public const string SchannelClientCertificateNotInStore = "schannel: client cert not found in cert store";

    /// <summary>
    /// Every build's message for exit 90, when the server's public key does not match
    /// <c>--pinnedpubkey</c>, the key file cannot be read or holds no key: measured with curl
    /// 8.21.0's Schannel build and curl 8.18.0's OpenSSL build, 2026-09-29 (BL-608).
    /// </summary>
    public const string PinnedPublicKeyMismatch = "SSL: public key does not match pinned public key";

    /// <summary>
    /// Every build's message for exit 35 when a <c>--curves</c> list the OpenSSL build accepts
    /// leaves no group to offer, such as <c>?bogus</c> or <c>-X25519</c>: measured with curl
    /// 8.18.0's OpenSSL 3.5.5 build, 2026-09-30 (BL-709, ADR-0284).
    /// </summary>
    public const string OpenSslNoSuitableGroups = "TLS connect error: error:0A000127:SSL routines::no suitable groups";

    /// <summary>
    /// Every build's message for exit 35 when a <c>--curves</c> list stars only groups
    /// TLS 1.3 cannot share a key for, such as <c>*brainpoolP256r1:P-384</c>, and TLS 1.3 is
    /// offered: measured with curl 8.18.0's OpenSSL 3.5.5 build, 2026-10-01 (BL-1082).
    /// </summary>
    public const string OpenSslNoSuitableKeyShare = "TLS connect error: error:0A000065:SSL routines::no suitable key share";

    /// <summary>
    /// Every build's message for exit 35 when a <c>--sigalgs</c> list names only schemes the
    /// client cannot offer, such as <c>RSA+SHA1</c>: measured with curl 8.18.0's OpenSSL 3.5.5
    /// build, 2026-09-30 (BL-709, ADR-0284).
    /// </summary>
    public const string OpenSslNoSuitableSignatureAlgorithm = "TLS connect error: error:0A000076:SSL routines::no suitable signature algorithm";

    /// <summary>
    /// Every build's message for exit 35 when a <c>--sigalgs</c> list names no scheme TLS 1.2
    /// can check, such as <c>RSA+SHA1</c> or <c>mldsa65</c>, under a TLS 1.2 ceiling: measured
    /// with curl 8.18.0's OpenSSL 3.5.5 build, 2026-10-01 (BL-1094).
    /// </summary>
    public const string OpenSslNoCiphersAvailable = "TLS connect error: error:0A0000B5:SSL routines::no ciphers available";

    /// <summary>
    /// The Windows message for exit 35 when the server answers a <c>--curves</c> ClientHello
    /// with a <c>handshake_failure</c> alert, as the one Windows build that applies
    /// <c>--curves</c>, curl.se's curl 8.18.0 with LibreSSL 4.2.1, prints it (measured
    /// 2026-09-30 against a server limited to P-384; BL-709, ADR-0151, ADR-0284).
    /// </summary>
    public const string LibreSslHandshakeFailureAlert = "TLS connect error: error:14004410:SSL routines:CONNECT_CR_SRVR_HELLO:sslv3 alert handshake failure";

    /// <summary>
    /// Every build's message for exit 59 when OpenSSL refuses a <c>--curves</c> value, as curl
    /// 8.18.0's OpenSSL and LibreSSL builds print it (measured, ADR-0151, BL-709).
    /// </summary>
    /// <param name="curves">The value, verbatim.</param>
    /// <returns>The message curl prints.</returns>
    public static string CurvesListRefused(string curves) => $"failed setting curves list: '{curves}'";

    /// <summary>
    /// Every build's message for exit 59 when OpenSSL refuses a <c>--sigalgs</c> value, as curl
    /// 8.18.0's OpenSSL build prints it (measured, ADR-0151, BL-709).
    /// </summary>
    /// <param name="signatureAlgorithms">The value, verbatim.</param>
    /// <returns>The message curl prints.</returns>
    public static string SignatureAlgorithmsRefused(string signatureAlgorithms) => $"failed setting signature algorithms: '{signatureAlgorithms}'";

    /// <summary>
    /// curl's own text for exit 58, printed when the failure has no message of its own, as
    /// for a <c>--cert</c> store path whose thumbprint is not hex (measured 2026-09-27).
    /// </summary>
    public const string SslCertProblem = "Problem with the local SSL certificate";

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
    /// <param name="privateKeyType">The <c>--key-type</c> value as given, or <c>PEM</c> without one.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslPrivateKeyUnusable(string privateKeyFile, string privateKeyType) =>
        $"unable to set private key file: '{privateKeyFile}' type {privateKeyType}";

    /// <summary>
    /// The Schannel build's message for exit 58 when <c>--cert-type</c> names any type but
    /// <c>P12</c>.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string SchannelClientCertificateTypeIncompatible(string clientCertificateFile) =>
        $"schannel: certificate format compatibility error for {clientCertificateFile}";

    /// <summary>OpenSSL's error string for a path that is a directory.</summary>
    public const string OpenSslIsADirectory = "error:80000015:system library::Is a directory";

    /// <summary>OpenSSL's error string for a file it may not read.</summary>
    public const string OpenSslPermissionDenied = "error:8000000D:system library::Permission denied";

    /// <summary>OpenSSL's error string for an empty DER certificate file.</summary>
    public const string OpenSslAsn1Lib = "error:0A08000D:SSL routines::ASN1 lib";

    /// <summary>
    /// OpenSSL's error string for a DER file whose outer length runs past its end.
    /// </summary>
    public const string OpenSslNotEnoughData = "error:0680008E:asn1 encoding routines::not enough data";

    /// <summary>
    /// OpenSSL's error string for a DER file that is whole but is not a certificate.
    /// </summary>
    public const string OpenSslWrongTag = "error:068000A8:asn1 encoding routines::wrong tag";

    /// <summary>
    /// The OpenSSL build's message for exit 58: the <c>--cert</c> file did not load as a DER
    /// certificate (<c>--cert-type DER</c>).
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <param name="openSslError">The OpenSSL error string for why it did not load.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslDerClientCertificateNotLoaded(string clientCertificateFile, string openSslError) =>
        $"could not load ASN1 client certificate from {clientCertificateFile}, OpenSSL error {openSslError}, (no key found, wrong passphrase, or wrong file format?)";

    /// <summary>
    /// The OpenSSL build's message for exit 58 when the <c>--cert-type P12</c> file cannot be
    /// opened.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslPkcs12NotOpened(string clientCertificateFile) =>
        $"could not open PKCS12 file '{clientCertificateFile}'";

    /// <summary>
    /// The OpenSSL build's message for exit 58 when the <c>--cert-type P12</c> file is a
    /// directory, is empty or is not PKCS#12.
    /// </summary>
    /// <param name="clientCertificateFile">The certificate file, as split from <c>--cert</c>.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslPkcs12NotRead(string clientCertificateFile) =>
        $"error reading PKCS12 file '{clientCertificateFile}'";

    /// <summary>
    /// The OpenSSL build's message for exit 58 when the passphrase, or its absence, does not
    /// open the <c>--cert-type P12</c> file.
    /// </summary>
    public const string OpenSslPkcs12PassphraseBad =
        "could not parse PKCS12 file, check password, OpenSSL error error:11800071:PKCS12 routines::mac verify failure";

    /// <summary>
    /// The OpenSSL build's message for exit 43: <c>--cert-type</c> names no type it knows.
    /// </summary>
    /// <param name="certificateType">The <c>--cert-type</c> value as given.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslClientCertificateTypeUnsupported(string certificateType) =>
        $"not supported file type '{certificateType}' for certificate";

    /// <summary>The OpenSSL build's message for exit 58: <c>--cert-type ENG</c>.</summary>
    public const string OpenSslClientCertificateEngineNotSet = "crypto engine not set, cannot load certificate";

    /// <summary>The OpenSSL build's message for exit 58: <c>--cert-type PROV</c>.</summary>
    public const string OpenSslClientCertificateProviderNotSet = "crypto provider not set, cannot load certificate";

    /// <summary>
    /// The OpenSSL build's message for exit 43: <c>--key-type</c> names no type it knows.
    /// </summary>
    public const string OpenSslPrivateKeyTypeUnsupported = "not supported file type for private key";

    /// <summary>The OpenSSL build's message for exit 58: <c>--key-type ENG</c>.</summary>
    public const string OpenSslPrivateKeyEngineNotSet = "crypto engine not set, cannot load private key";

    /// <summary>The OpenSSL build's message for exit 58: <c>--key-type PROV</c>.</summary>
    public const string OpenSslPrivateKeyProviderNotSet = "crypto provider not set, cannot load private key";

    /// <summary>The OpenSSL build's message for exit 58: <c>--key-type P12</c>.</summary>
    public const string OpenSslPrivateKeyTypePkcs12Refused = "file type P12 for private key not supported";

    /// <summary>
    /// The Schannel build's message for exit 59: it refuses every <c>--ciphers</c> value
    /// (ADR-0011).
    /// </summary>
    public const string SchannelCipherListRefused = "schannel: Failed setting algorithm cipher list";

    /// <summary>
    /// The OpenSSL build's message for exit 59: no entry of the <c>--ciphers</c> list names a
    /// TLS 1.2-and-below suite it knows.
    /// </summary>
    /// <param name="ciphers">The <c>--ciphers</c> value, verbatim.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslCipherListUnusable(string ciphers) =>
        $"failed setting cipher list: {ciphers}";

    /// <summary>
    /// The OpenSSL build's message for exit 59: no entry of the <c>--tls13-ciphers</c> list
    /// names a TLS 1.3 suite.
    /// </summary>
    /// <param name="tls13Ciphers">The <c>--tls13-ciphers</c> value, verbatim.</param>
    /// <returns>The message curl prints.</returns>
    public static string OpenSslTls13CipherSuiteUnusable(string tls13Ciphers) =>
        $"failed setting TLS 1.3 cipher suite: {tls13Ciphers}";

    // OpenSSL reports a failed trust before a failed validity period.
    private static string OpenSslVerifyError(X509Chain? chain)
    {
        if (chain is null || chain.ChainElements.Count == 0)
        {
            return "unable to get local issuer certificate (20)";
        }

        return ChainStatus(chain) == X509ChainStatusFlags.NotTimeValid ? OpenSslValidityError(chain) : OpenSslTrustError(chain);
    }

    // curl's Schannel build checks the chain's trust errors in this order. An untrusted root
    // is also what anything else, or no chain at all, is reported as.
    private static string SchannelCaCertificateFileChainError(X509Chain? chain)
    {
        var status = ChainStatus(chain);
        if (status.HasFlag(X509ChainStatusFlags.NotTimeValid))
        {
            return SchannelChainNotTimeValid;
        }

        if (status.HasFlag(X509ChainStatusFlags.PartialChain))
        {
            return SchannelChainIncomplete;
        }

        var untrustedOrUnknown = status & (X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.RevocationStatusUnknown);
        return untrustedOrUnknown == X509ChainStatusFlags.RevocationStatusUnknown
            ? SchannelRevocationStatusUnknown
            : SchannelCaCertificateFileAnchorsNothing;
    }

    private static X509ChainStatusFlags ChainStatus(X509Chain? chain) =>
        chain?.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, next) => all | next.Status) ?? X509ChainStatusFlags.NoError;

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

    private static AddressFamily? ToIpAddressFamily(string host) =>
        IPAddress.TryParse(host, out var address) ? address.AddressFamily : null;

    private static string DescribeTarget(string host) => ToIpAddressFamily(host) switch
    {
        null => "hostname",
        AddressFamily.InterNetworkV6 => "ipv6 address",
        _ => "ipv4 address",
    };

    private static X509SubjectAlternativeNameExtension? FindAlternativeNames(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();

    // curl's OpenSSL build falls back to the common name only when the subjectAltName
    // extension holds no DNS name and no IP address.
    private static bool HasDnsOrIpAlternativeNames(X509Certificate2 certificate)
    {
        var alternativeNames = FindAlternativeNames(certificate);
        return alternativeNames is not null
            && (alternativeNames.EnumerateDnsNames().Any() || alternativeNames.EnumerateIPAddresses().Any());
    }

    private static string? RecvFailure(Exception exception, FrozenDictionary<SocketError, string> socketErrorTexts)
    {
        var socketError = FindInnerException<SocketException>(exception);
        return socketError is null
            ? null
            : $"Recv failure: {socketErrorTexts.GetValueOrDefault(socketError.SocketErrorCode, socketError.Message)}";
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
