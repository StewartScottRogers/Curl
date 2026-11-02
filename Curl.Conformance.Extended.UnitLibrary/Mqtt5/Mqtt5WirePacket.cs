using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Mqtt5;

/// <summary>
/// One MQTT 5 control packet as the reference broker received it: the fixed header's first byte and the
/// body its Remaining Length framed (section 2.1).
/// </summary>
/// <param name="FirstByte">The packet type in the high four bits and its flags in the low four.</param>
/// <param name="Body">The variable header and payload.</param>
public sealed record Mqtt5WirePacket(byte FirstByte, byte[] Body)
{
    /// <summary>The CONNECT packet type.</summary>
    public const int Connect = 1;

    /// <summary>The PUBLISH packet type.</summary>
    public const int Publish = 3;

    /// <summary>The SUBSCRIBE packet type.</summary>
    public const int Subscribe = 8;

    /// <summary>The DISCONNECT packet type.</summary>
    public const int Disconnect = 14;

    /// <summary>Gets the packet type (table 2-1).</summary>
    public int PacketType => FirstByte >> 4;

    /// <summary>Gets the fixed header's flags (table 2-2).</summary>
    public int Flags => FirstByte & 0x0F;

    /// <summary>
    /// Reads one packet from the connection and checks its fixed header flags against table 2-2
    /// (MQTT-2.1.3-1): PUBLISH's are its own, PUBREL, SUBSCRIBE and UNSUBSCRIBE carry 0010, the rest 0000.
    /// </summary>
    /// <param name="connection">The connection to the client.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The packet, or null when the client closed the connection between packets.</returns>
    public static async Task<Mqtt5WirePacket?> ReadAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        byte[] one = new byte[1];
        if (await connection.ReadAsync(one, cancellationToken).ConfigureAwait(false) == 0)
        {
            return null;
        }

        byte firstByte = one[0];
        List<byte> lengthBytes = [];
        while (lengthBytes.Count < 4 && (lengthBytes.Count == 0 || (lengthBytes[^1] & 0x80) != 0))
        {
            lengthBytes.Add(await ReadByteAsync(connection, one, cancellationToken).ConfigureAwait(false));
        }

        int next = 0;
        int remainingLength = Mqtt5WireReader.DecodeVariableByteInteger(() => lengthBytes[next++]);
        byte[] body = new byte[remainingLength];
        for (int filled = 0; filled < remainingLength; filled++)
        {
            body[filled] = await ReadByteAsync(connection, one, cancellationToken).ConfigureAwait(false);
        }

        Mqtt5WirePacket packet = new(firstByte, body);
        packet.CheckFlags();
        return packet;
    }

    /// <summary>Gets the bytes of a packet with this type and flags and the given body, as sent on the wire.</summary>
    /// <param name="firstByte">The fixed header's first byte.</param>
    /// <param name="body">The variable header and payload.</param>
    /// <returns>The fixed header and the body.</returns>
    public static byte[] Frame(byte firstByte, params byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        List<byte> bytes = [firstByte];
        int length = body.Length;
        do
        {
            byte encoded = (byte)(length % 128);
            length /= 128;
            bytes.Add(length > 0 ? (byte)(encoded | 0x80) : encoded);
        }
        while (length > 0);
        return [.. bytes, .. body];
    }

    private void CheckFlags()
    {
        int expected = PacketType switch
        {
            Publish => Flags,
            6 or Subscribe or 10 => 0b0010,
            _ => 0,
        };
        if (Flags != expected)
        {
            throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT-2.1.3-1", $"packet type {PacketType} has flags {Flags:X1}, not the reserved {expected:X1}.");
        }
    }

    private static async Task<byte> ReadByteAsync(ReferenceConnection connection, byte[] one, CancellationToken cancellationToken) =>
        await connection.ReadAsync(one, cancellationToken).ConfigureAwait(false) == 0
            ? throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT5-2.1-1", "the client closed the connection inside a packet.")
            : one[0];
}
