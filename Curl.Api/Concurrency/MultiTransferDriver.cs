using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Api;

/// <summary>
/// Runs a client's transfers together on one <see cref="IMultiTransfer"/> (ADR-0644): one driver loop,
/// started by the first transfer and ended when none is left, adds each transfer, performs, takes each
/// <c>CURLMSG_DONE</c> message to finish its transfer, and waits in <see cref="IMultiTransfer.PollAsync"/>,
/// which <see cref="IMultiTransfer.Wakeup"/> ends when a transfer arrives or is cancelled. Only the loop
/// calls the multi handle, <see cref="IMultiTransfer.Wakeup"/> aside, so it is never used from two threads.
/// The multi handle is created on the first transfer and disposed with the driver.
/// </summary>
/// <param name="createMulti"><c>curl_multi_init</c> with the client's limits set; called once, on the first transfer.</param>
internal sealed class MultiTransferDriver(Func<IMultiTransfer> createMulti) : IAsyncDisposable
{
    /// <summary>The longest one wait in <see cref="IMultiTransfer.PollAsync"/> lasts before the loop performs again.</summary>
    public static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(1);

    // Guards _changes, _looping and _disposed. _running and _multi's handle calls belong to the loop alone.
    private readonly Lock _gate = new();

    // Add, remove and abort requests from other threads; the loop applies them so the multi handle has one caller.
    private readonly Queue<Action<IMultiTransfer>> _changes = new();

    // Each transfer on the multi handle and the source of the result its caller awaits.
    private readonly Dictionary<IEasyTransfer, TaskCompletionSource<CurlCode>> _running = [];
    private IMultiTransfer? _multi;
    private Task _loop = Task.CompletedTask;

    // True from the moment the loop is started until ApplyChanges finds nothing queued and nothing running.
    private bool _looping;
    private bool _disposed;

    /// <summary>
    /// Runs <paramref name="easy"/>'s transfer on the multi handle beside every other running one, as
    /// <see cref="IEasyTransfer.PerformAsync"/> would on its own.
    /// </summary>
    /// <param name="easy">The transfer; it is removed from the multi handle once it ends.</param>
    /// <param name="cancellationToken">
    /// Removes the transfer from the multi handle (<c>curl_multi_remove_handle</c>), stopping it; it then
    /// ends with <see cref="CurlCode.AbortedByCallback"/>, the code a progress callback's abort gives.
    /// </param>
    /// <returns>The transfer's result.</returns>
    /// <exception cref="ObjectDisposedException">The driver has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The multi handle refused the transfer or failed a perform or wait.</exception>
    public async Task<CurlCode> RunAsync(IEasyTransfer easy, CancellationToken cancellationToken)
    {
        TaskCompletionSource<CurlCode> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ObjectDisposedException.ThrowIf(!TryEnqueue(multi => Start(multi, easy, done)), this);
        using (cancellationToken.Register(() => TryEnqueue(multi => Finish(multi, easy, CurlCode.AbortedByCallback))))
        {
            return await done.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops every running transfer (each ends with <see cref="CurlCode.AbortedByCallback"/>), waits for the
    /// loop to end and disposes the multi handle. Safe to call more than once.
    /// </summary>
    /// <returns>A task that completes once the multi handle is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_looping)
            {
                EnqueueLocked(FinishAll);
            }

            _disposed = true;
        }

        await _loop.ConfigureAwait(false);
        if (_multi is not null)
        {
            await _multi.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Queues a change for the loop unless the driver is disposed.</summary>
    /// <returns>Whether the change was queued.</returns>
    private bool TryEnqueue(Action<IMultiTransfer> change)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            EnqueueLocked(change);
            return true;
        }
    }

    /// <summary>Queues a change for the loop, starting the loop when it is not running and waking it when it is.</summary>
    private void EnqueueLocked(Action<IMultiTransfer> change)
    {
        _changes.Enqueue(change);
        if (_looping)
        {
            _multi!.Wakeup();
            return;
        }

        _multi ??= createMulti();
        _looping = true;
        _loop = Task.Run(RunLoopAsync);
    }

