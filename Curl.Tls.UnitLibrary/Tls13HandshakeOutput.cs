namespace Curl.Tls;

/// <summary>
/// What one step of <see cref="Tls13ClientHandshake" /> asks of its caller, in order: send
/// these bytes at these levels, install these secrets, and whether the handshake is now
/// complete or has failed (in which case the caller sends <see cref="TlsHandshakeFailure.Alert" />).
/// </summary>
/// <param name="BytesToSend">Handshake bytes to send, each at its level, in order.</param>
/// <param name="SecretsInstalled">Traffic secrets that now apply, in the order they took effect.</param>
/// <param name="IsComplete">Whether the handshake has completed.</param>
/// <param name="Failure">Why the handshake failed, or <see langword="null" /> while it has not.</param>
public sealed record Tls13HandshakeOutput(
    IReadOnlyList<TlsHandshakeBytes> BytesToSend,
    IReadOnlyList<Tls13TrafficSecret> SecretsInstalled,
    bool IsComplete,
    TlsHandshakeFailure? Failure);
