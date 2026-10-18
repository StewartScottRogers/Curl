using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3027.c</c>: a transfer of the case's URL with <c>CURLOPT_FILETIME</c> on whose server fails <c>MDTM</c>, so its
/// <c>CURLINFO_FILETIME</c> must be -1; any other time ends the program with <see cref="CurlCode.UnsupportedProtocol"/>.
/// Otherwise the transfer's (or the getinfo's) code is the program's. A library that gives no handle ends with
/// <see cref="CurlCode.Ok"/>.
/// </summary>
public sealed class Lib3027 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib3027";

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
        ArgumentNullException.ThrowIfNull(library);
        // start_test_timing only feeds abort_on_test_timeout, which this program never calls, so it is left out.
        // Upstream's `if(curl)`: a library cleaned up gives no handle, and the program ends with CURLE_OK.
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Filetime, 1L);
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            // Libcurl answers CURLINFO_FILETIME on any live handle, so upstream's check of the getinfo's code is left out.
            _ = easy.GetInfo(CurlInfo.Filetime, out object? fileTime);
            if (Convert.ToInt64(fileTime, System.Globalization.CultureInfo.InvariantCulture) != -1)
            {
                steps.Result = CurlCode.UnsupportedProtocol;
            }
        }
    }
}