    /// <summary>The driver loop: applies queued changes, then performs and polls, until no change is queued and no transfer runs.</summary>
    private async Task RunLoopAsync()
    {
        while (ApplyChanges())
        {
            try
            {
                await MoveTransfersOnAsync(_multi!).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A failed perform or poll cannot be pinned on one transfer, so none can safely go on.
                FailAll(_multi!, exception);
            }
        }
    }

    /// <summary>Applies every queued change; ends the loop, under the gate, once none is queued and no transfer runs.</summary>
    /// <returns>Whether the loop goes on.</returns>
    private bool ApplyChanges()
    {
        while (true)
        {
            Action<IMultiTransfer>? change;
            lock (_gate)
            {
                if (!_changes.TryDequeue(out change))
                {
                    _looping = _running.Count > 0;
                    return _looping;
                }
            }

            change(_multi!);
        }
    }

    /// <summary>One turn of the loop: <c>curl_multi_perform</c>, finishing each transfer curl reports done, then waiting for activity while any transfer runs.</summary>
    private async Task MoveTransfersOnAsync(IMultiTransfer multi)
    {
        (CurlMultiCode performed, _) = await multi.PerformAsync(CancellationToken.None).ConfigureAwait(false);
        ThrowIfFailed(performed, "curl_multi_perform");
        while (multi.TryReadInfo(out CurlMultiMessage? message, out _))
        {
            Finish(multi, message.Transfer, message.Result);
        }

        // The wait ends early on Wakeup (a new or cancelled transfer) or socket activity; with nothing running there is nothing to wait for.
        if (_running.Count > 0)
        {
            (CurlMultiCode polled, _) = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, PollTimeout, CancellationToken.None).ConfigureAwait(false);
            ThrowIfFailed(polled, "curl_multi_poll");
        }
    }

    /// <summary>Adds a transfer to the multi handle and tracks it; a refused add ends the transfer's task with an <see cref="InvalidOperationException"/>.</summary>
    private void Start(IMultiTransfer multi, IEasyTransfer easy, TaskCompletionSource<CurlCode> done)
    {
        CurlMultiCode added = multi.Add(easy);
        if (added != CurlMultiCode.Ok)
        {
            done.SetException(MultiFailure(added, "curl_multi_add_handle"));
            return;
        }

        _running.Add(easy, done);
    }

    /// <summary>Removes a running transfer from the multi handle and ends it with <paramref name="result"/>; a transfer that has ended already is left alone.</summary>
    private void Finish(IMultiTransfer multi, IEasyTransfer easy, CurlCode result)
    {
        if (_running.Remove(easy, out TaskCompletionSource<CurlCode>? done))
        {
            multi.Remove(easy);
            done.SetResult(result);
        }
    }

    /// <summary>Ends every running transfer with <see cref="CurlCode.AbortedByCallback"/>; the dispose path.</summary>
    private void FinishAll(IMultiTransfer multi)
    {
        foreach (IEasyTransfer easy in _running.Keys.ToArray())
        {
            Finish(multi, easy, CurlCode.AbortedByCallback);
        }
    }

    /// <summary>Ends every running transfer with <paramref name="exception"/>: a failed perform or wait does not say which transfer it was about.</summary>
    private void FailAll(IMultiTransfer multi, Exception exception)
    {
        foreach ((IEasyTransfer easy, TaskCompletionSource<CurlCode> done) in _running.ToArray())
        {
            _running.Remove(easy);
            multi.Remove(easy);
            done.SetException(exception);
        }
    }

    /// <summary>Throws the exception <c>MultiFailure</c> builds unless <paramref name="code"/> is <see cref="CurlMultiCode.Ok"/>.</summary>
    /// <param name="code">The multi call's answer.</param>
    /// <param name="call">The libcurl function that answered, named in the message.</param>
    private static void ThrowIfFailed(CurlMultiCode code, string call)
    {
        if (code != CurlMultiCode.Ok)
        {
            throw MultiFailure(code, call);
        }
    }

    /// <summary>The exception for a multi call that did not answer <c>CURLM_OK</c>.</summary>
    private static InvalidOperationException MultiFailure(CurlMultiCode code, string call) =>
        new($"{call} answered {code}.");
}
