using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>An <see cref="IConnection"/> that gives scripted reads, one per call, then end of stream, and records every write.</summary>
internal sealed class ScriptedConnection(params byte[][] reads) : IConnection
{
    private readonly Queue<byte[]> pendingReads = new(reads);

    private readonly MemoryStream written = new();

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool IsDisposed { get; private set; }

    public byte[] Written => written.ToArray();

    public string WrittenText => Encoding.Latin1.GetString(Written);

    public static ScriptedConnection FromText(params string[] reads) =>
        new([.. reads.Select(Encoding.Latin1.GetBytes)]);

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!pendingReads.TryDequeue(out byte[]? next))
        {
            return ValueTask.FromResult(0);
        }

        int count = Math.Min(next.Length, buffer.Length);
        next.AsSpan(0, count).CopyTo(buffer.Span);
        if (count < next.Length)
        {
            Queue<byte[]> rest = new([next[count..], .. pendingReads]);
            pendingReads.Clear();
            foreach (byte[] read in rest)
            {
                pendingReads.Enqueue(read);
            }
        }

        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
