using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib526.c</c>: <see cref="HandleCount"/> easy handles to the same URL, verbose, through one multi handle one
/// after another (<c>Start at URL 0</c>, then <c>Advancing to URL n</c> on stderr), driven by <c>multi_perform</c>
/// and a one-second <c>select()</c>. Test 527 cleans each finished handle up without removing it; test 532 instead
/// removes handle 0, resets it with <c>curl_easy_reset</c> and re-adds it for every URL. Cleanup type PB for 526 and
/// 528, UB for 527 (its handles only when a step failed) and 532; any other test number cleans up only the multi
/// handle, where upstream cleans up nothing.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib526(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>NUM_HANDLES</c>: the easy handles the program makes.</summary>
    public const int HandleCount = 4;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib526";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        var handles = new CurlEasy?[HandleCount];
        CurlMulti? multi = null;
        if (InitHandles(steps, library, handles)
            && steps.MultiInit(library, out multi)
            && steps.SetOptionsAndAddHandle(handles[0]!, OptionsOf(context), multi))
        {
            context.StandardError.Write("Start at URL 0\n");
            await TransferEachAsync(context, steps, clock, multi, handles, cancellationToken).ConfigureAwait(false);
        }

        await CleanUpAsync(context.TestNumber, steps, multi, handles).ConfigureAwait(false);
    }

    private static bool InitHandles(LibTestSteps steps, CurlLibrary library, CurlEasy?[] handles)
    {
        for (int i = 0; i < handles.Length; i++)
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return false;
            }

            handles[i] = easy;
        }

        return true;
    }

    private static async ValueTask TransferEachAsync(LibTestContext context, LibTestSteps steps, LibTestClock clock, CurlMulti multi, CurlEasy?[] handles, CancellationToken cancellationToken)
    {
        int current = 0;
        while (true)
        {
            var (goesOn, running) = await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false);
            if (!goesOn || (running == 0 && !await AdvanceAsync(context, steps, multi, handles, ++current).ConfigureAwait(false)))
            {
                return;
            }

            if (!await steps.SelectTestAsync(multi, 1000, cancellationToken).ConfigureAwait(false) || !steps.AbortOnTestTimeout(clock))
            {
                return;
            }
        }
    }

    private static async ValueTask<bool> AdvanceAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlEasy?[] handles, int next)
    {
        if (context.TestNumber == 527)
        {
            await handles[next - 1]!.DisposeAsync().ConfigureAwait(false);
            handles[next - 1] = null;
        }

        if (next >= handles.Length)
        {
            return false;
        }

        context.StandardError.Write($"Advancing to URL {next}\n");
        if (context.TestNumber != 532)
        {
            return steps.SetOptionsAndAddHandle(handles[next]!, OptionsOf(context), multi);
        }

        CurlEasy first = handles[0]!;
        multi.RemoveHandle(first);
        first.Reset();
        return steps.SetOptionsAndAddHandle(first, OptionsOf(context), multi);
    }

    private static async ValueTask CleanUpAsync(int testNumber, LibTestSteps steps, CurlMulti? multi, CurlEasy?[] handles)
    {
        if (testNumber is 526 or 528)
        {
            RemoveAll(multi, handles);
        }

        if (CleansUpTheHandles(testNumber, steps))
        {
            foreach (CurlEasy? easy in handles)
            {
                await DisposeAsync(easy).ConfigureAwait(false);
            }
        }

        await DisposeAsync(multi).ConfigureAwait(false);
    }

    private static void RemoveAll(CurlMulti? multi, CurlEasy?[] handles)
    {
        foreach (CurlEasy? easy in handles)
        {
            multi?.RemoveHandle(easy);
        }
    }

    private static bool CleansUpTheHandles(int testNumber, LibTestSteps steps) =>
        testNumber is 526 or 528 or 532 || (testNumber == 527 && steps.Result != Curl.Api.Codes.CurlCode.Ok);

    private static ValueTask DisposeAsync(IAsyncDisposable? handle) => handle?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Url, context.Url),
        (CurlOption.Verbose, 1L),
    ];
}
