using Curl.Api.EngineContracts;

namespace Curl.Engine;

/// <summary>
/// The pause state of one easy handle, <c>curl_easy_pause</c>'s <see cref="CurlPause" /> bits: a write or header
/// callback that returns <c>CURL_WRITEFUNC_PAUSE</c> pauses receiving, a read callback that returns
/// <c>CURL_READFUNC_PAUSE</c> pauses sending, and the transfer waits in <see cref="WhenResumedAsync" /> until
/// <see cref="Set" /> clears that direction's bit, as libcurl holds a paused transfer until the application
/// unpauses it (ADR-0573).
/// </summary>
internal sealed class EasyPauseGate
{
    private readonly Lock gate = new();

    private TaskCompletionSource resumed = NewResumed();

    private int unpauses;

    /// <summary>Gets the directions left paused.</summary>
    internal CurlPause Directions { get; private set; }

    /// <summary>Gets how many times <see cref="Set" /> has run; a stream reads it before calling its callback so <see cref="Pause" /> can tell whether an unpause arrived meanwhile.</summary>
    internal int Unpauses
    {
        get
        {
            lock (gate)
            {
                return unpauses;
            }
        }
    }

    /// <summary>Leaves <paramref name="directions" /> paused and resumes every other direction, as <c>curl_easy_pause</c> does.</summary>
    /// <param name="directions">The directions to leave paused.</param>
    internal void Set(CurlPause directions)
    {
        TaskCompletionSource released;
        lock (gate)
        {
            Directions = directions;
            unpauses++;
            released = resumed;
            resumed = NewResumed();
        }

        released.TrySetResult();
    }

    /// <summary>Pauses <paramref name="direction" />, keeping the other direction as it is.</summary>
    /// <param name="direction">The direction a callback paused.</param>
    /// <param name="unpausesBeforeCallback">What <see cref="Unpauses" /> was before the callback ran; a <see cref="Set" /> since then already happened, so the pause is not recorded.</param>
    internal void Pause(CurlPause direction, int unpausesBeforeCallback)
    {
        lock (gate)
        {
            if (unpauses == unpausesBeforeCallback)
            {
                Directions |= direction;
            }
        }
    }

    /// <summary>Waits until <paramref name="direction" /> is no longer paused.</summary>
    /// <param name="direction">The paused direction.</param>
    /// <param name="cancellationToken">Stops the wait, as the transfer's abort or timeout does.</param>
    /// <returns>A task that completes once the direction is resumed.</returns>
    internal async Task WhenResumedAsync(CurlPause direction, CancellationToken cancellationToken)
    {
        while (WaitFor(direction) is { } released)
        {
            await released.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The task the next <see cref="Set" /> completes while <paramref name="direction" /> is paused, else <see langword="null" />.</summary>
    /// <param name="direction">The paused direction.</param>
    /// <returns>The task, or <see langword="null" /> when the direction is not paused.</returns>
    private Task? WaitFor(CurlPause direction)
    {
        lock (gate)
        {
            return (Directions & direction) != 0 ? resumed.Task : null;
        }
    }

    private static TaskCompletionSource NewResumed() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
