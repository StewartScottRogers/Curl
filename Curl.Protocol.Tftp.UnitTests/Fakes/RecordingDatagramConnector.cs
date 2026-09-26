using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IDatagramConnector" /> that returns one prepared result and records the
/// host and port of every open.
/// </summary>
/// <param name="result">The result every open returns.</param>
public sealed class RecordingDatagramConnector(DatagramOpenResult result) : IDatagramConnector
{
    /// <summary>
    /// Gets the host and port of every open, in order.
    /// </summary>
    public List<(string Host, int Port)> Opens { get; } = [];

    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        Opens.Add((host, port));
        return ValueTask.FromResult(result);
    }
}
