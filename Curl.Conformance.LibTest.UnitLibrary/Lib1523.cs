using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1523.c</c>: the same slow GET, its body thrown away, twice on one handle with a progress callback that never
/// fails: first with a low speed limit of 1 byte a second over 2 seconds, which must succeed, then 12000 bytes a
/// second over 1 second, which must time out. Each run that does otherwise writes <c>error (N) &lt;error buffer&gt;</c>
/// on stderr; the program's code is the second run's, or 0 when it timed out. The program's
/// <c>curl_global_init</c> is unchecked, so it runs as <c>global_init</c>, which differs only with no engine.
/// </summary>
public sealed class Lib1523 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1523";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var errorBuffer = new StringBuilder();
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            _ = easy.SetOption(CurlOption.Errorbuffer, errorBuffer);
            _ = easy.SetOption(CurlOption.Noprogress, 0L);
            _ = easy.SetOption(CurlOption.Xferinfofunction, (TransferInfoCallback)((_, _, _, _) => 0));

            CurlCode result = await RunAsync(easy, 1, 2, cancellationToken).ConfigureAwait(false);
            if (result != CurlCode.Ok)
            {
                context.StandardError.Write($"error ({(int)result}) {errorBuffer}\n");
            }

            result = await RunAsync(easy, 12000, 1, cancellationToken).ConfigureAwait(false);
            if (result != CurlCode.OperationTimedOut)
            {
                context.StandardError.Write($"error ({(int)result}) {errorBuffer}\n");
            }
            else
            {
                result = CurlCode.Ok;
            }

            steps.Result = result;
        }
    }

    private static async ValueTask<CurlCode> RunAsync(CurlEasy easy, long limit, long seconds, CancellationToken cancellationToken)
    {
        _ = easy.SetOption(CurlOption.LowSpeedLimit, limit);
        _ = easy.SetOption(CurlOption.LowSpeedTime, seconds);
        return await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
    }
}
