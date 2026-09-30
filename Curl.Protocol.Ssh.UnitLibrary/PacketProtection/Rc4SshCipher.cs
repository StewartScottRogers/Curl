using Curl.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// <c>arcfour</c> (RFC 4253 section 6.3) and <c>arcfour128</c> (RFC 4345): the RC4 stream
/// cipher under a 16-byte key, its keystream running on across packets. <c>arcfour128</c>
/// first discards <see cref="Rc4.Rfc4345DiscardLength" /> keystream bytes; <c>arcfour</c>
/// discards none. Encryption and decryption are the same operation. The cipher is
/// <see cref="Rc4" /> from <c>Curl.Cryptography</c> (ADR-0118). Packets are padded to 8
/// bytes, libssh2's block size for both.
/// </summary>
internal sealed class Rc4SshCipher : ISshCipher
{
    private readonly Rc4 rc4;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rc4SshCipher" /> class.
    /// </summary>
    /// <param name="key">The derived encryption key: 16 bytes.</param>
    /// <param name="discardLength">How many keystream bytes to discard first.</param>
    internal Rc4SshCipher(byte[] key, int discardLength)
    {
        rc4 = new Rc4(key);
        rc4.DiscardKeyStream(discardLength);
    }

    /// <inheritdoc />
    public int BlockSize => 8;

    /// <inheritdoc />
    public void Encrypt(ReadOnlySpan<byte> source, Span<byte> destination) =>
        rc4.ApplyKeyStream(source, destination);

    /// <inheritdoc />
    public void Decrypt(ReadOnlySpan<byte> source, Span<byte> destination) =>
        rc4.ApplyKeyStream(source, destination);

    /// <inheritdoc />
    public void Dispose() => rc4.Dispose();
}
