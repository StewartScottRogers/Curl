using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2505.c</c>: two transfers of the case's URL on one handle with <c>CURLOPT_AUTOREFERER</c> on, the first
/// following redirects and the second not, each followed by <c>reqN=code</c> on stdout. The second transfer's code is
/// the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2505 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2505";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
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
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            _ = easy.SetOption(CurlOption.Autoreferer, 1L);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            await RequestAsync(context, steps, easy, 1, cancellationToken).ConfigureAwait(false);
            _ = easy.SetOption(CurlOption.Followlocation, 0L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            await RequestAsync(context, steps, easy, 2, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask RequestAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, int request, CancellationToken cancellationToken)
    {
        steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        context.StandardOutput.Write(Encoding.UTF8.GetBytes($"req{request}={(int)steps.Result}\n"));
    }
}
