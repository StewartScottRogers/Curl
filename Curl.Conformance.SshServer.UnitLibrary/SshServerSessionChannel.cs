using System.Text;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The server's side of one <c>session</c> channel (RFC 4254): it confirms the client's
/// <c>CHANNEL_OPEN</c>, starts the process the client's <c>exec</c> or <c>subsystem</c> request
/// names (refusing any other request that wants a reply), then carries data both ways with
/// window accounting, and ends with <c>exit-status</c>, <c>EOF</c> and <c>CLOSE</c>. What runs on
/// it, SCP or SFTP, is BL-1917's and BL-1918's.
/// </summary>
internal sealed class SshServerSessionChannel
{
    /// <summary>The window the server grants the client, and restores once half of it is used.</summary>
    internal const uint InitialWindowSize = 2 * 1024 * 1024;

    /// <summary>The largest data packet the server accepts.</summary>
    internal const uint MaximumPacketSize = 32768;

    private const uint ServerChannel = 0;

    private readonly SshServerTransport transport;

    private readonly uint clientChannel;

    private readonly uint clientMaximumPacketSize;

    private long clientWindow;

    private long serverWindow = InitialWindowSize;

    private SshServerSessionChannel(SshServerTransport transport, string user, uint clientChannel, uint clientWindow, uint clientMaximumPacketSize)
    {
        this.transport = transport;
        User = user;
        this.clientChannel = clientChannel;
        this.clientWindow = clientWindow;
        this.clientMaximumPacketSize = Math.Max(clientMaximumPacketSize, 1);
    }

    /// <summary>Gets the user the session authenticated as.</summary>
    internal string User { get; }

    /// <summary>Gets the process request that started the channel: <c>exec</c> or <c>subsystem</c>.</summary>
    internal string ProcessRequest { get; private set; } = string.Empty;

    /// <summary>Gets the request's command (for <c>exec</c>, such as <c>scp -f /file</c>) or subsystem name (such as <c>sftp</c>).</summary>
    internal string Process { get; private set; } = string.Empty;

    /// <summary>
    /// Confirms the client's <c>session</c> channel and waits for its <c>exec</c> or
    /// <c>subsystem</c> request, which it accepts.
    /// </summary>
    /// <param name="transport">The session's transport, past authentication.</param>
    /// <param name="user">The user that authenticated.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The open channel.</returns>
    /// <exception cref="InvalidDataException">The client sends another message, or opens a channel of another type.</exception>
    internal static async ValueTask<SshServerSessionChannel> OpenAsync(SshServerTransport transport, string user, CancellationToken cancellationToken)
    {
        SshWireReader open = new(await ReadMessageAsync(transport, SshConnectionMessageNumber.ChannelOpen, cancellationToken).ConfigureAwait(false));
        open.ReadByte();
        string type = open.ReadName();
        if (type != "session")
        {
            throw new InvalidDataException($"The SSH client opens a '{type}' channel, not a session.");
        }

        SshServerSessionChannel channel = new(transport, user, open.ReadUInt32(), open.ReadUInt32(), open.ReadUInt32());
        SshWireWriter confirmation = new();
        confirmation.WriteByte(SshConnectionMessageNumber.ChannelOpenConfirmation);
        confirmation.WriteUInt32(channel.clientChannel);
        confirmation.WriteUInt32(ServerChannel);
        confirmation.WriteUInt32(InitialWindowSize);
        confirmation.WriteUInt32(MaximumPacketSize);
        await transport.WritePacketAsync(confirmation.ToArray(), cancellationToken).ConfigureAwait(false);
        await channel.AcceptProcessRequestAsync(cancellationToken).ConfigureAwait(false);
        return channel;
    }

