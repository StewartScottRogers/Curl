using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>A datagram connector that hands out queued <see cref="FakeDatagramChannel" />s, or fails once they run out.</summary>
internal sealed class FakeDatagramConnector : IDatagramConnector
{
    private readonly Queue<FakeDatagramChannel> channels = new();

    public List<(string Host, int Port)> Opens { get; } = [];

    public FakeDatagramChannel Add(int localPort)
    {
        FakeDatagramChannel channel = new(localPort);
        channels.Enqueue(channel);
        return channel;
    }

    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        Opens.Add((host, port));
        return ValueTask.FromResult(channels.Count == 0
            ? DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "no network")
            : DatagramOpenResult.Opened(channels.Dequeue()).WithConnectionNumber(4));
    }
}
