using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
    /// <summary>
    /// Tells the timer callback the next timeout when it changed, or, with <paramref name="tellsAgain" />, whenever
    /// it is not -1: after <see cref="CurlMultiSockets.Timeout" />, whose timer is spent, and on each add, as
    /// curl 8.22.0's <c>curl_multi_add_handle</c> tells 0 for every handle it adds (CGR-034 C_21).
    /// </summary>
    private CurlMultiCode UpdateTimer(bool tellsAgain)
    {
        MultiTimerCallback? callback = Options.Timer;
        if (callback is null)
        {
            return CurlMultiCode.Ok;
        }

        long timer = TimerMilliseconds();
        DateTimeOffset due = timeProvider.GetUtcNow().AddMilliseconds(timer);
        if (IsTimerUnchanged(timer, due) && !(tellsAgain && timer >= 0))
        {
            return CurlMultiCode.Ok;
        }

        reportedTimerMilliseconds = timer;
        reportedTimerDue = due;
        if (callback(timer) != -1)
        {
            return CurlMultiCode.Ok;
        }

        abortedByCallback = true;
        return CurlMultiCode.AbortedByCallback;
    }

    /// <summary>
    /// Whether the timer callback was told this already: the same value, or a positive one ending within a
    /// millisecond of the one told, so a deadline that has not moved is not told again as time passes.
    /// </summary>
    private bool IsTimerUnchanged(long timer, DateTimeOffset due) =>
        timer == reportedTimerMilliseconds || (timer > 0 && reportedTimerMilliseconds > 0 && (due - reportedTimerDue).Duration() <= TimeSpan.FromMilliseconds(1));

    /// <summary>
    /// The timer callback's value: the handle's timeout with no transfer running or no socket callback set, the
    /// earliest deadline of the transfers when every running one waits on its real socket (-1 when none has one,
    /// as libcurl's), 0 while one on a socket wait still computes, waiting on nothing after
    /// <see cref="SocketWaitSettleLimit" />, as libcurl's <c>Curl_expire(data, 0)</c> for a transfer that can run on
    /// (ADR-0986), else at most <see cref="RunningTransferCheckInterval" />, for transfers the application cannot
    /// poll a socket for (ADR-0973).
    /// </summary>
    private long TimerMilliseconds()
    {
        long handleTimeout = TimeoutMilliseconds();
        List<MultiEntry> running = entries.FindAll(entry => !entry.Done);
        return running.Count == 0 || Options.Socket is null ? handleTimeout : RunningTimerMilliseconds(handleTimeout, running);
    }

    // A handle timeout other than 0 means no transfer waits to start or to be reported (TimeoutMilliseconds), so
    // every running one has a perform in progress.
    private long RunningTimerMilliseconds(long handleTimeout, List<MultiEntry> running)
    {
        if (handleTimeout != 0 && running.TrueForAll(IsWaitingOnItsSocket))
        {
            return MillisecondsUntil(running.Min(EarliestDeadlineOf));
        }

        return CheckIntervalMilliseconds(handleTimeout, running);
    }

    // 0 while a running transfer on a socket wait computes (ADR-0986), else the check interval within the handle's timeout.
    private static long CheckIntervalMilliseconds(long handleTimeout, List<MultiEntry> running)
    {
        if (running.Exists(IsComputing))
        {
            return 0;
        }

        long check = (long)RunningTransferCheckInterval.TotalMilliseconds;
        return handleTimeout < 0 ? check : Math.Min(handleTimeout, check);
    }

    // A running transfer on a socket wait that waits on nothing computes, or has ended and waits to be reported:
    // either way the next action has work for it.
    private static bool IsComputing(MultiEntry entry) => entry.SocketWait is { Waiting: CurlPoll.None };

    private static DateTimeOffset? EarliestDeadlineOf(MultiEntry entry) => entry.SocketWait!.EarliestDeadline;

    private long MillisecondsUntil(DateTimeOffset? deadline) =>
        deadline is { } due ? Math.Max(0, (long)Math.Ceiling((due - timeProvider.GetUtcNow()).TotalMilliseconds)) : -1;

}
