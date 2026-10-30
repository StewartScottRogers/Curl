using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.GoogleCloudStorage.Http;

/// <summary>A fake <see cref="IConnection"/> that replays recorded response bytes, at most <c>readSize</c> per read, and records what is written.</summary>
internal sealed class RecordedConnection(string response, int readSize = int.MaxValue) : IConnection
{
    private readonly byte[] responseBytes = Encoding.Latin1.GetBytes(response);
    private readonly MemoryStream written = new();
    private int position;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public int FlushCount { get; private set; }

    public string Written => Encoding.Latin1.GetString(written.ToArray());

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
