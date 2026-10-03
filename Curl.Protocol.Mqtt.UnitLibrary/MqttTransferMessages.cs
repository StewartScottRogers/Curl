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
    /// The exit 55 message curl falls back to for any other failed send,
    /// <c>curl_easy_strerror(CURLE_SEND_ERROR)</c>.
    /// </summary>
    internal const string SendFailed = "Failed sending data to the peer";

    /// <summary>
    /// The <c>-v</c> line curl's <c>mqtt_do</c> writes through <c>failf</c> after a CONNECT
    /// that could not be sent: <c>Error %d sending MQTT CONNECT request</c> with exit 55.
    /// </summary>
    internal const string ConnectNotSent = "Error 55 sending MQTT CONNECT request";

    /// <summary>
    /// The exit 18 message curl falls back to for a PUBLISH cut short by the peer.
    /// </summary>
    internal const string PartialFile = "Transferred a partial file";

    /// <summary>
    /// The exit 8 message curl falls back to for a packet it does not expect.
    /// </summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>
    /// The exit 100 message curl falls back to for a PUBLISH over its size limit.
    /// </summary>
    internal const string TooLarge = "A value or data field grew larger than allowed";

    /// <summary>
    /// The exit 63 message for a PUBLISH whose remaining length is over <c>--max-filesize</c>.
    /// </summary>
    internal const string MaximumFileSizeExceeded = "Maximum file size exceeded";

    /// <summary>The <c>-v</c> line for a DISCONNECT received.</summary>
    internal const string GotDisconnect = "Got DISCONNECT";

    /// <summary>The <c>-v</c> line for a PINGRESP received.</summary>
    internal const string ReceivedPingResponse = "Received ping response.";

    /// <summary>The <c>-v</c> line for a PINGREQ sent after the connection sat idle.</summary>
    internal const string SentPingRequest = "mqtt_ping: sent ping request.";

    /// <summary>The <c>-v</c> line for a peer that closed inside a PUBLISH body.</summary>
    internal const string ServerDisconnected = "server disconnected";

    /// <summary>The <c>-v</c> line for a packet curl's state machine has no state for.</summary>
    internal const string StateNotHandled = "State not handled yet";

    /// <summary>
    /// Whether <paramref name="message" /> is one of the texts curl prints for an exit code
    /// <c>lib/mqtt.c</c> returns without calling <c>failf</c>, and so without a <c>-v</c> line.
    /// </summary>
    /// <param name="message">A failure's message.</param>
    /// <returns><see langword="true" /> for a <c>curl_easy_strerror</c> text.</returns>
    internal static bool IsStrerrorText(string message) =>
        message is ReceiveFailed or SendFailed or PartialFile or WeirdServerReply or TooLarge;

    /// <summary>The <c>-v</c> line naming the client identifier, written before the CONNECT.</summary>
    /// <param name="clientIdentifier">The client identifier the CONNECT carries.</param>
    /// <returns>The line, such as <c>Using client id 'curlPBadK4E3'</c>.</returns>
    internal static string UsingClientId(string clientIdentifier) => $"Using client id '{clientIdentifier}'";

    /// <summary>
    /// The <c>-v</c> line curl's <c>mqtt_doing</c> writes each time it runs, naming the state
    /// it is in by <c>enum mqttstate</c>'s number: 0 awaiting a packet, 2 the CONNACK, 3 the
    /// SUBACK, 5 a PUBLISH, 6 the rest of one, 7 a state it has none for.
    /// </summary>
    /// <param name="state">The state's number.</param>
    /// <returns>The line, such as <c>mqtt_doing: state [0]</c>.</returns>
    internal static string DoingState(int state) =>
        string.Create(CultureInfo.InvariantCulture, $"mqtt_doing: state [{state}]");

    /// <summary>The <c>-v</c> line written before a PUBLISH body is read.</summary>
    /// <param name="remainingLength">The PUBLISH's remaining length.</param>
    /// <returns>The line, such as <c>Remaining length: 8 bytes</c>.</returns>
    internal static string RemainingLength(int remainingLength) =>
        string.Create(CultureInfo.InvariantCulture, $"Remaining length: {remainingLength} bytes");

    /// <summary>The <c>-v</c> line that ends the connection after anything but exit 23.</summary>
    /// <param name="connectionNumber">The connection's number.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDownConnection(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>The <c>-v</c> line that ends the connection after an output write failure.</summary>
    /// <param name="connectionNumber">The connection's number.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string ClosingConnection(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");

    /// <summary>
    /// The exit 23 message for an output that stopped accepting bytes.
    /// </summary>
    /// <param name="passed">The number of bytes offered to the output in the failed write.</param>
    /// <param name="returned">
    /// How many of those bytes the output accepted before failing: 0 unless it reported
    /// more.
    /// </param>
    /// <returns>The message to report.</returns>
    internal static string OutputWriteFailed(int passed, int returned) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Failure writing output to destination, passed {passed} returned {returned}");

    /// <summary>
    /// The exit 8 message for a user name over 65535 bytes.
    /// </summary>
    /// <param name="length">The user name's length in bytes.</param>
    /// <returns>The message to report.</returns>
    internal static string UserNameTooLong(int length) =>
        string.Create(CultureInfo.InvariantCulture, $"Username too long: [{length}]");

    /// <summary>
    /// The exit 8 message for a password over 65535 bytes.
    /// </summary>
    /// <param name="length">The password's length in bytes.</param>
    /// <returns>The message to report.</returns>
    internal static string PasswordTooLong(int length) =>
        string.Create(CultureInfo.InvariantCulture, $"Password too long: [{length}]");

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
