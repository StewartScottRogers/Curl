using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Http;

/// <summary>A fake <see cref="IConnection"/> that replays recorded response bytes, at most <c>readSize</c> per read, records what is written, and breaks (an <see cref="IOException"/>) on the write after the first <c>writesBeforeBreak</c>.</summary>
internal sealed class RecordedConnection(string response, int readSize = int.MaxValue, int writesBeforeBreak = int.MaxValue) : IConnection
{
    private readonly byte[] responseBytes = Encoding.Latin1.GetBytes(response);
    private readonly MemoryStream written = new();
    private int position;
    private int writes;

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
        if (writes++ >= writesBeforeBreak)
        {
            throw new IOException("The connection was reset.");
        }

        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return ValueTask.CompletedTask;
    }

    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