    /// <summary>
    /// Reads the client's next data, granting more window once half of it is used.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The data, or nothing once the client sends <c>EOF</c> or <c>CLOSE</c>.</returns>
    /// <exception cref="InvalidDataException">The client sends a message the channel does not carry.</exception>
    internal async ValueTask<byte[]> ReadDataAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] payload = await transport.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
            SshWireReader message = new(payload);
            byte number = message.ReadByte();
            message.ReadUInt32();
            switch (number)
            {
                case SshConnectionMessageNumber.ChannelData:
                    byte[] data = message.ReadString().ToArray();
                    await ConsumeWindowAsync(data.Length, cancellationToken).ConfigureAwait(false);
                    return data;
                case SshConnectionMessageNumber.ChannelWindowAdjust:
                    clientWindow += message.ReadUInt32();
                    break;
                case SshConnectionMessageNumber.ChannelEof or SshConnectionMessageNumber.ChannelClose:
                    return [];
                default:
                    throw new InvalidDataException($"The SSH client sent message {number} on the session channel.");
            }
        }
    }

    /// <summary>
    /// Sends data in packets no larger than the client's maximum, waiting for
    /// <c>WINDOW_ADJUST</c> whenever the client's window is used up.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once every byte is sent.</returns>
    /// <exception cref="InvalidDataException">The client sends another message while the server waits for window.</exception>
    internal async ValueTask WriteDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (!data.IsEmpty)
        {
            while (clientWindow == 0)
            {
                SshWireReader adjust = new(await ReadMessageAsync(transport, SshConnectionMessageNumber.ChannelWindowAdjust, cancellationToken).ConfigureAwait(false));
                adjust.ReadBytes(5);
                clientWindow += adjust.ReadUInt32();
            }

            int size = (int)Math.Min(data.Length, Math.Min(clientWindow, clientMaximumPacketSize));
            SshWireWriter packet = Header(SshConnectionMessageNumber.ChannelData);
            packet.WriteString(data.Span[..size]);
            await transport.WritePacketAsync(packet.ToArray(), cancellationToken).ConfigureAwait(false);
            clientWindow -= size;
            data = data[size..];
        }
    }

    /// <summary>Ends the process: <c>exit-status</c>, then <c>EOF</c> and <c>CLOSE</c>.</summary>
    /// <param name="exitStatus">The process's exit status.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the three messages are sent.</returns>
    internal async ValueTask CloseAsync(uint exitStatus, CancellationToken cancellationToken)
    {
        SshWireWriter status = Header(SshConnectionMessageNumber.ChannelRequest);
        status.WriteString("exit-status"u8);
        status.WriteBoolean(false);
        status.WriteUInt32(exitStatus);
        await transport.WritePacketAsync(status.ToArray(), cancellationToken).ConfigureAwait(false);
        await transport.WritePacketAsync(Header(SshConnectionMessageNumber.ChannelEof).ToArray(), cancellationToken).ConfigureAwait(false);
        await transport.WritePacketAsync(Header(SshConnectionMessageNumber.ChannelClose).ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> ReadMessageAsync(SshServerTransport transport, byte expectedMessageNumber, CancellationToken cancellationToken)
    {
        byte[] payload = await transport.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        return payload[0] == expectedMessageNumber
            ? payload
            : throw new InvalidDataException($"The SSH client sent message {payload[0]} where the server expects {expectedMessageNumber}.");
    }

    // RFC 4254 section 6.5: the first exec or subsystem request starts the process; anything
    // before it, such as pty-req or env, is refused when the client wants a reply.
    private async ValueTask AcceptProcessRequestAsync(CancellationToken cancellationToken)
    {
        while (!await AnswerRequestAsync(cancellationToken).ConfigureAwait(false))
        {
        }
    }

    // Answers one CHANNEL_REQUEST, when the client wants a reply, and says whether it started the process.
    private async ValueTask<bool> AnswerRequestAsync(CancellationToken cancellationToken)
    {
        SshWireReader request = new(await ReadMessageAsync(transport, SshConnectionMessageNumber.ChannelRequest, cancellationToken).ConfigureAwait(false));
        (bool starts, bool wantReply) = ReadRequest(request);
        if (wantReply)
        {
            await transport.WritePacketAsync(Header(starts ? SshConnectionMessageNumber.ChannelSuccess : SshConnectionMessageNumber.ChannelFailure).ToArray(), cancellationToken).ConfigureAwait(false);
        }

        return starts;
    }

    // Reads a CHANNEL_REQUEST, keeping an exec or subsystem request's type and value.
    private (bool Starts, bool WantReply) ReadRequest(SshWireReader request)
    {
        request.ReadBytes(5);
        string type = request.ReadName();
        bool wantReply = request.ReadBoolean();
        if (type is not ("exec" or "subsystem"))
        {
            return (false, wantReply);
        }

        ProcessRequest = type;
        Process = Encoding.UTF8.GetString(request.ReadString().Span);
        return (true, wantReply);
    }

    private async ValueTask ConsumeWindowAsync(int length, CancellationToken cancellationToken)
    {
        serverWindow -= length;
        if (serverWindow < InitialWindowSize / 2)
        {
            SshWireWriter adjust = Header(SshConnectionMessageNumber.ChannelWindowAdjust);
            adjust.WriteUInt32((uint)(InitialWindowSize - serverWindow));
            await transport.WritePacketAsync(adjust.ToArray(), cancellationToken).ConfigureAwait(false);
            serverWindow = InitialWindowSize;
        }
    }

    private SshWireWriter Header(byte messageNumber)
    {
        SshWireWriter writer = new();
        writer.WriteByte(messageNumber);
        writer.WriteUInt32(clientChannel);
        return writer;
    }
}
