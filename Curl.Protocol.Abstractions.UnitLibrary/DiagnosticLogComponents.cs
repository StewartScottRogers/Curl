namespace Curl.Protocol.Abstractions;

/// <summary>
/// The fixed component names a diagnostic log line carries, one per area, so no caller
/// spells one by hand (ADR-0222, decision 6). A new area adds its name to the ADR first.
/// </summary>
public static class DiagnosticLogComponents
{
    /// <summary>
    /// <c>cli</c>: The command-line parser.
    /// </summary>
    public const string Cli = "cli";

    /// <summary>
    /// <c>runner</c>: The transfer runner.
    /// </summary>
    public const string Runner = "runner";

    /// <summary>
    /// <c>dns</c>: Name resolution.
    /// </summary>
    public const string Dns = "dns";

    /// <summary>
    /// <c>connect</c>: Opening connections.
    /// </summary>
    public const string Connect = "connect";

    /// <summary>
    /// <c>proxy</c>: Proxy tunnels and handshakes.
    /// </summary>
    public const string Proxy = "proxy";

    /// <summary>
    /// <c>tls</c>: TLS handshakes.
    /// </summary>
    public const string Tls = "tls";

    /// <summary>
    /// <c>quic</c>: QUIC connections.
    /// </summary>
    public const string Quic = "quic";

    /// <summary>
    /// <c>http</c>: HTTP/1.x transfers.
    /// </summary>
    public const string Http = "http";

    /// <summary>
    /// <c>http2</c>: HTTP/2 transfers.
    /// </summary>
    public const string Http2 = "http2";

    /// <summary>
    /// <c>http3</c>: HTTP/3 transfers.
    /// </summary>
    public const string Http3 = "http3";

    /// <summary>
    /// <c>auth</c>: Authentication.
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// <c>retry</c>: Retries.
    /// </summary>
    public const string Retry = "retry";

    /// <summary>
    /// <c>redirect</c>: Following redirects.
    /// </summary>
    public const string Redirect = "redirect";

    /// <summary>
    /// <c>hsts</c>: The HSTS cache.
    /// </summary>
    public const string Hsts = "hsts";

    /// <summary>
    /// <c>altsvc</c>: The Alt-Svc cache.
    /// </summary>
    public const string AltSvc = "altsvc";

    /// <summary>
    /// <c>ftp</c>: FTP and FTPS transfers.
    /// </summary>
    public const string Ftp = "ftp";

    /// <summary>
    /// <c>tftp</c>: TFTP transfers.
    /// </summary>
    public const string Tftp = "tftp";

    /// <summary>
    /// <c>ssh</c>: SCP and SFTP transfers.
    /// </summary>
    public const string Ssh = "ssh";

    /// <summary>
    /// <c>smtp</c>: SMTP transfers.
    /// </summary>
    public const string Smtp = "smtp";

    /// <summary>
    /// <c>imap</c>: IMAP transfers.
    /// </summary>
    public const string Imap = "imap";

    /// <summary>
    /// <c>pop3</c>: POP3 transfers.
    /// </summary>
    public const string Pop3 = "pop3";

    /// <summary>
    /// <c>dict</c>: DICT transfers.
    /// </summary>
    public const string Dict = "dict";

    /// <summary>
    /// <c>gopher</c>: Gopher transfers.
    /// </summary>
    public const string Gopher = "gopher";

    /// <summary>
    /// <c>telnet</c>: TELNET transfers.
    /// </summary>
    public const string Telnet = "telnet";

    /// <summary>
    /// <c>mqtt</c>: MQTT transfers.
    /// </summary>
    public const string Mqtt = "mqtt";

    /// <summary>
    /// <c>file</c>: file:// transfers.
    /// </summary>
    public const string File = "file";

    /// <summary>
    /// <c>smb</c>: SMB transfers.
    /// </summary>
    public const string Smb = "smb";

    /// <summary>
    /// <c>ldap</c>: LDAP transfers.
    /// </summary>
    public const string Ldap = "ldap";

    /// <summary>
    /// <c>rtsp</c>: RTSP transfers.
    /// </summary>
    public const string Rtsp = "rtsp";

    /// <summary>
    /// <c>ws</c>: WebSocket transfers.
    /// </summary>
    public const string Ws = "ws";
}
