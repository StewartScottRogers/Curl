namespace Curl.Http2;

/// <summary>
/// Thrown when an HPACK header block, or a Huffman-coded string in one, cannot be decoded.
/// <see cref="Error" /> says which rule of RFC 7541 the input broke.
/// </summary>
public sealed class HpackDecodingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HpackDecodingException" /> class.
    /// </summary>
    /// <param name="error">The rule the input broke.</param>
    public HpackDecodingException(HpackDecodingError error)
        : base($"HPACK decoding failed: {error}.")
    {
        Error = error;
    }

    /// <summary>
    /// Gets the rule of RFC 7541 the input broke.
    /// </summary>
    public HpackDecodingError Error { get; }
}
