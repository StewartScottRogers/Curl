using System.Buffers;
using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// The <c>quoted-printable</c> encoding as libcurl 8.21.0's <c>encoder_qp_read</c> sends it:
/// printable ASCII other than <c>=</c> is sent as it is, a CRLF pair as a line break, a space
/// or tab as it is unless a line break or the end of the data follows it, and anything else as
/// <c>=XX</c>; a line that would pass 76 columns ends with the soft break <c>=</c> CRLF first.
/// </summary>
internal sealed class QuotedPrintableDataEncoding : MultipartDataEncoding
{
    private const int MaximumLineLength = 76;

    /// <summary>
    /// The bytes a piece's rules look at, from its first: the piece itself, and whether a CRLF
    /// pair follows it.
    /// </summary>
    private const int LookAhead = 4;

    private const byte CarriageReturn = 0x0D;

    private const byte LineFeed = 0x0A;

    private int column;

    /// <inheritdoc />
    internal override bool TryEncode(ReadOnlySpan<byte> data, bool isFinal, IBufferWriter<byte> output, out int consumed)
    {
        int end = isFinal ? data.Length : data.Length - LookAhead;
        int index = 0;
        while (index < end)
        {
            (string piece, int pieceConsumed) = NextPiece(data, index);
            if (piece[^1] != '\n' && NeedsSoftLineBreak(data, index + pieceConsumed, column + piece.Length))
            {
                (piece, pieceConsumed) = ("=\r\n", 0);
            }

            Encoding.ASCII.GetBytes(piece, output);
            column = piece[^1] == '\n' ? 0 : column + piece.Length;
            index += pieceConsumed;
        }

        consumed = index;
        return true;
    }

    private static (string Piece, int Consumed) NextPiece(ReadOnlySpan<byte> data, int index)
    {
        byte value = data[index];
        if (IsPrintable(value) || (IsSpaceOrTab(value) && !IsLineEnd(data, index + 1)))
        {
            return (((char)value).ToString(), 1);
        }

        return value == CarriageReturn && IsLineEnd(data, index) ? ("\r\n", 2) : ($"={value:X2}", 1);
    }

    private static bool IsPrintable(byte value) => value is >= 0x21 and <= 0x7E and not (byte)'=';

    private static bool IsSpaceOrTab(byte value) => value is (byte)' ' or (byte)'\t';

    private static bool NeedsSoftLineBreak(ReadOnlySpan<byte> data, int next, int columnAfter) =>
        columnAfter > MaximumLineLength || (columnAfter == MaximumLineLength && !IsLineEnd(data, next));

    /// <summary>Tells whether a CRLF pair or the end of the data is at <paramref name="index" />.</summary>
    private static bool IsLineEnd(ReadOnlySpan<byte> data, int index) =>
        index >= data.Length
        || (index + 1 < data.Length && data[index] == CarriageReturn && data[index + 1] == LineFeed);
}
