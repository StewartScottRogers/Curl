using System.Threading.Channels;
using Curl.Conformance.Differential.Protocols.Proxy;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Proxy;

[TestClass]
public sealed class ProxyTunnelTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task RelayAsync_TargetEndsFirst_ClosesTheClientAndReturns()
    {
        using var cts = new CancellationTokenSource(Limit);
        var (client, connection) = InMemoryDuplexStream.CreatePair();
        var (target, tunnelRemote) = InMemoryDuplexStream.CreatePair();
        using var clientStream = client;
        using var connectionStream = connection;
        using var targetStream = target;

        var relay = ProxyTunnel.RelayAsync(connectionStream, new FixedConnector(tunnelRemote), "example.test", 80, cts.Token);
        await target.WriteAsync(new byte[] { 1, 2, 3 }, cts.Token);
        var buffer = new byte[8];
        Assert.AreEqual(3, await ReadAtLeastAsync(client, buffer, 3, cts.Token));
        target.Dispose();

        await relay;
        Assert.AreEqual(0, await client.ReadAsync(buffer, cts.Token));
    }

    [TestMethod]
    public async Task RelayAsync_ClientEndsFirst_ClosesTheTargetAndReturns()
    {
        using var cts = new CancellationTokenSource(Limit);
        var (client, connection) = InMemoryDuplexStream.CreatePair();
        var (target, tunnelRemote) = InMemoryDuplexStream.CreatePair();
        using var targetStream = target;
        using var connectionStream = connection;

        var relay = ProxyTunnel.RelayAsync(connectionStream, new FixedConnector(tunnelRemote), "localhost.test", 80, cts.Token);
        await client.WriteAsync(new byte[] { 4, 5 }, cts.Token);
        var buffer = new byte[8];
        Assert.AreEqual(2, await ReadAtLeastAsync(target, buffer, 2, cts.Token));
        client.Dispose();

        await relay;
        Assert.AreEqual(0, await target.ReadAsync(buffer, cts.Token));
    }

    private static async Task<int> ReadAtLeastAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            Assert.AreNotEqual(0, read);
            total += read;
        }

        return total;
    }

    private sealed class FixedConnector(Stream stream) : IProxyTunnelConnector
    {
        public Task<Stream?> ConnectAsync(string host, int port, CancellationToken cancellationToken) => Task.FromResult<Stream?>(stream);
    }

    /// <summary>One end of an in-memory connection: bytes written here are read at the other end, and disposing this end makes the other end read end of stream, as a socket does.</summary>
    private sealed class InMemoryDuplexStream : Stream
    {
        private readonly Channel<byte[]> incoming;
        private readonly Channel<byte[]> outgoing;
        private readonly CancellationTokenSource disposed = new();
        private byte[] pending = [];
        private int pendingOffset;

        private InMemoryDuplexStream(Channel<byte[]> incoming, Channel<byte[]> outgoing)
        {
            this.incoming = incoming;
            this.outgoing = outgoing;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public static (InMemoryDuplexStream Left, InMemoryDuplexStream Right) CreatePair()
        {
            var leftToRight = Channel.CreateUnbounded<byte[]>();
            var rightToLeft = Channel.CreateUnbounded<byte[]>();
            return (new InMemoryDuplexStream(rightToLeft, leftToRight), new InMemoryDuplexStream(leftToRight, rightToLeft));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (pendingOffset == pending.Length)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposed.Token);
                try
                {
                    pending = await incoming.Reader.ReadAsync(linked.Token);
                    pendingOffset = 0;
                }
                catch (ChannelClosedException)
                {
                    return 0;
                }
                catch (OperationCanceledException) when (disposed.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }
            }

            var count = Math.Min(buffer.Length, pending.Length - pendingOffset);
            pending.AsMemory(pendingOffset, count).CopyTo(buffer);
            pendingOffset += count;
            return count;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            outgoing.Writer.TryWrite(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                outgoing.Writer.TryComplete();
                disposed.Cancel();
            }

            base.Dispose(disposing);
        }
    }
}
