using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Checks the <c>Content-Length</c> headers of a reply that refuses the upgrade as curl 8.21.0's
/// shared HTTP header code does while each header arrives (measured, BL-1404): a value that is
/// not a comma-separated list of equal decimal numbers, or one that disagrees with an earlier
/// header, fails with exit 8 <c>Invalid Content-Length: value</c>; a number too large for 64 bits
/// gets <c>Overflow Content-Length: value</c> before its header line, or, with
/// <c>--max-filesize</c> set, fails with exit 63 <c>Maximum file size exceeded</c>. A failing
/// header line is neither written nor reported. A <c>101</c> is bodyless, so curl does not check it.
/// </summary>
/// <remarks>
/// The HTTP library's <c>HttpContentLength</c> parses the same values; protocol libraries never
/// reference each other, so this is the WebSocket library's own copy.
/// </remarks>
internal static class WsContentLength
{
    /// <summary>The exit 8 message for a value that is not a list of equal numbers.</summary>
    internal const string InvalidValue = "Invalid Content-Length: value";

    /// <summary>The line written before a header whose number is too large for 64 bits.</summary>
    internal const string OverflowValue = "Overflow Content-Length: value";

    /// <summary>The exit 63 message for a number too large for 64 bits under <c>--max-filesize</c>.</summary>
    internal const string MaximumFileSizeExceeded = "Maximum file size exceeded";

    private const string HeaderName = "Content-Length:";

    private static readonly char[] Blanks = [' ', '\t'];

    /// <summary>Checks every <c>Content-Length</c> header in <paramref name="head" />, in order.</summary>
    /// <param name="head">The reply head, status line to blank line.</param>
    /// <param name="maxFileSize">The <c>--max-filesize</c> limit; 0 or <see langword="null" /> is none.</param>
    /// <returns>Where the head stops being accepted, the overflow lines and any failure.</returns>
    internal static WsContentLengthCheck Check(byte[] head, long? maxFileSize)
    {
        var overflowLineStarts = new List<int>();
        long? length = null;
        int lineStart = Array.IndexOf(head, (byte)'\n') + 1;
        while (lineStart < head.Length)
        {
            int lineEnd = Array.IndexOf(head, (byte)'\n', lineStart) + 1;
            string line = Encoding.Latin1.GetString(head, lineStart, lineEnd - lineStart);
            ItemVerdict verdict = CheckLine(line, ref length);
            if (verdict == ItemVerdict.Overflow && maxFileSize > 0)
            {
                return new WsContentLengthCheck(lineStart, overflowLineStarts, CurlExitCode.FilesizeExceeded, MaximumFileSizeExceeded);
            }

            if (verdict == ItemVerdict.Invalid)
            {
                return new WsContentLengthCheck(lineStart, overflowLineStarts, CurlExitCode.WeirdServerReply, InvalidValue);
            }

            if (verdict == ItemVerdict.Overflow)
            {
                overflowLineStarts.Add(lineStart);
            }

            lineStart = lineEnd;
        }

        return WsContentLengthCheck.Accepted(head.Length, overflowLineStarts);
    }

    /// <summary>Checks one header line; any line that is not <c>Content-Length</c> is accepted.</summary>
    private static ItemVerdict CheckLine(string line, ref long? length)
    {
        if (!line.StartsWith(HeaderName, StringComparison.OrdinalIgnoreCase))
        {
            return ItemVerdict.Accepted;
        }

        foreach (string item in line[HeaderName.Length..].TrimEnd('\r', '\n').Split(','))
        {
            ItemVerdict verdict = CheckItem(item.Trim(Blanks), ref length);
            if (verdict != ItemVerdict.Accepted)
            {
                return verdict;
            }
        }

        return ItemVerdict.Accepted;
    }

    /// <summary>Checks one list item against the number every earlier item gave.</summary>
    private static ItemVerdict CheckItem(string item, ref long? length)
    {
        ItemVerdict verdict = ParseItem(item, out long value);
        if (verdict != ItemVerdict.Accepted)
        {
            return verdict;
        }

        ItemVerdict agreement = length is null || length == value ? ItemVerdict.Accepted : ItemVerdict.Invalid;
        length = value;
        return agreement;
    }

    /// <summary>Parses one list item, without the blanks around it, as a decimal number.</summary>
    private static ItemVerdict ParseItem(string item, out long value)
    {
        value = 0;
        if (!IsDecimalNumber(item))
        {
            return ItemVerdict.Invalid;
        }

        return long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? ItemVerdict.Accepted : ItemVerdict.Overflow;
    }

    private static bool IsDecimalNumber(string item) => item.Length > 0 && item.All(char.IsAsciiDigit);

    private enum ItemVerdict
    {
        Accepted,
        Overflow,
        Invalid,
    }
}
