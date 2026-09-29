namespace Curl.Tls;

/// <summary>The content type byte that opens every TLS record (RFC 5246 section 6.2.1).</summary>
public enum TlsContentType : byte
{
    /// <summary><c>change_cipher_spec</c>: the sender switches to the pending write state.</summary>
    ChangeCipherSpec = 20,

    /// <summary><c>alert</c>: a warning or fatal alert.</summary>
    Alert = 21,

    /// <summary><c>handshake</c>: handshake messages.</summary>
    Handshake = 22,

    /// <summary><c>application_data</c>: the application's bytes.</summary>
    ApplicationData = 23,
}
