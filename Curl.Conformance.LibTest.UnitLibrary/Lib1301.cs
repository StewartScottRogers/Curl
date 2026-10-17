using Curl.Api.Codes;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1301.c</c>: <c>curl_strequal</c> and <c>curl_strnequal</c> compare case-insensitively, the length-limited
/// form only up to its limit. The first failed check writes its assertion on stderr and ends the program with
/// <see cref="LibTestErrorCodes.Failure"/>; the program never calls <c>curl_global_init</c>.
/// </summary>
public sealed class Lib1301 : ILibTest
{
    /// <summary>
    /// The program's checks in order: the two strings, the <c>curl_strnequal</c> limit (<see langword="null"/> for
    /// <c>curl_strequal</c>) and whether they must compare equal.
    /// </summary>
    public static IReadOnlyList<(string First, string Second, int? Limit, bool Equal)> Checks { get; } =
    [
        ("iii", "III", null, true),
        ("iiia", "III", null, false),
        ("iii", "IIIa", null, false),
        ("iiiA", "IIIa", null, true),
        ("iii", "III", 3, true),
        ("iiiABC", "IIIcba", 3, true),
        ("ii", "II", 3, true),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1301";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(LibTestResult.FromCurlCode(Test(new LibTestAssertions(context.StandardError), Checks)));
    }

    /// <summary>Runs <paramref name="checks"/>, stopping at the first that fails.</summary>
    /// <param name="check">Counts and reports a failed check.</param>
    /// <param name="checks">The comparisons, as <see cref="Checks"/>.</param>
    /// <returns><see cref="CurlCode.Ok"/>, or <see cref="LibTestErrorCodes.Failure"/> at the first failed check.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CurlCode Test(LibTestAssertions check, IEnumerable<(string First, string Second, int? Limit, bool Equal)> checks)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(checks);
        foreach (var (first, second, limit, equal) in checks)
        {
            bool compared = limit is null ? CurlStrings.StrEqual(first, second) : CurlStrings.StrNEqual(first, second, (nuint)limit.Value);
            string message = equal ? "return code should be non-zero" : "return code should be zero";
            if (!check.FailUnless(compared == equal, message, equal ? "rc != 0" : "rc == 0"))
            {
                return LibTestErrorCodes.Failure;
            }
        }

        return CurlCode.Ok;
    }
}
