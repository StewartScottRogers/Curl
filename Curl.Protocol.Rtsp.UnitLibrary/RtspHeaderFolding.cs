using System.Runtime.InteropServices;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Joins an RTSP reply header line and the continuation lines folded under it into one line,
/// as curl 8.21.0 writes and reads it (measured, BL-840).
/// </summary>
/// <remarks>
/// A continuation line starts with a space or a tab. Each one is joined to the line before it
/// by one space: the line before loses its line ending and the blanks before it, the
/// continuation loses its leading blanks and keeps the rest, line ending included. So
/// <c>X-A: 1  \r\n\t cont  \r\n</c> is <c>X-A: 1 cont  \r\n</c>, <c>X-A: 1\r\n   \r\n</c> is
/// <c>X-A: 1 \r\n</c>, and <c>X-A: 1\n cont\r\n</c> is <c>X-A: 1 cont\r\n</c>.
/// </remarks>
internal static class RtspHeaderFolding
{
    private static ReadOnlySpan<byte> Blanks => " \t"u8;

    /// <summary>Determines whether <paramref name="character" /> starts a continuation line.</summary>
    /// <param name="character">The first byte of a line.</param>
    /// <returns><see langword="true" /> for a space or a tab.</returns>
    internal static bool IsBlank(byte character) => character is (byte)' ' or (byte)'\t';

    /// <summary>Joins a header line and its continuation lines into one line.</summary>
    /// <param name="lines">
    /// The header line and every continuation line after it, each ending in a line feed, with
    /// any bytes of a line not yet ended after them, which are kept as they are.
    /// </param>
    /// <returns>The joined line, followed by any bytes of a line not yet ended.</returns>
    internal static byte[] Unfold(ReadOnlySpan<byte> lines)
    {
        int lineFeed = lines.IndexOf((byte)'\n');
        List<byte> joined = [.. lines[..(lineFeed + 1)]];
        ReadOnlySpan<byte> rest = lines[(lineFeed + 1)..];
        while ((lineFeed = rest.IndexOf((byte)'\n')) >= 0)
        {
            RemoveLineEndingAndBlanks(joined);
            joined.Add((byte)' ');
            joined.AddRange(rest[..(lineFeed + 1)].TrimStart(Blanks));
            rest = rest[(lineFeed + 1)..];
        }

        joined.AddRange(rest);
        return [.. joined];
    }

    /// <summary>
    /// Removes the line feed, one carriage return before it, and the blanks before that, so a
    /// carriage return inside the line is kept for the parser to refuse.
    /// </summary>
    private static void RemoveLineEndingAndBlanks(List<byte> line)
    {
        ReadOnlySpan<byte> content = CollectionsMarshal.AsSpan(line)[..^1];
        if (content.EndsWith("\r"u8))
        {
            content = content[..^1];
        }

        int length = content.TrimEnd(Blanks).Length;
        line.RemoveRange(length, line.Count - length);
    }
}
