using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2397.c</c>: a transfer of the case's URL following redirects with <c>CURLOPT_AUTOREFERER</c>, then
/// <c>CURLINFO_REFERER</c> read back and set again as <c>CURLOPT_REFERER</c>. A failing transfer writes
/// <c>transfer failed: text</c>, and a missing referer <c>CURLINFO_REFERER was not populated</c>, to stderr; the last
/// call's code is the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and
/// ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2397 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2397";

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
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Autoreferer, 1L);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            object? referer = null;
            if (steps.Result != CurlCode.Ok)
            {
                context.StandardError.Write($"transfer failed: {CurlErrorText.Easy(steps.Result)}\n");
            }
            else
            {
                steps.Result = easy.GetInfo(CurlInfo.Referer, out referer);
            }

            SetRefererBack(context, steps, easy, referer as string);
        }
    }

    private static void SetRefererBack(LibTestContext context, LibTestSteps steps, CurlEasy easy, string? referer)
    {
        if (referer is null)
        {
            context.StandardError.Write("CURLINFO_REFERER was not populated\n");
            return;
        }

        steps.Result = easy.SetOption(CurlOption.Referer, referer);
    }
}
