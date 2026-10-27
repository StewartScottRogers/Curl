using System.Runtime.ExceptionServices;

namespace Curl.Engine;

/// <summary>
/// The callbacks a multi handle's transfers have asked to run, which the thread calling the handle's
/// <c>curl_multi_perform</c> or <c>curl_multi_socket_action</c> runs inside the call (<see cref="RunQueued" />), as
/// libcurl calls every callback of a multi-driven transfer from inside those calls and on their thread
/// (CGR-033). A transfer's own thread waits on <see cref="Run{T}" /> until its callback has run; a callback asked
/// for on the performing thread itself (<see cref="EnterPerform" />) runs at once.
/// </summary>
internal sealed class MultiCallbackQueue
{
    private readonly Lock gate = new();
    private readonly Queue<QueuedCallback> queued = new();
    private readonly ManualResetEventSlim queuedEvent = new();
    private TaskCompletionSource arrived = NewArrivedSignal();
    private int performingThread;

    /// <summary>Gets whether a callback waits for the next perform.</summary>
    public bool HasQueued
    {
        get
        {
            lock (gate)
            {
                return queued.Count > 0;
            }
        }
    }

    /// <summary>Gets a task that completes when a callback is queued, or at once while one is.</summary>
    public Task Arrived
    {
        get
        {
            lock (gate)
            {
                return arrived.Task;
            }
        }
    }

    /// <summary>Marks the calling thread as the one inside the handle's perform, until <see cref="LeavePerform" />.</summary>
    /// <returns>The thread marked before, which <see cref="LeavePerform" /> puts back.</returns>
    public int EnterPerform()
    {
        int previous = performingThread;
        performingThread = Environment.CurrentManagedThreadId;
        return previous;
    }

    /// <summary>Puts back the thread <see cref="EnterPerform" /> replaced.</summary>
    /// <param name="previous">What <see cref="EnterPerform" /> returned.</param>
    public void LeavePerform(int previous) => performingThread = previous;

    /// <summary>
    /// Gets how long <see cref="RunQueued" /> waits, after running a callback, for the transfer to ask for its next
    /// one: libcurl's perform hands a transfer every chunk its socket already holds in one call, and a transfer
    /// given its callback's answer asks for the next chunk's callback at once, so the call goes on until the
    /// transfer waits on the network.
    /// </summary>
    internal static TimeSpan FollowUpWait { get; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Runs every callback queued, oldest first, on the calling thread, and each one queued within
    /// <see cref="FollowUpWait" /> of the last one run.
    /// </summary>
    public void RunQueued()
    {
        while (TryDequeue() is { } callback)
        {
            callback.RunWhenTaken();
            queuedEvent.Wait(FollowUpWait);
        }
    }

    /// <summary>
    /// Runs <paramref name="callback" /> on the performing thread: at once when the calling thread is it, else
    /// queued until the next perform runs it, the calling thread waiting. Once <paramref name="cancellationToken" />
    /// is cancelled (the transfer removed or the handle cleaned up) a callback not yet taken runs on the calling
    /// thread, so a transfer being torn down never waits for a perform that may not come.
    /// </summary>
    /// <typeparam name="T">The callback's result.</typeparam>
    /// <param name="callback">The callback.</param>
    /// <param name="cancellationToken">The transfer's token.</param>
    /// <returns>What the callback returned; an exception it threw is thrown again here.</returns>
    public T Run<T>(Func<T> callback, CancellationToken cancellationToken)
    {
        if (Environment.CurrentManagedThreadId == performingThread || cancellationToken.IsCancellationRequested)
        {
            return callback();
        }

        T result = default!;
        QueuedCallback queuedCallback = new(() => result = callback());
        Enqueue(queuedCallback);
        if (!queuedCallback.WaitUntilRun(cancellationToken))
        {
            return callback();
        }

        queuedCallback.Failure?.Throw();
        return result;
    }

    private static TaskCompletionSource NewArrivedSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Enqueue(QueuedCallback callback)
    {
        lock (gate)
        {
            queued.Enqueue(callback);
            arrived.TrySetResult();
            queuedEvent.Set();
        }
    }

    private QueuedCallback? TryDequeue()
    {
        lock (gate)
        {
            QueuedCallback? next = queued.TryDequeue(out QueuedCallback? callback) ? callback : null;
            if (queued.Count == 0 && arrived.Task.IsCompleted)
            {
                arrived = NewArrivedSignal();
                queuedEvent.Reset();
            }

            return next;
        }
    }

    /// <summary>One queued callback, taken once: by the perform that runs it, or by its own thread when cancelled.</summary>
    private sealed class QueuedCallback(Action run)
    {
        private readonly ManualResetEventSlim ran = new();
        private int taken;

        public ExceptionDispatchInfo? Failure { get; private set; }

        public void RunWhenTaken()
        {
            if (!TryTake())
            {
                return;
            }

            try
            {
                run();
            }
            catch (Exception exception)
            {
                Failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                ran.Set();
            }
        }

        /// <summary>Waits until a perform has run the callback; false when cancelled before one took it.</summary>
        public bool WaitUntilRun(CancellationToken cancellationToken)
        {
            try
            {
                ran.Wait(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                if (TryTake())
                {
                    return false;
                }

                ran.Wait(CancellationToken.None);
                return true;
            }
        }

        private bool TryTake() => Interlocked.Exchange(ref taken, 1) == 0;
    }
}
