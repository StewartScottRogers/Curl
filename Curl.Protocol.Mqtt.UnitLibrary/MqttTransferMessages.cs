using System.Globalization;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Every failure message the <c>mqtt</c> and <c>mqtts</c> schemes report, as curl 8.21.0's
/// <c>lib/mqtt.c</c> words it.
/// </summary>
/// <remarks>
/// Where <c>lib/mqtt.c</c> fails without calling <c>failf</c>, curl prints the text
/// <c>curl_easy_strerror</c> gives for the code, so those cases carry that text here.
/// </remarks>
internal static class MqttTransferMessages
{
    /// <summary>
    /// The exit 56 message for a peer that closed where a packet's first byte was due.
    /// </summary>
    internal const string ConnectionDisconnected = "Connection disconnected";

    /// <summary>
    /// The exit 3 message for a URL whose path names no topic.
    /// </summary>
    internal const string NoTopic = "No MQTT topic found. Forgot to URL encode it?";

    /// <summary>
    /// The exit 3 message for a topic longer than its two-byte length field can carry.
    /// </summary>
    internal const string TopicTooLong = "Too long MQTT topic";

    /// <summary>
    /// The exit 56 message curl falls back to for a failed or closed receive.
    /// </summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>
    /// The exit 55 message curl falls back to for a failed send.
    /// </summary>
    internal const string SendFailed = "Failure when sending data to the peer";

    /// <summary>
    /// The exit 18 message curl falls back to for a PUBLISH cut short by the peer.
    /// </summary>
    internal const string PartialFile = "Transferred a partial file";

    /// <summary>
    /// The exit 8 message curl falls back to for a packet it does not expect.
    /// </summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>
    /// The exit 23 message for an output that stopped accepting bytes, as curl words it
    /// when a write takes none of what it was offered.
    /// </summary>
    /// <param name="passed">The number of bytes offered to the output.</param>
    /// <returns>The message to report.</returns>
    internal static string OutputWriteFailed(int passed) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Failure writing output to destination, passed {passed} returned 0");

    /// <summary>
    /// The exit 8 message for a CONNACK whose remaining length is not two.
    /// </summary>
    /// <param name="remainingLength">The remaining length the CONNACK carried.</param>
    /// <returns>The message to report.</returns>
    internal static string ConnackLengthUnexpected(int remainingLength) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"CONNACK expected Remaining Length 2, got {remainingLength}");

    /// <summary>
    /// The exit 8 message for a CONNACK that refused the connection.
    /// </summary>
    /// <param name="flags">The CONNACK's first byte, its acknowledge flags.</param>
    /// <param name="returnCode">The CONNACK's second byte, its return code.</param>
    /// <returns>The message to report, each byte as two lowercase hex digits.</returns>
    internal static string ConnackRefused(byte flags, byte returnCode) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Expected 0000 but got {flags:x2}{returnCode:x2}");

    /// <summary>
    /// The exit 8 message for a SUBACK whose remaining length is not three.
    /// </summary>
    /// <param name="remainingLength">The remaining length the SUBACK carried.</param>
    /// <returns>The message to report.</returns>
    internal static string SubackLengthUnexpected(int remainingLength) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"SUBACK expected Remaining Length 3, got {remainingLength}");

    /// <summary>
    /// The exit 8 message for a DISCONNECT or PINGRESP that carries a body or sets its
    /// reserved flag bits, both of which MQTT 3.1.1 forbids.
    /// </summary>
    /// <param name="packetName">The packet's name, <c>DISCONNECT</c> or <c>PINGRESP</c>.</param>
    /// <param name="header">The packet's fixed header.</param>
    /// <returns>The message to report.</returns>
    internal static string MalformedControlPacket(string packetName, MqttFixedHeader header) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Broker sent malformed {packetName} (remaining_length={header.RemainingLength}, header byte=0x{header.FirstByte:x2})");
}
