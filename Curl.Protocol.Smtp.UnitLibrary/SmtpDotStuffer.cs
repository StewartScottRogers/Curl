namespace Curl.Protocol.Smtp;

/// <summary>
/// Turns an upload into the bytes sent after <c>DATA</c>: every byte as it is, a <c>.</c>
/// doubled where it starts a line, and the end-of-data mark after the last byte, as curl
/// 8.21.0 sends them (measured, BL-542).
/// </summary>
/// <remarks>
/// A line starts at the first byte of the message and after each CRLF; a bare LF or a bare
/// CR starts none, and no line ending is rewritten, so <c>a\nb</c> goes out as it is. The
/// end-of-data mark is <c>.\r\n</c> when the message ends in CRLF or is empty, and
/// <c>\r\n.\r\n</c> otherwise. The state carries over from one chunk to the next, so the
/// upload can be encoded as it is read.
/// <para>
/// Under <c>--crlf</c> (<paramref name="convertsLineFeeds" />) a carriage return is first
/// inserted before each line feed that does not already follow one, as curl 8.21.0 converts
/// an SMTP upload before stuffing it (upstream test941, BL-1994), so <c>a\n.b</c> goes out as
/// <c>a\r\n..b</c>; the byte before carries across chunks here too.
/// </para>
/// </remarks>
/// <param name="convertsLineFeeds">Whether <c>--crlf</c> converts each bare line feed to CRLF.</param>
internal sealed class SmtpDotStuffer(bool convertsLineFeeds = false)
{
    private const byte Dot = (byte)'.';

    private const byte CarriageReturn = (byte)'\r';

    private const byte LineFeed = (byte)'\n';

    private static readonly byte[] EndAtLineStart = ".\r\n"u8.ToArray();

    private static readonly byte[] EndMidLine = "\r\n.\r\n"u8.ToArray();

    private bool afterCarriageReturn;

    private bool atLineStart = true;

    /// <summary>
    /// Gets the end-of-data mark for the message encoded so far.
    /// </summary>
    public byte[] EndOfData => atLineStart ? EndAtLineStart : EndMidLine;

    /// <summary>
    /// Encodes the next chunk of the message.
    /// </summary>
    /// <param name="chunk">The bytes read from the upload.</param>
    /// <returns>The bytes to send for <paramref name="chunk" />, each line-starting <c>.</c> doubled.</returns>
    public byte[] Encode(ReadOnlySpan<byte> chunk)
    {
        var encoded = new List<byte>(chunk.Length + 8);
        foreach (byte next in chunk)
        {
            if (convertsLineFeeds && next == LineFeed && !afterCarriageReturn)
            {
                Add(encoded, CarriageReturn);
            }

            Add(encoded, next);
        }

        return [.. encoded];
    }

    /// <summary>Adds one byte of the message to <paramref name="encoded" />, its dot doubled at a line start.</summary>
    private void Add(List<byte> encoded, byte next)
    {
        if (atLineStart && next == Dot)
        {
            encoded.Add(Dot);
        }

        atLineStart = afterCarriageReturn && next == LineFeed;
        afterCarriageReturn = next == CarriageReturn;
        encoded.Add(next);
    }
}
