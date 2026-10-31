using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Ssh.Connection;

namespace Curl.Extension.SshJump;

/// <summary>
/// A transfer's connection through the jump host: the bytes of its <c>direct-tcpip</c> channel, and on
/// disposal the channel closed (RFC 4254 section 5.3) and then the connection to the jump host.
/// </summary>
/// <param name="channel">The open channel to the transfer's host and port.</param>
/// <param name="jumpConnection">The connection to the jump host the channel runs over, which this connection owns.</param>
internal sealed class SshJumpConnection(SshDirectTcpipChannel channel, IConnection jumpConnection) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => channel.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => channel.RemoteEndPoint;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        channel.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        channel.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        channel.FlushAsync(cancellationToken);

    /// <summary>
    /// Closes the channel, then the connection to the jump host; a jump host that has already gone away
    /// does not fail the close.
    /// </summary>
    /// <returns>A task that completes once both are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        // A close the jump host cannot answer, as it has gone away, still closes its connection.
        await channel.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await jumpConnection.DisposeAsync().ConfigureAwait(false);
    }
}
