using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that answers each connect with the next of
/// <paramref name="results" />, in order, and records every target it was asked for:
/// an IMAP transfer asks for one.
/// </summary>
/// <param name="results">The result of each connect, in order.</param>
public sealed class QueuedConnector(params ConnectResult[] results) : IConnector
{
    /// <summary>Gets every target asked for, in order.</summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(results[Targets.Count - 1]);
    }
}
