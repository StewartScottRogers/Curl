using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that records each target it is asked for and returns one
/// prepared result.
/// </summary>
/// <param name="result">The result every connect returns.</param>
public sealed class RecordingConnector(ConnectResult result) : IConnector
{
    /// <summary>
    /// Gets every target asked for, in order.
    /// </summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(result);
    }
}
