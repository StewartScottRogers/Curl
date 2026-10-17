using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1506.c</c>: <see cref="HandleCount"/> verbose handles with headers, each to its own
/// <c>serverN.example.com</c> resolved to the test server (<see cref="LibTestServerResolves"/>), through one multi
/// handle allowed three connections (<c>CURLMOPT_MAXCONNECTS</c>), added one at a time, each run to its end by
/// <c>multi_perform</c> and a one-second <c>select()</c> with a 1 ms wait after it, so the oldest connection is the one
/// closed. Cleanup type PB. Its <c>easy_setopt</c> and <c>multi_setopt</c> checks are left out: Libcurl takes these
/// values without fail.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from, and its 1 ms waits.</param>
public sealed class Lib1506(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>NUM_HANDLES</c>: the easy handles the program makes.</summary>
    public const int HandleCount = 4;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1506";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        CurlSlist? resolve = LibTestServerResolves.AppendAndWrite(context, HandleCount);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        LibTestResult result = await LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, resolve, cancellationToken)).ConfigureAwait(false);
        CurlSlist.FreeAll(resolve);
        return result;
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="resolve">The <c>CURLOPT_RESOLVE</c> list every handle gets.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CurlSlist? resolve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        var handles = new CurlEasy?[HandleCount];
        CurlMulti? multi = null;
        if (steps.MultiInit(library, out multi))
        {
            _ = steps.MultiSetOption(multi, CurlMultiOption.MaxConnects, 3L);
            if (InitHandles(context, steps, library, resolve, handles))
            {
                context.StandardError.Write("Start at URL 0\n");
                await TransferEachAsync(steps, clock, multi, handles, cancellationToken).ConfigureAwait(false);
            }
        }

        await CleanUpAsync(multi, handles).ConfigureAwait(false);
    }

    // Cleanup type PB: each handle removed and cleaned up, then the multi handle.
    private static async ValueTask CleanUpAsync(CurlMulti? multi, CurlEasy?[] handles)
    {
        foreach (CurlEasy? easy in handles)
        {
            multi?.RemoveHandle(easy);
            await DisposeAsync(easy).ConfigureAwait(false);
        }

        await DisposeAsync(multi).ConfigureAwait(false);
    }

    private static ValueTask DisposeAsync(IAsyncDisposable? handle) => handle?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static bool InitHandles(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlSlist? resolve, CurlEasy?[] handles)
    {
        for (int i = 0; i < handles.Length; i++)
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return false;
            }

            handles[i] = easy;
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, LibTestServerResolves.Url(context, i + 1)),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Resolve, resolve),
            ]);
        }

        return true;
    }

    private static async ValueTask TransferEachAsync(LibTestSteps steps, LibTestClock clock, CurlMulti multi, CurlEasy?[] handles, CancellationToken cancellationToken)
    {
        foreach (CurlEasy? easy in handles)
        {
            if (!steps.MultiAddHandle(multi, easy)
                || !await steps.PerformUntilDoneAsync(multi, clock, 1000, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await clock.WaitAsync(1, cancellationToken).ConfigureAwait(false);
        }
    }
}
