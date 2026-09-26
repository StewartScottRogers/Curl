using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Gopher.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that records each target it is asked for and returns one
/// scripted result.
/// </summary>
/// <param name="result">The result every connect returns.</param>
public sealed class FakeConnector(ConnectResult result) : IConnector
{
    /// <summary>
    /// Gets every target asked for, in order.
    /// </summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>
    /// Creates a connector that hands out <paramref name="connection" />.
    /// </summary>
    /// <param name="connection">The connection every connect returns.</param>
    /// <returns>The connector.</returns>
    public static FakeConnector For(IConnection connection) => new(ConnectResult.Connected(connection));

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(result);
    }
}
