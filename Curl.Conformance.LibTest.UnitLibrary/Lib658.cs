using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib658.c</c>: a verbose transfer of the URL given as a <c>CURLOPT_CURLU</c> handle, set after a
/// <c>CURLOPT_URL</c> of <c>http://www.example.com</c> that it overrides. A URL the handle refuses writes
/// <c>problem setting CURLUPART_URL: &lt;error&gt;.</c> on stderr and ends the program with success, as upstream's
/// does; a failing transfer writes its code and text there.
/// </summary>
public sealed class Lib658 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib658";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>global_init</c>: builds the URL handle, then the transfer.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
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
                (CurlOption.Url, "http://www.example.com"),
                (CurlOption.Curlu, url),
                (CurlOption.Verbose, 1L),
            ]);
            _ = await steps.EasyPerformOrReportAsync(easy, cancellationToken).ConfigureAwait(false);
        }
    }
}
