using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// An <see cref="IConnection" /> whose peer sends the given bytes and then closes, and
/// which discards what is written to it: the connection <see cref="SshWireDecoders" /> reads
/// hostile packet bytes through, so the packet reader runs as it does against a server.
/// </summary>
/// <param name="received">The bytes the peer sends.</param>
internal sealed class SshByteArrayConnection(ReadOnlyMemory<byte> received) : IConnection
{
    private ReadOnlyMemory<byte> unread = received;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int count = Math.Min(buffer.Length, unread.Length);
        unread[..count].CopyTo(buffer);
        unread = unread[count..];
        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
