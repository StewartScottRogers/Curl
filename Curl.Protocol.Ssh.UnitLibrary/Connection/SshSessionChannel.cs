using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Connection;

/// <summary>
/// One <c>session</c> channel of the SSH connection protocol (RFC 4254) as libssh2 1.11.1
/// opens it for curl 8.21.0: a 2 MiB window and 32 KiB packets, a subsystem started with a
/// reply wanted, the channel's data read and written as a byte stream within both
/// windows, and the channel closed as measured (ADR-0220).
/// </summary>
/// <remarks>
/// While it waits, the channel answers what the connection asks of it: a <c>KEXINIT</c>
/// with a key re-exchange, a global or channel request that wants a reply with a
/// refusal, and a window adjustment by growing the server's window. Every other message
/// it does not wait for is skipped.
/// </remarks>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal sealed class SshSessionChannel(SshTransport transport)
{
    /// <summary>The client's number for the channel: the first and only one it opens.</summary>
    internal const uint LocalChannelNumber = 0;

    /// <summary>
    /// The window the client grants the server, libssh2's
    /// <c>LIBSSH2_CHANNEL_WINDOW_DEFAULT</c>: measured 2026-09-29 as <c>win 2097152</c> in
    /// OpenSSH's log of curl's <c>CHANNEL_OPEN</c>.
    /// </summary>
    internal const uint InitialWindowSize = 2 * 1024 * 1024;

    /// <summary>
    /// The largest data packet the client accepts, libssh2's
    /// <c>LIBSSH2_CHANNEL_PACKET_DEFAULT</c>: measured as <c>max 32768</c>.
    /// </summary>
    internal const uint MaximumPacketSize = 32768;

    /// <summary>
    /// The window below which the client grants the server more: three quarters of
    /// <see cref="InitialWindowSize" />, after which it restores the whole window, as
    /// libssh2 grants about half a mebibyte at a time.
    /// </summary>
    internal const uint WindowAdjustThreshold = InitialWindowSize / 4 * 3;

    private readonly Queue<ReadOnlyMemory<byte>> received = new();

    private ReadOnlyMemory<byte> unread;

    private uint remoteChannelNumber;

    private long remoteWindow;

    private uint remoteMaximumPacketSize;

    private long localWindow = InitialWindowSize;

    private bool remoteSentEof;

    private bool remoteClosed;

    /// <summary>
    /// Gets the reason code of the server's <c>SSH_MSG_CHANNEL_OPEN_FAILURE</c> once
    /// <see cref="OpenAsync" /> has returned <see langword="false" />, such as 2 for
    /// <c>SSH_OPEN_CONNECT_FAILED</c>; 0 before then.
    /// </summary>
    internal uint OpenFailureReasonCode { get; private set; }

    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_OPEN</c> for a <c>session</c> channel and waits for the
    /// server's answer.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the server opened the channel.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal async ValueTask<bool> OpenAsync(CancellationToken cancellationToken)
    {
        SshWireWriter open = new();
        open.WriteByte(SshConnectionMessageNumber.ChannelOpen);
        open.WriteString("session"u8);
        open.WriteUInt32(LocalChannelNumber);
        open.WriteUInt32(InitialWindowSize);
        open.WriteUInt32(MaximumPacketSize);
        await transport.PacketWriter.WriteAsync(open.ToArray(), cancellationToken).ConfigureAwait(false);
        byte[] answer = await WaitForAsync(SshConnectionMessageNumber.ChannelOpenConfirmation, SshConnectionMessageNumber.ChannelOpenFailure, cancellationToken).ConfigureAwait(false);
        SshWireReader confirmation = new(answer.AsMemory(1));
        confirmation.ReadUInt32();
        if (answer[0] == SshConnectionMessageNumber.ChannelOpenFailure)
        {
            OpenFailureReasonCode = confirmation.ReadUInt32();
            return false;
        }

        remoteChannelNumber = confirmation.ReadUInt32();
        remoteWindow = confirmation.ReadUInt32();
        remoteMaximumPacketSize = Math.Max(confirmation.ReadUInt32(), 1);
        return true;
    }

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
        request.WriteUInt32(remoteChannelNumber);
        request.WriteString(requestType);
        request.WriteBoolean(true);
        request.WriteString(value);
        await transport.PacketWriter.WriteAsync(request.ToArray(), cancellationToken).ConfigureAwait(false);
        byte[] answer = await WaitForAsync(SshConnectionMessageNumber.ChannelSuccess, SshConnectionMessageNumber.ChannelFailure, cancellationToken).ConfigureAwait(false);
        return answer[0] == SshConnectionMessageNumber.ChannelSuccess;
    }

    /// <summary>
    /// Sends <paramref name="data" /> as <c>SSH_MSG_CHANNEL_DATA</c>, in packets no larger
    /// than the server accepts, waiting for a window adjustment whenever the server's
    /// window is used up.
    /// </summary>
    /// <param name="data">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once every byte is sent.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected while the window was used up.</exception>
    internal async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (!data.IsEmpty)
        {
            while (remoteWindow == 0)
            {
                await ReadNextMessageAsync(cancellationToken).ConfigureAwait(false);
            }

            int size = (int)Math.Min(data.Length, Math.Min(remoteWindow, remoteMaximumPacketSize));
            SshWireWriter packet = new();
            packet.WriteByte(SshConnectionMessageNumber.ChannelData);
            packet.WriteUInt32(remoteChannelNumber);
            packet.WriteString(data.Span[..size]);
            await transport.PacketWriter.WriteAsync(packet.ToArray(), cancellationToken).ConfigureAwait(false);
            remoteWindow -= size;
            data = data[size..];
        }
    }

    /// <summary>
    /// Reads the next bytes of the channel's data into <paramref name="buffer" />.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many bytes were read; 0 once the server has sent <c>EOF</c> or <c>CLOSE</c> and every byte before it was read.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (unread.IsEmpty)
        {
            if (received.TryDequeue(out ReadOnlyMemory<byte> next))
            {
                unread = next;
            }
            else if (remoteSentEof || remoteClosed)
            {
                return 0;
            }
            else
            {
                await ReadNextMessageAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        int taken = Math.Min(unread.Length, buffer.Length);
        unread[..taken].CopyTo(buffer);
        unread = unread[taken..];
        return taken;
    }

    /// <summary>
    /// Closes the channel as libssh2 does: sends <c>SSH_MSG_CHANNEL_EOF</c>, waits for the
    /// server's <c>SSH_MSG_CHANNEL_CLOSE</c>, then sends its own.
    /// </summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes once the client's <c>CLOSE</c> is sent.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected first.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        await SendChannelMessageAsync(SshConnectionMessageNumber.ChannelEof, cancellationToken).ConfigureAwait(false);
        while (!remoteClosed)
        {
            await ReadNextMessageAsync(cancellationToken).ConfigureAwait(false);
        }

        await SendChannelMessageAsync(SshConnectionMessageNumber.ChannelClose, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<byte[]> WaitForAsync(byte wanted, byte alternative, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[]? payload = await ReadNextMessageAsync(cancellationToken).ConfigureAwait(false);
            if (payload is not null && (payload[0] == wanted || payload[0] == alternative))
            {
                return payload;
            }
        }
    }

    // Null when the message was one the channel handles itself; the payload otherwise.
    private async ValueTask<byte[]?> ReadNextMessageAsync(CancellationToken cancellationToken)
    {
        byte[] payload = await transport.PacketReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (payload[0] is >= SshConnectionMessageNumber.ChannelWindowAdjust and <= SshConnectionMessageNumber.ChannelRequest)
        {
            await HandleChannelMessageAsync(payload, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return await HandleConnectionMessageAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<byte[]?> HandleConnectionMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        switch (payload[0])
        {
            case SshMessageNumber.Disconnect:
                throw new EndOfStreamException("The SSH server disconnected.");
            case SshMessageNumber.KeyExchangeInit:
                await transport.ReExchangeKeysAsync(payload, cancellationToken).ConfigureAwait(false);
                return null;
            case SshConnectionMessageNumber.GlobalRequest:
                await RefuseIfReplyWantedAsync(new SshWireReader(payload.AsMemory(1)), [SshConnectionMessageNumber.RequestFailure], cancellationToken).ConfigureAwait(false);
                return null;
            default:
                return payload;
        }
    }

    // Every channel message starts with the recipient channel, which is always this one.
    private async ValueTask HandleChannelMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        SshWireReader reader = new(payload.AsMemory(1));
        reader.ReadUInt32();
        switch (payload[0])
        {
            case SshConnectionMessageNumber.ChannelWindowAdjust:
                remoteWindow += reader.ReadUInt32();
                break;
            case SshConnectionMessageNumber.ChannelData:
                await ReceiveAsync(reader.ReadString(), cancellationToken).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelExtendedData:
                reader.ReadUInt32();
                await ConsumeWindowAsync(reader.ReadString().Length, cancellationToken).ConfigureAwait(false);
                break;
            case SshConnectionMessageNumber.ChannelEof:
                remoteSentEof = true;
                break;
            case SshConnectionMessageNumber.ChannelClose:
                remoteClosed = true;
                break;
            default:
                await RefuseIfReplyWantedAsync(reader, [SshConnectionMessageNumber.ChannelFailure, .. RemoteChannelNumberBytes()], cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    // A global request and a channel request both carry their name, then want-reply.
    private async ValueTask RefuseIfReplyWantedAsync(SshWireReader request, byte[] refusal, CancellationToken cancellationToken)
    {
        request.ReadString();
        if (request.ReadBoolean())
        {
            await transport.PacketWriter.WriteAsync(refusal, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ReceiveAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        received.Enqueue(data);
        await ConsumeWindowAsync(data.Length, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ConsumeWindowAsync(int count, CancellationToken cancellationToken)
    {
        localWindow -= count;
        if (localWindow >= WindowAdjustThreshold)
        {
            return;
        }

        SshWireWriter adjust = new();
        adjust.WriteByte(SshConnectionMessageNumber.ChannelWindowAdjust);
        adjust.WriteUInt32(remoteChannelNumber);
        adjust.WriteUInt32((uint)(InitialWindowSize - localWindow));
        await transport.PacketWriter.WriteAsync(adjust.ToArray(), cancellationToken).ConfigureAwait(false);
        localWindow = InitialWindowSize;
    }

    private async ValueTask SendChannelMessageAsync(byte messageNumber, CancellationToken cancellationToken)
    {
        byte[] message = [messageNumber, .. RemoteChannelNumberBytes()];
        await transport.PacketWriter.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private byte[] RemoteChannelNumberBytes()
    {
        SshWireWriter number = new();
        number.WriteUInt32(remoteChannelNumber);
        return number.ToArray();
    }
}
