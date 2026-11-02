using System.Collections.Frozen;

namespace Curl.Mcp;

/// <summary>
/// How every curl command-line option, by long name, uses a local path (ADR-0548, ADR-0559):
/// the list the MCP server's argument check confines to <c>CURL_MCP_ROOT</c>.
/// </summary>
/// <remarks>
/// Derived from <c>Curl.Cli</c>'s <c>CommandLineOptionTable</c> and the manpage of curl 8.22.0
/// (<see href="https://curl.se/docs/manpage.html" />), the curl the gap office targets, consulted
/// on 2026-10-15. A drift test in <c>Curl.Mcp.UnitTests</c> fails, naming the option, when the
/// option table gains a row this list does not classify. An option this list does not name is
/// unknown, and the server refuses it: <see cref="TryFind(string, out McpOptionPathUse)" />
/// answers <see langword="false" /> for it.
/// </remarks>
public static class McpOptionPathTable
{
    private static readonly string[] ReadsFileOptions =
    [
        "cacert", "capath", "cert", "config", "crlfile", "etag-compare", "httpsig-key", "key",
        "knownhosts", "netrc", "netrc-file", "netrc-optional", "proxy-cacert", "proxy-capath",
        "proxy-cert", "proxy-crlfile", "proxy-key", "pubkey", "upload-file",
    ];

    private static readonly string[] WritesFileOptions =
    [
        "cookie-jar", "dump-header", "etag-save", "libcurl", "log-file", "output", "remote-header-name",
        "remote-name", "remote-name-all", "stderr", "trace", "trace-ascii",
    ];

    private static readonly string[] WritesDirectoryOptions = ["output-dir"];

    private static readonly string[] ReadsOrWritesFileOptions = ["alt-svc", "hsts", "ssl-sessions"];

    private static readonly string[] ReadsFileOrLiteralOptions =
    [
        "cookie", "pinnedpubkey", "proxy-pinnedpubkey", "time-cond",
    ];

    private static readonly string[] ValueEmbeddedOptions =
    [
        "data", "data-ascii", "data-binary", "data-urlencode", "form", "header", "json", "preproxy",
        "proxy", "proxy-header", "socks4", "socks4a", "socks5", "socks5-hostname", "url", "url-query",
        "variable", "write-out",
    ];

    private static readonly string[] UnixSocketPathOptions = ["abstract-unix-socket", "unix-socket"];

