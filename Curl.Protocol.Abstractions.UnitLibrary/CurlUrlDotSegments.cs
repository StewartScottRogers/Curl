using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Removes <c>.</c> and <c>..</c> segments from a URL path as curl 8.22.0's
/// <c>dedotdotify</c> does, following RFC 3986 section 5.2.4, with <c>%2e</c> or
/// <c>%2E</c> counted as a dot.
/// </summary>
internal static class CurlUrlDotSegments
{
    /// <summary>Returns <paramref name="path" /> with its dot segments removed, or <paramref name="path" /> itself when it has none.</summary>
    public static string Remove(string path) => Dedotdotify(path) ?? path;

    /// <summary>
    /// curl 8.22.0's <c>dedotdotify</c>, row for row: <see langword="null" /> when
    /// <paramref name="input" /> has no dot segment to remove (<c>needs_dedotdot</c>),
    /// else the input with a leading <c>./</c> or <c>../</c>, a whole <c>.</c> or <c>..</c>,
    /// and every <c>/./</c> and <c>/../</c> removed (rules A to E).
    /// </summary>
    public static string? Dedotdotify(string input)
    {
        if (!NeedsDedotdot(input))
        {
            return null;
        }

        int index = SkipLeadingDotSegment(input);
        var output = new StringBuilder(input.Length);
        while (index < input.Length)
        {
            if (input[index] == '/' && TryRemoveSegment(input, output, ref index))
            {
                continue;
            }

            output.Append(input[index]);
            index++;
        }

        return output.ToString();
    }

    /// <summary>
    /// <c>needs_dedotdot</c>: whether <paramref name="path" /> holds a <c>.</c> or <c>..</c>
    /// followed by a slash or the end; a path shorter than two characters never does.
    /// </summary>
    private static bool NeedsDedotdot(string path)
    {
        if (path.Length < 2)
        {
            return false;
        }

        int index = 0;
        while (index < path.Length)
        {
            int dot = DotLength(path, index);
            if (dot == 0)
            {
                index++;
                continue;
            }

            index += dot;
            if (EndsDotSegment(path, index))
            {
                return true;
            }

            index += DotLength(path, index);
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="index" />, just after a dot, ends a <c>.</c> segment or, after
    /// one more dot, a <c>..</c> segment.
    /// </summary>
    private static bool EndsDotSegment(string path, int index)
    {
        if (EndsSegment(path, index))
        {
            return true;
        }

        int secondDot = DotLength(path, index);

        return secondDot > 0 && EndsSegment(path, index + secondDot);
    }

    /// <summary>
    /// Rules A and D: returns the index after a leading <c>./</c> or <c>../</c>, the length of
    /// the path when it is <c>.</c> or <c>..</c> alone, else 0.
    /// </summary>
    private static int SkipLeadingDotSegment(string path)
    {
        int afterDot = DotLength(path, 0);
        if (afterDot == 0)
        {
            return 0;
        }

        if (EndsSegment(path, afterDot))
        {
            return Math.Min(afterDot + 1, path.Length);
        }

        int afterSecondDot = afterDot + DotLength(path, afterDot);
        if (afterSecondDot == afterDot || !EndsSegment(path, afterSecondDot))
        {
            return 0;
        }

        return Math.Min(afterSecondDot + 1, path.Length);
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
