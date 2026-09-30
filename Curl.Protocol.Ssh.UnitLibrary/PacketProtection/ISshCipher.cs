namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// A cipher that encrypts or decrypts one direction's packets as one continuous stream of
/// whole blocks, paired with a separate MAC by <see cref="CipherAndMacPacketProtection" />.
/// A direction's instance only encrypts (the sender's) or only decrypts (the receiver's),
/// and a chaining cipher carries its state from one call to the next.
/// </summary>
internal interface ISshCipher : IDisposable
{
    /// <summary>Gets the cipher's block size in bytes.</summary>
    int BlockSize { get; }

    /// <summary>
    /// Encrypts the next bytes of the direction's stream, a whole number of blocks.
    /// <paramref name="destination" /> may be <paramref name="source" /> itself.
    /// </summary>
    /// <param name="source">The plaintext.</param>
    /// <param name="destination">Where the ciphertext goes, as long as <paramref name="source" />.</param>
    void Encrypt(ReadOnlySpan<byte> source, Span<byte> destination);

    /// <summary>
    /// Decrypts the next bytes of the direction's stream, a whole number of blocks.
    /// <paramref name="destination" /> may be <paramref name="source" /> itself.
    /// </summary>
    /// <param name="source">The ciphertext.</param>
    /// <param name="destination">Where the plaintext goes, as long as <paramref name="source" />.</param>
    void Decrypt(ReadOnlySpan<byte> source, Span<byte> destination);
}
