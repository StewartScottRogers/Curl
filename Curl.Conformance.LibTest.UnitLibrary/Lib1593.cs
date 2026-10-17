using Curl.Api.Options.Behaviour;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1593.c</c>: the case's URL with <c>CURL_TIMECOND_IFMODSINCE</c> at <see cref="TimeValue"/> and the
/// <c>If-Modified-Since:</c> header suppressed by an empty one. After the transfer <c>CURLINFO_CONDITION_UNMET</c> must
/// be <c>1</c> (the server answers 304); otherwise the program ends with <see cref="LibTestErrorCodes.Failure"/>.
/// </summary>
public sealed class Lib1593 : ILibTest
{
    /// <summary>The <c>CURLOPT_TIMEVALUE</c>, "some TIMEVALUE; it does not matter".</summary>
    public const long TimeValue = 1566210680L;

    /// <inheritdoc/>
    public string ProgramName => "lib1593";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
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
                        (CurlOption.Timecondition, (long)TimeCondition.IfModifiedSince),
                        (CurlOption.Timevalue, TimeValue),
                        (CurlOption.Httpheader, CurlSlist.Append(null, "If-Modified-Since:")),
                    ],
                    cancellationToken).ConfigureAwait(false))
            {
                // Libcurl cannot fail CURLINFO_CONDITION_UNMET on a live handle, so its check is left out.
                _ = steps.EasyGetInfo(easy, CurlInfo.ConditionUnmet, out object? unmet);
                steps.Result = (long)unmet! == 1L ? steps.Result : LibTestErrorCodes.Failure;
            }
        }
    }
}
