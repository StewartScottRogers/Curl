namespace Curl.Tls;

/// <summary>
/// The handshake message types (RFC 8446 section 4, with the TLS 1.2 types of RFC 5246
/// and RFC 6066 the client also meets). A byte that is none of these is an unknown
/// message type, which <see cref="HandshakeMessageReader" /> rejects with
/// <see cref="TlsAlertDescription.UnexpectedMessage" />.
/// </summary>
public enum HandshakeType : byte
{
    /// <summary><c>hello_request</c> (TLS 1.2 and below).</summary>
    HelloRequest = 0,

    /// <summary><c>client_hello</c>.</summary>
    ClientHello = 1,

    /// <summary><c>server_hello</c>, which also carries a HelloRetryRequest.</summary>
    ServerHello = 2,

    /// <summary><c>new_session_ticket</c>.</summary>
    NewSessionTicket = 4,

    /// <summary><c>end_of_early_data</c>.</summary>
    EndOfEarlyData = 5,

    /// <summary><c>encrypted_extensions</c>.</summary>
    EncryptedExtensions = 8,

    /// <summary><c>certificate</c>.</summary>
    Certificate = 11,

    /// <summary><c>server_key_exchange</c> (TLS 1.2 and below).</summary>
    ServerKeyExchange = 12,

    /// <summary><c>certificate_request</c>.</summary>
    CertificateRequest = 13,

    /// <summary><c>server_hello_done</c> (TLS 1.2 and below).</summary>
    ServerHelloDone = 14,

    /// <summary><c>certificate_verify</c>.</summary>
    CertificateVerify = 15,

    /// <summary><c>client_key_exchange</c> (TLS 1.2 and below).</summary>
    ClientKeyExchange = 16,

    /// <summary><c>finished</c>.</summary>
    Finished = 20,

    /// <summary><c>certificate_status</c> (RFC 6066, TLS 1.2 and below).</summary>
    CertificateStatus = 22,

    /// <summary><c>key_update</c>.</summary>
    KeyUpdate = 24,

    /// <summary><c>compressed_certificate</c> (RFC 8879).</summary>
    CompressedCertificate = 25,

    /// <summary><c>message_hash</c>, the synthetic message a HelloRetryRequest puts in the transcript.</summary>
    MessageHash = 254,
}
