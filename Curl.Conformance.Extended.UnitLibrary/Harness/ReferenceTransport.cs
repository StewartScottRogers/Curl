namespace Curl.Conformance.Extended.Harness;

/// <summary>How a <see cref="ReferenceRoute" /> carries curl's bytes to its reference server.</summary>
public enum ReferenceTransport
{
    /// <summary>A plaintext TCP connection.</summary>
    Tcp,

    /// <summary>A UDP channel.</summary>
    Udp,
}
