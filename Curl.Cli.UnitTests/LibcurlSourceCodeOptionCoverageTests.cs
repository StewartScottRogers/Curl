namespace Curl.Cli;

/// <summary>
/// Keeps <see cref="LibcurlSourceCode" /> in step with <see cref="CommandLineOptionTable" />: every option the
/// table parses is listed here as one whose <c>--libcurl</c> lines the generator writes, one curl 8.21.0
/// (Schannel) writes nothing for, or one still waiting for BL-1106, so an option added later cannot be
/// forgotten (BL-654).
/// </summary>
[TestClass]
public sealed class LibcurlSourceCodeOptionCoverageTests
{
    /// <summary>Options whose lines are written, each pinned by <c>LibcurlSourceCodeOptionTests</c> or <c>LibcurlSourceCodeProxyTlsAndAuthenticationTests</c>.</summary>
    private static readonly string[] Written =
    [
        "url", "silent", "progress-meter", "data", "data-ascii", "data-binary", "data-raw", "data-urlencode", "json",
        "form", "form-string", "user", "location", "referer", "user-agent", "cookie", "cookie-jar", "compressed",
        "head", "remote-time", "fail", "header", "request", "max-time", "connect-timeout", "resolve", "connect-to",
        "ipv4", "ipv6", "get", "insecure", "cacert", "cert", "key", "cert-type", "key-type", "pass", "ciphers",
        "pinnedpubkey", "ssl-no-revoke", "ssl-revoke-best-effort", "ssl-allow-beast", "ca-native",
        "ssl-auto-client-cert", "tls-earlydata", "alpn", "tlsv1", "tlsv1.0", "tlsv1.1", "tlsv1.2", "tlsv1.3",
        "tls-max", "proxy", "socks4", "socks4a", "socks5", "socks5-hostname", "proxy1.0", "preproxy", "socks5-basic",
        "socks5-gssapi", "socks5-gssapi-nec", "haproxy-protocol", "haproxy-clientip", "suppress-connect-headers",
        "proxy-user", "proxy-basic", "proxy-digest", "proxy-ntlm", "proxy-negotiate", "proxy-anyauth",
        "proxy-service-name", "noproxy", "proxytunnel", "proxy-header", "proxy-insecure", "proxy-cacert",
        "proxy-cert", "proxy-key", "proxy-cert-type", "proxy-key-type", "proxy-pass", "proxy-ciphers",
        "proxy-pinnedpubkey", "proxy-ca-native", "proxy-ssl-auto-client-cert", "proxy-ssl-allow-beast",
        "proxy-tlsv1", "basic", "digest", "ntlm", "negotiate", "anyauth", "delegation", "service-name",
        "oauth2-bearer", "aws-sigv4", "netrc", "netrc-optional", "netrc-file", "login-options", "sasl-authzid",
        "sasl-ir", "location-trusted",
    ];

    /// <summary>Options curl 8.21.0's Schannel build writes no line for, measured (BL-653 and BL-654 Notes).</summary>
    private static readonly string[] WritesNothing =
    [
        "output", "remote-name", "include", "fail-with-body", "capath", "crlfile", "tls13-ciphers", "curves",
        "sigalgs", "cert-status", "engine", "sessionid", "socks5-gssapi-service", "proxy-capath",
        "proxy-tls13-ciphers", "proxy-crlfile",
    ];

    /// <summary>Options whose lines BL-1106 measures and writes; each moves to one of the lists above as it lands.</summary>
    private static readonly string[] WaitingForBl1106 =
    [
        "globoff", "show-error", "progress-bar", "buffer", "verbose", "trace", "trace-ascii", "trace-time",
        "trace-ids", "trace-config", "stderr", "log-level", "log-file", "libcurl", "upload-file", "out-null",
        "remote-name-all", "remote-header-name", "output-dir", "create-dirs", "clobber", "skip-existing",
        "remove-on-error", "write-out", "form-escape", "disallow-username-in-url", "url-query", "dump-header",
        "etag-save", "etag-compare", "alt-svc", "hsts", "telnet-option", "tftp-blksize", "mail-from", "mail-rcpt",
        "mail-auth", "mail-rcpt-allowfails", "upload-flags", "interface", "local-port", "dns-servers",
        "dns-interface", "dns-ipv4-addr", "dns-ipv6-addr", "doh-url", "doh-insecure", "doh-cert-status",
        "unix-socket", "abstract-unix-socket", "tftp-no-options", "disable-epsv", "epsv", "ftp-skip-pasv-ip",
        "ftp-method", "ftp-create-dirs", "ftp-port", "ftp-pasv", "disable-eprt", "eprt", "ssl", "ftp-ssl",
        "ssl-reqd", "ftp-ssl-reqd", "ftp-ssl-control", "ftp-ssl-ccc", "ftp-ssl-ccc-mode", "ftp-account",
        "ftp-alternative-to-user", "ftp-pret", "list-only", "use-ascii", "crlf", "append", "quote",
        "create-file-mode", "tcp-nodelay", "keepalive", "keepalive-time", "keepalive-cnt", "ip-tos", "vlan-priority",
        "tcp-fastopen", "mptcp", "styled-output", "pubkey", "knownhosts", "hostpubmd5", "hostpubsha256",
        "compressed-ssh", "proto", "proto-redir", "proto-default", "ech", "ssl-sessions", "dump-ca-embed", "tlsuser",
        "tlspassword", "tlsauthtype", "range", "continue-at", "max-filesize", "happy-eyeballs-timeout-ms",
        "expect100-timeout", "retry", "retry-delay", "retry-max-time", "retry-all-errors", "retry-connrefused",
        "limit-rate", "rate", "speed-limit", "speed-time", "xattr", "time-cond", "junk-session-cookies", "follow",
        "max-redirs", "post301", "post302", "post303", "show-headers", "fail-early", "parallel",
        "parallel-immediate", "parallel-max", "parallel-max-host", "config", "next", "variable", "disable",
        "version", "help", "manual", "ai-help", "raw", "tr-encoding", "ignore-content-length", "path-as-is",
        "http0.9", "request-target", "ipfs-gateway", "http1.0", "http1.1", "http2", "http2-prior-knowledge", "http3",
        "http3-only", "sslv2", "sslv3", "metalink", "npn", "ntlm-wb", "false-start", "egd-file", "random-file",
        "krb4", "krb",
    ];

    [TestMethod]
    public void EveryParsedOption_IsWrittenOrKnownToWriteNothingOrWaitingForItsTask()
    {
        HashSet<string> listed = [.. Written, .. WritesNothing, .. WaitingForBl1106];

        string[] unlisted = [.. CommandLineOptionTable.Rows.Select(row => row.LongName).Where(name => !listed.Contains(name))];

        Assert.IsEmpty(unlisted, "Options with no --libcurl entry: " + string.Join(", ", unlisted));
    }

    [TestMethod]
    public void EveryListedOption_IsParsedAndListedOnce()
    {
        string[] listed = [.. Written, .. WritesNothing, .. WaitingForBl1106];
        HashSet<string> parsed = [.. CommandLineOptionTable.Rows.Select(row => row.LongName)];

        Assert.HasCount(parsed.Count, listed);
        Assert.IsTrue(listed.All(parsed.Contains));
    }
}
