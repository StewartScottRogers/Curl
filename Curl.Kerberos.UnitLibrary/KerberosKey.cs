using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// An encryption key (RFC 4120's <c>EncryptionKey</c>): its encryption type and its bytes,
/// zeroed by <see cref="Dispose" />.
/// </summary>
/// <param name="encryptionType">The encryption type, e.g. 18 for <c>aes256-cts-hmac-sha1-96</c>.</param>
/// <param name="value">The key bytes; the key takes ownership of the array.</param>
public sealed class KerberosKey(int encryptionType, byte[] value) : IDisposable
{
    /// <summary>Gets the encryption type, e.g. 18 for <c>aes256-cts-hmac-sha1-96</c>.</summary>
    public int EncryptionType { get; } = encryptionType;

    /// <summary>Gets the key bytes; all zero after <see cref="Dispose" />.</summary>
    public ReadOnlySpan<byte> Value => value;

    /// <summary>Zeroes the key bytes.</summary>
    public void Dispose() => CryptographicOperations.ZeroMemory(value);
}
