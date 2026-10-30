namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Thrown when a Brotli stream (RFC 7932) is truncated or breaks one of the format's rules.</summary>
public sealed class BrotliFormatException : FormatException
{
    /// <summary>Creates the exception with a message naming the rule the stream broke.</summary>
    public BrotliFormatException(string message)
        : base(message)
    {
    }
}
