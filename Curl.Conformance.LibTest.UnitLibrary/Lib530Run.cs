using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// One <c>testone(URL, timer_fail_at, socket_fail_at)</c> of <c>lib530.c</c>: the transfer driven by
/// <c>curl_multi_socket_action</c>, its socket callback keeping the program's read and write socket sets and
/// its timer callback the deadline, each answering -1 on the chosen call (0 for none). Every <c>stderr</c> line
/// is tagged <c>[T530-&lt;socket fail&gt;-&lt;timer fail&gt;] [&lt;socket calls&gt;/&lt;timer calls&gt;]</c>.
/// <c>select()</c> over the program's sets is <c>curl_multi_wait</c> for the same timeout, after which every
/// socket in the sets counts as ready (ADR-0663).
/// </summary>
/// <param name="context">The program's context.</param>
/// <param name="timeProvider">Where the timer and the hang timeout are measured from.</param>
/// <param name="timerFailAt">The timer callback call that answers -1, or 0 for none.</param>
/// <param name="socketFailAt">The socket callback call that answers -1, or 0 for none.</param>
public sealed class Lib530Run(LibTestContext context, TimeProvider timeProvider, int timerFailAt, int socketFailAt)
{
    private const int SelectTimeoutMilliseconds = 10_000;

    private readonly LibTestContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly LibTestClock clock = new(timeProvider);
    private readonly List<CurlSocket> readSockets = [];
    private readonly List<CurlSocket> writeSockets = [];
    private int socketCalls;
    private int timerCalls;
    private long? timerDeadline;

    /// <summary>The sockets the socket callback was told to wait on for reading.</summary>
    public IReadOnlyList<CurlSocket> ReadSockets => readSockets;

    /// <summary>The sockets the socket callback was told to wait on for writing.</summary>
    public IReadOnlyList<CurlSocket> WriteSockets => writeSockets;

    /// <summary><c>t530_tag</c>: the run's limits and how many times each callback has been called.</summary>
    /// <returns>The tag, such as <c>[T530-2-0] [1/1]</c>.</returns>
    public string Tag() => $"[T530-{socketFailAt}-{timerFailAt}] [{socketCalls}/{timerCalls}]";

