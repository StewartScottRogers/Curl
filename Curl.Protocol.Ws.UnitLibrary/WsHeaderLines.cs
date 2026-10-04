using System.Text;

namespace Curl.Protocol.Ws;

/// <summary>
/// Finds the first header line of an upgrade reply's head that curl 8.21.0's shared HTTP header
/// code refuses with exit 8 (measured, BL-1405), checking each line in curl's order: a carriage
/// return anywhere but before the line feed (<c>Carriage return found in header</c>), a NUL byte
/// (<c>Nul byte in header</c>), no colon, or a continuation line with no header before it
/// (<c>Header without colon</c>), and a second non-empty <c>Location</c> whose value differs
/// from the first (<c>Multiple Location headers</c>). Every head is checked, a <c>101</c>'s too.
/// </summary>
/// <remarks>
/// The HTTP library's <c>HttpLine</c> and <c>HttpResponseHeadBuilder</c> make the same checks;
/// protocol libraries never reference each other, so this is the WebSocket library's own copy.
/// </remarks>
internal static class WsHeaderLines
{
    /// <summary>The exit 8 message for a header line with no colon.</summary>
    internal const string HeaderWithoutColon = "Header without colon";

    /// <summary>The exit 8 message for a header line holding a NUL byte.</summary>
    internal const string NulByteInHeader = "Nul byte in header";

    /// <summary>The exit 8 message for a carriage return inside a header line.</summary>
    internal const string CarriageReturnInHeader = "Carriage return found in header";

    /// <summary>The exit 8 message for a second <c>Location</c> that differs from the first.</summary>
    internal const string MultipleLocationHeaders = "Multiple Location headers";

    private const string LocationName = "Location:";

    /// <summary>Finds the first refused header line in <paramref name="head" />.</summary>
    /// <param name="head">The reply head, status line to blank line.</param>
    /// <param name="refusal">The refused line's message, or <see langword="null" /> when none is refused.</param>
    /// <returns>Where the refused line starts, or the head's length when none is refused.</returns>
    internal static int FindRefusedLine(byte[] head, out string? refusal)
    {
        string? location = null;
        bool hasHeader = false;
        int lineStart = Array.IndexOf(head, (byte)'\n') + 1;
        while (lineStart < head.Length)
        {
            int lineEnd = Array.IndexOf(head, (byte)'\n', lineStart) + 1;
            string content = Encoding.Latin1.GetString(head, lineStart, lineEnd - lineStart - 1);
            refusal = CheckLine(content.EndsWith('\r') ? content[..^1] : content, hasHeader, ref location);
            if (refusal is not null)
            {
                return lineStart;
            }

            hasHeader = true;
            lineStart = lineEnd;
        }

        refusal = null;
        return head.Length;
    }

    /// <summary>
    /// Checks one header line, without its terminator; the blank line, and a continuation line
    /// after a header, are accepted once they hold no carriage return or NUL byte.
    /// </summary>
    private static string? CheckLine(string content, bool hasHeader, ref string? location) =>
        CheckCharacters(content) ?? CheckHeader(content, hasHeader, ref location);

    /// <summary>Refuses a carriage return before a NUL byte, as curl checks them.</summary>
    private static string? CheckCharacters(string content)
    {
        if (content.Contains('\r', StringComparison.Ordinal))
        {
            return CarriageReturnInHeader;
        }

        return content.Contains('\0', StringComparison.Ordinal) ? NulByteInHeader : null;
    }

    /// <summary>Refuses a header line with no colon, and a second different <c>Location</c>.</summary>
    private static string? CheckHeader(string content, bool hasHeader, ref string? location)
    {
        if (content.Length == 0 || IsContinuation(content, hasHeader))
        {
            return null;
        }

        return content.Contains(':', StringComparison.Ordinal) ? CheckLocation(content, ref location) : HeaderWithoutColon;
    }

    private static bool IsContinuation(string content, bool hasHeader) => hasHeader && content[0] is ' ' or '\t';

    /// <summary>
    /// Keeps the first non-empty <c>Location</c> value and refuses a later one that differs from
    /// it, as curl 8.21.0's <c>http_header_l</c> does; an empty value, or an exact repeat, is ignored.
    /// </summary>
    private static string? CheckLocation(string content, ref string? location)
    {
        if (!content.StartsWith(LocationName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string value = content[LocationName.Length..].Trim(' ', '\t');
        if (value.Length == 0)
        {
            return null;
        }

        if (location is not null && !location.Equals(value, StringComparison.Ordinal))
        {
            return MultipleLocationHeaders;
        }

        location = value;
        return null;
    }
}
