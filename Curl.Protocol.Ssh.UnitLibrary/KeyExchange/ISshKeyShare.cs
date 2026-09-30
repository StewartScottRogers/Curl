namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The client's half of one key agreement or key encapsulation inside an SSH key exchange:
/// the public bytes the client sends, the length the server's answer must have, and the
/// secret the two give. Disposing it zeroes the client's private key.
/// </summary>
internal interface ISshKeyShare : IDisposable
{
    /// <summary>Gets the public bytes the client sends: a public key, point or encapsulation key.</summary>
    byte[] ClientShare { get; }

    /// <summary>Gets the length in bytes the server's answer must have.</summary>
    int ServerShareLength { get; }

    /// <summary>
    /// Computes the shared secret from the server's answer.
    /// </summary>
    /// <param name="serverShare">
    /// The server's public key, point or ciphertext. <see cref="HybridKemSshKeyExchange" />
    /// checks its length before a key-encapsulation share sees it.
    /// </param>
    /// <returns>The secret, raw.</returns>
    /// <exception cref="InvalidDataException">
    /// An X25519 or NIST-curve answer has the wrong length or is unusable (an X25519 key giving an all-zero
    /// secret, a point off the curve).
    /// </exception>
    byte[] ComputeSharedSecret(ReadOnlySpan<byte> serverShare);
}
