using System.Buffers;
using System.Buffers.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// The <c>base64</c> encoding as libcurl 8.21.0's <c>encoder_base64_read</c> sends it: 76-column
/// lines, each the encoding of 57 data bytes, separated by CRLF, with no CRLF after the last.
/// </summary>
internal sealed class Base64DataEncoding : MultipartDataEncoding
{
    /// <summary>The data bytes one 76-column line encodes.</summary>
    internal const int BytesPerLine = 57;

    private const int CharactersPerLine = 76;

    private static readonly byte[] LineBreak = "\r\n"u8.ToArray();

    private bool wroteLine;

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
        consumed = isFinal ? data.Length : data.Length - (data.Length % BytesPerLine);
        for (int start = 0; start < consumed; start += BytesPerLine)
        {
            WriteLine(data.Slice(start, Math.Min(BytesPerLine, consumed - start)), output);
        }

        return true;
    }

    private void WriteLine(ReadOnlySpan<byte> line, IBufferWriter<byte> output)
    {
        if (wroteLine)
        {
            output.Write(LineBreak);
        }

        Base64.EncodeToUtf8(line, output.GetSpan(CharactersPerLine), out _, out int written);
        output.Advance(written);
        wroteLine = true;
    }
}
