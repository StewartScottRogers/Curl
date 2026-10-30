namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// Thrown when a well-formed key configuration (RFC 9458 section 3.1) names a KEM, or offers
/// only KDF and AEAD pairs, that this library does not implement.
/// </summary>
public sealed class UnsupportedKeyConfigurationException : NotSupportedException
{
    /// <summary>Initializes a new instance of the <see cref="UnsupportedKeyConfigurationException" /> class.</summary>
    /// <param name="message">What is not supported.</param>
    public UnsupportedKeyConfigurationException(string message)
        : base(message)
    {
    }
}
