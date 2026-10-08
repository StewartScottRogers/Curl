using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Compiles the Perl regular expressions a test case's <c>&lt;strip&gt;</c>, <c>&lt;strippart&gt;</c>
/// and <c>&lt;stripfile&gt;</c> parts hold as .NET regular expressions, which read the patterns
/// upstream's cases use the same way.
/// </summary>
internal static class UpstreamRegex
{
    /// <summary>
    /// How long one match may run before it is abandoned: long enough that a busy test runner's
    /// stall never cuts short a pattern upstream's cases use, which matches in microseconds, and
    /// short enough that a pattern that backtracks without end still stops (BL-1718).
    /// </summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Compiles a pattern whose matches may run for <see cref="MatchTimeout"/>.</summary>
    /// <param name="pattern">The pattern, as written between the delimiters.</param>
    /// <param name="options">The options its flags ask for.</param>
    /// <returns>The regular expression, or <see langword="null"/> when .NET cannot read the pattern.</returns>
    public static Regex? TryCreate(string pattern, RegexOptions options) => TryCreate(pattern, options, MatchTimeout);

    /// <summary>Compiles a pattern whose matches may run for the given time.</summary>
    /// <param name="pattern">The pattern, as written between the delimiters.</param>
    /// <param name="options">The options its flags ask for.</param>
    /// <param name="matchTimeout">How long one match may run before it throws <see cref="RegexMatchTimeoutException"/>.</param>
    /// <returns>The regular expression, or <see langword="null"/> when .NET cannot read the pattern.</returns>
    public static Regex? TryCreate(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        try
        {
            return new Regex(pattern, options, matchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
