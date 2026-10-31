using System.Text;
using Curl.Ssh.Transport;

namespace Curl.Ssh.Connection;

/// <summary>
/// One channel of the SSH connection protocol (RFC 4254 section 5) as libssh2 1.11.1 opens
/// it for curl 8.21.0: a 2 MiB window and 32 KiB packets, the channel's data read and
/// written as a byte stream within both windows, and the channel closed as measured
/// (ADR-0220). <see cref="SshSessionChannel" /> and <see cref="SshDirectTcpipChannel" /> open
/// it with their own channel type.
/// </summary>
/// <remarks>
/// While it waits, the channel answers what the connection asks of it: a <c>KEXINIT</c>
/// with a key re-exchange, a global or channel request that wants a reply with a
/// refusal, and a window adjustment by growing the server's window. Every other message
/// it does not wait for is skipped.
/// </remarks>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal abstract class SshChannel(SshTransport transport)
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

    private long remoteWindow;

    private uint remoteMaximumPacketSize;

    private long localWindow = InitialWindowSize;

    private bool remoteSentEof;

    private bool remoteClosed;

    /// <summary>
    /// Gets the reason code of the server's <c>SSH_MSG_CHANNEL_OPEN_FAILURE</c> once the
    /// channel's open has returned <see langword="false" />, such as 2 for
    /// <c>SSH_OPEN_CONNECT_FAILED</c>; 0 before then.
    /// </summary>
    internal uint OpenFailureReasonCode { get; private set; }

    /// <summary>
    /// Gets where the channel's steps are logged: beside the transport's log, to the same diagnostic log.
    /// </summary>
    internal SshChannelLog DiagnosticLog { get; } = new(transport.DiagnosticLog);

    /// <summary>Gets the transport the channel's messages go over.</summary>
    private protected SshTransport Transport => transport;

    /// <summary>Gets the server's number for the channel, once it is open.</summary>
    private protected uint RemoteChannelNumber { get; private set; }

    /// <summary>
    /// Reads the next bytes of the channel's data into <paramref name="buffer" />.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many bytes were read; 0 once the server has sent <c>EOF</c> or <c>CLOSE</c> and every byte before it was read.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
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
    /// Sends <paramref name="data" /> as <c>SSH_MSG_CHANNEL_DATA</c>, in packets no larger
    /// than the server accepts, waiting for a window adjustment whenever the server's
    /// window is used up (RFC 4254 section 5.2).
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
            packet.WriteUInt32(RemoteChannelNumber);
            packet.WriteString(data.Span[..size]);
            await transport.PacketWriter.WriteAsync(packet.ToArray(), cancellationToken).ConfigureAwait(false);
            remoteWindow -= size;
            data = data[size..];
        }
    }

    /// <summary>
    /// Closes the channel as libssh2 does: sends <c>SSH_MSG_CHANNEL_EOF</c>, waits for the
    /// server's <c>SSH_MSG_CHANNEL_CLOSE</c>, then sends its own (RFC 4254 section 5.3).
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

    /// <summary>
    /// Closes the channel as libssh2 frees one a failed SCP upload leaves open: sends
    /// <c>SSH_MSG_CHANNEL_EOF</c> and <c>SSH_MSG_CHANNEL_CLOSE</c> at once, then waits for
    /// the server's <c>SSH_MSG_CHANNEL_CLOSE</c>, as measured 2026-09-29 (BL-577).
    /// </summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes once the server's <c>CLOSE</c> has arrived.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected first.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    internal async ValueTask CloseAtOnceAsync(CancellationToken cancellationToken)
    {
        await SendChannelMessageAsync(SshConnectionMessageNumber.ChannelEof, cancellationToken).ConfigureAwait(false);
        await SendChannelMessageAsync(SshConnectionMessageNumber.ChannelClose, cancellationToken).ConfigureAwait(false);
        while (!remoteClosed)
        {
            await ReadNextMessageAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_OPEN</c> for <paramref name="channelType" />, with the
    /// client's channel number, window and packet size and then
    /// <paramref name="typeSpecificData" />, and waits for the server's answer (RFC 4254
    /// section 5.1).
    /// </summary>
    /// <param name="channelType">The channel type's name, such as <c>session</c>.</param>
    /// <param name="typeSpecificData">The fields the channel type adds, already encoded.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the server opened the channel.</returns>
    /// <exception cref="EndOfStreamException">The peer closed or disconnected.</exception>
    /// <exception cref="InvalidDataException">The peer broke the framing or sent a malformed message.</exception>
    private protected async ValueTask<bool> OpenChannelAsync(string channelType, byte[] typeSpecificData, CancellationToken cancellationToken)
    {
        SshWireWriter open = new();
        open.WriteByte(SshConnectionMessageNumber.ChannelOpen);
        open.WriteString(Encoding.ASCII.GetBytes(channelType));
        open.WriteUInt32(LocalChannelNumber);
        open.WriteUInt32(InitialWindowSize);
        open.WriteUInt32(MaximumPacketSize);
        open.WriteBytes(typeSpecificData);
        await transport.PacketWriter.WriteAsync(open.ToArray(), cancellationToken).ConfigureAwait(false);
        byte[] answer = await WaitForAsync(SshConnectionMessageNumber.ChannelOpenConfirmation, SshConnectionMessageNumber.ChannelOpenFailure, cancellationToken).ConfigureAwait(false);
        SshWireReader confirmation = new(answer.AsMemory(1));
        confirmation.ReadUInt32();
        if (answer[0] == SshConnectionMessageNumber.ChannelOpenFailure)
        {
            OpenFailureReasonCode = confirmation.ReadUInt32();
            DiagnosticLog.ChannelOpened(channelType, false);
            return false;
        }

        RemoteChannelNumber = confirmation.ReadUInt32();
        remoteWindow = confirmation.ReadUInt32();
        remoteMaximumPacketSize = Math.Max(confirmation.ReadUInt32(), 1);
        DiagnosticLog.ChannelOpened(channelType, true);
        return true;
    }

    /// <summary>Waits for the message <paramref name="wanted" /> or <paramref name="alternative" />, answering every other one.</summary>
    /// <param name="wanted">One message number waited for.</param>
    /// <param name="alternative">The other message number waited for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The message's payload, from its number on.</returns>
    private protected async ValueTask<byte[]> WaitForAsync(byte wanted, byte alternative, CancellationToken cancellationToken)
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
        adjust.WriteUInt32(RemoteChannelNumber);
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
        number.WriteUInt32(RemoteChannelNumber);
        return number.ToArray();
    }
}
