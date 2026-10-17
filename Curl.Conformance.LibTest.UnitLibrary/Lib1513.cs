using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1513.c</c>: a GET with a seven-second timeout, <c>CURLOPT_NOSIGNAL</c> and a progress callback
/// (<see cref="KillProgress"/>) that writes <c>PROGRESSFUNCTION called</c> on stdout and fails at once, so the
/// transfer ends with <c>CURLE_ABORTED_BY_CALLBACK</c>, the program's code.
/// </summary>
public sealed class Lib1513 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1513";

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
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Timeout, 7L),
                    (CurlOption.Nosignal, 1L),
                    (CurlOption.Progressfunction, (ProgressCallback)((_, _, _, _) => KillProgress(context))),
                    (CurlOption.Progressdata, null),
                    (CurlOption.Noprogress, 0L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>progressKiller</c>: writes <c>PROGRESSFUNCTION called</c> on stdout and answers 1, failing the transfer.</summary>
    /// <param name="context">The program's context.</param>
    /// <returns>1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static int KillProgress(LibTestContext context)
    {
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        output.Write("PROGRESSFUNCTION called\n");
        return 1;
    }
}
