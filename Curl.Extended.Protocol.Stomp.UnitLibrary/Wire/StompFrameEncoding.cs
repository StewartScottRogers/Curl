using System.Text;

namespace Curl.Extended.Protocol.Stomp.Wire;

/// <summary>
/// Writes a <see cref="StompFrame"/> as STOMP 1.2 puts it on the wire (STOMP 1.2, "STOMP Frames"): the command, one
/// <c>name:value</c> line per header, a blank line, the body and a NUL octet, each line ending in LF. Header names and
/// values are escaped (backslash, CR, LF and colon, "Value Encoding") in every frame but <c>CONNECT</c> and
/// <c>CONNECTED</c>, which the specification leaves unescaped.
/// </summary>
internal static class StompFrameEncoding
{
    /// <summary>The letters that may follow a backslash, in the order of <see cref="EscapedOctets"/>.</summary>
    private const string EscapeSequenceLetters = "rnc\\";

    /// <summary>The octet each of <see cref="EscapeSequenceLetters"/> stands for.</summary>
    private const string EscapedOctets = "\r\n:\\";

    /// <summary>Gives the bytes of <paramref name="frame"/>.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The frame's bytes, ending in NUL.</returns>
    internal static byte[] Encode(StompFrame frame)
    {
        bool escaped = IsEscaped(frame.Command);
        StringBuilder text = new StringBuilder(frame.Command).Append('\n');
        foreach (StompHeader header in frame.Headers)
        {
            text.Append(EscapeIf(escaped, header.Name)).Append(':').Append(EscapeIf(escaped, header.Value)).Append('\n');
        }

        text.Append('\n');
        return [.. Encoding.UTF8.GetBytes(text.ToString()), .. frame.Body, 0];
    }

    /// <summary>Tells whether a frame with <paramref name="command"/> escapes its headers: every one but <c>CONNECT</c> and <c>CONNECTED</c>.</summary>
    /// <param name="command">The frame's command.</param>
    /// <returns><see langword="true"/> when its headers are escaped.</returns>
    internal static bool IsEscaped(string command) => command is not ("CONNECT" or "CONNECTED");

    /// <summary>Escapes backslash as <c>\\</c>, CR as <c>\r</c>, LF as <c>\n</c> and colon as <c>\c</c> ("Value Encoding").</summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    internal static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(":", "\\c", StringComparison.Ordinal);

    /// <summary>Undoes <see cref="Escape"/>. Any other escape sequence is a fatal protocol error ("Value Encoding").</summary>
    /// <param name="text">The escaped text.</param>
    /// <returns>The text.</returns>
    /// <exception cref="FormatException">The text holds an undefined escape sequence or ends in a lone backslash.</exception>
    internal static string Unescape(string text)
    {
        StringBuilder result = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            result.Append(text[index] == '\\' ? UnescapedAt(text, ++index) : text[index]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Reads one header line: the name before the first colon and the value after it, unescaped when
    /// <paramref name="escaped"/> is set.
    /// </summary>
    /// <param name="line">The line, without its EOL.</param>
    /// <param name="escaped">Whether the frame's headers are escaped.</param>
    /// <returns>The header.</returns>
    /// <exception cref="FormatException">The line has no colon or holds an undefined escape sequence.</exception>
    internal static StompHeader ParseHeader(string line, bool escaped)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            throw new FormatException($"STOMP header line without a colon: {line}");
        }

        string name = line[..colon];
        string value = line[(colon + 1)..];
        return escaped ? new StompHeader(Unescape(name), Unescape(value)) : new StompHeader(name, value);
    }

    private static string EscapeIf(bool escaped, string text) => escaped ? Escape(text) : text;

    private static char UnescapedAt(string text, int index)
    {
        int sequence = index < text.Length ? EscapeSequenceLetters.IndexOf(text[index], StringComparison.Ordinal) : -1;
        return sequence >= 0 ? EscapedOctets[sequence] : throw new FormatException($"Undefined STOMP escape sequence in: {text}");
    }
}
