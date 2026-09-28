using System.Buffers;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Turns the multi-line answer to <c>LIST</c> or <c>RETR</c> into the bytes curl 8.21.0 writes,
/// a received chunk at a time, as its <c>pop3_write</c> does: the doubled dot of a stuffed line
/// is dropped, and the terminating <c>CRLF.CRLF</c> ends the body with its first CRLF written.
/// </summary>
/// <remarks>
/// Measured in BL-549 and kept exactly, oddities included: only CRLF line ends count, so a
/// body with LF-only ends is written through; the terminator ends the body only when it ends
/// a received chunk, so bytes after it in the same chunk make it part of the body; a line
/// <c>.x</c> is written as it came; and an empty message, whose body is the terminator alone,
/// writes CRLF. Bytes that might begin the terminator are held back until they cannot, so a
/// body cut off by the server closing loses them.
/// </remarks>
internal sealed class Pop3BodyDecoder
{
    private const int CarriageReturnMatched = 1;

    private const int LineEndMatched = 2;

    private const int DotMatched = 3;

    private const int SecondCarriageReturnMatched = 4;

    private static ReadOnlySpan<byte> EndOfBody => "\r\n.\r\n"u8;

    /// <summary>
    /// How many bytes of <see cref="EndOfBody" /> the bytes received last match. The body
    /// starts as if just after a CRLF, so a stuffed first line is recognised.
    /// </summary>
    private int matched = LineEndMatched;

    /// <summary>How many bytes of that assumed CRLF are still to be kept out of the output.</summary>
    private int unwrittenStart = LineEndMatched;

    /// <summary>
    /// Decodes one received chunk, writing to <paramref name="output" /> what curl writes for it.
    /// </summary>
    /// <param name="chunk">The bytes one read returned.</param>
    /// <param name="output">Receives the body bytes.</param>
    /// <returns><see langword="true" /> when the chunk ended with the terminator, which ends the body.</returns>
    public bool Decode(ReadOnlySpan<byte> chunk, IBufferWriter<byte> output)
    {
        int written = 0;
        for (int index = 0; index < chunk.Length; index++)
        {
            written = DecodeByte(chunk, index, written, output);
        }

        return Finish(chunk[written..], output);
    }

    /// <summary>
    /// Takes the byte at <paramref name="index" />, writing what it lets go of.
    /// </summary>
    /// <returns>Where the bytes still to be written start.</returns>
    private int DecodeByte(ReadOnlySpan<byte> chunk, int index, int written, IBufferWriter<byte> output)
    {
        int previous = matched;
        if (chunk[index] == (byte)'\r' && matched == 0)
        {
            output.Write(chunk[written..index]);
            written = index;
        }

        bool dotStripped = Advance(chunk[index]);
        return previous > 0 && previous >= matched && WriteAbandonedMatch(previous, dotStripped, output)
            ? index
            : written;
    }

    /// <summary>
    /// Ends the chunk: the terminator ends the body with its first CRLF written; otherwise the
    /// rest is written unless it might begin the terminator.
    /// </summary>
    private bool Finish(ReadOnlySpan<byte> rest, IBufferWriter<byte> output)
    {
        if (matched == EndOfBody.Length)
        {
            output.Write(EndOfBody[..LineEndMatched]);
            matched = 0;
            return true;
        }

        if (matched == 0)
        {
            output.Write(rest);
        }

        return false;
    }

    /// <summary>
    /// Moves the match on by one byte.
    /// </summary>
    /// <returns><see langword="true" /> when the byte was the second dot of a stuffed line.</returns>
    private bool Advance(byte next)
    {
        bool dotStripped = false;
        matched = next switch
        {
            (byte)'\r' => matched == DotMatched ? SecondCarriageReturnMatched : CarriageReturnMatched,
            (byte)'\n' => AfterLineFeed(),
            (byte)'.' => AfterDot(out dotStripped),
            _ => 0,
        };
        return dotStripped;
    }

    private int AfterLineFeed() =>
        matched is CarriageReturnMatched or SecondCarriageReturnMatched ? matched + 1 : 0;

    private int AfterDot(out bool dotStripped)
    {
        dotStripped = matched == DotMatched;
        return matched == LineEndMatched ? DotMatched : 0;
    }

    /// <summary>
    /// Writes the bytes a match that has just failed was holding back: none of the assumed
    /// CRLF at the start, and not the dot of a stuffed line.
    /// </summary>
    /// <returns><see langword="true" /> when something was held back, so writing resumes at the current byte.</returns>
    private bool WriteAbandonedMatch(int previous, bool dotStripped, IBufferWriter<byte> output)
    {
        int skipped = Math.Min(previous, unwrittenStart);
        previous -= skipped;
        unwrittenStart -= skipped;
        if (previous == 0)
        {
            return false;
        }

        output.Write(EndOfBody[..(dotStripped ? previous - 1 : previous)]);
        return true;
    }
}
