using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Sends tagged commands on an IMAP connection and reads their responses, a line at a time.
/// </summary>
/// <param name="connection">The connection; the caller owns and disposes it.</param>
/// <param name="cancellationToken">Cancels every send and read.</param>
/// <remarks>
/// <para>
/// Commands and responses are Latin-1. As curl 8.21.0 does (<c>lib/imap.c</c>, measured in
/// BL-553): each command is tagged <c>A</c> and a three-digit number that starts at
/// <c>001</c> and wraps from <c>255</c> to <c>000</c>, curl counting in an unsigned char; the
/// greeting is read as the response tagged <c>*</c>; a line ends at LF, with a CR before it
/// kept; a line starting with the tag and a space completes the response; a line starting
/// <c>* </c> is kept when the caller is interested in it and skipped otherwise; a
/// continuation (<c>+ </c>, or <c>+</c> and one character) is refused unless
/// <c>AUTHENTICATE</c> is waiting for one; and any other line
/// is skipped.
/// </para>
/// <para>
/// A kept untagged line ending in a literal, <c>{n}</c>, is followed by its n bytes and the
/// rest of the response, however the reads split them. curl reads a literal only in the
/// response it is waiting for; the others it reads as lines, and so does this channel.
/// </para>
/// </remarks>
internal sealed class ImapControlChannel(IConnection connection, CancellationToken cancellationToken)
{
    private const int ReadBufferSize = 4096;

    /// <summary>
    /// The most bytes a response line may hold before its LF, literals included, so that
    /// the line with its LF is at most 65535 bytes: one byte more fails with exit 100, as
    /// curl's pingpong reader does for every line-based protocol.
    /// </summary>
    private const int MaxLineBytes = 65535;

    /// <summary>The most digits a literal's length is read from; a longer one is no literal.</summary>
    private const int MaxLiteralLengthDigits = 9;

    private readonly byte[] buffer = new byte[ReadBufferSize];

    private int bufferStart;

    private int bufferEnd;

    private byte commandId;

    /// <summary>
    /// Gets the connection commands are sent on and responses read from: the one the channel
    /// was built with until <see cref="SwitchTo(IConnection)" />.
    /// </summary>
    public IConnection Connection => connection;

    /// <summary>
    /// Gets the tag the next response is completed by: <c>*</c> for the greeting, then the
    /// tag of the last command sent.
    /// </summary>
    public string Tag { get; private set; } = "*";

    /// <summary>
    /// Gets a value indicating whether bytes the server sent after the last complete
    /// response are already read and waiting, as curl's "pipelining" check after
    /// <c>STARTTLS</c> asks.
    /// </summary>
    public bool HasUnreadBytes => bufferStart < bufferEnd;

    /// <summary>
    /// Carries on over <paramref name="secured" />, the connection after <c>STARTTLS</c>
    /// upgraded it to TLS.
    /// </summary>
    /// <param name="secured">The upgraded connection; the caller owns it.</param>
    public void SwitchTo(IConnection secured) => connection = secured;

    /// <summary>
    /// Tags <paramref name="command" /> with the next tag and sends it followed by CRLF. A
    /// connection that fails with an <see cref="IOException" /> is left for the next
    /// <see cref="ReadResponseAsync" /> to find closed.
    /// </summary>
    /// <param name="command">The command without its tag or line end, such as <c>CAPABILITY</c>.</param>
    /// <returns>A task that completes once the command is sent or the send has failed.</returns>
    public ValueTask SendCommandAsync(string command)
    {
        commandId = unchecked((byte)(commandId + 1));
        Tag = string.Create(CultureInfo.InvariantCulture, $"A{commandId:D3}");
        return SendLineAsync(Tag + " " + command);
    }

