using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Removes <c>.</c> and <c>..</c> segments from a URL path as curl 8.21.0's
/// <c>dedotdotify</c> does, following RFC 3986 section 5.2.4, with <c>%2e</c> or
/// <c>%2E</c> counted as a dot.
/// </summary>
internal static class CurlUrlDotSegments
{
    /// <summary>Returns <paramref name="path" /> with its dot segments removed.</summary>
    /// <remarks>
    /// The path of a parsed URL always starts with a slash or, for <c>file</c>, a drive
    /// letter, so rules A and D, which remove a leading <c>./</c> or <c>../</c>, never apply
    /// and are not implemented.
    /// </remarks>
    public static string Remove(string path)
    {
        var output = new StringBuilder(path.Length);
        int index = 0;
        while (index < path.Length)
        {
            if (path[index] == '/' && TryRemoveSegment(path, output, ref index))
            {
                continue;
            }

            output.Append(path[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Rules B and C at a slash: <c>/./</c> and <c>/.</c> at the end become <c>/</c>, and
    /// <c>/../</c> and <c>/..</c> at the end become <c>/</c> and remove the last segment
    /// written. Returns <see langword="false" /> when the slash starts no dot segment; a
    /// dot segment at the end of the path writes its slash and moves the index to the end.
    /// </summary>
    private static bool TryRemoveSegment(string path, StringBuilder output, ref int index)
    {
        int afterDot = index + 1 + DotLength(path, index + 1);
        if (afterDot == index + 1)
        {
            return false;
        }

        if (!EndsSegment(path, afterDot))
        {
            int afterSecondDot = afterDot + DotLength(path, afterDot);
            if (afterSecondDot == afterDot || !EndsSegment(path, afterSecondDot))
            {
                return false;
            }

            RemoveLastSegment(output);
            afterDot = afterSecondDot;
        }

        return EndSegment(path, output, afterDot, ref index);
    }

    private static bool EndSegment(string path, StringBuilder output, int afterSegment, ref int index)
    {
        if (afterSegment == path.Length)
        {
            output.Append('/');
        }

        index = afterSegment;

        return true;
    }

    private static void RemoveLastSegment(StringBuilder output)
    {
        for (int index = output.Length - 1; index >= 0; index--)
        {
            if (output[index] == '/')
            {
                output.Length = index;

                return;
            }
        }
    }

    /// <summary>Returns 1 for a <c>.</c> at <paramref name="index" />, 3 for <c>%2e</c> or <c>%2E</c>, else 0.</summary>
    private static int DotLength(string path, int index)
    {
        if (index < path.Length && path[index] == '.')
        {
            return 1;
        }

        return path.AsSpan(index).StartsWith("%2e", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
    }

    /// <summary>Gets a value indicating whether a segment ends at <paramref name="index" />: a slash or the end of the path.</summary>
    private static bool EndsSegment(string path, int index) => index == path.Length || path[index] == '/';
}
