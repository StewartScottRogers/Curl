namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// What curl and a reference server sent each other on one connection or datagram channel, each
/// side's writes (or datagrams) in order.
/// </summary>
/// <param name="port">The route's port.</param>
/// <param name="transport">The route's transport.</param>
public sealed class WireExchange(int port, ReferenceTransport transport)
{
    private readonly Lock gate = new();

    private readonly List<byte[]> clientSent = [];

    private readonly List<byte[]> serverSent = [];

    /// <summary>Gets the route's port.</summary>
    public int Port => port;

    /// <summary>Gets the route's transport.</summary>
    public ReferenceTransport Transport => transport;

    /// <summary>Gets what curl sent: each read the server made of a connection, or each datagram.</summary>
    public IReadOnlyList<byte[]> ClientSent => Snapshot(clientSent);

    /// <summary>Gets what the server sent: each write to a connection, or each datagram.</summary>
    public IReadOnlyList<byte[]> ServerSent => Snapshot(serverSent);

    /// <summary>Gets every byte curl sent, joined in order.</summary>
    public byte[] ClientBytes => [.. ClientSent.SelectMany(chunk => chunk)];

    /// <summary>Gets every byte the server sent, joined in order.</summary>
    public byte[] ServerBytes => [.. ServerSent.SelectMany(chunk => chunk)];

    internal void RecordClient(ReadOnlySpan<byte> bytes) => Record(clientSent, bytes);

    internal void RecordServer(ReadOnlySpan<byte> bytes) => Record(serverSent, bytes);

    private void Record(List<byte[]> side, ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            side.Add(bytes.ToArray());
        }
    }

    private byte[][] Snapshot(List<byte[]> side)
    {
        lock (gate)
        {
            return [.. side];
        }
    }
}
