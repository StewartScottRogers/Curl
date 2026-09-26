using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One head line split into its text and its terminator, which is a line feed or a
/// carriage return and line feed; curl 8.21.0 accepts both and writes back whichever came.
/// </summary>
internal readonly struct HttpLine
{
    private HttpLine(string content, string terminator)
    {
        Content = content;
        Terminator = terminator;
    }

    /// <summary>
    /// Gets the line's bytes before its terminator, one character per byte.
    /// </summary>
    internal string Content { get; }

    /// <summary>
    /// Gets the line's terminator, <c>"\r\n"</c> or <c>"\n"</c>.
    /// </summary>
    internal string Terminator { get; }

    /// <summary>
    /// Gets a value indicating whether the line is empty, which ends a head.
    /// </summary>
    internal bool IsEmpty => Content.Length == 0;

    /// <summary>
    /// Gets a value indicating whether the line starts with a space or a tab, which makes it
    /// an obsolete continuation of the header line before it. Only asked of a line that is
    /// not <see cref="IsEmpty" />.
    /// </summary>
    internal bool IsContinuation => IsBlank(Content[0]);

    /// <summary>
    /// Splits one line, as <see cref="HttpLineReader" /> returns it, at its terminator.
    /// </summary>
    /// <param name="line">The line's bytes, ending in a line feed.</param>
    /// <returns>The line.</returns>
    /// <exception cref="HttpTransferException">
    /// A carriage return appears anywhere but directly before the line feed (exit 8,
    /// <c>Carriage return found in header</c>).
    /// </exception>
    internal static HttpLine Split(ReadOnlySpan<byte> line)
    {
        int terminatorLength = line.EndsWith("\r\n"u8) ? 2 : 1;
        ReadOnlySpan<byte> content = line[..^terminatorLength];
        if (content.Contains((byte)'\r'))
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.CarriageReturnInHeader);
        }

        return new HttpLine(Encoding.Latin1.GetString(content), terminatorLength == 2 ? "\r\n" : "\n");
    }

    /// <summary>
    /// Tells whether <paramref name="character" /> is a space or a tab, what curl calls blank.
    /// </summary>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true" /> for a space or a tab.</returns>
    internal static bool IsBlank(char character) => character is ' ' or '\t';
}
