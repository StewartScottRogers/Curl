using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1649.c</c>: <c>CURLOPT_REFERER</c> set to <see cref="Referer"/> and a verbose GET of the case's URL, then, unless
/// it failed, the referer set back to <c>NULL</c> and the same GET again, which must send no <c>Referer:</c> line. The
/// last perform's code is the program's. Its <c>start_test_timing</c> is left out: the program never checks the time.
/// </summary>
public sealed class Lib1649 : ILibTest
{
    /// <summary>The referer the first request sends.</summary>
    public const string Referer = "https://secret.example.com/";

    /// <inheritdoc/>
    public string ProgramName => "lib1649";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            // Libcurl takes any CURLOPT_REFERER text, or none, without fail, so its two easy_setopt checks are left out.
            _ = easy.SetOption(CurlOption.Referer, Referer);
            if (!await RunAsync(context, steps, easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            _ = easy.SetOption(CurlOption.Referer, null);
            _ = await RunAsync(context, steps, easy, cancellationToken).ConfigureAwait(false);
        }
    }

    private static ValueTask<bool> RunAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, CancellationToken cancellationToken) =>
        steps.SetOptionsAndPerformAsync(
            easy,
            [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Verbose, 1L)],
            cancellationToken);
}
