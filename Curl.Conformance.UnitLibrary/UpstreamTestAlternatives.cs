namespace Curl.Conformance;

/// <summary>
/// Resolves the <c>%alternatives[A,B]</c> that <c>%days[N]</c> expands to in an expected part,
/// as <c>getpart.pm</c>'s <c>compareparts</c> does at <c>curl-8_22_0</c>: the expected text
/// matches when some choice of A or B at each one makes it equal to what was produced.
/// </summary>
internal static class UpstreamTestAlternatives
{
    private const string Opening = "%alternatives[";

    /// <summary>
    /// Replaces each <c>%alternatives[A,B]</c> in <paramref name="expected"/> with A or B, picking the
    /// choices that make it equal to <paramref name="actual"/> when there are any, otherwise B for every
    /// one whose A cannot lead to a match.
    /// </summary>
    /// <param name="expected">The expected text, one character per byte.</param>
    /// <param name="actual">The produced text, one character per byte.</param>
    /// <returns>The expected text with no <c>%alternatives</c> left in it.</returns>
    public static string Choose(string expected, string actual)
    {
        int start = expected.IndexOf(Opening, StringComparison.Ordinal);
        int comma = start < 0 ? -1 : expected.IndexOf(',', start + Opening.Length);
        int end = comma < 0 ? -1 : expected.IndexOf(']', comma + 1);
        if (end < 0)
        {
            return expected;
        }

        string before = expected[..start];
        string first = expected[(start + Opening.Length)..comma];
        string after = expected[(end + 1)..];

        // Everything up to the first choice is settled, so a choice the produced text does not
        // start with cannot match, whatever follows; skipping it keeps the search linear.
        if (actual.StartsWith(before + first, StringComparison.Ordinal))
        {
            string chosen = Choose(before + first + after, actual);
            if (chosen == actual)
            {
                return chosen;
            }
        }

        return Choose(before + expected[(comma + 1)..end] + after, actual);
    }
}
