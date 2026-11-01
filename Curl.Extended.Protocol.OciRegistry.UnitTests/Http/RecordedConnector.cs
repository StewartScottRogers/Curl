using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>A fake <see cref="IConnector"/> that hands out one <see cref="RecordedConnection"/>, or fails to connect, and records the target.</summary>
internal sealed class RecordedConnector(ConnectResult result) : IConnector
{
    public RecordedConnector(RecordedConnection connection)
        : this(ConnectResult.Connected(connection))
    {
    }

    public ConnectTarget? Target { get; private set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Target = target;
        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// A fake <see cref="IConnection"/> that replays recorded response bytes, at most <c>readSize</c> per read, records what is
/// written, and throws an <see cref="IOException"/> on read when given no response.
/// </summary>
internal sealed class RecordedConnection(string? response, int readSize = int.MaxValue) : IConnection
{
    private readonly byte[] responseBytes = Encoding.UTF8.GetBytes(response ?? string.Empty);
    private readonly MemoryStream written = new();
    private int position;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public int FlushCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public string Written => Encoding.UTF8.GetString(written.ToArray());

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            throw new IOException("An existing connection was forcibly closed by the remote host.");
        }

        var count = Math.Min(Math.Min(buffer.Length, readSize), responseBytes.Length - position);
        responseBytes.AsMemory(position, count).CopyTo(buffer);
        position += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
