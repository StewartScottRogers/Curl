namespace Curl.Tls;

/// <summary>
/// Where the TLS client's randomness comes from: the hello random, the legacy session ID
/// and the ephemeral key shares. Production uses <see cref="SystemTlsRandomSource" />;
/// tests replay the fixed values of RFC 8448's traces.
/// </summary>
public interface ITlsRandomSource
{
    /// <summary>Fills <paramref name="destination" /> with random bytes.</summary>
    /// <param name="destination">The bytes to fill.</param>
    void Fill(Span<byte> destination);

    /// <summary>Creates a fresh ephemeral key share on <paramref name="group" />.</summary>
    /// <param name="group">A group <see cref="TlsNamedGroup.CanShare" /> accepts.</param>
    /// <returns>The key share, which the caller disposes.</returns>
    Tls13KeyShare CreateKeyShare(ushort group);
}
