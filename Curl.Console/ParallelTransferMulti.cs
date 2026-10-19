using Curl.Api.EngineContracts.Multi;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Multi;

namespace Curl.Console;

/// <summary>
/// The one <c>Libcurl</c> <see cref="CurlMulti" /> a <c>-Z</c> run's transfers perform through (ADR-0628), as
/// curl's tool adds each parallel transfer's easy handle to its multi handle. Each attempt is
/// <see cref="PerformAsync" />: the handle is added and the multi handle performed in the transfer's own flow, so
/// its transfer starts there and sees the transfer's own <see cref="RunningTransferState" />; a loop then performs
/// the multi handle each time <c>curl_multi_poll</c> ends, takes every <c>CURLMSG_DONE</c> message with
/// <c>curl_multi_info_read</c>, removes that handle and hands its result to the attempt waiting on it. The multi
/// handle is for one flow at a time, so every call on it is made holding one gate; an attempt about to add wakes
/// the loop's poll (<c>curl_multi_wakeup</c>) so it need not wait for a transfer to end.
/// </summary>
/// <param name="multi">The run's multi handle; <see cref="DisposeAsync" /> cleans it up.</param>
internal sealed class ParallelTransferMulti(CurlMulti multi) : IAsyncDisposable
{
    /// <summary>
    /// Gets the longest a poll waits before the loop looks again; a transfer that ends, or an attempt about to
    /// add its handle, ends the poll sooner.
    /// </summary>
    internal static int PollTimeoutMilliseconds { get; } = 1000;

    /// <summary>Held for every call on the multi handle.</summary>
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>The attempts added and not yet ended, by their easy handle.</summary>
    private readonly Dictionary<CurlEasy, TaskCompletionSource<CurlCode>> waiting = new(ReferenceEqualityComparer.Instance);

    /// <summary>The loop, while one runs; completed otherwise.</summary>
    private Task loop = Task.CompletedTask;

    /// <summary>Whether the loop runs; read and written holding <see cref="gate" />.</summary>
    private bool looping;

    /// <summary>
    /// Performs one attempt of <paramref name="easy" /> through the multi handle: <c>curl_multi_add_handle</c>,
    /// then <c>curl_multi_perform</c>, which starts its transfer, and <c>curl_multi_perform</c> again, which
    /// reports it done if it already is, and waits for its <c>CURLMSG_DONE</c> message.
    /// </summary>
    /// <param name="easy">The transfer's easy handle, set up for the attempt and in no multi handle.</param>
    /// <returns>The attempt's result, from its message; the handle has been removed again.</returns>
    /// <exception cref="Exception">A transfer's perform threw, which ends every attempt waiting with it.</exception>
    internal async Task<CurlCode> PerformAsync(CurlEasy easy)
    {
        TaskCompletionSource<CurlCode> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = multi.Wakeup();
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // A handle in no multi handle is always added.
            _ = multi.AddHandle(easy);
            waiting.Add(easy, ended);

            // The perform that starts a transfer never reports it done, as libcurl's first perform only begins
            // its connect; a second takes the message of one that has already finished (BL-2743).
            await PerformAndTakeMessagesAsync().ConfigureAwait(false);
            await PerformAndTakeMessagesAsync().ConfigureAwait(false);
            StartLoopWhileAnyWaits();
        }
        finally
        {
            gate.Release();
        }

        return await ended.Task.ConfigureAwait(false);
    }

    /// <summary><c>curl_multi_cleanup</c>, once the loop has ended.</summary>
    /// <returns>A task that completes once the multi handle is cleaned up.</returns>
    public async ValueTask DisposeAsync()
    {
        await loop.ConfigureAwait(false);
        await multi.DisposeAsync().ConfigureAwait(false);
        gate.Dispose();
    }

    /// <summary>Starts the loop, in no transfer's flow, when an attempt waits and none runs; holding <see cref="gate" />.</summary>
    private void StartLoopWhileAnyWaits()
    {
        if (looping || waiting.Count == 0)
        {
            return;
        }

        looping = true;
        using (ExecutionContext.SuppressFlow())
        {
            loop = Task.Run(RunLoopAsync);
        }
    }

    /// <summary>Polls, performs and takes the messages until no attempt waits.</summary>
    /// <returns>A task that completes when the loop ends.</returns>
    private async Task RunLoopAsync()
    {
        while (await PollPerformAndTakeMessagesAsync().ConfigureAwait(false))
        {
        }
    }

    /// <summary>One turn of the loop, holding <see cref="gate" />.</summary>
    /// <returns>Whether an attempt still waits, so the loop goes on.</returns>
    private async Task<bool> PollPerformAndTakeMessagesAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _ = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, PollTimeoutMilliseconds).ConfigureAwait(false);
            await PerformAndTakeMessagesAsync().ConfigureAwait(false);
            looping = waiting.Count > 0;
            return looping;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// <c>curl_multi_perform</c>, then each <c>CURLMSG_DONE</c> message ends its attempt, holding <see cref="gate" />.
    /// A perform that throws, because a transfer's perform threw, ends every waiting attempt with that exception.
    /// </summary>
    /// <returns>A task that completes once the messages are taken.</returns>
    private async Task PerformAndTakeMessagesAsync()
    {
        try
        {
            _ = await multi.PerformAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            EndEveryWaitingAttempt(exception);
            return;
        }

        while (multi.InfoRead(out _) is { } message)
        {
            _ = multi.RemoveHandle(message.Easy);
            _ = waiting.Remove(message.Easy, out TaskCompletionSource<CurlCode>? ended);
            ended!.SetResult(message.Result);
        }
    }

    /// <summary>Ends every waiting attempt with <paramref name="exception" />.</summary>
    /// <param name="exception">What a transfer's perform threw.</param>
    private void EndEveryWaitingAttempt(Exception exception)
    {
        foreach ((CurlEasy easy, TaskCompletionSource<CurlCode> ended) in waiting)
        {
            _ = multi.RemoveHandle(easy);
            ended.SetException(exception);
        }

        waiting.Clear();
    }
}
