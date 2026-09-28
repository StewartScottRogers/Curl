namespace Curl.Cli;

/// <summary>
/// The help categories curl 8.21.0 files its options under (<c>CURLHELP_*</c> in its <c>tool_help.h</c>),
/// with the same bit values. <see cref="Important"/> is the default <c>-h</c> page and is not listed by
/// <c>--help category</c>.
/// </summary>
[Flags]
public enum CurlHelpCategories
{
    /// <summary>No category.</summary>
    None = 0,

    /// <summary><c>auth</c>: Authentication methods.</summary>
    Auth = 1 << 0,

    /// <summary><c>connection</c>: Manage connections.</summary>
    Connection = 1 << 1,

    /// <summary><c>curl</c>: The command line tool itself.</summary>
    Curl = 1 << 2,

    /// <summary><c>deprecated</c>: Legacy.</summary>
    Deprecated = 1 << 3,

    /// <summary><c>dns</c>: Names and resolving.</summary>
    Dns = 1 << 4,

    /// <summary><c>file</c>: FILE protocol.</summary>
    File = 1 << 5,

    /// <summary><c>ftp</c>: FTP protocol.</summary>
    Ftp = 1 << 6,

    /// <summary><c>global</c>: Global options.</summary>
    Global = 1 << 7,

    /// <summary><c>http</c>: HTTP and HTTPS protocol.</summary>
    Http = 1 << 8,

    /// <summary><c>imap</c>: IMAP protocol.</summary>
    Imap = 1 << 9,

    /// <summary>The options <c>-h</c> with no subject lists.</summary>
    Important = 1 << 10,

    /// <summary><c>ldap</c>: LDAP protocol.</summary>
    Ldap = 1 << 11,

    /// <summary><c>output</c>: File system output.</summary>
    Output = 1 << 12,

    /// <summary><c>pop3</c>: POP3 protocol.</summary>
    Pop3 = 1 << 13,

    /// <summary><c>post</c>: HTTP POST specific.</summary>
    Post = 1 << 14,

    /// <summary><c>proxy</c>: Options for proxies.</summary>
    Proxy = 1 << 15,

    /// <summary><c>scp</c>: SCP protocol.</summary>
    Scp = 1 << 16,

    /// <summary><c>sftp</c>: SFTP protocol.</summary>
    Sftp = 1 << 17,

    /// <summary><c>smtp</c>: SMTP protocol.</summary>
    Smtp = 1 << 18,

    /// <summary><c>ssh</c>: SSH protocol.</summary>
    Ssh = 1 << 19,

    /// <summary><c>telnet</c>: TELNET protocol.</summary>
    Telnet = 1 << 20,

    /// <summary><c>tftp</c>: TFTP protocol.</summary>
    Tftp = 1 << 21,

    /// <summary><c>timeout</c>: Timeouts and delays.</summary>
    Timeout = 1 << 22,

    /// <summary><c>tls</c>: TLS/SSL related.</summary>
    Tls = 1 << 23,

    /// <summary><c>upload</c>: Upload, sending data.</summary>
    Upload = 1 << 24,

    /// <summary><c>verbose</c>: Tracing, logging etc.</summary>
    Verbose = 1 << 25,

    /// <summary>Every category, as <c>--help all</c> asks for (<c>CURLHELP_ALL</c>).</summary>
    All = 0xfffffff,
}
