using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3010.c</c>: a transfer of the case's URL, then <c>Retry-After N</c> on stdout from its
/// <c>CURLINFO_RETRY_AFTER</c>, a transfer of its <c>CURLINFO_REDIRECT_URL</c>, a <c>curl_easy_reset</c>, and
/// <c>Retry-After N</c> again, which the reset clears to 0. A failing transfer writes its failure line and its code is
/// the program's; a library that gives no handle ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib3010 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib3010";

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
        ArgumentNullException.ThrowIfNull(library);
        steps.Result = LibTestErrorCodes.MajorBad;
        // Upstream's `if(curl)`: a library cleaned up gives no handle, and the program ends with TEST_ERR_MAJOR_BAD.
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Url, context.Url);
            if (!await steps.EasyPerformOrReportAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            _ = easy.GetInfo(CurlInfo.RedirectUrl, out object? followUrl);
            WriteRetryAfter(context, easy);
            _ = easy.SetOption(CurlOption.Url, followUrl);
            if (!await steps.EasyPerformOrReportAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            easy.Reset();
            WriteRetryAfter(context, easy);
        }
    }

    private static void WriteRetryAfter(LibTestContext context, CurlEasy easy)
    {
        _ = easy.GetInfo(CurlInfo.RetryAfter, out object? retryAfter);
        context.StandardOutput.Write(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Retry-After {retryAfter}\n")));
    }
}
