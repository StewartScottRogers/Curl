namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>
/// Thrown when a Binary HTTP message (RFC 9292) is invalid (section 4): it ends where it may
/// not be truncated, a length is longer than what is left, the framing indicator is not one
/// the decoder reads, a status code or field name is out of range, or padding is not zero.
/// </summary>
public sealed class BinaryHttpFormatException : FormatException
{
    /// <summary>Initializes a new instance of the <see cref="BinaryHttpFormatException" /> class.</summary>
    /// <param name="message">What is invalid about the message.</param>
    public BinaryHttpFormatException(string message)
        : base(message)
    {
    }
}
