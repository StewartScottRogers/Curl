using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Why a QUIC connection failed, during its handshake or after it, as curl reports it
/// (ADR-0144 section 7, ADR-0165, ADR-0177): the exit code, the message, and when TLS failed,
/// its failure.
/// </summary>
/// <param name="ExitCode">The exit code curl ends with.</param>
/// <param name="Message">What failed; the connector puts it in curl's lines.</param>
/// <param name="TlsFailure">The TLS handshake's own failure when it is what failed, otherwise <see langword="null" />.</param>
public sealed record QuicHandshakeFailure(CurlExitCode ExitCode, string Message, TlsHandshakeFailure? TlsFailure = null)
{
    /// <summary>Gets the CONNECTION_CLOSE frame the server closed the connection with, or <see langword="null" /> when the server did not close it.</summary>
    public QuicConnectionCloseFrame? ServerClose { get; init; }
}
