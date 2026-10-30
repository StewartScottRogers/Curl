using System.Security.Cryptography;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// Thrown when <c>ContextR.Open</c> (RFC 9180 section 5.2) fails: the ciphertext is shorter
/// than a tag, or its tag does not match because the ciphertext, the associated data or the
/// key is not the one it was sealed with.
/// </summary>
public sealed class HpkeOpenException : CryptographicException
{
    /// <summary>Initializes a new instance of the <see cref="HpkeOpenException" /> class.</summary>
    public HpkeOpenException()
        : base("HPKE open failed: the ciphertext does not authenticate.")
    {
    }
}
