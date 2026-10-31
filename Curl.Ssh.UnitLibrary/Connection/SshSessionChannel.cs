using System.Text;
using Curl.Ssh.Transport;

namespace Curl.Ssh.Connection;

/// <summary>
/// One <c>session</c> channel of the SSH connection protocol (RFC 4254 section 6) as libssh2
/// 1.11.1 opens it for curl 8.21.0, with a subsystem or a command started with a reply
/// wanted (ADR-0220).
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal sealed class SshSessionChannel(SshTransport transport) : SshChannel(transport)
{
    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_OPEN</c> for a <c>session</c> channel and waits for the
    /// server's answer.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the server opened the channel.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal ValueTask<bool> OpenAsync(CancellationToken cancellationToken) =>
        OpenChannelAsync("session", [], cancellationToken);

    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_REQUEST</c> to start <paramref name="subsystem" />, with a
    /// reply wanted, and waits for the reply.
    /// </summary>
    /// <param name="subsystem">The subsystem's name, such as <c>sftp</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the server started the subsystem.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal ValueTask<bool> RequestSubsystemAsync(string subsystem, CancellationToken cancellationToken) =>
        RequestProcessAsync("subsystem"u8.ToArray(), Encoding.ASCII.GetBytes(subsystem), cancellationToken);

    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_REQUEST</c> to run <paramref name="command" /> with
    /// <c>exec</c>, with a reply wanted, as libssh2 starts <c>scp</c>, and waits for the
    /// reply.
    /// </summary>
    /// <param name="command">The command line's bytes, such as <c>scp -pf '/f'</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the server started the command.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal ValueTask<bool> RequestExecAsync(byte[] command, CancellationToken cancellationToken) =>
        RequestProcessAsync("exec"u8.ToArray(), command, cancellationToken);

    // RFC 4254 section 6.5: the request's type, want-reply, then the subsystem or command.
    private async ValueTask<bool> RequestProcessAsync(byte[] requestType, byte[] value, CancellationToken cancellationToken)
    {
        SshWireWriter request = new();
        request.WriteByte(SshConnectionMessageNumber.ChannelRequest);
        request.WriteUInt32(RemoteChannelNumber);
        request.WriteString(requestType);
        request.WriteBoolean(true);
        request.WriteString(value);
        await Transport.PacketWriter.WriteAsync(request.ToArray(), cancellationToken).ConfigureAwait(false);
        byte[] answer = await WaitForAsync(SshConnectionMessageNumber.ChannelSuccess, SshConnectionMessageNumber.ChannelFailure, cancellationToken).ConfigureAwait(false);
        bool started = answer[0] == SshConnectionMessageNumber.ChannelSuccess;
        DiagnosticLog.ChannelRequested(requestType, value, started);
        return started;
    }
}
