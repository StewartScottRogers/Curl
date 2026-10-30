using System.Security.Cryptography;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// Thrown when an encapsulated response (RFC 9458 section 4.4) is shorter than its response
/// nonce and tag, or its tag does not match the request's context.
/// </summary>
public sealed class EncapsulatedResponseException : CryptographicException
{
    /// <summary>Initializes a new instance of the <see cref="EncapsulatedResponseException" /> class.</summary>
    /// <param name="message">Why the response could not be decapsulated.</param>
    public EncapsulatedResponseException(string message)
        : base(message)
    {
    }
}
