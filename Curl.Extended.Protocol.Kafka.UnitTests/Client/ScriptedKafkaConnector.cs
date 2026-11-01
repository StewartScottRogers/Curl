using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>
/// A fake <see cref="IConnector"/> that records every target it is asked for and returns its set results in turn, the
/// last one again once they run out.
/// </summary>
internal sealed class ScriptedKafkaConnector(params ConnectResult[] results) : IConnector
{
    public List<ConnectTarget> Targets { get; } = [];

    public ConnectTarget? Target => Targets.FirstOrDefault();

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(results[Math.Min(Targets.Count, results.Length) - 1]);
    }
}
