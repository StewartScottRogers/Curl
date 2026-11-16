using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A connector whose one connection hands over its server chunks at the pace a streaming server does: the first
/// <paramref name="chunksPerWrite" /> chunks are released one per client flush, and each later chunk is released by a
/// read that finds nothing waiting. Past the last chunk a read sees the server close. It counts the connects.
/// </summary>
/// <param name="blocksWhenDrained">Whether a read past the last chunk waits for its cancellation instead of seeing the close.</param>
/// <param name="chunksPerWrite">How many chunks the client's flushes release, one each.</param>
/// <param name="chunks">The server's bytes, as Latin-1 text, one chunk each.</param>
internal sealed class RtspPacedConnector(int chunksPerWrite, bool blocksWhenDrained, params string[] chunks) : IConnector
{
    private readonly bool blocksWhenDrained = blocksWhenDrained;

    private readonly Queue<string> pending = new(chunks);

    private readonly Queue<byte> released = new();

    private int writesLeft = chunksPerWrite;

    /// <summary>Gets how many connections were opened.</summary>
    public int Connects { get; private set; }

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Connects++;
        return ValueTask.FromResult(ConnectResult.Connected(new PacedConnection(this)));
    }

    private void ReleaseNext()
    {
        if (pending.TryDequeue(out string? chunk))
        {
            foreach (byte value in Encoding.Latin1.GetBytes(chunk))
            {
                released.Enqueue(value);
            }
        }
    }

    private sealed class PacedConnection(RtspPacedConnector owner) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (owner.released.Count == 0 && owner.writesLeft == 0)
            {
                owner.ReleaseNext();
            }

            int count = 0;
            while (count < buffer.Length && owner.released.TryDequeue(out byte value))
            {
                buffer.Span[count++] = value;
            }

            if (count == 0 && owner.blocksWhenDrained && owner.pending.Count == 0)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            return count;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            if (owner.writesLeft > 0)
            {
                owner.writesLeft--;
                owner.ReleaseNext();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
