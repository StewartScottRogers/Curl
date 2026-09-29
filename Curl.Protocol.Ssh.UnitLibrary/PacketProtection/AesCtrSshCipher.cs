using Curl.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// <c>aes128-ctr</c>, <c>aes192-ctr</c> and <c>aes256-ctr</c> (RFC 4344 section 4): AES in
/// counter mode, the counter starting at the derived IV and running on across packets.
/// Encryption and decryption are the same operation. The mode is
/// <see cref="AesCtr" /> from <c>Curl.Cryptography</c> (ADR-0118).
/// </summary>
/// <param name="key">The derived encryption key: 16, 24 or 32 bytes.</param>
/// <param name="initialCounter">The derived IV: 16 bytes.</param>
internal sealed class AesCtrSshCipher(byte[] key, byte[] initialCounter) : ISshCipher
{
    private readonly AesCtr aesCtr = new(key, initialCounter);

    /// <inheritdoc />
    public int BlockSize => AesCtr.BlockSize;

    /// <inheritdoc />
    public void Transform(ReadOnlySpan<byte> source, Span<byte> destination) =>
        aesCtr.ApplyKeyStream(source, destination);

    /// <inheritdoc />
    public void Dispose() => aesCtr.Dispose();
}
