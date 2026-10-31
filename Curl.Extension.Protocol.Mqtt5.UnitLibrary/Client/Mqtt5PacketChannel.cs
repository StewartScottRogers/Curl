using Curl.Extension.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Mqtt5.Client;

/// <summary>
/// Sends and receives whole MQTT 5 control packets over a byte transport. The transport is any
/// <see cref="IConnection"/>: TCP, TLS, or a WebSocket carrying binary messages (BL-4244).
/// </summary>
internal sealed class Mqtt5PacketChannel(IConnection transport, CancellationToken cancellationToken)
{
    private const int ReadSize = 4096;

    private byte[] buffer = new byte[ReadSize];

    private int filled;

    /// <summary>Writes <paramref name="packet"/> and flushes it; a failed write ends the transfer with a send error.</summary>
    internal async ValueTask SendAsync(Mqtt5Packet packet)
    {
        byte[] bytes = packet.ToBytes();
        try
        {
            await transport.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Mqtt5ClientException(CurlExitCode.SendError, Mqtt5ClientMessages.SendFailed);
        }
    }

    /// <summary>
    /// Reads the next whole packet. The peer closing first ends the transfer with a receive error, and a packet that
    /// breaks the wire format with a weird server reply.
    /// </summary>
    internal async ValueTask<Mqtt5Packet> ReceiveAsync()
    {
        while (true)
        {
            if (TryTakePacket() is { } packet)
            {
                return packet;
            }

            await ReadMoreAsync().ConfigureAwait(false);
        }
    }

    private Mqtt5Packet? TryTakePacket()
    {
        try
        {
            if (!Mqtt5PacketReader.TryRead(buffer.AsMemory(0, filled), out Mqtt5Packet? packet, out int packetLength))
            {
                return null;
            }

            buffer.AsSpan(packetLength, filled - packetLength).CopyTo(buffer);
            filled -= packetLength;
            return packet;
        }
        catch (Mqtt5MalformedPacketException malformed)
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.MalformedPacket(malformed.Message));
        }
    }

    private async ValueTask ReadMoreAsync()
    {
        if (buffer.Length - filled < ReadSize)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        int read = await ReadTransportAsync().ConfigureAwait(false);
        if (read == 0)
        {
            throw new Mqtt5ClientException(CurlExitCode.RecvError, Mqtt5ClientMessages.ConnectionDisconnected);
        }

        filled += read;
    }

    private async ValueTask<int> ReadTransportAsync()
    {
        try
        {
            return await transport.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Mqtt5ClientException(CurlExitCode.RecvError, Mqtt5ClientMessages.ReceiveFailed);
        }
    }
}
