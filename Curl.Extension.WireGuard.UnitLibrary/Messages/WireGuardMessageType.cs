namespace Curl.Extension.WireGuard.Messages;

/// <summary>The first byte of every WireGuard message (whitepaper sections 5.4.2 to 5.4.7).</summary>
public enum WireGuardMessageType : byte
{
    /// <summary>Handshake initiation, 148 bytes (section 5.4.2).</summary>
    HandshakeInitiation = 1,

    /// <summary>Handshake response, 92 bytes (section 5.4.3).</summary>
    HandshakeResponse = 2,

    /// <summary>Cookie reply, 64 bytes (section 5.4.7).</summary>
    CookieReply = 3,

    /// <summary>Transport data, 32 bytes or more (section 5.4.6).</summary>
    TransportData = 4,
}
