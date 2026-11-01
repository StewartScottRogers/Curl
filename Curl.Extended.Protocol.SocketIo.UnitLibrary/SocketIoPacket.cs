using System.Globalization;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>
/// One Socket.IO protocol v5 packet as section "Packet encoding" writes it:
/// <c>&lt;type&gt;[&lt;namespace&gt;,][&lt;ack id&gt;][&lt;JSON data&gt;]</c>, the namespace left out when it is <c>/</c>.
/// </summary>
/// <param name="Type">The packet type.</param>
/// <param name="Namespace">The namespace, <c>/</c> for the main one.</param>
/// <param name="AckId">The acknowledgement id, or <see langword="null" /> when none was asked for.</param>
/// <param name="Data">The JSON payload, empty when there is none.</param>
internal sealed record SocketIoPacket(SocketIoPacketType Type, string Namespace, long? AckId, string Data)
{
    /// <summary>The main namespace.</summary>
    public const string MainNamespace = "/";

    /// <summary>Gives the packet's text, which goes out as an Engine.IO <c>message</c> packet's payload.</summary>
    /// <returns>The encoded packet.</returns>
    public string Encode()
    {
        string namespacePart = Namespace == MainNamespace ? string.Empty : Namespace + ",";
        string ackPart = AckId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{(int)Type}{namespacePart}{ackPart}{Data}";
    }

    /// <summary>Reads a packet from an Engine.IO <c>message</c> packet's payload.</summary>
    /// <param name="text">The payload.</param>
    /// <returns>The packet, or <see langword="null" /> when the text does not start with a packet type.</returns>
    public static SocketIoPacket? Decode(string text)
    {
        if (text.Length == 0 || text[0] is < '0' or > '6')
        {
            return null;
        }

        int index = 1;
        string packetNamespace = ReadNamespace(text, ref index);
        long? ackId = ReadAckId(text, ref index);
        return new SocketIoPacket((SocketIoPacketType)(text[0] - '0'), packetNamespace, ackId, text[index..]);
    }

    // The namespace and its comma, when the text after the type starts with '/'; a namespace with no data has no comma.
    private static string ReadNamespace(string text, ref int index)
    {
        if (!text.AsSpan(index).StartsWith("/", StringComparison.Ordinal))
        {
            return MainNamespace;
        }

        int comma = text.IndexOf(',', index);
        int end = comma < 0 ? text.Length : comma;
        string packetNamespace = text[index..end];
        index = Math.Min(end + 1, text.Length);
        return packetNamespace;
    }

    private static long? ReadAckId(string text, ref int index)
    {
        int digitsStart = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return index > digitsStart ? long.Parse(text.AsSpan(digitsStart, index - digitsStart), CultureInfo.InvariantCulture) : null;
    }
}
