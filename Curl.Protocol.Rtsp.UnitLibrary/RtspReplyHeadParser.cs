using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Reads the lines of an RTSP reply head one at a time, as curl 8.21.0 reads them: the status
/// line first, then header lines up to the blank line, taking the status code, <c>CSeq</c> and
/// <c>Content-Length</c> (ADR-0169).
/// </summary>
/// <remarks>
/// Measured (BL-591): a status line must be <c>RTSP/1.0</c>, one blank and a number of at most
/// 999, or the transfer fails with 8, <c>Weird server reply</c> (<c>RTSP/1.0 abc</c>,
/// <c>RTSP/2.0 200 OK</c>). <c>CSeq:</c> is matched in any case and read as C's
/// <c>sscanf(": %ld")</c> reads it, so <c>cseq:   1 </c> and <c>CSeq: 1x</c> are both 1 and a
/// later <c>CSeq</c> replaces an earlier one; a value with no number fails with 85,
/// <c>Unable to read the CSeq header: [&lt;line&gt;]</c>. A <c>Content-Length</c> is a comma list
/// of equal decimal numbers (<c>2, 2</c>), and a second one must agree with the first; anything
/// else fails with 8, <c>Invalid Content-Length: value</c>, while a number too large for 64 bits
/// is accepted and leaves no body (BL-840). A carriage return inside a line fails with 8,
/// <c>Carriage return found in header</c> (on the status line only once it has passed the
/// status check), and a header line with no colon with 8, <c>Header without colon</c>; each
/// header line arrives with its continuation lines already joined (<see cref="RtspHeaderFolding" />).
/// The line that fails is not written. A <c>Session</c> header, in any case, is handed to <c>session</c>, which keeps
/// the first ID and fails a different one with 86 as the line is read (BL-592).
/// </remarks>
/// <param name="session">The transfer's session state, which reads each <c>Session</c> header.</param>
internal sealed class RtspReplyHeadParser(RtspSessionState session)
{
    /// <summary>The exit 8 message for a status line curl does not accept.</summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>The exit 8 message for a <c>Content-Length</c> that is not a number.</summary>
    internal const string InvalidContentLength = "Invalid Content-Length: value";

    /// <summary>The exit 8 message for a carriage return inside a line of the head.</summary>
    internal const string CarriageReturnInHeader = "Carriage return found in header";

    /// <summary>The exit 8 message for a header line with no colon.</summary>
    internal const string HeaderWithoutColon = "Header without colon";

    private const string Version = "RTSP/1.0";

    private const int MaximumStatusCode = 999;

    private const string WhiteSpace = " \t\r\n\v\f";

    private long? declaredLength;

    private bool lengthTooLarge;

    /// <summary>Gets a value indicating whether the status line has been read.</summary>
    internal bool HasStatus { get; private set; }

    /// <summary>Gets a value indicating whether the blank line that ends the head has been read.</summary>
    internal bool IsComplete { get; private set; }

    /// <summary>Gets the status code, or 0 before the status line has been read.</summary>
    internal int StatusCode { get; private set; }

    /// <summary>Gets the reply's <c>CSeq</c>, or 0 when none has been read.</summary>
    internal long SequenceNumber { get; private set; }

    /// <summary>
    /// Gets the reply's <c>Content-Length</c>, or 0 when none has been read or its number is too
    /// large for 64 bits.
    /// </summary>
    internal long ContentLength => declaredLength ?? 0;

    /// <summary>Reads one line of the head.</summary>
    /// <param name="line">The line, with its line ending.</param>
    /// <exception cref="RtspTransferException">curl refuses the line.</exception>
    internal void Accept(ReadOnlySpan<byte> line)
    {
        string text = Encoding.Latin1.GetString(line);
        if (!HasStatus)
        {
            StatusCode = ParseStatusCode(text);
            RefuseCarriageReturn(text);
            HasStatus = true;
        }
        else if (text is "\n" or "\r\n")
        {
            IsComplete = true;
        }
        else
        {
            RefuseCarriageReturn(text);
            RefuseMissingColon(text);
            AcceptHeader(text);
        }
    }

