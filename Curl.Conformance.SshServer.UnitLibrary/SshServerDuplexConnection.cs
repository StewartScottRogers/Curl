using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.SshServer;

/// <summary>
/// One end of an in-memory byte pipe between Curl's SSH client and the conformance SSH
/// server: what one end writes, the other end reads, and disposing an end tells the other
/// end the stream has ended.
/// </summary>
internal sealed class SshServerDuplexConnection : IConnection
{
    private readonly ChannelReader<byte[]> inbound;

    private readonly ChannelWriter<byte[]> outbound;

    private ReadOnlyMemory<byte> pending;

    private SshServerDuplexConnection(ChannelReader<byte[]> inbound, ChannelWriter<byte[]> outbound)
    {
        this.inbound = inbound;
        this.outbound = outbound;
    }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Creates two connected ends.
    /// </summary>
    /// <returns>The client's end and the server's end.</returns>
    internal static (SshServerDuplexConnection Client, SshServerDuplexConnection Server) CreatePair()
    {
        Channel<byte[]> toServer = Channel.CreateUnbounded<byte[]>();
        Channel<byte[]> toClient = Channel.CreateUnbounded<byte[]>();
        return (new SshServerDuplexConnection(toClient.Reader, toServer.Writer), new SshServerDuplexConnection(toServer.Reader, toClient.Writer));
    }

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pending.IsEmpty)
        {
            if (!await inbound.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            pending = await inbound.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        int taken = Math.Min(buffer.Length, pending.Length);
        pending[..taken].CopyTo(buffer);
        pending = pending[taken..];
        return taken;
    }

    /// <inheritdoc />
    /// <exception cref="IOException">This end has been closed.</exception>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        outbound.TryWrite(buffer.ToArray())
            ? ValueTask.CompletedTask
            : throw new IOException("The in-memory SSH connection has been closed.");

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        outbound.TryComplete();
        return ValueTask.CompletedTask;
    }
}
