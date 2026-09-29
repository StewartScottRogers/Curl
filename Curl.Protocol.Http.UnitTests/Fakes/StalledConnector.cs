using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnector" /> whose connect never completes: it waits until its token is
/// cancelled, as a connect to an address that never answers does.
/// </summary>
public sealed class StalledConnector : IConnector
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets a task that completes when a connect has started to wait.
    /// </summary>
    public Task Started => started.Task;

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite wait ended without cancellation.");
    }

    /// <inheritdoc />
    public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite wait ended without cancellation.");
    }
}
