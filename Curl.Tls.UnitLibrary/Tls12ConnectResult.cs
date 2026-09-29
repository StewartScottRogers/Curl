namespace Curl.Tls;

/// <summary>What <see cref="Tls12ClientConnection.ConnectAsync" /> ends with: the connected stream, or why the handshake failed.</summary>
/// <param name="Stream">The connected stream, or <see langword="null" /> when the handshake failed.</param>
/// <param name="Failure">Why the handshake failed, or <see langword="null" /> when it completed.</param>
public sealed record Tls12ConnectResult(Tls12ClientStream? Stream, TlsHandshakeFailure? Failure)
{
    /// <summary>Gets a value indicating whether the handshake completed.</summary>
    public bool Succeeded => Stream is not null;
}
