using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Maps what ends a QUIC handshake to curl's exit code (ADR-0144 section 7, ADR-0165): the
/// server's CONNECTION_CLOSE, and a TLS failure the client detected.
/// </summary>
internal static class QuicHandshakeFailures
{
    private const ulong CryptoErrorLast = 0x01ff;

    /// <summary>Returns the transport error that carries a TLS alert, <c>CRYPTO_ERROR</c> 0x0100 plus the alert (RFC 9001 section 4.8).</summary>
    public static QuicTransportErrorCode CryptoError(TlsAlertDescription alert) => QuicTransportErrorCode.CryptoErrorBase + (byte)alert;

    /// <summary>
    /// Maps the server closing the connection: <c>CONNECTION_REFUSED</c> is exit 8, any other
    /// transport or application error, a TLS alert included, exit 7.
    /// </summary>
    public static QuicHandshakeFailure FromServerClose(QuicConnectionCloseFrame close)
    {
        var exitCode = close.FrameType is not null && close.ErrorCode == (ulong)QuicTransportErrorCode.ConnectionRefused
            ? CurlExitCode.WeirdServerReply
            : CurlExitCode.CouldntConnect;
        return new QuicHandshakeFailure(exitCode, $"The server closed the QUIC connection with {Describe(close)}.");
    }

    /// <summary>Maps a TLS failure the client detected: the verifier rejecting the certificate is exit 60 with its reason, any other failure exit 35.</summary>
    public static QuicHandshakeFailure FromTls(TlsHandshakeFailure failure) => failure.IsCertificateRejection
        ? new QuicHandshakeFailure(CurlExitCode.PeerFailedVerification, $"{failure.CertificateRejection}", failure)
        : new QuicHandshakeFailure(CurlExitCode.SslConnectError, $"The QUIC TLS handshake failed with alert {failure.Alert}.", failure);

    private static string Describe(QuicConnectionCloseFrame close)
    {
        var reason = close.ReasonPhrase.IsEmpty ? string.Empty : $" ({System.Text.Encoding.UTF8.GetString(close.ReasonPhrase.Span)})";
        if (close.FrameType is null)
        {
            return $"application error 0x{close.ErrorCode:x}{reason}";
        }

        return close.ErrorCode is >= (ulong)QuicTransportErrorCode.CryptoErrorBase and <= CryptoErrorLast
            ? $"TLS alert {(TlsAlertDescription)(close.ErrorCode - (ulong)QuicTransportErrorCode.CryptoErrorBase)}{reason}"
            : $"transport error {(QuicTransportErrorCode)close.ErrorCode}{reason}";
    }
}
