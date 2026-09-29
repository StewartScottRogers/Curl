using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that answers each connect with the next of
/// <paramref name="results" />, in order, and records every target it was asked for:
/// the first is the control connection, the second the data connection.
/// </summary>
/// <param name="results">The result of each connect, in order.</param>
public sealed class QueuedConnector(params ConnectResult[] results) : IConnector
{
    /// <summary>
    /// Gets every target asked for, in order, without its events, so a test compares the host,
    /// port, TLS and proxy; what a connect reports on its events is driven by
    /// <see cref="DataConnectReports" />.
    /// </summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>
    /// Gets what the data connection's connect reports on its target's events before it
    /// answers, as <c>TcpConnector</c> reports curl's <c>-v</c> lines; nothing when <see langword="null" />.
    /// </summary>
    public Action<ITransferEvents>? DataConnectReports { get; init; }

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target with { Events = NoTransferEvents.Instance });
        if (Targets.Count == 2)
        {
            DataConnectReports?.Invoke(target.Events);
        }

        return ValueTask.FromResult(results[Targets.Count - 1]);
    }
}
