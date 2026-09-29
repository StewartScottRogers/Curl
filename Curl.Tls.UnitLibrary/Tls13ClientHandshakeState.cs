namespace Curl.Tls;

/// <summary>Where <see cref="Tls13ClientHandshake" /> is: the message it waits for next (RFC 8446 appendix A.1).</summary>
internal enum Tls13ClientHandshakeState
{
    /// <summary>Not started: the ClientHello has not been built.</summary>
    Start,

    /// <summary>The ClientHello is sent; a ServerHello or HelloRetryRequest comes next.</summary>
    WaitServerHello,

    /// <summary>Handshake keys are in place; EncryptedExtensions comes next.</summary>
    WaitEncryptedExtensions,

    /// <summary>A CertificateRequest or the server's Certificate comes next.</summary>
    WaitCertificateOrRequest,

    /// <summary>The server asked for a certificate; its own Certificate comes next.</summary>
    WaitCertificate,

    /// <summary>The server's CertificateVerify comes next.</summary>
    WaitCertificateVerify,

    /// <summary>The server's Finished comes next.</summary>
    WaitFinished,

    /// <summary>The handshake is complete; only post-handshake messages arrive.</summary>
    Connected,

    /// <summary>The handshake failed; it takes no more bytes.</summary>
    Failed,
}
