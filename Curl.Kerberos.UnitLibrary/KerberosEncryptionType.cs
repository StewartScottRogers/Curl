namespace Curl.Kerberos;

/// <summary>
/// The Kerberos encryption types this library encrypts, decrypts and checksums with, by
/// their numbers in IANA's "Kerberos Encryption Type Numbers" registry.
/// </summary>
public enum KerberosEncryptionType
{
    /// <summary><c>aes128-cts-hmac-sha1-96</c> (RFC 3962).</summary>
    Aes128CtsHmacSha196 = 17,

    /// <summary><c>aes256-cts-hmac-sha1-96</c> (RFC 3962).</summary>
    Aes256CtsHmacSha196 = 18,

    /// <summary><c>aes128-cts-hmac-sha256-128</c> (RFC 8009).</summary>
    Aes128CtsHmacSha256128 = 19,

    /// <summary><c>aes256-cts-hmac-sha384-192</c> (RFC 8009).</summary>
    Aes256CtsHmacSha384192 = 20,

    /// <summary><c>rc4-hmac</c> (RFC 4757).</summary>
    Rc4Hmac = 23,
}
