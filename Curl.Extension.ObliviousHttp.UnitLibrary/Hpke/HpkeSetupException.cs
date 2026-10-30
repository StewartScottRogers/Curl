using System.Security.Cryptography;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// Thrown when an HPKE base-mode setup (RFC 9180 section 5.1) cannot use the peer's key: a
/// recipient public key or encapsulated key of the wrong length, or one whose Diffie-Hellman
/// result is all zero (section 7.1.4).
/// </summary>
public sealed class HpkeSetupException : CryptographicException
{
    /// <summary>Initializes a new instance of the <see cref="HpkeSetupException" /> class.</summary>
    /// <param name="message">Which key could not be used.</param>
    public HpkeSetupException(string message)
        : base(message)
    {
    }
}
