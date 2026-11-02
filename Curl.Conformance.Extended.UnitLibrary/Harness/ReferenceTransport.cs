namespace Curl.Conformance.Extended.Harness;

/// <summary>How a <see cref="ReferenceRoute" /> carries curl's bytes to its reference server.</summary>
public enum ReferenceTransport
{
    /// <summary>A plaintext TCP connection.</summary>
    Tcp,

    /// <summary>A TCP connection carrying TLS; the exchange records the plaintext behind it.</summary>
    Tls,

    /// <summary>A UDP channel.</summary>
    Udp,
}
