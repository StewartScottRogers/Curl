using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Maps what ends a QUIC connection to curl's exit code (ADR-0144 section 7, ADR-0165,
/// ADR-0177): the server's CONNECTION_CLOSE, a TLS failure the client detected, and, once
/// the handshake is complete, a transport error, a stateless reset and the idle timeout.
/// </summary>
internal static class QuicHandshakeFailures
{
    /// <summary>curl's message for exit 56, which it prints when <c>ngtcp2_conn_read_pkt</c> fails after the handshake without a message of its own.</summary>
    public const string ReceiveFailureMessage = "Failure when receiving data from the peer";

    private const ulong CryptoErrorLast = 0x01ff;

    /// <summary>The failure of a connection whose idle timeout passed: <c>ngtcp2_conn_handle_expiry</c> returns <c>ERR_IDLE_CLOSE</c>, which curl reports as exit 55.</summary>
    public static QuicHandshakeFailure IdleTimeout { get; } = new(CurlExitCode.SendError, "ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE");

    /// <summary>The failure of a connection lost after the handshake, to a transport error, a stateless reset or the server's CONNECTION_CLOSE: exit 56 with curl's message for it.</summary>
    public static QuicHandshakeFailure ConnectionLost { get; } = new(CurlExitCode.RecvError, ReceiveFailureMessage);

    /// <summary>Returns the transport error that carries a TLS alert, <c>CRYPTO_ERROR</c> 0x0100 plus the alert (RFC 9001 section 4.8).</summary>
    public static QuicTransportErrorCode CryptoError(TlsAlertDescription alert) => QuicTransportErrorCode.CryptoErrorBase + (byte)alert;

    /// <summary>
    /// Maps the server closing the connection. During the handshake <c>CONNECTION_REFUSED</c>
    /// is exit 8 and any other transport or application error, a TLS alert included, exit 7;
    /// once the handshake is complete every close is exit 56.
    /// </summary>
    public static QuicHandshakeFailure FromServerClose(QuicConnectionCloseFrame close, bool handshakeComplete)
    {
        if (handshakeComplete)
        {
            return ConnectionLost with { ServerClose = close };
        }

        var exitCode = close.FrameType is not null && close.ErrorCode == (ulong)QuicTransportErrorCode.ConnectionRefused
            ? CurlExitCode.WeirdServerReply
            : CurlExitCode.CouldntConnect;
        return new QuicHandshakeFailure(exitCode, $"The server closed the QUIC connection with {Describe(close)}.") { ServerClose = close };
    }

    /// <summary>Maps a transport error the client detected in what the server sent: exit 7 with its reason during the handshake, exit 56 once it is complete.</summary>
    public static QuicHandshakeFailure FromTransportError(QuicTransportException error, bool handshakeComplete) => handshakeComplete
        ? ConnectionLost
        : new QuicHandshakeFailure(CurlExitCode.CouldntConnect, error.Message);

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
