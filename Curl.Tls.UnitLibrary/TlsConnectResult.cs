namespace Curl.Tls;

/// <summary>
/// What <see cref="TlsClientConnection.ConnectAsync" /> ends with: the connected stream of
/// the version the server chose, or why the handshake failed.
/// </summary>
/// <param name="Tls13Stream">The connected stream when the server chose TLS 1.3, otherwise <see langword="null" />.</param>
/// <param name="Tls12Stream">The connected stream when the server chose TLS 1.2 or below, otherwise <see langword="null" />.</param>
/// <param name="Failure">Why the handshake failed, or <see langword="null" /> when it completed.</param>
public sealed record TlsConnectResult(Tls13ClientStream? Tls13Stream, Tls12ClientStream? Tls12Stream, TlsHandshakeFailure? Failure)
{
    /// <summary>Gets a value indicating whether the handshake completed.</summary>
    public bool Succeeded => Failure is null;
}
