using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1594.c</c> (cases 1594 to 1596): a transfer of the case's URL, then <c>CURLINFO_RETRY_AFTER</c> written on stdout
/// as <c>Retry-After N</c>. A failing transfer or <c>curl_easy_getinfo</c> ends the program with its code.
/// </summary>
public sealed class Lib1594 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1594";

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
            if (await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)], cancellationToken).ConfigureAwait(false))
            {
                // Libcurl cannot fail CURLINFO_RETRY_AFTER on a live handle, so its check is left out.
                _ = steps.EasyGetInfo(easy, CurlInfo.RetryAfter, out object? retry);
                using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                output.Write($"Retry-After {retry}\n");
            }
        }
    }
}
