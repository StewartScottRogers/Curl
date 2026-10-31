namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// Thrown when a key configuration or an <c>application/ohttp-keys</c> list (RFC 9458 sections
/// 3.1 and 3.2) is truncated, has bytes left over, or has an empty or ragged list of symmetric algorithms.
/// </summary>
public sealed class MalformedKeyConfigurationException : FormatException
{
    /// <summary>Initializes a new instance of the <see cref="MalformedKeyConfigurationException" /> class.</summary>
    /// <param name="message">Where the bytes stopped making sense.</param>
    public MalformedKeyConfigurationException(string message)
        : base(message)
    {
    }
}