    /// <summary>Fails with 8 when a carriage return comes anywhere but just before the line feed.</summary>
    private static void RefuseCarriageReturn(string line)
    {
        ReadOnlySpan<char> content = line.AsSpan(0, line.Length - 1);
        if (content[..^(content.EndsWith('\r') ? 1 : 0)].Contains('\r'))
        {
            throw new RtspTransferException(CurlExitCode.WeirdServerReply, CarriageReturnInHeader);
        }
    }

    private static void RefuseMissingColon(string line)
    {
        if (!line.Contains(':', StringComparison.Ordinal))
        {
            throw new RtspTransferException(CurlExitCode.WeirdServerReply, HeaderWithoutColon);
        }
    }

    /// <summary>
    /// Reads the status code from a status line: the text up to the first blank, trimmed, is
    /// <c>RTSP/1.0</c>, and the digits right after that blank are a number of at most 999.
    /// </summary>
    /// <param name="line">The status line, with its line ending.</param>
    private static int ParseStatusCode(string line)
    {
        int blank = line.IndexOf(' ', StringComparison.Ordinal);
        if (blank < 0 || !line.AsSpan(0, blank).Trim(" \t").SequenceEqual(Version))
        {
            throw Weird();
        }

        ReadOnlySpan<char> rest = line.AsSpan(blank + 1);
        ReadOnlySpan<char> number = rest[..rest.IndexOfAnyExceptInRange('0', '9')];
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int code) && code <= MaximumStatusCode
            ? code
            : throw Weird();
    }

    private static RtspTransferException Weird() => new(CurlExitCode.WeirdServerReply, WeirdServerReply);

    private void AcceptHeader(string line)
    {
        if (line.StartsWith("CSeq:", StringComparison.OrdinalIgnoreCase))
        {
            SequenceNumber = ParseSequenceNumber(line);
        }
        else if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
        {
            AcceptContentLength(line["Content-Length:".Length..]);
        }
        else if (line.StartsWith("Session:", StringComparison.OrdinalIgnoreCase))
        {
            session.AcceptSession(line["Session:".Length..]);
        }
    }

    /// <summary>Reads a <c>CSeq</c> value as <c>sscanf(": %ld")</c> does: blanks, a sign, digits.</summary>
    private static long ParseSequenceNumber(string line)
    {
        ReadOnlySpan<char> value = line.AsSpan("CSeq:".Length).TrimStart(WhiteSpace);
        int signLength = value.StartsWith('-') || value.StartsWith('+') ? 1 : 0;
        int end = value[signLength..].IndexOfAnyExceptInRange('0', '9');
        ReadOnlySpan<char> number = end < 0 ? value : value[..(signLength + end)];
        return long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long sequenceNumber)
            ? sequenceNumber
            : throw new RtspTransferException(CurlExitCode.RtspCseqError, $"Unable to read the CSeq header: [{line}]");
    }

    /// <summary>
    /// Reads a <c>Content-Length</c> value as a comma list of decimal numbers, each with blanks
    /// around it, that must all equal every number read before; a number too large for 64 bits
    /// leaves the length unknown, which ends every check (measured, BL-840).
    /// </summary>
    private void AcceptContentLength(string value)
    {
        foreach (string item in value.AsSpan().Trim(WhiteSpace).ToString().Split(','))
        {
            if (lengthTooLarge)
            {
                return;
            }

            AcceptContentLengthItem(item.AsSpan().Trim(" \t").ToString());
        }
    }

    private void AcceptContentLengthItem(string item)
    {
        if (item.Length == 0 || item.AsSpan().ContainsAnyExceptInRange('0', '9'))
        {
            throw InvalidLength();
        }

        lengthTooLarge = !long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out long length);
        declaredLength = lengthTooLarge || declaredLength is null || declaredLength == length ? length : throw InvalidLength();
    }

    private static RtspTransferException InvalidLength() => new(CurlExitCode.WeirdServerReply, InvalidContentLength);
}
