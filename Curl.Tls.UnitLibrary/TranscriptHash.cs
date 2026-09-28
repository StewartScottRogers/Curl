using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The running hash of the handshake messages (RFC 8446 section 4.4.1): each message goes
/// in with its header, and the hash so far can be read at any point without ending it.
/// </summary>
public sealed class TranscriptHash : IDisposable
{
    private readonly IncrementalHash hash;

    /// <summary>Starts an empty transcript hashed with <paramref name="hashAlgorithm" />.</summary>
    /// <param name="hashAlgorithm">The cipher suite's hash, SHA-256 or SHA-384.</param>
    /// <exception cref="ArgumentException">The hash is neither SHA-256 nor SHA-384.</exception>
    public TranscriptHash(HashAlgorithmName hashAlgorithm)
    {
        HashLength = Tls13HashLength.Of(hashAlgorithm);
        hash = IncrementalHash.CreateHash(hashAlgorithm);
    }

    /// <summary>Gets the length in bytes of the hash.</summary>
    public int HashLength { get; }

    /// <summary>Adds one handshake message, header included, to the transcript.</summary>
    /// <param name="handshakeMessage">The message as framed on the wire.</param>
    public void Append(ReadOnlySpan<byte> handshakeMessage) => hash.AppendData(handshakeMessage);

    /// <summary>Returns the hash of every message appended so far; the transcript carries on.</summary>
    /// <returns>The transcript hash.</returns>
    public byte[] GetCurrentHash() => hash.GetCurrentHash();

    /// <summary>
    /// Replaces the transcript so far, the first ClientHello, with the synthetic
    /// <c>message_hash</c> message that carries its hash, as a HelloRetryRequest requires.
    /// </summary>
    public void ReplaceWithMessageHash()
    {
        byte[] clientHelloHash = hash.GetHashAndReset();
        hash.AppendData(new HandshakeMessage(HandshakeType.MessageHash, clientHelloHash).Encode());
    }

    /// <inheritdoc />
    public void Dispose() => hash.Dispose();
}
