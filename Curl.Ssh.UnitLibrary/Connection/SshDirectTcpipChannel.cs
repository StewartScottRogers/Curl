using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Ssh.Transport;

namespace Curl.Ssh.Connection;

/// <summary>
/// One <c>direct-tcpip</c> channel of the SSH connection protocol (RFC 4254 section 7.2): a
/// TCP connection the server opens to a target host and port on the client's behalf, read
/// and written as an <see cref="IConnection" />, such as the transfer's connection through
/// an SSH jump host.
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
/// <param name="targetHost">The host the server connects to.</param>
/// <param name="targetPort">The port the server connects to.</param>
internal sealed class SshDirectTcpipChannel(SshTransport transport, string targetHost, int targetPort) : SshChannel(transport), IConnection
{
    private bool closed;

    /// <summary>Gets a value indicating that the bytes are encrypted between the client and the SSH server.</summary>
    public bool IsSecure => true;

    /// <summary>Gets the target host and port the server connects to.</summary>
    public EndPoint? RemoteEndPoint { get; } = new DnsEndPoint(targetHost, targetPort);

    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_OPEN</c> for a <c>direct-tcpip</c> channel to the target host
    /// and port, naming where the connection comes from, and waits for the server's answer.
    /// </summary>
    /// <param name="originatorAddress">The address the connection comes from, such as <c>127.0.0.1</c>.</param>
    /// <param name="originatorPort">The port the connection comes from.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the server opened the channel; when not, <see cref="SshChannel.OpenFailureReasonCode" /> says why.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal ValueTask<bool> OpenAsync(string originatorAddress, int originatorPort, CancellationToken cancellationToken)
    {
        SshWireWriter fields = new();
        fields.WriteString(Encoding.UTF8.GetBytes(targetHost));
        fields.WriteUInt32((uint)targetPort);
        fields.WriteString(Encoding.UTF8.GetBytes(originatorAddress));
        fields.WriteUInt32((uint)originatorPort);
        return OpenChannelAsync("direct-tcpip", fields.ToArray(), cancellationToken);
    }

    /// <summary>Sends <paramref name="buffer" /> as channel data within the server's window and packet size.</summary>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once every byte is sent.</returns>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => SendAsync(buffer, cancellationToken);

    /// <summary>Does nothing: every channel data packet is written to the transport as it is sent.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Closes the channel once, as <see cref="SshChannel.CloseAsync" /> does: <c>EOF</c>,
    /// the server's <c>CLOSE</c>, then the client's.
    /// </summary>
    /// <returns>A task that completes once the channel is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
