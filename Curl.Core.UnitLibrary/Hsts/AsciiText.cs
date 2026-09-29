namespace Curl.Core.Hsts;

/// <summary>
/// Compares text as libcurl's <c>curl_strnequal</c> does: only the letters <c>A</c> to <c>Z</c>
/// fold to lower case, so no other character ever matches an ASCII one.
/// </summary>
internal static class AsciiText
{
    /// <summary>Whether <paramref name="left" /> and <paramref name="right" /> are equal ignoring ASCII case.</summary>
    public static bool EqualsIgnoringCase(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (ToLower(left[index]) != ToLower(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static char ToLower(char character) => char.IsAsciiLetterUpper(character) ? (char)(character | 0x20) : character;
}
