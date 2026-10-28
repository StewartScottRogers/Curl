using System.Text.RegularExpressions;

namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>One printf scenario: a format string and the typed arguments it is called with.</summary>
/// <param name="Index">The scenario's position in its input file, from 0.</param>
/// <param name="Format">The format string.</param>
/// <param name="Arguments">The arguments, at most <see cref="MaximumArguments"/>.</param>
public sealed record LibcurlPrintfScenario(int Index, string Format, IReadOnlyList<LibcurlPrintfArgument> Arguments)
{
    /// <summary>The most arguments a scenario may carry: the reference driver passes this many variadic slots.</summary>
    public const int MaximumArguments = 10;

    private static readonly Regex LongDoubleConversion = new(@"%[-+ #0-9.*$']*L[aAeEfFgG]", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether the format has a floating-point conversion with the <c>L</c> (<c>long double</c>)
    /// length modifier, which crashes the reference libcurl 8.22.0 with an access violation (the
    /// tester's <c>EXC OSError</c>), so the scenario is not run against it (ADR-0876).
    /// </summary>
    public bool CrashesReference => LongDoubleConversion.IsMatch(Format);
}
