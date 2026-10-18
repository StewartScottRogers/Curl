using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1508.c</c>: <c>global_init</c>, <c>multi_init</c> and the cleanup with no transfer, then <c>We are done</c> on
/// stdout, whether or not a step failed.
/// </summary>
public sealed class Lib1508 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1508";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        LibTestResult result = await LibTestRun.WithMacroLibraryAsync(context, TestAsync).ConfigureAwait(false);
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        output.Write("We are done\n");
        return result;
    }

    /// <summary>The program's body after <c>global_init</c>: <c>multi_init</c> and <c>curl_multi_cleanup</c>.</summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.MultiInit(library, out CurlMulti? multi))
        {
            await multi.DisposeAsync().ConfigureAwait(false);
        }
    }
}
