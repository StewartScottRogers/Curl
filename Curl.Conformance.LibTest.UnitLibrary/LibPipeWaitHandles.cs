using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The program <c>lib2402.c</c>, <c>lib2404.c</c> and <c>lib2502.c</c> share: <c>localhost:port:address</c> (from
/// <c>libtest_arg3</c> and <c>libtest_arg2</c>) on stdout and as every handle's <c>CURLOPT_RESOLVE</c>, then
/// <see cref="HandleCount"/> verbose handles with headers and <c>CURLOPT_PIPEWAIT</c>, each to
/// <c>https://localhost:port/path/&lt;N&gt;000i</c>, through one multi handle allowed one connection, added one at a time,
/// each run to its end by <c>multi_perform</c> and a one-second <c>select()</c> with a 1 ms wait after it. Cleanup type
/// PB. Each program adds its own options (<see cref="ProgramOptions"/>); their <c>easy_setopt</c> and
/// <c>multi_setopt</c> checks are left out, as Libcurl takes these values without fail.
/// </summary>
/// <param name="programNumber">The program's number, such as 2402.</param>
/// <param name="timeProvider">Where the program's hang timeout is measured from, and its 1 ms waits.</param>
public abstract class LibPipeWaitHandles(int programNumber, TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>NUM_HANDLES</c>: the easy handles the program makes.</summary>
    public const int HandleCount = 4;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => $"lib{programNumber}";

    /// <summary>The time the program's debug traces read.</summary>
    protected TimeProvider TimeProvider => timeProvider;

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string entry = $"localhost:{context.Argument3}:{context.Argument2}";
        context.StandardOutput.Write(Encoding.UTF8.GetBytes($"{entry}\n"));
        CurlSlist resolve = CurlSlist.Append(null, entry);
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
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CurlSlist? resolve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        var handles = new CurlEasy?[HandleCount];
        if (steps.MultiInit(library, out CurlMulti? multi))
        {
            _ = multi.SetOption(CurlMultiOption.MaxConnects, 1L);
            if (InitHandles(context, steps, library, resolve, handles))
            {
                context.StandardError.Write("Start at URL 0\n");
                await TransferEachAsync(steps, clock, multi, handles, cancellationToken).ConfigureAwait(false);
            }
        }

        await CleanUpAsync(multi, handles).ConfigureAwait(false);
    }

    /// <summary>The options this program sets on each handle after <c>CURLOPT_URL</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="index">The handle's index, from 0.</param>
    /// <returns>The options and their values, in the program's order.</returns>
    protected abstract IEnumerable<(CurlOption Option, object? Value)> ProgramOptions(LibTestContext context, int index);

    /// <summary>
    /// libcurl's own verbose output on the program's stderr, for the programs that set no debug function of their
    /// own: the engine writes nothing without a <c>CURLOPT_DEBUGFUNCTION</c>, so this one stands in for the default.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <returns>The <c>CURLOPT_DEBUGFUNCTION</c> option and its value.</returns>
    protected static IEnumerable<(CurlOption Option, object? Value)> DefaultVerboseOptions(LibTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [(CurlOption.Debugfunction, (DebugCallback)new LibTestDefaultVerboseTrace(context.StandardError).Trace)];
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

    private bool InitHandles(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlSlist? resolve, CurlEasy?[] handles)
    {
        for (int i = 0; i < handles.Length; i++)
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return false;
            }

            handles[i] = easy;
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, $"https://localhost:{context.Argument3}/path/{programNumber}{i + 1:D4}");
            foreach (var (option, value) in ProgramOptions(context, i))
            {
                _ = easy.SetOption(option, value);
            }

            _ = easy.SetOption(CurlOption.Pipewait, 1L);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Header, 1L);
            _ = easy.SetOption(CurlOption.Resolve, resolve);
        }

        return true;
    }
}
