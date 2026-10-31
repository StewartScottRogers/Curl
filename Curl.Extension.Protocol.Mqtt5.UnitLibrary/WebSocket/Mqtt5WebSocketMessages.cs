namespace Curl.Extension.Protocol.Mqtt5.WebSocket;

/// <summary>The error messages of <c>mqtt5+ws://</c>'s WebSocket opening handshake and frames (ADR-1054).</summary>
internal static class Mqtt5WebSocketMessages
{
    internal const string EmptyReply = "Empty reply from server";

    internal const string ReplyHeadTooLong = "MQTT 5 over WebSocket: the upgrade reply head is too long";

    internal const string NoStatusLine = "MQTT 5 over WebSocket: the upgrade reply has no status line";

    internal const string AcceptMismatch = "MQTT 5 over WebSocket: the server's Sec-WebSocket-Accept does not match the key sent";

    internal const string SubprotocolNotSelected = "MQTT 5 over WebSocket: the server did not select the mqtt subprotocol";

    internal const string TextFrame = "MQTT 5 over WebSocket: the server sent a text frame";

    internal static string RefusedUpgrade(string status) => $"Refused WebSocket upgrade: {status}";
}
