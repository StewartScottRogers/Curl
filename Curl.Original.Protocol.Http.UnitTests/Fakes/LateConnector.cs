using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A connector that ignores its cancellation token: a connect starts, waits until
/// <see cref="Finish" /> and then returns the scripted result or throws the scripted fault.
/// </summary>
public sealed class LateConnector : IConnector
{
    private readonly TaskCompletionSource<ConnectResult> late = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets a task that completes when a connect has started to wait.
    /// </summary>
    public Task Started => started.Task;

    /// <summary>
    /// Ends the waiting connect with <paramref name="result" />.
    /// </summary>
    /// <param name="result">What the connect returns.</param>
    public void Finish(ConnectResult result) => late.SetResult(result);

    /// <summary>Ends the connect with <paramref name="exception" />.</summary>
    /// <param name="exception">What the connect throws.</param>
    public void Fail(Exception exception) => late.SetException(exception);

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        started.TrySetResult();
        return new ValueTask<ConnectResult>(late.Task);
    }

    /// <inheritdoc />
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
