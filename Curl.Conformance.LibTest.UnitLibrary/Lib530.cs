using Curl.Api.Codes;
using Libcurl.Global;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib530.c</c>: one transfer run five times through <c>curl_multi_socket_action</c> (<see cref="Lib530Run"/>),
/// with socket and timer callbacks that fail on a chosen call (<see cref="Runs"/>). The first run must succeed and
/// the others fail; a run that does otherwise writes <c>[T530-..] [..] FAILED: N</c> to <c>stderr</c>. The program
/// always answers <see cref="LibTestResult.Success"/>. Upstream's <c>testone</c> calls <c>curl_global_init</c> and
/// <c>curl_global_cleanup</c> each run; the port inits once around all five, because Libcurl's cleanup disposes
/// the case's engine (ADR-0663).
/// </summary>
/// <param name="timeProvider">Where the runs' timer and hang timeout are measured from.</param>
public sealed class Lib530(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary><c>test_lib530</c>'s <c>testone(URL, timer_fail_at, socket_fail_at)</c> calls, in order.</summary>
    public static IReadOnlyList<(int TimerFailAt, int SocketFailAt)> Runs { get; } = [(0, 0), (1, 0), (2, 0), (0, 2), (0, 3)];

    /// <inheritdoc/>
    public string ProgramName => "lib530";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LibTestRun.WithMacroLibraryAsync(context, (_, library) => TestAsync(context, library, timeProvider, cancellationToken));
    }

    /// <summary>The program's body after <c>curl_global_init</c>: the five runs.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">Where the runs' timer and hang timeout are measured from.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the last run has cleaned up.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        for (int index = 0; index < Runs.Count; index++)
        {
            var run = new Lib530Run(context, timeProvider, Runs[index].TimerFailAt, Runs[index].SocketFailAt);
            CurlCode result = await run.TestOneAsync(library, cancellationToken).ConfigureAwait(false);
            if ((index == 0) == (result != CurlCode.Ok))
            {
                context.StandardError.Write($"{run.Tag()} FAILED: {(int)result}\n");
            }
        }
    }
}
