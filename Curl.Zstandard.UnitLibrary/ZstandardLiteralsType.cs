namespace Curl.Zstandard;

/// <summary>A literals section's <c>Literals_Block_Type</c> (RFC 8878 section 3.1.1.3.1.1).</summary>
internal enum ZstandardLiteralsType
{
    /// <summary>The literals are stored as they are.</summary>
    Raw,

    /// <summary>One byte, repeated <c>Regenerated_Size</c> times.</summary>
    Rle,

    /// <summary>Huffman-coded, with a Huffman tree description first.</summary>
    Compressed,

    /// <summary>Huffman-coded with the previous compressed literals' tree.</summary>
    Treeless,
}
