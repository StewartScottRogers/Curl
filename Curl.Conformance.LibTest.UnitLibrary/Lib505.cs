using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib505.c</c>: an FTP upload of the file the second argument names, with <c>CURLOPT_POSTQUOTE</c>
/// <c>RNFR 505</c> and <c>RNTO 505-forreal</c> to rename it after the transfer. No second argument answers
/// <see cref="LibTestErrorCodes.Usage"/>; a file that cannot be opened or is empty answers
/// <see cref="LibTestErrorCodes.MajorBad"/> before <c>curl_global_init</c> (<see cref="LibTestUploadFile"/>). The
/// <c>curl_slist_append</c> checks are left out, as neither can fail.
/// </summary>
public sealed class Lib505 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib505";

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
    /// <param name="cancellationToken">Stops the transfer.</param>
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
            CurlSlist commands = CurlSlist.Append(CurlSlist.Append(null, "RNFR 505"), "RNTO 505-forreal");
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Upload, 1L),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Postquote, commands),
                    (CurlOption.Readdata, source),
                    (CurlOption.InfilesizeLarge, source.Length),
                ],
                cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(commands);
        }
    }
}
