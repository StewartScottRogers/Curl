using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Why a QUIC handshake failed, as curl reports it (ADR-0144 section 7, ADR-0165): the exit
/// code, the message, and when TLS failed, its failure.
/// </summary>
/// <param name="ExitCode">The exit code curl ends with.</param>
/// <param name="Message">What failed; the connector puts it in curl's lines.</param>
/// <param name="TlsFailure">The TLS handshake's own failure when it is what failed, otherwise <see langword="null" />.</param>
public sealed record QuicHandshakeFailure(CurlExitCode ExitCode, string Message, TlsHandshakeFailure? TlsFailure = null);
