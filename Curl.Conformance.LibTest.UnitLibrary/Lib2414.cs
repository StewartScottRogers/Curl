using Curl.Api.EngineContracts.Multi;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2414.c</c>: <c>curl_multi_wakeup</c>, <c>curl_multi_perform</c> and then <c>curl_multi_poll</c> for
/// <see cref="int.MaxValue"/> ms on an empty multi handle, which must end at once because of the wakeup. Their codes are
/// not checked. A failing <c>curl_multi_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2414 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2414";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the poll.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInitOrMajorBad(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            _ = multi.Wakeup();
            _ = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
            _ = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, int.MaxValue, cancellationToken).ConfigureAwait(false);
        }
    }
}
