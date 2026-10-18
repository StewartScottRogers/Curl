using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1502.c</c>, which serves cases 1502 to 1505: a handle with headers and <c>CURLOPT_RESOLVE</c> pointing
/// <c>google.com:&lt;libtest_arg2&gt;</c> at <c>libtest_arg3</c> is duplicated (<c>curl_easy_duphandle</c>), the original
/// cleaned up, and the duplicate run through a multi handle by <c>multi_perform</c> and a one-second <c>select()</c>.
/// Each case then cleans up in its own order: 1502 type UA (multi, then easy, the handle still added), 1503 type PA
/// (remove, multi, easy), 1504 type UB (easy, then multi) and 1505 type PB (remove, easy, multi); any other number as 1502.
/// The program's checks of <c>curl_slist_append</c>, <c>easy_setopt</c> and <c>curl_easy_duphandle</c> are left out:
/// none can fail here.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib1502(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1502";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started here as upstream starts it after the init.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
        CurlSlist resolve = CurlSlist.Append(null, $"google.com:{context.Argument2}:{context.Argument3}");
        clock.StartTestTiming();
        CurlEasy? easy = null;
        CurlMulti? multi = null;
        if (steps.EasyInit(library, out CurlEasy? original))
        {
            _ = steps.EasySetOptions(original, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Header, 1L), (CurlOption.Resolve, resolve)]);
            easy = original.DupHandle();
            await original.DisposeAsync().ConfigureAwait(false);
            if (steps.MultiInit(library, out multi) && steps.MultiAddHandle(multi, easy))
            {
                await steps.PerformUntilDoneAsync(multi, clock, 1000, cancellationToken).ConfigureAwait(false);
            }
        }

        await CleanUpAsync(context.TestNumber, multi, easy).ConfigureAwait(false);
        CurlSlist.FreeAll(resolve);
    }

    private static async ValueTask CleanUpAsync(int testNumber, CurlMulti? multi, CurlEasy? easy)
    {
        foreach (IAsyncDisposable? handle in RemoveAndOrder(testNumber, multi, easy))
        {
            await DisposeAsync(handle).ConfigureAwait(false);
        }
    }

    // The proper sequences (1503, 1505) remove the handle first; the B sequences (1504, 1505) clean the easy handle up first.
    private static IAsyncDisposable?[] RemoveAndOrder(int testNumber, CurlMulti? multi, CurlEasy? easy)
    {
        if (RemovesFirst(testNumber))
        {
            multi?.RemoveHandle(easy);
        }

        return CleansUpEasyFirst(testNumber) ? [easy, multi] : [multi, easy];
    }

    private static bool RemovesFirst(int testNumber) => testNumber is 1503 or 1505;

    private static bool CleansUpEasyFirst(int testNumber) => testNumber is 1504 or 1505;

    private static ValueTask DisposeAsync(IAsyncDisposable? handle) => handle?.DisposeAsync() ?? ValueTask.CompletedTask;
}
