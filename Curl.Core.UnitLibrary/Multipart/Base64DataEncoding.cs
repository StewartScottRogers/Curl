using System.Buffers;
using System.Buffers.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// The <c>base64</c> encoding as libcurl 8.22.0's <c>encoder_base64_read</c> sends it: 76-column
/// lines, each the encoding of 57 data bytes, separated by CRLF, with no CRLF after the last.
/// Every complete 3-byte group is encoded as soon as it arrives, so data a read callback gives a
/// few bytes at a time is sent a group at a time, as curl sends it (upstream test667, BL-2630).
/// </summary>
internal sealed class Base64DataEncoding : MultipartDataEncoding
{
    /// <summary>The data bytes one 76-column line encodes.</summary>
    internal const int BytesPerLine = 57;

    private const int CharactersPerLine = 76;

    private const int BytesPerGroup = 3;

    private const int CharactersPerGroup = 4;

    private static readonly byte[] LineBreak = "\r\n"u8.ToArray();

    private int column;

    /// <summary>Gives the encoded size of <paramref name="dataLength" /> bytes of data.</summary>
    /// <param name="dataLength">The data's size in bytes.</param>
    /// <returns>The size of its encoding in bytes, line breaks included.</returns>
    internal static long EncodedLength(long dataLength)
    {
        long lines = (dataLength + BytesPerLine - 1) / BytesPerLine;
        return ((dataLength + 2) / 3 * 4) + (2 * Math.Max(0, lines - 1));
    }

    /// <inheritdoc />
    internal override bool TryEncode(ReadOnlySpan<byte> data, bool isFinal, IBufferWriter<byte> output, out int consumed)
    {
        consumed = isFinal ? data.Length : data.Length - (data.Length % BytesPerGroup);
        for (int start = 0; start < consumed;)
        {
            start += WriteRestOfLine(data[start..consumed], output);
        }

        return true;
    }

    /// <summary>
    /// Writes as much of <paramref name="data" /> as the current line holds, after a line break when the line
    /// before is full, and returns the data bytes it wrote.
    /// </summary>
    private int WriteRestOfLine(ReadOnlySpan<byte> data, IBufferWriter<byte> output)
    {
        if (column == CharactersPerLine)
        {
            output.Write(LineBreak);
            column = 0;
        }

        int count = Math.Min(data.Length, (CharactersPerLine - column) / CharactersPerGroup * BytesPerGroup);
        Base64.EncodeToUtf8(data[..count], output.GetSpan(CharactersPerLine), out _, out int written);
        output.Advance(written);
        column += written;
        return count;
    }
}
