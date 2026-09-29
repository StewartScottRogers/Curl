namespace Curl.Tls;

/// <summary>
/// What one step of <see cref="Tls12ClientHandshake" /> asks of its caller: send these
/// messages in order, and whether the handshake is now complete or has failed (in which
/// case the caller sends <see cref="TlsHandshakeFailure.Alert" />).
/// </summary>
/// <param name="MessagesToSend">The messages to send, in order.</param>
/// <param name="IsComplete">Whether the handshake has completed.</param>
/// <param name="Failure">Why the handshake failed, or <see langword="null" /> while it has not.</param>
public sealed record Tls12HandshakeOutput(IReadOnlyList<Tls12OutgoingMessage> MessagesToSend, bool IsComplete, TlsHandshakeFailure? Failure);
