namespace Curl.Http3;

/// <summary>
/// The HTTP/3 application error codes QPACK defines (RFC 9204 section 6), each a
/// connection error.
/// </summary>
public enum QpackErrorCode : long
{
    /// <summary>
    /// <c>QPACK_DECOMPRESSION_FAILED</c> (<c>0x0200</c>): an encoded field section could not
    /// be decoded.
    /// </summary>
    DecompressionFailed = 0x0200,

    /// <summary>
    /// <c>QPACK_ENCODER_STREAM_ERROR</c> (<c>0x0201</c>): an instruction on the encoder
    /// stream could not be interpreted.
    /// </summary>
    EncoderStreamError = 0x0201,

    /// <summary>
    /// <c>QPACK_DECODER_STREAM_ERROR</c> (<c>0x0202</c>): an instruction on the decoder
    /// stream could not be interpreted.
    /// </summary>
    DecoderStreamError = 0x0202,
}
