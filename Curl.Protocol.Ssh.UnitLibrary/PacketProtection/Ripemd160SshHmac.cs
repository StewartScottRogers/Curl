using Curl.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// HMAC-RIPEMD-160, for <c>hmac-ripemd160</c> and <c>hmac-ripemd160@openssh.com</c>:
/// <see cref="HmacRipemd160" /> from <c>Curl.Cryptography</c>, since the BCL has no
/// RIPEMD-160 (ADR-0118).
/// </summary>
/// <param name="key">The derived integrity key.</param>
internal sealed class Ripemd160SshHmac(byte[] key) : ISshHmac
{
    private readonly HmacRipemd160 hmac = new(key);

    /// <inheritdoc />
    public void AppendData(ReadOnlySpan<byte> data) => hmac.AppendData(data);

    /// <inheritdoc />
    public byte[] GetHashAndReset()
    {
        byte[] result = new byte[HmacRipemd160.HashSize];
        hmac.GetHashAndReset(result);
        return result;
    }

    /// <inheritdoc />
    public void Dispose() => hmac.Dispose();
}
