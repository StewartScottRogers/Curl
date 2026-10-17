using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Headers;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Headers;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1947.c</c>: a GET of the case's URL following redirects, its body thrown away, then <c>count = &lt;n&gt;</c> on
/// stdout, the requests the transfer made counted by asking <c>curl_easy_nextheader</c> for each request's first
/// header from every origin; then the same handle fetches <c>libtest_arg2</c>, which does not redirect, and counts
/// again. A failing perform writes <c>curl_easy_perform() failed: &lt;text&gt;</c> on stderr and its code is the program's.
/// </summary>
public sealed class Lib1947 : ILibTest
{
    /// <summary>The origins counted: every <c>CURLH_*</c> bit, <c>CURLH_PSEUDO</c> included.</summary>
    public const CurlHeaderOrigin CountedOrigins =
        CurlHeaderOrigin.Header | CurlHeaderOrigin.Trailer | CurlHeaderOrigin.Connect | CurlHeaderOrigin.Informational | CurlHeaderOrigin.Pseudo;

    /// <inheritdoc/>
    public string ProgramName => "lib1947";

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
            // These easy_setopt calls cannot fail on a live Libcurl handle, so their answers are not checked.
            _ = steps.EasySetOptions(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite),
                    (CurlOption.Followlocation, 1L),
                ]);
            if (await PerformAndCountAsync(context, steps, easy, cancellationToken).ConfigureAwait(false))
            {
                _ = steps.EasySetOption(easy, CurlOption.Url, context.Argument2);
                _ = await PerformAndCountAsync(context, steps, easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Counts the requests the handle's last transfer made: those with a first header from <see cref="CountedOrigins"/>.</summary>
    /// <param name="easy">The handle whose transfer is read.</param>
    /// <returns>The number of requests.</returns>
    public static int CountRequests(CurlEasy easy)
    {
        int count = 0;
        while (easy.NextHeader(CountedOrigins, count, null) is not null)
        {
            count++;
        }

        return count;
    }

    // One perform, then its failure on stderr, or the count on stdout; answers whether the program goes on.
    private static async ValueTask<bool> PerformAndCountAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, CancellationToken cancellationToken)
    {
        if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
        {
            context.StandardError.Write($"curl_easy_perform() failed: {CurlErrorText.Easy(steps.Result)}\n");
            return false;
        }

        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        output.Write($"count = {CountRequests(easy)}\n");
        return true;
    }
}
