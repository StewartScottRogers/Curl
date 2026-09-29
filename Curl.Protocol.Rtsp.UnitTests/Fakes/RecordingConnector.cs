using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that returns a fixed <see cref="ConnectResult" /> and records
/// every target it was asked for.
/// </summary>
/// <param name="result">The result every connect returns.</param>
public sealed class RecordingConnector(ConnectResult result) : IConnector
{
    /// <summary>Gets every target asked for, in order.</summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(result);
    }
}
