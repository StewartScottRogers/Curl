using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib578.c</c>: a POST of 25 bytes (<c>"this is a short string.\n"</c> and its terminating zero) with headers on,
/// verbose, whose progress callback writes to the file in <c>libtest_arg2</c>, replacing it each call,
/// <c>PASSED, UL data matched data size</c> once the upload total and count both reach 25, else
/// <c>Progress callback called with UL %f out of %f</c>.
/// </summary>
public sealed class Lib578 : ILibTest
{
    /// <summary><c>t578_testdata</c> with the terminating zero <c>CURL_ARRAYSIZE</c> counts.</summary>
    public static readonly byte[] PostData = Encoding.ASCII.GetBytes("this is a short string.\n\0");

    /// <inheritdoc/>
    public string ProgramName => "lib578";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: <c>curl_easy_init</c>, the options and one perform.</summary>
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
            string? path = context.Argument2;
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Post, 1L),
                    (CurlOption.Postfieldsize, (long)PostData.Length),
                    (CurlOption.Postfields, PostData),
                    (CurlOption.Noprogress, 0L),
                    (CurlOption.Progressfunction, (ProgressCallback)((_, _, total, now) => Progress(path, total, now))),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t578_progress_callback</c>: writes the verdict on the upload counts to <paramref name="path"/>.</summary>
    /// <param name="path">The file in <c>libtest_arg2</c>.</param>
    /// <param name="uploadTotal">The upload total.</param>
    /// <param name="uploadNow">The bytes uploaded so far.</param>
    /// <returns>0, to go on.</returns>
    public static int Progress(string? path, double uploadTotal, double uploadNow)
    {
        string line = (long)uploadTotal == PostData.Length && (long)uploadNow == PostData.Length
            ? "PASSED, UL data matched data size\n"
            : string.Create(CultureInfo.InvariantCulture, $"Progress callback called with UL {uploadNow:F6} out of {uploadTotal:F6}\n");
        LibTestArgumentFile.TryWrite(path, line, append: false);
        return 0;
    }
}
