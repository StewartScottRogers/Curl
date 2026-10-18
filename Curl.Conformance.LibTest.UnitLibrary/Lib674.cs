using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib674.c</c>: a verbose transfer of the URL given as a <c>CURLOPT_CURLU</c> handle, then the same again on a
/// <c>curl_easy_duphandle</c> copy of the handle. A URL the handle refuses writes
/// <c>problem setting CURLUPART_URL: &lt;error&gt;.</c> on stderr and ends the program with success, as upstream's
/// does; a failing first transfer writes its code and text there and skips the copy.
/// </summary>
public sealed class Lib674 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib674";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>global_init</c>: builds the URL handle, then the two transfers.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
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
            using CurlUrlHandle url = library.UrlInit();
            CurlUrlCode code = url.Set(CurlUrlPart.Url, context.Url, CurlUrlFlags.None);
            if (code != CurlUrlCode.Ok)
            {
                context.StandardError.Write($"problem setting CURLUPART_URL: {CurlUrlHandle.ErrorText(code)}.");
                return;
            }

            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Curlu, url),
                (CurlOption.Verbose, 1L),
            ]);
            if (await steps.EasyPerformOrReportAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                await using CurlEasy duplicate = easy.DupHandle();
                _ = await steps.EasyPerformAsync(duplicate, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
