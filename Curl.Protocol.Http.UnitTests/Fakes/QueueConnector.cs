using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that returns scripted results in order, one per connect, and
/// records each target it was asked for: the fake for a transfer that connects more than
/// once, as a redirect or an authentication retry does.
/// </summary>
/// <param name="results">The results the connects return, first to last.</param>
public sealed class QueueConnector(params ConnectResult[] results) : IConnector
{
    private readonly Queue<ConnectResult> pending = new(results);

    /// <summary>
    /// Gets every target asked for, in order.
    /// </summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>
    /// Creates a connector that hands out <paramref name="connections" /> in order.
    /// </summary>
    /// <param name="connections">The connections the connects return, first to last.</param>
    /// <returns>The connector.</returns>
    public static QueueConnector For(params IConnection[] connections) =>
        new([.. connections.Select(ConnectResult.Connected)]);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Every scripted result has been returned.</exception>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Targets.Add(target);
        if (!pending.TryDequeue(out ConnectResult? result))
        {
            throw new InvalidOperationException($"Connect {Targets.Count} was not scripted.");
        }

        return ValueTask.FromResult(result);
    }
}
