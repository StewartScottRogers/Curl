using Curl.Api.Codes;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The process exit code a libtest program's <c>main()</c> returns: the <c>CURLcode</c> its
/// <c>test()</c> returned, as a number, or one of <c>first.c</c>'s <c>TEST_ERR_*</c> codes.
/// </summary>
/// <param name="ExitCode">The exit code; 0 is success.</param>
public readonly record struct LibTestResult(int ExitCode)
{
    /// <summary>The result of a program that succeeded: exit code 0 (<c>CURLE_OK</c>).</summary>
    public static LibTestResult Success { get; } = new(0);

    /// <summary>Whether the program succeeded, that is <see cref="ExitCode"/> is 0.</summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>The result of a program whose <c>test()</c> returned <paramref name="code"/>, by its number.</summary>
    /// <param name="code">The <c>CURLcode</c> the program returned.</param>
    /// <returns>The result whose exit code is the number of <paramref name="code"/>.</returns>
    public static LibTestResult FromCurlCode(CurlCode code) => new((int)code);
}
