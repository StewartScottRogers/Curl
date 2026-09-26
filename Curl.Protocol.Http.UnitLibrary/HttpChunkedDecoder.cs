using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Decodes a <c>Transfer-Encoding: chunked</c> body byte by byte, however its bytes are
/// split across reads, accepting and refusing the same framing curl 8.21.0 does (measured,
/// BL-171).
/// </summary>
/// <remarks>
/// A chunk size is 1 to <see cref="MaximumSizeDigits" /> hexadecimal digits; whatever
/// follows them up to the line feed, a chunk extension or a stray carriage return, is
/// ignored. After a chunk's data any number of carriage returns may come before its line
/// feed, and nothing else. After the last chunk, each trailer line is kept for header output
/// with a carriage return and line feed, whichever ending it arrived with, until an empty
/// line ends the body; bytes after that are never read.
/// </remarks>
internal sealed class HttpChunkedDecoder
{
    /// <summary>
    /// The most hexadecimal digits a chunk size may have; one more is exit 56.
    /// </summary>
    internal const int MaximumSizeDigits = 16;

    /// <summary>
    /// The length, carriage return and line feed included, at which curl 8.21.0 rejects a
    /// trailer line with exit 100: 4093 bytes before the line ending are accepted and 4094
    /// are not (measured).
    /// </summary>
    internal const int MaximumTrailerLineLength = 4096;

    private static readonly byte[] LineEnding = [(byte)'\r', (byte)'\n'];

    private readonly StringBuilder sizeDigits = new(MaximumSizeDigits);

    private readonly MemoryStream trailerLine = new();

    private readonly MemoryStream trailers = new();

    private State state;

    private long remaining;

    private enum State
    {
        Size,
        SizeLine,
        Data,
        DataEnd,
        Trailer,
        TrailerCarriageReturn,
        Complete,
    }

    /// <summary>
    /// Gets a value indicating whether the empty line after the trailers has arrived, so
    /// the body is whole.
    /// </summary>
    internal bool IsComplete => state == State.Complete;

    /// <summary>
    /// Gets every trailer line decoded so far, each ending in a carriage return and line
    /// feed, as curl 8.21.0 writes them after the body for <c>-i</c> and <c>-D</c>. The empty
    /// line that ends the trailers is not included.
    /// </summary>
    internal ReadOnlyMemory<byte> TrailerBytes => trailers.GetBuffer().AsMemory(0, (int)trailers.Length);

    /// <summary>
    /// Decodes <paramref name="input" /> up to the end of the next run of chunk data, or to
    /// its end, or to the end of the body.
    /// </summary>
    /// <param name="input">The bytes to decode.</param>
    /// <param name="consumed">How many bytes of <paramref name="input" /> were decoded.</param>
    /// <returns>The chunk data found, a slice of <paramref name="input" />; empty when none.</returns>
    /// <exception cref="HttpTransferException">
    /// A chunk size is malformed or too large, or a line ending is malformed (exit 56), a
    /// trailer line has no colon (exit 8), or a trailer line is too long (exit 100).
    /// </exception>
    internal ReadOnlyMemory<byte> DecodeNext(ReadOnlyMemory<byte> input, out int consumed)
    {
        consumed = 0;
        while (consumed < input.Length && state != State.Complete)
        {
            if (state == State.Data)
            {
                int length = (int)Math.Min(remaining, input.Length - consumed);
                ReadOnlyMemory<byte> data = input.Slice(consumed, length);
                consumed += length;
                remaining -= length;
                state = remaining == 0 ? State.DataEnd : State.Data;
                return data;
            }

            Accept(input.Span[consumed]);
            consumed++;
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    private static HttpTransferException Malformed(string message) =>
        new(CurlExitCode.RecvError, message);

    private void Accept(byte value)
    {
        switch (state)
        {
            case State.Size:
                AcceptSize(value);
                break;
            case State.SizeLine:
                AcceptSizeLine(value);
                break;
            case State.DataEnd:
                AcceptDataEnd(value);
                break;
            case State.Trailer:
                AcceptTrailer(value);
                break;
            default:
                AcceptTrailerCarriageReturn(value);
                break;
        }
    }

    private void AcceptSize(byte value)
    {
        if (char.IsAsciiHexDigit((char)value))
        {
            if (sizeDigits.Length == MaximumSizeDigits)
            {
                throw Malformed(HttpTransferMessages.ChunkSizeTooLong);
            }

            sizeDigits.Append((char)value);
            return;
        }

        if (sizeDigits.Length == 0)
        {
            throw Malformed(HttpTransferMessages.ChunkSizeNotHex(value));
        }

        // Sixteen digits always fit 64 bits; a size past long.MaxValue parses negative.
        string digits = sizeDigits.ToString();
        remaining = long.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (remaining < 0)
        {
            throw Malformed(HttpTransferMessages.InvalidChunkSize(digits));
        }

        sizeDigits.Clear();
        state = State.SizeLine;
        AcceptSizeLine(value);
    }

    private void AcceptSizeLine(byte value)
    {
        if (value == '\n')
        {
            state = remaining == 0 ? State.Trailer : State.Data;
        }
    }

    private void AcceptDataEnd(byte value)
    {
        if (value == '\n')
        {
            state = State.Size;
        }
        else if (value != '\r')
        {
            throw Malformed(HttpTransferMessages.MalformedChunkedEncoding);
        }
    }

    private void AcceptTrailer(byte value)
    {
        if (value == '\r')
        {
            state = State.TrailerCarriageReturn;
        }
        else if (value == '\n')
        {
            EndTrailerLine();
        }
        else
        {
            AppendToTrailerLine(value);
        }
    }

    private void AcceptTrailerCarriageReturn(byte value)
    {
        if (value != '\n')
        {
            throw Malformed(HttpTransferMessages.MalformedChunkedEncoding);
        }

        state = State.Trailer;
        EndTrailerLine();
    }

    private void AppendToTrailerLine(byte value)
    {
        if (trailerLine.Length + 1 >= MaximumTrailerLineLength)
        {
            throw new HttpTransferException(CurlExitCode.TooLarge, HttpTransferMessages.TrailerTooLarge);
        }

        trailerLine.WriteByte(value);
    }

    /// <summary>
    /// Ends a trailer line: the empty line ends the body, and any other line is kept for
    /// header output.
    /// </summary>
    private void EndTrailerLine()
    {
        if (trailerLine.Length == 0)
        {
            state = State.Complete;
            return;
        }

        if (trailerLine.Length + LineEnding.Length >= MaximumTrailerLineLength)
        {
            throw new HttpTransferException(CurlExitCode.TooLarge, HttpTransferMessages.TrailerTooLarge);
        }

        if (Array.IndexOf(trailerLine.GetBuffer(), (byte)':', 0, (int)trailerLine.Length) < 0)
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.HeaderWithoutColon);
        }

        trailerLine.WriteTo(trailers);
        trailers.Write(LineEnding);
        trailerLine.SetLength(0);
    }
}
