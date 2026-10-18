using System.Text;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1907.c</c>: a verbose GET of the case's URL, which has no scheme, with an error buffer; a perform that succeeds
/// writes <c>failure expected, ...</c> to stderr, and the handle's <c>CURLINFO_EFFECTIVE_URL</c> is then written to stdout
/// as <c>Effective URL: &lt;url&gt;</c>. The perform's code is the program's; an easy handle that cannot be created ends
/// it with <see cref="LibTestErrorCodes.EasyInit"/>.
/// </summary>
public sealed class Lib1907 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1907";

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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var errorBuffer = new StringBuilder();
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Errorbuffer, errorBuffer);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                context.StandardError.Write($"failure expected, curl_easy_perform returned 0: <{CurlErrorText.Easy(CurlCode.Ok)}>, <{errorBuffer}>\n");
            }

            // Libcurl answers CURLINFO_EFFECTIVE_URL on a live handle without fail, so its check is left out.
            _ = easy.GetInfo(CurlInfo.EffectiveUrl, out object? effectiveUrl);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"Effective URL: {effectiveUrl}\n");
        }
    }
}
