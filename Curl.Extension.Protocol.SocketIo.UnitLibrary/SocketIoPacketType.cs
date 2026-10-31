namespace Curl.Extension.Protocol.SocketIo;

/// <summary>The Socket.IO protocol v5 packet types (section "Packet format").</summary>
internal enum SocketIoPacketType
{
    /// <summary>Connects to a namespace, or the server's reply that it did.</summary>
    Connect = 0,

    /// <summary>Leaves a namespace.</summary>
    Disconnect = 1,

    /// <summary>An event: a JSON array whose first element is the event name.</summary>
    Event = 2,

    /// <summary>The acknowledgement of an event that asked for one.</summary>
    Ack = 3,

    /// <summary>The server refused the connection to a namespace.</summary>
    ConnectError = 4,

    /// <summary>An event with binary attachments.</summary>
    BinaryEvent = 5,

    /// <summary>An acknowledgement with binary attachments.</summary>
    BinaryAck = 6,
}
