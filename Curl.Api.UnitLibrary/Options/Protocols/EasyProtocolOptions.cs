namespace Curl.Api.Options.Protocols;

/// <summary>
/// The protocol-specific options of one easy handle: the <c>curl_easy_setopt</c> options of
/// the protocol group (ADR-0506), one nested record per protocol that has options of its own,
/// each defaulting to libcurl 8.22.0's default. IMAP, POP3, LDAP, DICT, GOPHER, MQTT and SMB
/// take no options of their own beyond the shared groups (network, authentication, TLS and
/// the rest), so they have no record here.
/// </summary>
public sealed record EasyProtocolOptions
{
    /// <summary>FTP's and FTPS's options. Default: every one at libcurl's default.</summary>
    public EasyFtpOptions Ftp { get; init; } = new();

    /// <summary>SCP's and SFTP's options. Default: every one at libcurl's default.</summary>
    public EasySshOptions Ssh { get; init; } = new();

    /// <summary>SMTP's options. Default: every one at libcurl's default.</summary>
    public EasySmtpOptions Smtp { get; init; } = new();

    /// <summary>TFTP's options. Default: every one at libcurl's default.</summary>
    public EasyTftpOptions Tftp { get; init; } = new();

    /// <summary>TELNET's options. Default: every one at libcurl's default.</summary>
    public EasyTelnetOptions Telnet { get; init; } = new();

    /// <summary>RTSP's options. Default: every one at libcurl's default.</summary>
    public EasyRtspOptions Rtsp { get; init; } = new();

    /// <summary>WebSocket's (<c>ws://</c> and <c>wss://</c>) options. Default: every one at libcurl's default.</summary>
    public EasyWebSocketOptions WebSocket { get; init; } = new();
}
