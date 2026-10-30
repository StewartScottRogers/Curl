namespace Curl.Extension.WireGuard.IpStack;

/// <summary>The control bits of a TCP header (RFC 9293 section 3.1) that the client sends or reads.</summary>
[Flags]
public enum TcpFlags : byte
{
    /// <summary>No control bit.</summary>
    None = 0,

    /// <summary>FIN: the sender has no more data.</summary>
    Fin = 0x01,

    /// <summary>SYN: synchronise sequence numbers.</summary>
    Syn = 0x02,

    /// <summary>RST: reset the connection.</summary>
    Rst = 0x04,

    /// <summary>PSH: push the data to the application.</summary>
    Psh = 0x08,

    /// <summary>ACK: the acknowledgement number is significant.</summary>
    Ack = 0x10,
}
