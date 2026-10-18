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
/// One run of <c>lib582.c</c> after its file opened: the read and write socket sets its socket callback keeps
/// (<c>Add socket fd N for read</c>, <c>Remove socket fd N</c> on stderr), the deadline its timer callback sets,
/// and the loop that waits on them and hands each set's sockets and the elapsed timer to
/// <c>curl_multi_socket_action</c> until a message comes. A failed socket action is written
/// (<c>curl error on read (N) text</c>) and the loop goes on; an unsuccessful transfer writes
/// <c>Error uploading file.</c> and answers <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
/// <param name="context">The program's context.</param>
/// <param name="timeProvider">The program's clock source, for the timer deadline.</param>
/// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
/// <param name="source">The file to upload.</param>
public sealed class Lib582Run(LibTestContext context, TimeProvider timeProvider, LibTestClock clock, Stream source)
{
    private const int SelectTimeoutMilliseconds = 10_000;
    private const int IdleTimeoutMilliseconds = 100;

    private readonly LibTestContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly LibTestClock clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly Stream source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly List<CurlSocket> readSockets = [];
    private readonly List<CurlSocket> writeSockets = [];
    private long? timerDeadline;

    /// <summary>The sockets the callback asked to be waited on for reading.</summary>
    public IReadOnlyList<CurlSocket> ReadSockets => readSockets;

    /// <summary>The sockets the callback asked to be waited on for writing.</summary>
    public IReadOnlyList<CurlSocket> WriteSockets => writeSockets;

    /// <summary>The body after <c>curl_global_init</c>: the handles, the options, the loop and cleanup type PB.</summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Upload, 1L),
                (CurlOption.Url, context.Url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Readdata, source),
                (CurlOption.Userpwd, context.Argument3),
                (CurlOption.SshPublicKeyfile, context.Argument4),
                (CurlOption.SshPrivateKeyfile, context.Arguments.ElementAtOrDefault(4)),
                (CurlOption.SslVerifyhost, 0L),
                (CurlOption.InfilesizeLarge, source.Length),
            ]);
            if (!steps.MultiInit(library, out CurlMulti? multi))
            {
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                multi.SetOption(CurlMultiOption.SocketFunction, (CurlMultiSocketCallback)SocketCallback);
                multi.SetOption(CurlMultiOption.TimerFunction, (CurlMultiTimerCallback)TimerCallback);
                if (steps.MultiAddHandle(multi, easy))
                {
                    await RunUntilCompleteAsync(steps, multi, cancellationToken).ConfigureAwait(false);
                }

                multi.RemoveHandle(easy);
            }
        }
    }

    /// <summary><c>t582_curlSocketCallback</c>: adds the socket to the sets <paramref name="what"/> names, or removes it from both.</summary>
    /// <param name="easy">The transfer.</param>
    /// <param name="socket">The socket.</param>
    /// <param name="what">What to wait for.</param>
    /// <param name="socketState">Not used.</param>
    /// <returns>0.</returns>
    public int SocketCallback(CurlEasy easy, CurlSocket socket, CurlPoll what, object? socketState)
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
            context.StandardError.Write($"Remove socket fd {socket.Handle}\n");
            readSockets.Remove(socket);
            writeSockets.Remove(socket);
        }

        return 0;
    }

    /// <summary><c>t582_curlTimerCallback</c>: sets the deadline <paramref name="timeoutMilliseconds"/> from now, or clears it for -1.</summary>
    /// <param name="multi">The multi handle.</param>
    /// <param name="timeoutMilliseconds">The timeout, -1 for none.</param>
    /// <returns>0.</returns>
    public int TimerCallback(CurlMulti multi, long timeoutMilliseconds)
    {
        timerDeadline = timeoutMilliseconds == -1 ? null : clock.Now() + (timeoutMilliseconds * timeProvider.TimestampFrequency / 1000);
        return 0;
    }

    private void AddSocket(List<CurlSocket> sockets, CurlSocket socket, string what)
    {
        context.StandardError.Write($"Add socket fd {socket.Handle} for {what}\n");
        sockets.Remove(socket);
        sockets.Add(socket);
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
            context.StandardError.Write("Error uploading file.\n");
            steps.Result = LibTestErrorCodes.MajorBad;
        }
    }

    // t582_checkForCompletion: whether a message came, and whether the last one was a successful done.
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
                context.StandardError.Write($"Got an unexpected message from curl: {(int)message.Type}\n");
            }
        }

        return completed;
    }

    // One turn: select_test over the sets, socket_action on each socket of each set, then on the timer once it
    // has elapsed, then abort_on_test_timeout.
    private async ValueTask<bool> TurnAsync(LibTestSteps steps, CurlMulti multi, CancellationToken cancellationToken)
    {
        int timeout = timerDeadline is not null ? (int)((RemainingMicroseconds() + 999) / 1000)
            : readSockets.Count + writeSockets.Count == 0 ? IdleTimeoutMilliseconds
            : SelectTimeoutMilliseconds;
        if (!await steps.SelectTestAsync(multi, timeout, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ActOnSocketsAsync(multi, readSockets, CurlSocketSelect.In, "read", cancellationToken).ConfigureAwait(false);
        await ActOnSocketsAsync(multi, writeSockets, CurlSocketSelect.Out, "write", cancellationToken).ConfigureAwait(false);
        if (timerDeadline is not null && RemainingMicroseconds() == 0)
        {
            await SocketActionAsync(multi, CurlMultiSockets.Timeout, CurlSocketSelect.None, "timeout", cancellationToken).ConfigureAwait(false);
        }

        return steps.AbortOnTestTimeout(clock);
    }

    // t582_checkFdSet: socket_action on every socket in the set; the callbacks may change the set as it is walked.
    private async ValueTask ActOnSocketsAsync(CurlMulti multi, List<CurlSocket> sockets, CurlSocketSelect events, string name, CancellationToken cancellationToken)
    {
        for (int index = 0; index < sockets.Count; index++)
        {
            await SocketActionAsync(multi, sockets[index], events, name, cancellationToken).ConfigureAwait(false);
        }
    }

    // notifyCurl: a failure is written and the loop goes on.
    private async ValueTask SocketActionAsync(CurlMulti multi, CurlSocket socket, CurlSocketSelect events, string info, CancellationToken cancellationToken)
    {
        var (code, _) = await multi.SocketActionAsync(socket, events, cancellationToken).ConfigureAwait(false);
        if (code != CurlMultiCode.Ok)
        {
            context.StandardError.Write($"curl error on {info} ({(int)code}) {CurlErrorText.Multi(code)}\n");
        }
    }

    // t582_getMicroSecondTimeout: the time left to the deadline, never below 0.
    private long RemainingMicroseconds() =>
        Math.Max(0, (timerDeadline!.Value - clock.Now()) * 1_000_000 / timeProvider.TimestampFrequency);
}
