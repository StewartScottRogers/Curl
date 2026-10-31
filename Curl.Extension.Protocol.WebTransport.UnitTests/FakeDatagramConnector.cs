using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.WebTransport.Session;

/// <summary>
/// An <see cref="IDatagramConnector" /> that hands out one prepared channel, or fails when it has none, and records
/// every host and port it was asked to open.
/// </summary>
/// <param name="channel">The channel to hand out, or <see langword="null" /> to fail the open.</param>
internal sealed class FakeDatagramConnector(IDatagramChannel? channel) : IDatagramConnector
{
    /// <summary>Gets the host and port of every open asked for.</summary>
    public List<(string Host, int Port)> Opens { get; } = [];

    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        Opens.Add((host, port));
        return ValueTask.FromResult(channel is null
            ? DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server")
            : DatagramOpenResult.Opened(channel));
    }
}