    /// <summary>The run: its transfer, then its cleanup.</summary>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The run's <c>result</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public async ValueTask<CurlCode> TestOneAsync(CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        WriteMessage("start");
        clock.StartTestTiming();
        var steps = new LibTestSteps(context.StandardError);
        await TransferAsync(steps, library, cancellationToken).ConfigureAwait(false);
        WriteMessage("done");
        return steps.Result;
    }

    /// <summary><c>t530_curlSocketCallback</c>: adds the socket to the sets <paramref name="what"/> names, or removes it.</summary>
    /// <param name="easy">The transfer's handle.</param>
    /// <param name="socket">The socket.</param>
    /// <param name="what">What to wait for on the socket.</param>
    /// <param name="socketState">The value <c>curl_multi_assign</c> gave the socket.</param>
    /// <returns>-1 on the chosen call, else 0.</returns>
    public int SocketCallback(CurlEasy easy, CurlSocket socket, CurlPoll what, object? socketState)
    {
        socketCalls++;
        WriteMessage("-> CURLMOPT_SOCKETFUNCTION");
        if (socketCalls == socketFailAt)
        {
            WriteMessage("<- CURLMOPT_SOCKETFUNCTION returns error");
            return -1;
        }

        UpdateSets(socket, what);
        return 0;
    }

    private void UpdateSets(CurlSocket socket, CurlPoll what)
    {
        // CURL_POLL_INOUT is CURL_POLL_IN | CURL_POLL_OUT.
        if ((what & CurlPoll.In) != 0)
        {
            AddSocket(readSockets, socket, "read");
        }

        if ((what & CurlPoll.Out) != 0)
        {
            AddSocket(writeSockets, socket, "write");
        }

        if (what == CurlPoll.Remove)
        {
            context.StandardError.Write($"{Tag()} remove socket fd {socket.Handle}\n");
            readSockets.Remove(socket);
            writeSockets.Remove(socket);
        }
    }

    /// <summary><c>t530_curlTimerCallback</c>: sets the deadline <paramref name="timeoutMilliseconds"/> from now, or clears it for -1.</summary>
    /// <param name="multi">The multi handle.</param>
    /// <param name="timeoutMilliseconds">The timeout, or -1 for none.</param>
    /// <returns>-1 on the chosen call, else 0.</returns>
    public int TimerCallback(CurlMulti multi, long timeoutMilliseconds)
    {
        timerCalls++;
        WriteMessage("-> CURLMOPT_TIMERFUNCTION");
        if (timerCalls == timerFailAt)
        {
            WriteMessage("<- CURLMOPT_TIMERFUNCTION returns error");
            return -1;
        }

        timerDeadline = timeoutMilliseconds == -1 ? null : clock.Now() + (timeoutMilliseconds * timeProvider.TimestampFrequency / 1000);
        return 0;
    }

    private void WriteMessage(string message) => context.StandardError.Write($"{Tag()} {message}\n");

    // t530_addFd: once per set, so it is removed before it is added.
    private void AddSocket(List<CurlSocket> sockets, CurlSocket socket, string what)
    {
        context.StandardError.Write($"{Tag()} add socket fd {socket.Handle} for {what}\n");
        sockets.Remove(socket);
        sockets.Add(socket);
    }

    // easy_init to curl_multi_add_handle, the loop, then the cleanup: remove_handle, easy and multi cleanup.
    // The setopt values cannot be refused by Libcurl, so their answers are not checked (ADR-0662).
    private async ValueTask TransferAsync(LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            WriteMessage("cleanup");
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: false);
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Debugdata, trace),
                (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                (CurlOption.Verbose, 1L),
            ]);
            if (!steps.MultiInit(library, out CurlMulti? multi))
            {
                WriteMessage("cleanup");
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                await AddAndRunAsync(steps, easy, multi, cancellationToken).ConfigureAwait(false);
                WriteMessage("cleanup");
                multi.RemoveHandle(easy);
            }
        }
    }

    private async ValueTask AddAndRunAsync(LibTestSteps steps, CurlEasy easy, CurlMulti multi, CancellationToken cancellationToken)
    {
        multi.SetOption(CurlMultiOption.SocketFunction, (CurlMultiSocketCallback)SocketCallback);
        multi.SetOption(CurlMultiOption.SocketData, this);
        multi.SetOption(CurlMultiOption.TimerFunction, (CurlMultiTimerCallback)TimerCallback);
        multi.SetOption(CurlMultiOption.TimerData, this);
        if (!steps.MultiAddHandle(multi, easy))
        {
            return;
        }

        if (await SocketActionAsync(multi, CurlMultiSockets.Timeout, CurlSocketSelect.None, "timeout", cancellationToken).ConfigureAwait(false) != CurlMultiCode.Ok)
        {
            steps.Result = LibTestErrorCodes.MajorBad;
            return;
        }

        await RunUntilCompleteAsync(steps, multi, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RunUntilCompleteAsync(LibTestSteps steps, CurlMulti multi, CancellationToken cancellationToken)
    {
        bool success;
        while (!CheckForCompletion(multi, out success))
        {
            if (!await TurnAsync(steps, multi, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        if (!success)
        {
            WriteMessage("Error getting file.");
            steps.Result = LibTestErrorCodes.MajorBad;
        }
    }

    // t530_checkForCompletion: whether a message came, and whether the last one was a successful done.
    private bool CheckForCompletion(CurlMulti multi, out bool success)
    {
        bool completed = false;
        success = false;
        while (multi.InfoRead(out _) is { } message)
        {
            completed = true;
            success = message.Type == CurlMultiMessageType.Done && message.Result == CurlCode.Ok;
            if (message.Type != CurlMultiMessageType.Done)
            {
                context.StandardError.Write($"{Tag()} got an unexpected message from curl: {(int)message.Type}\n");
            }
        }

        return completed;
    }

    // One turn of the loop: select_test over the sets, socket_action on each ready socket, then on the timer
    // once it has elapsed, then abort_on_test_timeout.
    private async ValueTask<bool> TurnAsync(LibTestSteps steps, CurlMulti multi, CancellationToken cancellationToken)
    {
        if (readSockets.Count + writeSockets.Count == 0)
        {
            WriteMessage("maxFd == 0");
            steps.Result = LibTestErrorCodes.MajorBad;
            return false;
        }

        int timeout = timerDeadline is null ? SelectTimeoutMilliseconds : (int)((RemainingMicroseconds() + 999) / 1000);
        if (!await steps.SelectTestAsync(multi, timeout, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (await ActOnSocketsAsync(multi, readSockets, CurlSocketSelect.In, "read", cancellationToken).ConfigureAwait(false) != CurlMultiCode.Ok
            || await ActOnSocketsAsync(multi, writeSockets, CurlSocketSelect.Out, "write", cancellationToken).ConfigureAwait(false) != CurlMultiCode.Ok)
        {
            steps.Result = LibTestErrorCodes.MajorBad;
            return false;
        }

        return await ActOnTimerAsync(steps, multi, cancellationToken).ConfigureAwait(false) && steps.AbortOnTestTimeout(clock);
    }

    private async ValueTask<bool> ActOnTimerAsync(LibTestSteps steps, CurlMulti multi, CancellationToken cancellationToken)
    {
        if (timerDeadline is null || RemainingMicroseconds() > 0
            || await SocketActionAsync(multi, CurlMultiSockets.Timeout, CurlSocketSelect.None, "timeout", cancellationToken).ConfigureAwait(false) == CurlMultiCode.Ok)
        {
            return true;
        }

        steps.Result = LibTestErrorCodes.BadTimeout;
        return false;
    }

    // t530_checkFdSet: socket_action on every socket in the set, stopping at the first failure. The callbacks
    // may change the set as it is walked, as upstream's array.
    private async ValueTask<CurlMultiCode> ActOnSocketsAsync(CurlMulti multi, List<CurlSocket> sockets, CurlSocketSelect events, string name, CancellationToken cancellationToken)
    {
        var code = CurlMultiCode.Ok;
        for (int index = 0; index < sockets.Count && code == CurlMultiCode.Ok; index++)
        {
            code = await SocketActionAsync(multi, sockets[index], events, name, cancellationToken).ConfigureAwait(false);
        }

        return code;
    }

    private async ValueTask<CurlMultiCode> SocketActionAsync(CurlMulti multi, CurlSocket socket, CurlSocketSelect events, string info, CancellationToken cancellationToken)
    {
        var (code, _) = await multi.SocketActionAsync(socket, events, cancellationToken).ConfigureAwait(false);
        if (code != CurlMultiCode.Ok)
        {
            context.StandardError.Write($"{Tag()} curl error on {info} ({(int)code}) {CurlErrorText.Multi(code)}\n");
        }

        return code;
    }

    // t530_getMicroSecondTimeout: the time left to the deadline, never below 0.
    private long RemainingMicroseconds() =>
        Math.Max(0, (timerDeadline!.Value - clock.Now()) * 1_000_000 / timeProvider.TimestampFrequency);
}
