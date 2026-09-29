namespace Curl.Quic;

/// <summary>
/// What removing packet protection came to. A packet that cannot be unprotected is
/// dropped, never fatal (RFC 9001 section 5.3 and section 9.5): the connection carries on
/// with the next packet.
/// </summary>
public enum QuicUnprotectStatus
{
    /// <summary>Header and payload protection were removed; the packet is authentic.</summary>
    Unprotected,

    /// <summary>The packet is too short for the header protection sample, so it was dropped.</summary>
    DroppedTooShortForSample,

    /// <summary>The AEAD tag did not match: the packet was forged, damaged, or protected with other keys, so it was dropped.</summary>
    DroppedAuthenticationFailed,
}
