using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A connector whose one connection replays scripted server reads, one per
/// <see cref="IConnection.ReadAsync" />, splitting a scripted read longer than the caller's
/// buffer across reads as a socket would, then reports the server closing. Writes to it are
/// discarded, so no socket is ever opened.
/// </summary>
internal sealed class ScriptedConnector(IEnumerable<byte[]> reads) : IConnector
{
    private readonly Queue<byte[]> pendingReads = new(reads);

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection(pendingReads)));

    private sealed class ScriptedConnection(Queue<byte[]> pendingReads) : IConnection
    {
        private byte[] remainder = [];

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (remainder.Length == 0 && !pendingReads.TryDequeue(out remainder!))
            {
                remainder = [];
                return ValueTask.FromResult(0);
            }

            int count = Math.Min(remainder.Length, buffer.Length);
            remainder.AsSpan(0, count).CopyTo(buffer.Span);
            remainder = remainder[count..];

            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