    private static readonly string[] NoPathOptions =
    [
        "ai-help", "alpn", "anyauth", "append", "aws-sigv4", "basic", "buffer", "ca-native",
        "cert-status", "cert-type", "ciphers", "clobber", "compressed", "compressed-ssh",
        "connect-timeout", "connect-to", "continue-at", "create-dirs", "create-file-mode", "crlf",
        "curves", "data-raw", "delegation", "digest", "disable", "disable-eprt", "disable-epsv",
        "disallow-username-in-url", "dns-interface", "dns-ipv4-addr", "dns-ipv6-addr", "dns-servers",
        "doh-cert-status", "doh-insecure", "doh-url", "dump-ca-embed", "ech", "egd-file", "engine",
        "eprt", "epsv", "expect100-timeout", "extended-help", "fail", "fail-early", "fail-with-body", "false-start",
        "follow", "form-escape", "form-string", "ftp-account", "ftp-alternative-to-user",
        "ftp-create-dirs", "ftp-method", "ftp-pasv", "ftp-port", "ftp-pret", "ftp-skip-pasv-ip",
        "ftp-ssl", "ftp-ssl-ccc", "ftp-ssl-ccc-mode", "ftp-ssl-control", "ftp-ssl-reqd", "get",
        "globoff", "happy-eyeballs-timeout-ms", "haproxy-clientip", "haproxy-protocol", "head",
        "help", "hostpubmd5", "hostpubsha256", "http0.9", "http1.0", "http1.1", "http2",
        "http2-prior-knowledge", "http3", "http3-only", "httpsig-algo", "httpsig-headers",
        "httpsig-keyid", "ignore-content-length", "include", "insecure", "interface", "ip-tos",
        "ipfs-gateway", "ipv4", "ipv6", "junk-session-cookies", "keepalive", "keepalive-cnt",
        "keepalive-time", "key-type", "krb", "krb4", "limit-rate", "list-only", "local-port",
        "location", "location-trusted", "log-level", "login-options", "mail-auth", "mail-from",
        "mail-rcpt", "mail-rcpt-allowfails", "manual", "max-filesize", "max-redirs", "max-time",
        "metalink", "mptcp", "negotiate", "next", "noproxy", "npn", "ntlm", "ntlm-wb",
        "oauth2-bearer", "out-null", "parallel", "parallel-immediate", "parallel-max",
        "parallel-max-host", "pass", "path-as-is", "post301", "post302", "post303", "progress-bar",
        "progress-meter", "proto", "proto-default", "proto-redir", "proxy-anyauth", "proxy-basic",
        "proxy-ca-native", "proxy-cert-type", "proxy-ciphers", "proxy-digest", "proxy-http2",
        "proxy-http3", "proxy-insecure", "proxy-key-type", "proxy-negotiate", "proxy-ntlm",
        "proxy-pass", "proxy-service-name", "proxy-ssl-allow-beast", "proxy-ssl-auto-client-cert",
        "proxy-tls13-ciphers", "proxy-tlsauthtype", "proxy-tlspassword", "proxy-tlsuser",
        "proxy-tlsv1", "proxy-user", "proxy1.0", "proxytunnel", "quote", "random-file", "range",
        "rate", "raw", "referer", "remote-time", "remove-on-error", "request", "request-target",
        "resolve", "retry", "retry-all-errors", "retry-connrefused", "retry-delay", "retry-max-time",
        "sasl-authzid", "sasl-ir", "service-name", "sessionid", "show-error", "show-headers",
        "sigalgs", "silent", "skip-existing", "socks5-basic", "socks5-gssapi", "socks5-gssapi-nec",
        "socks5-gssapi-service", "speed-limit", "speed-time", "ssl", "ssl-allow-beast",
        "ssl-auto-client-cert", "ssl-no-revoke", "ssl-reqd", "ssl-revoke-best-effort", "sslv2",
        "sslv3", "styled-output", "suppress-connect-headers", "tcp-fastopen", "tcp-nodelay",
        "telnet-option", "tftp-blksize", "tftp-no-options", "tls-earlydata", "tls-max",
        "tls13-ciphers", "tlsauthtype", "tlspassword", "tlsuser", "tlsv1", "tlsv1.0", "tlsv1.1",
        "tlsv1.2", "tlsv1.3", "tr-encoding", "trace-config", "trace-ids", "trace-time",
        "upload-flags", "use-ascii", "user", "user-agent", "verbose", "version", "vlan-priority",
        "xattr",
    ];

    /// <summary>Gets every classified option long name (without the leading <c>--</c>) and its path use.</summary>
    public static IReadOnlyDictionary<string, McpOptionPathUse> Classifications { get; } = BuildClassifications();

    /// <summary>Finds how the option named <paramref name="longName" /> uses a local path.</summary>
    /// <param name="longName">The option's long name without the leading <c>--</c>, as curl spells it (case-sensitive).</param>
    /// <param name="use">The option's path use, or <see cref="McpOptionPathUse.None" /> when the option is unknown.</param>
    /// <returns><see langword="true" /> when the option is classified; <see langword="false" /> when it is unknown, which the server refuses.</returns>
    public static bool TryFind(string longName, out McpOptionPathUse use)
    {
        ArgumentNullException.ThrowIfNull(longName);
        return Classifications.TryGetValue(longName, out use);
    }

    /// <summary>Merges the name lists above into one case-sensitive lookup; a name listed twice throws, so a duplicate classification is caught at start-up.</summary>
    private static FrozenDictionary<string, McpOptionPathUse> BuildClassifications() =>
        new (string[] Names, McpOptionPathUse Use)[]
        {
            (NoPathOptions, McpOptionPathUse.None),
            (ReadsFileOptions, McpOptionPathUse.ReadsFile),
            (WritesFileOptions, McpOptionPathUse.WritesFile),
            (WritesDirectoryOptions, McpOptionPathUse.WritesDirectory),
            (ReadsOrWritesFileOptions, McpOptionPathUse.ReadsOrWritesFile),
            (ReadsFileOrLiteralOptions, McpOptionPathUse.ReadsFileOrLiteral),
            (ValueEmbeddedOptions, McpOptionPathUse.ValueEmbedded),
            (UnixSocketPathOptions, McpOptionPathUse.UnixSocketPath),
        }
        .SelectMany(group => group.Names.Select(name => KeyValuePair.Create(name, group.Use)))
        .ToDictionary(StringComparer.Ordinal)
        .ToFrozenDictionary(StringComparer.Ordinal);
}
