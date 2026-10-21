namespace Curl.Conformance;

/// <summary>The protocol features Curl reports on every platform: the protocols the harness can serve or emulate.</summary>
internal static class UpstreamServedProtocolFeatures
{
    public static readonly string[] Names =
    [
        "dict", "file", "ftp", "gopher", "gophers", "http", "https", "imap", "ipfs", "mqtt", "mqtts", "pop3", "scp", "sftp", "smtp", "telnet", "tftp", "ws", "wss",
    ];
}
