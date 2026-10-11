using System.Buffers;

namespace Curl.Core.Multipart;

/// <summary>
/// The <c>binary</c>, <c>8bit</c> and <c>7bit</c> encodings, which send the data as it is;
/// <c>7bit</c> refuses a byte above 127, as libcurl 8.21.0's <c>encoder_7bit_read</c> does,
/// after writing the bytes before it, which curl sends before it fails (measured, BL-2025).
/// </summary>
/// <param name="refusesEightBitData">Whether a byte above 127 is refused.</param>
internal sealed class PassThroughDataEncoding(bool refusesEightBitData) : MultipartDataEncoding
{
    /// <inheritdoc />
    internal override bool TryEncode(ReadOnlySpan<byte> data, bool isFinal, IBufferWriter<byte> output, out int consumed)
    {
        int refused = refusesEightBitData ? data.IndexOfAnyInRange((byte)0x80, (byte)0xFF) : -1;
        consumed = refused < 0 ? data.Length : refused;
        output.Write(data[..consumed]);
        return refused < 0;
    }
}
