using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// One of the five transfer encoders a <c>-F</c> part can name with <c>;encoder=</c>, as
/// libcurl 8.21.0's <c>encoders[]</c> in <c>lib/mime.c</c> defines them: <c>binary</c> and
/// <c>8bit</c> send the data as it is, <c>7bit</c> sends it as it is but refuses a byte above
/// 127, and <c>base64</c> and <c>quoted-printable</c> encode it in 76-column CRLF lines.
/// </summary>
internal sealed class MultipartPartEncoder
{
    private const int MaximumLineLength = 76;

    private const byte CarriageReturn = 0x0D;

    private const byte LineFeed = 0x0A;

    private static readonly MultipartPartEncoder[] Encoders =
    [
        new("binary", data => data, passesDataThrough: true, knowsEncodedLengthBeforehand: true),
        new("8bit", data => data, passesDataThrough: true, knowsEncodedLengthBeforehand: true),
        new("7bit", RefuseEightBitData, passesDataThrough: false, knowsEncodedLengthBeforehand: true),
        new("base64", EncodeBase64, passesDataThrough: false, knowsEncodedLengthBeforehand: true),
        new("quoted-printable", EncodeQuotedPrintable, passesDataThrough: false, knowsEncodedLengthBeforehand: false),
    ];

    private readonly Func<byte[], byte[]?> encode;

    private readonly bool knowsEncodedLengthBeforehand;

    private MultipartPartEncoder(string name, Func<byte[], byte[]?> encode, bool passesDataThrough, bool knowsEncodedLengthBeforehand)
    {
        Name = name;
        this.encode = encode;
        PassesDataThrough = passesDataThrough;
        this.knowsEncodedLengthBeforehand = knowsEncodedLengthBeforehand;
    }

    /// <summary>Gets the encoder's name as curl sends it in <c>Content-Transfer-Encoding</c>: lower case.</summary>
    internal string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the encoder sends every byte as it is, so a file part
    /// using it can be streamed rather than read whole.
    /// </summary>
    internal bool PassesDataThrough { get; }

    /// <summary>Finds the encoder named <paramref name="name" />, compared without regard to case as curl compares.</summary>
    /// <param name="name">The <c>;encoder=</c> value.</param>
    /// <returns>The encoder, or <see langword="null" /> when curl has none by that name.</returns>
    internal static MultipartPartEncoder? Find(string name) =>
        Array.Find(Encoders, encoder => encoder.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Encodes <paramref name="data" />.</summary>
    /// <param name="data">The part's data.</param>
    /// <returns>The encoded bytes, or <see langword="null" /> when the encoder cannot carry the data.</returns>
    internal byte[]? Encode(byte[] data) => encode(data);

    /// <summary>
    /// Tells whether curl knows the encoded size before it sends: always, when the data's own
    /// size is known, except for <c>quoted-printable</c>, whose size curl learns only by encoding
    /// and so leaves unknown for anything but empty data.
    /// </summary>
    /// <param name="dataLength">The data's size in bytes, or <see langword="null" /> when unknown.</param>
    /// <returns><see langword="true" /> when the encoded size is known beforehand.</returns>
    internal bool KnowsEncodedLength(long? dataLength) =>
        dataLength is { } length && (knowsEncodedLengthBeforehand || length == 0);

    private static byte[]? RefuseEightBitData(byte[] data) =>
        data.AsSpan().IndexOfAnyInRange((byte)0x80, (byte)0xFF) < 0 ? data : null;

    private static byte[] EncodeBase64(byte[] data) =>
        Encoding.ASCII.GetBytes(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks));

    /// <summary>
    /// Encodes as libcurl 8.21.0's <c>encoder_qp_read</c> does: printable ASCII other than
    /// <c>=</c> is sent as it is, a CRLF pair as a line break, a space or tab as it is unless a
    /// line break or the end of the data follows it, and anything else as <c>=XX</c>; a line
    /// that would pass 76 columns ends with the soft break <c>=</c> CRLF first.
    /// </summary>
    private static byte[] EncodeQuotedPrintable(byte[] data)
    {
        StringBuilder encoded = new(data.Length);
        int column = 0;
        int index = 0;
        while (index < data.Length)
        {
            (string piece, int consumed) = NextQuotedPrintablePiece(data, index);
            if (piece[^1] != '\n' && NeedsSoftLineBreak(data, index + consumed, column + piece.Length))
            {
                (piece, consumed) = ("=\r\n", 0);
            }

            encoded.Append(piece);
            column = piece[^1] == '\n' ? 0 : column + piece.Length;
            index += consumed;
        }

        return Encoding.ASCII.GetBytes(encoded.ToString());
    }

    private static (string Piece, int Consumed) NextQuotedPrintablePiece(byte[] data, int index)
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

    private static bool NeedsSoftLineBreak(byte[] data, int next, int columnAfter) =>
        columnAfter > MaximumLineLength || (columnAfter == MaximumLineLength && !IsLineEnd(data, next));

    /// <summary>Tells whether a CRLF pair or the end of the data is at <paramref name="index" />.</summary>
    private static bool IsLineEnd(byte[] data, int index) =>
        index >= data.Length
        || (index + 1 < data.Length && data[index] == CarriageReturn && data[index + 1] == LineFeed);
}
