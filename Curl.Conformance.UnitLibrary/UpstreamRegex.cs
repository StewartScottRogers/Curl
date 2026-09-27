using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Compiles the Perl regular expressions a test case's <c>&lt;strip&gt;</c>, <c>&lt;strippart&gt;</c>
/// and <c>&lt;stripfile&gt;</c> parts hold as .NET regular expressions, which read the patterns
/// upstream's cases use the same way.
/// </summary>
internal static class UpstreamRegex
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Compiles a pattern.</summary>
    /// <param name="pattern">The pattern, as written between the delimiters.</param>
    /// <param name="options">The options its flags ask for.</param>
    /// <returns>The regular expression, or <see langword="null"/> when .NET cannot read the pattern.</returns>
    public static Regex? TryCreate(string pattern, RegexOptions options)
    {
        try
        {
            return new Regex(pattern, options, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