    /// <summary>
    /// Sends <paramref name="text" /> untagged, followed by CRLF: an answer to an
    /// <c>AUTHENTICATE</c> continuation. A connection that fails with an
    /// <see cref="IOException" /> is left for the next <see cref="ReadResponseAsync" /> to
    /// find closed.
    /// </summary>
    /// <param name="text">The line without its line end.</param>
    /// <returns>A task that completes once the line is sent or the send has failed.</returns>
    public async ValueTask SendLineAsync(string text)
    {
        byte[] line = Encoding.Latin1.GetBytes(text + "\r\n");
        try
        {
            await connection.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// Reads up to and including the line completing the response tagged <see cref="Tag" />.
    /// </summary>
    /// <param name="isWanted">
    /// Whether an untagged line, starting <c>* </c>, belongs to the command waiting and is kept.
    /// </param>
    /// <param name="acceptsContinuation">
    /// Whether a continuation ends the read, as one does while <c>AUTHENTICATE</c> waits:
    /// it is returned as an <see cref="ImapResponseStatus.Continuation" /> response whose one
    /// untagged entry is the continuation line.
    /// </param>
    /// <returns>
    /// The response, or <see langword="null" /> when the server closed the connection or a
    /// read failed with an <see cref="IOException" /> before it was complete.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// A line reached 65536 bytes, its literals and line end included (exit 100).
    /// </exception>
    /// <exception cref="ImapWeirdResponseException">
    /// A line held a NUL byte, or was a continuation not accepted (exit 8).
    /// </exception>
    public async ValueTask<ImapResponse?> ReadResponseAsync(Func<string, bool> isWanted, bool acceptsContinuation = false)
    {
        var untagged = new List<string>();
        while (await ReadLineAsync(0).ConfigureAwait(false) is { } line)
        {
            if (line.StartsWith(Tag + " ", StringComparison.Ordinal))
            {
                return new ImapResponse(StatusOf(line.AsSpan(Tag.Length + 1)), untagged);
            }

            if (line.StartsWith("* ", StringComparison.Ordinal))
            {
                if (isWanted(line) && await ReadLiteralsAsync(line).ConfigureAwait(false) is { } response)
                {
                    untagged.Add(response);
                }
            }
            else if (IsContinuation(line))
            {
                return acceptsContinuation
                    ? new ImapResponse(ImapResponseStatus.Continuation, [line])
                    : throw new ImapWeirdResponseException(ImapSessionMessages.UnexpectedContinuation);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads until the first untagged line <paramref name="isWanted" /> accepts, or the line
    /// completing the response tagged <see cref="Tag" />, whichever comes first. A literal
    /// the untagged line announces is left unread for <see cref="ReadLiteralPieceAsync(long)" />,
    /// as curl leaves a <c>FETCH</c> body.
    /// </summary>
    /// <param name="isWanted">Whether an untagged line, starting <c>* </c>, is the one waited for.</param>
    /// <returns>
    /// An <see cref="ImapResponseStatus.Untagged" /> response whose one entry is the untagged
    /// line without its LF, or, when the response completed first, its completion with no
    /// untagged entries.
    /// </returns>
    /// <exception cref="ImapResponseMissingException">The server closed the connection first.</exception>
    /// <exception cref="InvalidDataException">A line reached 65536 bytes (exit 100).</exception>
    /// <exception cref="ImapWeirdResponseException">
    /// A line held a NUL byte, or was a continuation (exit 8).
    /// </exception>
    public async ValueTask<ImapResponse> ReadUntaggedAsync(Func<string, bool> isWanted)
    {
        while (await ReadLineAsync(0).ConfigureAwait(false) is { } line)
        {
            if (line.StartsWith(Tag + " ", StringComparison.Ordinal))
            {
                return new ImapResponse(StatusOf(line.AsSpan(Tag.Length + 1)), []);
            }

            if (line.StartsWith("* ", StringComparison.Ordinal) && isWanted(line))
            {
                return new ImapResponse(ImapResponseStatus.Untagged, [line]);
            }

            if (IsContinuation(line))
            {
                throw new ImapWeirdResponseException(ImapSessionMessages.UnexpectedContinuation);
            }
        }

        throw new ImapResponseMissingException();
    }

    /// <summary>
    /// Reads the next bytes the server sent, at most <paramref name="maxCount" />, without
    /// looking for line ends: the next piece of a literal being streamed.
    /// </summary>
    /// <param name="maxCount">The most bytes wanted; more than zero.</param>
    /// <returns>
    /// The bytes, valid until the next read on the channel; empty when the server closed the
    /// connection or a read failed with an <see cref="IOException" />.
    /// </returns>
    public async ValueTask<ReadOnlyMemory<byte>> ReadLiteralPieceAsync(long maxCount)
    {
        if (bufferStart == bufferEnd && !await TryFillAsync().ConfigureAwait(false))
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        int take = (int)Math.Min(maxCount, bufferEnd - bufferStart);
        var bytes = new ReadOnlyMemory<byte>(buffer, bufferStart, take);
        bufferStart += take;
        return bytes;
    }

    private static ImapResponseStatus StatusOf(ReadOnlySpan<char> afterTag) =>
        afterTag.StartsWith("OK", StringComparison.Ordinal) ? ImapResponseStatus.Ok
        : afterTag.StartsWith("PREAUTH", StringComparison.Ordinal) ? ImapResponseStatus.Preauth
        : ImapResponseStatus.NotOk;

    /// <summary>
    /// Whether <paramref name="line" /> (without its LF) is what curl takes for a
    /// continuation: <c>+ </c> and anything, or <c>+</c> and exactly one more character.
    /// </summary>
    private static bool IsContinuation(string line) =>
        (line.Length == 2 && line[0] == '+') || line.StartsWith("+ ", StringComparison.Ordinal);

    /// <summary>
    /// The length of the literal <paramref name="line" /> ends in, <c>{n}</c> before any
    /// CR, or <see langword="null" /> when it ends in none.
    /// </summary>
    private static int? LiteralLengthOf(string line)
    {
        ReadOnlySpan<char> text = line.AsSpan().TrimEnd('\r');
        int open = text.LastIndexOf('{');
        if (open < 0 || text[^1] != '}')
        {
            return null;
        }

        ReadOnlySpan<char> digits = text[(open + 1)..^1];
        return digits.Length is > 0 and <= MaxLiteralLengthDigits && !digits.ContainsAnyExceptInRange('0', '9')
            ? int.Parse(digits, provider: CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// Reads, while the response so far ends in a literal, the literal's bytes and the line
    /// that follows them; <see langword="null" /> when the server closed first.
    /// </summary>
    private async ValueTask<string?> ReadLiteralsAsync(string line)
    {
        var response = new StringBuilder(line);
        while (LiteralLengthOf(line) is { } length)
        {
            response.Append('\n');
            if (await ReadBytesAsync(length, response.Length).ConfigureAwait(false) is not { } literal
                || await ReadLineAsync(response.Length + literal.Length).ConfigureAwait(false) is not { } rest)
            {
                return null;
            }

            response.Append(literal).Append(rest);
            line = rest;
        }

        return response.ToString();
    }

    /// <summary>
    /// Reads a literal's <paramref name="count" /> bytes, <paramref name="lineBytes" /> of the
    /// response line already read.
    /// </summary>
    private async ValueTask<string?> ReadBytesAsync(int count, int lineBytes)
    {
        if (count > MaxLineBytes - lineBytes)
        {
            throw new InvalidDataException("An IMAP response line reached 65536 bytes.");
        }

        var literal = new byte[count];
        int read = 0;
        while (read < count)
        {
            if (bufferStart == bufferEnd && !await TryFillAsync().ConfigureAwait(false))
            {
                return null;
            }

            int take = Math.Min(count - read, bufferEnd - bufferStart);
            buffer.AsSpan(bufferStart, take).CopyTo(literal.AsSpan(read));
            bufferStart += take;
            read += take;
        }

        return Encoding.Latin1.GetString(literal);
    }

    /// <summary>
    /// Reads one line up to its LF, without the LF but with any CR before it,
    /// <paramref name="lineBytes" /> of the same response line already read.
    /// </summary>
    private async ValueTask<string?> ReadLineAsync(int lineBytes)
    {
        var line = new List<byte>();
        while (bufferStart < bufferEnd || await TryFillAsync().ConfigureAwait(false))
        {
            if (lineBytes + line.Count >= MaxLineBytes)
            {
                throw new InvalidDataException("An IMAP response line reached 65536 bytes.");
            }

            byte next = buffer[bufferStart++];
            if (next == (byte)'\n')
            {
                return line.Contains(0)
                    ? throw new ImapWeirdResponseException(ImapSessionMessages.NulByteInLine)
                    : Encoding.Latin1.GetString([.. line]);
            }

            line.Add(next);
        }

        return null;
    }

    private async ValueTask<bool> TryFillAsync()
    {
        bufferStart = 0;
        try
        {
            bufferEnd = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            bufferEnd = 0;
        }

        return bufferEnd > 0;
    }
}
