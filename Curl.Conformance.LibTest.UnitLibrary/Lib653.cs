using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib653.c</c>: a verbose one-part <c>CURLOPT_MIMEPOST</c> with headers on, <c>name=short value</c>, then,
/// when it succeeded, the same post again after the part's data changed to a longer value, so the second
/// request's length must follow the change.
/// </summary>
public sealed class Lib653 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib653";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>global_init</c>: the two posts.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
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
            using CurlMime mime = library.MimeInit();
            CurlMimePart field = mime.AddPart();
            field.SetName("name");
            field.SetData("short value");
            if (await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Header, 1L),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Mimepost, mime),
                    (CurlOption.Noprogress, 1L),
                ],
                cancellationToken).ConfigureAwait(false))
            {
                field.SetData("long value for length change");
                _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
