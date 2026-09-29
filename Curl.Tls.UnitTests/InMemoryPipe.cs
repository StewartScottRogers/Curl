namespace Curl.Tls;

/// <summary>
/// Two connected in-memory stream ends: what one end writes, the other reads, and
/// disposing an end ends the other's reads, as a TCP connection's close does.
/// </summary>
internal static class InMemoryPipe
{
    public static (Stream Client, Stream Server) Create()
    {
        ByteQueue toServer = new();
        ByteQueue toClient = new();
        return (new End(toClient, toServer), new End(toServer, toClient));
    }

    private sealed class ByteQueue
    {
        private readonly Queue<byte> bytes = new();
        private readonly SemaphoreSlim signal = new(0);
        private readonly Lock gate = new();
        private bool completed;

        public void Write(ReadOnlySpan<byte> data)
        {
            lock (gate)
            {
                foreach (byte octet in data)
                {
                    bytes.Enqueue(octet);
                }
            }

            signal.Release();
        }

        public void Complete()
        {
            lock (gate)
            {
                completed = true;
            }

            signal.Release();
        }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (gate)
                {
                    if (bytes.Count > 0 || completed)
                    {
                        int count = Math.Min(buffer.Length, bytes.Count);
                        for (int index = 0; index < count; index++)
                        {
                            buffer.Span[index] = bytes.Dequeue();
                        }

                        return count;
                    }
                }

                await signal.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class End(ByteQueue incoming, ByteQueue outgoing) : Stream
    {
        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            incoming.ReadAsync(buffer, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            outgoing.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => outgoing.Write(buffer.AsSpan(offset, count));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            outgoing.Complete();
            base.Dispose(disposing);
        }
    }
}
