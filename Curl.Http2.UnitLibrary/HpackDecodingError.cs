namespace Curl.Http2;

/// <summary>
/// Why an HPACK header block could not be decoded. Each is a compression error in the
/// sense of RFC 9113 section 4.3: the connection cannot go on.
/// </summary>
public enum HpackDecodingError
{
    /// <summary>The block ends in the middle of an integer, a string or a representation.</summary>
    TruncatedBlock,

    /// <summary>An integer does not fit in 31 bits (RFC 7541 section 5.1).</summary>
    IntegerOverflow,

    /// <summary>An index is zero or beyond the static and dynamic tables (RFC 7541 section 2.3.3).</summary>
    InvalidIndex,

    /// <summary>A dynamic table size update exceeds the limit the decoder allows (RFC 7541 section 6.3).</summary>
    TableSizeUpdateTooLarge,

    /// <summary>A dynamic table size update follows a header field in the block (RFC 7541 section 4.2).</summary>
    TableSizeUpdateNotAtStart,

    /// <summary>
    /// The decoder's limit was lowered and the next block did not start with a dynamic
    /// table size update bringing the table within it (RFC 7541 section 4.2).
    /// </summary>
    TableSizeUpdateMissing,

    /// <summary>
    /// A Huffman-coded string ends in more than seven bits of padding, or in padding that is
    /// not the most significant bits of the EOS code (RFC 7541 section 5.2).
    /// </summary>
    InvalidHuffmanPadding,

    /// <summary>A Huffman-coded string contains the EOS symbol (RFC 7541 section 5.2).</summary>
    HuffmanEndOfStringInData,
}
