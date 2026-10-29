using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketRepl;

/// <summary>
/// A connection that hands out recorded server bytes, each chunk only once the client has made a given number
/// of writes, so a test decides the order of what is sent and received; after the last chunk it reads zero.
/// </summary>
internal sealed class ScriptedConnection : IConnection
{
    private readonly Queue<(int WritesBefore, byte[] Bytes)> chunks;

    private readonly List<byte[]> writes = [];

    private readonly Lock gate = new();

    private ReadOnlyMemory<byte> pending;

    private TaskCompletionSource written = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ScriptedConnection(params (int WritesBefore, byte[] Bytes)[] chunks) => this.chunks = new(chunks);

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets each write the client made, in order.</summary>
    public IReadOnlyList<byte[]> Writes
    {
        get
        {
            lock (gate)
            {
                return [.. writes];
            }
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!pending.IsEmpty)
        {
            return TakePending(buffer);
        }

        if (!chunks.TryDequeue(out (int WritesBefore, byte[] Bytes) chunk))
        {
            return 0;
        }

        while (true)
        {
            Task waited;
            lock (gate)
            {
                if (writes.Count >= chunk.WritesBefore)
                {
                    break;
                }

                waited = written.Task;
            }

            await waited.WaitAsync(cancellationToken);
        }

        pending = chunk.Bytes;
        return TakePending(buffer);
    }

    private int TakePending(Memory<byte> buffer)
    {
        int length = Math.Min(buffer.Length, pending.Length);
        pending[..length].CopyTo(buffer);
        pending = pending[length..];
        return length;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        TaskCompletionSource signalled;
        lock (gate)
        {
            writes.Add(buffer.ToArray());
            signalled = written;
            written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signalled.SetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
