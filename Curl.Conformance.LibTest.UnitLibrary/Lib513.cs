using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib513.c</c>: a POST of one byte (<c>CURLOPT_POSTFIELDSIZE</c> 1) whose read callback answers
/// <c>CURL_READFUNC_ABORT</c>, so the transfer ends with <c>CURLE_ABORTED_BY_CALLBACK</c>; verbose and headers on.
/// </summary>
public sealed class Lib513 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib513";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary><c>t513_read_cb</c>: answers <c>CURL_READFUNC_ABORT</c> whatever it is given.</summary>
    /// <param name="buffer">The buffer, left untouched.</param>
    /// <returns><see cref="ReadCallbackResult.Abort"/>.</returns>
    public static long ReadAbort(Span<byte> buffer) => ReadCallbackResult.Abort;

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
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
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Post, 1L),
                    (CurlOption.Postfieldsize, 1L),
                    (CurlOption.Readfunction, (ReadCallback)ReadAbort),
                    (CurlOption.Readdata, null),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
