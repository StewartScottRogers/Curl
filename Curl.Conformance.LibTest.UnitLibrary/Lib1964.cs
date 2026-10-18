using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1964.c</c>: a verbose request of the case's URL signed with <c>CURLOPT_AWS_SIGV4</c> <c>xxx</c> and no
/// credentials (the URL carries them), connected through <c>CURLOPT_CONNECT_TO</c> <c>libtest_arg2</c> (no list when it
/// is not given) and sending <c>Content-Type: application/json</c> and its own date, <see cref="DateHeader"/>, which the
/// signature must use instead of the clock. The transfer's code is the program's. The <c>curl_slist_append</c> checks are
/// left out: Libcurl's list append does not fail.
/// </summary>
public sealed class Lib1964 : ILibTest
{
    /// <summary>The date header the program sends, <c>X-Xxx-Date: 19700101T000000Z</c>.</summary>
    public const string DateHeader = "X-Xxx-Date: 19700101T000000Z";

    /// <inheritdoc/>
    public string ProgramName => "lib1964";

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
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.AwsSigv4, "xxx"),
                    (CurlOption.Url, context.Url),
                    (CurlOption.ConnectTo, context.Argument2 is null ? null : CurlSlist.Append(null, context.Argument2)),
                    (CurlOption.Httpheader, CurlSlist.Append(CurlSlist.Append(null, "Content-Type: application/json"), DateHeader)),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
