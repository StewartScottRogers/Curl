using System.Text;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1549.c</c>: a GET with headers in the output and the cookie engine on (<c>CURLOPT_COOKIEFILE</c> <c>""</c>);
/// when it succeeds, every cookie <c>CURLINFO_COOKIELIST</c> holds is a line on stdout and <c>N cookies</c> a line on
/// stderr, so expired cookies the response sets must not be listed. The transfer's code, or the info's, is the
/// program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib1549 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1549";

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
            if (!await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Header, 1L),
                        (CurlOption.Cookiefile, string.Empty),
                    ],
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            WriteCookies(context, steps, easy);
        }
    }

    /// <summary>The cookie list, one line each on stdout, then their count on stderr.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void WriteCookies(LibTestContext context, LibTestSteps steps, CurlEasy easy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        int count = 0;
        // Libcurl answers the cookie list, possibly empty, on any live handle, so upstream's result and NULL checks never fail.
        _ = steps.EasyGetInfo(easy, CurlInfo.Cookielist, out object? value);
        foreach (string cookie in (IReadOnlyList<string>)value!)
        {
            context.StandardOutput.Write(Encoding.UTF8.GetBytes($"{cookie}\n"));
            count++;
        }

        context.StandardError.Write($"{count} cookies\n");
    }
}
