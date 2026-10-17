using System.Globalization;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1511.c</c>: a GET with headers and <c>CURL_TIMECOND_IFMODSINCE</c>, first with <c>CURLOPT_TIMEVALUE</c> in
/// the future, after which <c>CURLINFO_CONDITION_UNMET</c> must be 1, then in the past, after which it must be 0. It
/// ends with <see cref="LibTestErrorCodes.Success"/> when both hold, <see cref="LibTestErrorCodes.Failure"/> when one does
/// not, or the code of the step that failed.
/// </summary>
public sealed class Lib1511 : ILibTest
{
    /// <summary><c>CURL_TIMECOND_IFMODSINCE</c>.</summary>
    public const long TimeConditionIfModifiedSince = 1;

    /// <summary>The <c>CURLOPT_TIMEVALUE</c> in the future (2019-08-19 10:31:20 UTC, later than the reply's <c>Last-Modified</c>).</summary>
    public const long FutureTime = 1566210680;

    /// <inheritdoc/>
    public string ProgramName => "lib1511";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Header, 1L),
                        (CurlOption.Timecondition, TimeConditionIfModifiedSince),
                        (CurlOption.Timevalue, FutureTime),
                    ],
                    cancellationToken).ConfigureAwait(false)
                && ConditionUnmetIs(steps, easy, 1)
                && await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Timevalue, 1L)], cancellationToken).ConfigureAwait(false)
                && ConditionUnmetIs(steps, easy, 0))
            {
                steps.Result = LibTestErrorCodes.Success;
            }
        }
    }

    // The CURLINFO_CONDITION_UNMET check: on a mismatch the result becomes TEST_ERR_FAILURE.
    private static bool ConditionUnmetIs(LibTestSteps steps, CurlEasy easy, long expected)
    {
        easy.GetInfo(CurlInfo.ConditionUnmet, out object? unmet);
        if (Convert.ToInt64(unmet, CultureInfo.InvariantCulture) == expected)
        {
            return true;
        }

        steps.Result = LibTestErrorCodes.Failure;
        return false;
    }
}
