namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// A cipher that encrypts or decrypts one direction's packets as one continuous stream of
/// whole blocks, paired with a separate MAC by <see cref="CipherAndMacPacketProtection" />.
/// </summary>
internal interface ISshCipher : IDisposable
{
    /// <summary>Gets the cipher's block size in bytes.</summary>
    int BlockSize { get; }

    /// <summary>
    /// Encrypts or decrypts the next bytes of the direction's stream, a whole number of
    /// blocks. <paramref name="destination" /> may be <paramref name="source" /> itself.
    /// </summary>
    /// <param name="source">The bytes to transform.</param>
    /// <param name="destination">Where the result goes, as long as <paramref name="source" />.</param>
    void Transform(ReadOnlySpan<byte> source, Span<byte> destination);
}
