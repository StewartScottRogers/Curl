using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib659.c</c>: a verbose transfer through the proxy the URL names of a <c>CURLOPT_CURLU</c> handle with only
/// its scheme, host and port set - no path - so the request must still name one. A part the handle refuses writes
/// <c>problem setting CURLUPART</c> on stderr and ends the program with success, as upstream's does; a failing
/// transfer writes its code and text there.
/// </summary>
public sealed class Lib659 : ILibTest
{
    private static readonly (CurlUrlPart Part, string Value)[] Parts =
    [
        (CurlUrlPart.Host, "www.example.com"),
        (CurlUrlPart.Scheme, "http"),
        (CurlUrlPart.Port, "80"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib659";

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
            if (Parts.Any(p => url.Set(p.Part, p.Value, CurlUrlFlags.None) != CurlUrlCode.Ok))
            {
                context.StandardError.Write("problem setting CURLUPART");
                return;
            }

            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Curlu, url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Proxy, context.Url),
            ]);
            _ = await steps.EasyPerformOrReportAsync(easy, cancellationToken).ConfigureAwait(false);
        }
    }
}
