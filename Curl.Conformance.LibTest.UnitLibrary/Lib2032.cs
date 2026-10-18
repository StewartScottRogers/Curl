using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2032.c</c> (libntlmconnect): <see cref="Lib2032Connections.Handles"/> verbose fresh-connection requests on one
/// multi handle, the third (<c>0200</c>, NTLM) after two of <c>0100</c> (Basic), all as <c>testuser:testpass</c> with their
/// headers in the body, each started only once the previous one holds no socket in <c>curl_multi_fdset</c>; each write
/// checks the handle kept its socket (<see cref="Lib2032Connections"/>). The loop runs <c>multi_perform</c>,
/// <c>multi_fdset</c>, <c>multi_timeout</c> and a wait for that timeout (5 ms with none), writing its state lines to stderr,
/// until nothing runs after the last start; then every handle's byte count is written as <c>Data connection N: M</c>. The
/// program's <c>result</c> is its last failing step, or <c>ntlmcb_res</c> as it stood when the last handle started.
/// </summary>
/// <param name="timeProvider">The time the hang timeout is measured on.</param>
public sealed class Lib2032(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The wait when <c>curl_multi_timeout</c> gives none.</summary>
    public const int NoTimeoutWaitMilliseconds = 5;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private enum HandleState
    {
        ReadyForNewHandle,
        NeedSocketForNewHandle,
        NoMoreHandles,
    }

    /// <summary>How a turn of the loop ends: the loop stops, goes round again at once, or waits first.</summary>
    private enum Turn
    {
        Stop,
        Again,
        Wait,
    }

    /// <inheritdoc/>
    public string ProgramName => "lib2032";

    /// <summary>The loop's <c>num_handles</c> and <c>state</c>.</summary>
    private sealed class LoopState
    {
        public int Started { get; set; }

        public HandleState State { get; set; } = HandleState.ReadyForNewHandle;
    }

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
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
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        var connections = new Lib2032Connections(context.StandardError);
        var handles = new CurlEasy?[Lib2032Connections.Handles];
        await using (multi.ConfigureAwait(false))
        {
            await LoopAsync(context, steps, library, clock, multi, connections, handles, cancellationToken).ConfigureAwait(false);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            for (int index = 0; index < handles.Length; index++)
            {
                output.Write(string.Create(CultureInfo.InvariantCulture, $"Data connection {index}: {connections.Received[index]}\n"));
                _ = multi.RemoveHandle(handles[index]);
                await DisposeAsync(handles[index]).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask DisposeAsync(CurlEasy? easy)
    {
        if (easy is not null)
        {
            await easy.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask LoopAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        LibTestClock clock,
        CurlMulti multi,
        Lib2032Connections connections,
        CurlEasy?[] handles,
        CancellationToken cancellationToken)
    {
        var loop = new LoopState();
        while (await TurnAsync(context, steps, library, clock, multi, connections, handles, loop, cancellationToken).ConfigureAwait(false))
        {
        }
    }

    /// <summary>One turn of the loop: whether the loop goes round again.</summary>
    private static async ValueTask<bool> TurnAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        LibTestClock clock,
        CurlMulti multi,
        Lib2032Connections connections,
        CurlEasy?[] handles,
        LoopState loop,
        CancellationToken cancellationToken)
    {
        if (loop.State == HandleState.ReadyForNewHandle && !StartNextHandle(context, steps, library, multi, connections, handles, loop))
        {
            return false;
        }

        var (goesOn, running) = await PerformTurnAsync(context, steps, clock, multi, loop.State, cancellationToken).ConfigureAwait(false);
        Turn turn = goesOn ? AfterPerform(context, steps, multi, loop, running) : Turn.Stop;
        return turn == Turn.Wait
            ? await WaitTurnAsync(context, steps, clock, multi, loop.Started, running, cancellationToken).ConfigureAwait(false)
            : turn == Turn.Again;
    }

    private static bool StartNextHandle(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        CurlMulti multi,
        Lib2032Connections connections,
        CurlEasy?[] handles,
        LoopState loop)
    {
        if (!StartHandle(context, steps, library, multi, connections, handles, loop.Started))
        {
            return false;
        }

        loop.Started++;
        loop.State = HandleState.NeedSocketForNewHandle;
        steps.Result = connections.CallbackResult;
        return true;
    }

    /// <summary>What follows a successful <c>multi_perform</c>: the end check, <c>multi_fdset</c> and the state change.</summary>
    private static Turn AfterPerform(LibTestContext context, LibTestSteps steps, CurlMulti multi, LoopState loop, int running)
    {
        if ((running == 0 && loop.State == HandleState.NoMoreHandles)
            || !steps.MultiFdSet(multi, new HashSet<CurlSocket>(), new HashSet<CurlSocket>(), new HashSet<CurlSocket>(), out nint maxSocket))
        {
            return Turn.Stop;
        }

        HandleState? next = AdvanceAfterFdSet(context, loop.State, maxSocket, loop.Started);
        if (next is null)
        {
            return Turn.Again;
        }

        loop.State = next.Value;
        return Turn.Wait;
    }

    private static async ValueTask<(bool GoesOn, int RunningHandles)> PerformTurnAsync(
        LibTestContext context,
        LibTestSteps steps,
        LibTestClock clock,
        CurlMulti multi,
        HandleState state,
        CancellationToken cancellationToken)
    {
        var (goesOn, running) = await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false);
        if (!goesOn)
        {
            return (false, running);
        }

        context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"lib2032.c:156 running {running} state {(int)state}\n"));
        return (steps.AbortOnTestTimeout(clock), running);
    }

    /// <summary>The state after <c>multi_fdset</c>, or <see langword="null"/> to go round again while a new handle still holds a socket.</summary>
    private static HandleState? AdvanceAfterFdSet(LibTestContext context, HandleState state, nint maxSocket, int started)
    {
        if (state != HandleState.NeedSocketForNewHandle)
        {
            return state;
        }

        if (maxSocket != -1)
        {
            context.StandardError.Write("Warning: socket did not open immediately for new handle (trying again)\n");
            return null;
        }

        HandleState next = started < Lib2032Connections.Handles ? HandleState.ReadyForNewHandle : HandleState.NoMoreHandles;
        context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"lib2032.c:181 new state {(int)next}\n"));
        return next;
    }

    private static async ValueTask<bool> WaitTurnAsync(LibTestContext context, LibTestSteps steps, LibTestClock clock, CurlMulti multi, int started, int running, CancellationToken cancellationToken)
    {
        if (!steps.MultiTimeout(multi, out long timeout))
        {
            return false;
        }

        context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"lib2032.c:189 num_handles {started} timeout {timeout} running {running}\n"));
        if (timeout == -1 && running == 0 && started == Lib2032Connections.Handles)
        {
            return false;
        }

        int wait = timeout == -1 ? NoTimeoutWaitMilliseconds : (int)Math.Min(timeout, int.MaxValue);
        return await steps.SelectTestAsync(multi, wait, cancellationToken).ConfigureAwait(false) && steps.AbortOnTestTimeout(clock);
    }

    private static bool StartHandle(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        CurlMulti multi,
        Lib2032Connections connections,
        CurlEasy?[] handles,
        int index)
    {
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return false;
        }

        handles[index] = easy;
        bool ntlm = index % 3 == 2;
        return steps.SetOptionsAndAddHandle(
            easy,
            [
                (CurlOption.Httpauth, (long)(ntlm ? HttpAuthenticationMethods.Ntlm : HttpAuthenticationMethods.Basic)),
                (CurlOption.FreshConnect, 1L),
                (CurlOption.Url, context.Url + (ntlm ? "0200" : "0100")),
                (CurlOption.Verbose, 1L),
                (CurlOption.Httpget, 1L),
                (CurlOption.Userpwd, "testuser:testpass"),
                (CurlOption.Writefunction, (WriteCallback)(data => connections.Write(index, easy, data))),
                (CurlOption.Header, 1L),
            ],
            multi);
    }
}
