namespace Curl.Tls;

/// <summary>
/// The keys a handshake message travels under (RFC 9001 section 4.1.4): QUIC carries each
/// level in its own packet number space, the TLS record layer switches keys between them.
/// </summary>
public enum TlsEncryptionLevel
{
    /// <summary>No protection over TCP; QUIC Initial keys. ClientHello and ServerHello travel here.</summary>
    Initial,

    /// <summary>0-RTT keys from the client's early traffic secret.</summary>
    EarlyData,

    /// <summary>Handshake traffic keys: EncryptedExtensions through both Finished messages.</summary>
    Handshake,

    /// <summary>Application traffic keys: application data and post-handshake messages.</summary>
    Application,
}
