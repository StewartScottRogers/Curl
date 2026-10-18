using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2506.c</c>: a verbose transfer of <c>libtest_arg2</c> through the case's URL as proxy, following redirects,
/// with the optional <c>.netrc</c> at <c>libtest_arg3</c> and <c>CURLOPT_UNRESTRICTED_AUTH</c> on, its body thrown away.
/// The transfer's code is the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure
/// and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2506 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2506";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            _ = easy.SetOption(CurlOption.Proxy, context.Url);
            _ = easy.SetOption(CurlOption.Url, context.Argument2);
            _ = easy.SetOption(CurlOption.Netrc, (long)NetrcUsage.Optional);
            _ = easy.SetOption(CurlOption.NetrcFile, context.Argument3);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.UnrestrictedAuth, 1L);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
