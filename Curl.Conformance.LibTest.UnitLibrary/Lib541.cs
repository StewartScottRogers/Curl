using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib541.c</c>: two verbose FTP uploads of the file <c>libtest_arg2</c> names from the same open file, the
/// second without rewinding it, so it sends no content. The file is opened as <see cref="LibTestUploadFile"/>
/// does; the second upload runs only when the first succeeds.
/// </summary>
public sealed class Lib541 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib541";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestUploadFile.RunWithFileAsync(
            context,
            source => LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, source, cancellationToken)));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="source">The open file to upload.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(source);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            bool firstSucceeded = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Upload, 1L),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Readdata, source),
                ],
                cancellationToken).ConfigureAwait(false);
            if (firstSucceeded)
            {
                await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
