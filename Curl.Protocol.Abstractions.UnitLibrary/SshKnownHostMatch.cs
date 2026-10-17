namespace Curl.Protocol.Abstractions;

/// <summary>
/// How the server's SSH host key compares with the known-hosts file, numbered as libssh2's
/// <c>LIBSSH2_KNOWNHOST_CHECK_*</c> and libcurl's <c>enum curl_khmatch</c>, which curl casts one to the
/// other (ADR-0623).
/// </summary>
public enum SshKnownHostMatch
{
    /// <summary>An entry names the host and holds this very key.</summary>
    Match = 0,

    /// <summary>An entry names the host with a key of this type, but not this key.</summary>
    Mismatch = 1,

    /// <summary>No entry names the host with a key of this type.</summary>
    Missing = 2,
}
