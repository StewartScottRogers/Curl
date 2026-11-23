namespace Curl.Engine;

/// <summary>
/// Calls a perform's progress callback once every <see cref="EasyPauseGate.PausedTickInterval" /> for as long as it
/// runs, as libcurl's multi loop calls <c>Curl_pgrsUpdate</c> at least once a second while it waits for a server that
/// holds a command or a response (BL-5796), so a callback that returns non-zero ends the hold. The clock is the injected
/// <see cref="TimeProvider" />; a tick that falls while the handle is paused is left to the pause gate's own tick.
/// </summary>
internal sealed class ProgressHoldTicker : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;

    private ProgressHoldTicker(Action tick, Func<bool> isPaused, TimeProvider timeProvider) =>
        loop = TickUntilStoppedAsync(tick, isPaused, timeProvider);

    /// <summary>Starts ticking.</summary>
    /// <param name="tick">The progress callback call.</param>
    /// <param name="isPaused">Whether the handle is paused, a tick then being the pause gate's.</param>
    /// <param name="timeProvider">The clock the interval is measured on.</param>
    /// <returns>The running ticker; disposing it stops it.</returns>
    internal static ProgressHoldTicker Start(Action tick, Func<bool> isPaused, TimeProvider timeProvider) => new(tick, isPaused, timeProvider);

    /// <summary>Stops ticking and waits for a tick in flight to end.</summary>
    /// <returns>A task that completes once the ticker has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync().ConfigureAwait(false);
        await loop.ConfigureAwait(false);
        stop.Dispose();
    }

    private async Task TickUntilStoppedAsync(Action tick, Func<bool> isPaused, TimeProvider timeProvider)
    {
        using PeriodicTimer timer = new(EasyPauseGate.PausedTickInterval, timeProvider);
        try
        {
            while (true)
            {
                await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false);
                if (!isPaused())
                {
                    tick();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed: the perform ended.
        }
    }
}
