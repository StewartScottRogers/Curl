using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1556.c</c>: a verbose GET whose header callback remembers the longest header it was handed, then
/// <c>Max = N</c> on stdout after the body (case 1556 sends a 100009-byte header). A failing perform writes
/// <c>lib1556.c:41 curl_easy_perform() failed, with code N (TEXT)</c> on stderr and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib1556 : ILibTest
{
    /// <summary>The <c>lib1556.c</c> line of the failing perform's <c>curl_mfprintf</c>.</summary>
    public const int PerformFailedLine = 41;

    /// <inheritdoc/>
    public string ProgramName => "lib1556";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
            long largest = 0;
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Headerfunction, (HeaderCallback)(header =>
                {
                    largest = Math.Max(largest, header.Length);
                    return header.Length;
                })),
                (CurlOption.Verbose, 1L),
                (CurlOption.Url, context.Url),
            ]);

            CurlCode code = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (code != CurlCode.Ok)
            {
                context.StandardError.Write($"lib1556.c:{PerformFailedLine} curl_easy_perform() failed, with code {(int)code} ({CurlErrorText.Easy(code)})\n");
                steps.Result = LibTestErrorCodes.MajorBad;
                return;
            }

            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"Max = {largest}\n");
        }
    }
}
