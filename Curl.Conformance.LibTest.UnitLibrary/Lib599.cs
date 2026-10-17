using System.Globalization;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib599.c</c>: a verbose download with headers on that follows redirects, whose progress callback prints
/// <c>%.0f &gt; %.0f !!</c> on stdout and aborts once the download count passes a known total. Once it succeeded,
/// <c>CL %.0f</c> with <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD</c> replaces the file in <c>libtest_arg2</c>.
/// </summary>
public sealed class Lib599 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib599";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: <c>curl_easy_init</c>, the options, the perform and the length.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Noprogress, 0L),
                    (CurlOption.Progressfunction, (ProgressCallback)((total, now, _, _) => Progress(context, total, now))),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Followlocation, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false))
            {
                _ = steps.EasyGetInfo(easy, CurlInfo.ContentLengthDownload, out object? length);
                double contentLength = Convert.ToDouble(length, CultureInfo.InvariantCulture);
                LibTestArgumentFile.TryWrite(context.Argument2, string.Create(CultureInfo.InvariantCulture, $"CL {contentLength:F0}\n"), append: false);
            }
        }
    }

    /// <summary><c>t599_progress_callback</c>: -1 with a line on stdout when the download count passes a known total.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="downloadTotal">The download total, 0 when unknown.</param>
    /// <param name="downloadNow">The bytes downloaded so far.</param>
    /// <returns>-1 to abort, else 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static int Progress(LibTestContext context, double downloadTotal, double downloadNow)
    {
        if (downloadTotal <= 0.0 || downloadNow <= downloadTotal)
        {
            return 0;
        }

        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        output.Write(string.Create(CultureInfo.InvariantCulture, $"{downloadTotal:F0} > {downloadNow:F0} !!\n"));
        return -1;
    }
}
