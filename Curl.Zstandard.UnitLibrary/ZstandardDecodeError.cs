namespace Curl.Zstandard;

/// <summary>
/// Why <see cref="ZstandardDecoder.Decompress" /> last returned
/// <see cref="System.Buffers.OperationStatus.InvalidData" />: one member per libzstd error
/// code the decoder can raise, named after it, so a caller can map each to the text curl
/// prints for it.
/// </summary>
public enum ZstandardDecodeError
{
    /// <summary>No error: the decoder has not failed.</summary>
    None,

    /// <summary>The input starts with neither the Zstandard magic nor a skippable-frame magic (libzstd <c>prefix_unknown</c>).</summary>
    PrefixUnknown,

    /// <summary>The frame header sets its reserved bit (libzstd <c>frameParameter_unsupported</c>).</summary>
    FrameParameterUnsupported,

    /// <summary>The frame's window is larger than <c>2^maxWindowLog</c> (libzstd <c>frameParameter_windowTooLarge</c>).</summary>
    FrameParameterWindowTooLarge,

    /// <summary>The frame names a <c>Dictionary_ID</c> other than 0, and no dictionary is loaded (libzstd <c>dictionary_wrong</c>).</summary>
    DictionaryWrong,

    /// <summary>The frame's <c>Content_Checksum</c> does not match its decoded content (libzstd <c>checksum_wrong</c>).</summary>
    ChecksumWrong,

    /// <summary>
    /// The frame breaks RFC 8878 in its blocks: a reserved block type, a block larger than
    /// <c>Block_Maximum_Size</c>, content whose size differs from the declared
    /// <c>Frame_Content_Size</c>, a literals section that runs past its block or regenerates
    /// more than <c>Block_Maximum_Size</c>, a Huffman tree or stream that does not decode,
    /// treeless literals with no earlier tree, a sequences section whose tables or bitstream
    /// do not decode or leave bits over, a repeated table the frame has not had, or a sequence
    /// that takes more literals than there are, copies from before the frame's content or
    /// further back than its window, or regenerates past <c>Block_Maximum_Size</c> (libzstd
    /// <c>corruption_detected</c>).
    /// </summary>
    CorruptionDetected,

    /// <summary>
    /// A literals section splits fewer than 6 literals into four Huffman streams (libzstd
    /// <c>literals_headerWrong</c>).
    /// </summary>
    LiteralsHeaderWrong,
}
