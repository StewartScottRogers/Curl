using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>Compares the lines Curl's Libcurl and the reference libcurl wrote for one multi or share case file.</summary>
public static class LibcurlMultiComparer
{
    /// <summary>Pairs the lines by position; each pair that differs becomes a <c>real:</c> line and an <c>ours:</c> line.</summary>
    /// <param name="name">The case file's name, such as <c>m_in</c>.</param>
    /// <param name="real">The reference libcurl's lines.</param>
    /// <param name="ours">Curl's Libcurl's lines.</param>
    /// <returns>The outcome; a line one side lacks is written as <c>(none)</c>.</returns>
    public static LibcurlEasyCaseOutcome Compare(string name, IReadOnlyList<string> real, IReadOnlyList<string> ours)
    {
        ArgumentNullException.ThrowIfNull(real);
        ArgumentNullException.ThrowIfNull(ours);
        var differences = new List<string>();
        for (var index = 0; index < Math.Max(real.Count, ours.Count); index++)
        {
            var realLine = index < real.Count ? real[index] : "(none)";
            var ourLine = index < ours.Count ? ours[index] : "(none)";
            if (realLine != ourLine)
            {
                differences.Add($"real: {realLine}");
                differences.Add($"ours: {ourLine}");
            }
        }

        return new(name, differences);
    }
}
