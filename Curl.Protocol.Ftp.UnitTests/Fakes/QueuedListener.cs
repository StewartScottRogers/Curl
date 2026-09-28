using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="IConnectionListener" /> that answers each listen with the next of
/// <paramref name="results" />, in order, and records every target it was asked to bind.
/// </summary>
/// <param name="results">The result of each listen, in order.</param>
public sealed class QueuedListener(params ListenResult[] results) : IConnectionListener
{
    /// <summary>Gets every target asked for, in order.</summary>
    public List<ListenTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(results[Targets.Count - 1]);
    }
}
