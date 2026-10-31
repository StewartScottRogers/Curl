using System.Globalization;
using Curl.Extended.Protocol.Mqtt5.Packets;

namespace Curl.Extended.Protocol.Mqtt5.Client;

/// <summary>The error messages an MQTT 5 transfer ends with; the ones curl's <c>mqtt://</c> also gives are its text.</summary>
internal static class Mqtt5ClientMessages
{
    /// <summary>curl's text for a URL whose path names no topic.</summary>
    internal const string NoTopic = "No MQTT topic found. Forgot to URL encode it?";

    /// <summary>curl's text for the peer closing the connection while a packet is awaited.</summary>
    internal const string ConnectionDisconnected = "Connection disconnected";

    /// <summary>curl's text for a failed receive.</summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>curl's text for a failed send.</summary>
    internal const string SendFailed = "Failed sending data to the peer";

    /// <summary>curl's text for a server reply it did not expect.</summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>The text for a topic in the URL that MQTT 5 does not allow.</summary>
    internal static string TopicInvalid(string reason) => $"Invalid MQTT 5 topic: {reason}";

    /// <summary>The text for a packet from the server that breaks the wire format.</summary>
    internal static string MalformedPacket(string reason) => $"Weird server reply: malformed MQTT 5 packet: {reason}";

    /// <summary>The text for a packet carrying a failure reason code, naming the packet and the reason code.</summary>
    internal static string Refused(string packetName, Mqtt5ReasonCode reasonCode) =>
        string.Create(CultureInfo.InvariantCulture, $"MQTT 5 {packetName} reason code 0x{(byte)reasonCode:X2} {reasonCode}");

    /// <summary>curl's text for output that could not be written.</summary>
    internal static string OutputWriteFailed(int passed) =>
        string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned 0");
}
