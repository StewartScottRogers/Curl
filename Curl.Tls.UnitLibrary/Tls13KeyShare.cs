namespace Curl.Tls;

/// <summary>
/// One ephemeral key pair of a named group (RFC 8446 section 4.2.8): the public value the
/// client sends in its <c>key_share</c>, and the private half that combines with the
/// server's share into the (EC)DHE shared secret.
/// </summary>
public abstract class Tls13KeyShare : IDisposable
{
    /// <summary>Initialises the share of <paramref name="group" /> with its public value.</summary>
    /// <param name="group">The named group code point.</param>
    /// <param name="publicKey">The public value, encoded as the group defines for TLS 1.3.</param>
    protected Tls13KeyShare(ushort group, byte[] publicKey)
    {
        Group = group;
        PublicKey = publicKey;
    }

    /// <summary>Gets the named group code point.</summary>
    public ushort Group { get; }

    /// <summary>Gets the public value that goes in the <c>key_share</c> entry.</summary>
    public byte[] PublicKey { get; }

    /// <summary>Gets the share as a <c>key_share</c> entry.</summary>
    public KeyShareEntry Entry => new(Group, PublicKey);

    /// <summary>Combines this share with the peer's public value.</summary>
    /// <param name="peerPublicKey">The peer's public value for the same group.</param>
    /// <returns>The shared secret, or <see langword="null" /> when the peer's value is malformed or degenerate.</returns>
    public abstract byte[]? ComputeSharedSecret(byte[] peerPublicKey);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the private key.</summary>
    /// <param name="disposing"><see langword="true" /> when called from <see cref="Dispose()" />.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
