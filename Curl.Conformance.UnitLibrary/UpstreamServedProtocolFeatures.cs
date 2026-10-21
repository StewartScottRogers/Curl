namespace Curl.Conformance;

/// <summary>
/// The protocol features Curl reports on every platform: the protocols the harness can serve or emulate, and
/// <c>ldap</c> and <c>ldaps</c>, which Curl's <c>curl -V</c> lists on every platform though the harness has no
/// LDAP server, so a case such as 445 that only names the feature runs (BL-2856).
/// </summary>
internal static class UpstreamServedProtocolFeatures
{
    public static readonly string[] Names =
    [
        "dict",
        "file",
        "ftp",
        "gopher",
        "gophers",
        "http",
        "https",
        "imap",
        "ipfs",
        "ldap",
        "ldaps",
        "mqtt",
        "mqtts",
        "pop3",
        "rtsp",
        "scp",
        "sftp",
        "smtp",
        "telnet",
        "tftp",
        "ws",
        "wss",
    ];
}
