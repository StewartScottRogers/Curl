namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// How the server's host key compares with the known-hosts file, with libssh2 1.11.1's
/// <c>LIBSSH2_KNOWNHOST_CHECK_*</c> values, which curl prints under <c>-v</c> as
/// <c>SSH: host check &lt;n&gt;</c>.
/// </summary>
internal enum KnownHostsCheck
{
    /// <summary>An entry names the host and holds this very key.</summary>
    Match = 0,

    /// <summary>An entry names the host with a key of this type, but not this key.</summary>
    Mismatch = 1,

    /// <summary>No entry names the host with a key of this type.</summary>
    NotFound = 2,
}
