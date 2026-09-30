using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// An HMAC over a hash the BCL offers on every platform - SHA-1, SHA-256, SHA-512 and MD5 -
/// through <see cref="IncrementalHash" />.
/// </summary>
/// <param name="hash">The hash.</param>
/// <param name="key">The derived integrity key.</param>
internal sealed class BclSshHmac(HashAlgorithmName hash, byte[] key) : ISshHmac
{
    private readonly IncrementalHash hmac = IncrementalHash.CreateHMAC(hash, key);

    /// <inheritdoc />
    public void AppendData(ReadOnlySpan<byte> data) => hmac.AppendData(data);

    /// <inheritdoc />
    public byte[] GetHashAndReset() => hmac.GetHashAndReset();

    /// <inheritdoc />
    public void Dispose() => hmac.Dispose();
}
