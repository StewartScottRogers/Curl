using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Keeps <see cref="LibcurlSourceCode" /> in step with <see cref="CommandLineOptionTable" />: every option the
/// table parses is listed here as one whose <c>--libcurl</c> lines the generator writes, one curl 8.21.0
/// (Schannel) writes nothing for, so an option added later cannot be
/// forgotten (BL-654, BL-1106, BL-1174, BL-1177).
/// </summary>
[TestClass]
public sealed class LibcurlSourceCodeOptionCoverageTests
{
    /// <summary>Options whose lines are written, each pinned by <c>LibcurlSourceCodeOptionTests</c>, <c>LibcurlSourceCodeProxyTlsAndAuthenticationTests</c>, <c>LibcurlSourceCodeTransferOptionTests</c>, <c>LibcurlSourceCodeProtocolOptionTests</c> or <c>LibcurlSourceCodeTransferFileTests</c>.</summary>
    private static readonly string[] Written =
    [
        "url", "silent", "progress-meter", "data", "data-ascii", "data-binary", "data-raw", "data-urlencode", "json",
        "form", "form-string", "user", "location", "referer", "user-agent", "cookie", "cookie-jar", "compressed",
        "head", "remote-time", "fail", "header", "request", "max-time", "connect-timeout", "resolve", "connect-to",
        "ipv4", "ipv6", "get", "insecure", "cacert", "cert", "key", "cert-type", "key-type", "pass", "ciphers",
        "pinnedpubkey", "ssl-no-revoke", "ssl-revoke-best-effort", "ssl-allow-beast", "ca-native",
        "ssl-auto-client-cert", "tls-earlydata", "alpn", "tlsv1", "tlsv1.0", "tlsv1.1", "tlsv1.2", "tlsv1.3", "tls-max",
        "proxy", "socks4", "socks4a", "socks5", "socks5-hostname", "proxy1.0", "preproxy", "socks5-basic",
        "socks5-gssapi", "socks5-gssapi-nec", "haproxy-protocol", "haproxy-clientip", "suppress-connect-headers",
        "proxy-user", "proxy-basic", "proxy-digest", "proxy-ntlm", "proxy-negotiate", "proxy-anyauth",
        "proxy-service-name", "noproxy", "proxytunnel", "proxy-header", "proxy-insecure", "proxy-cacert", "proxy-cert",
        "proxy-key", "proxy-cert-type", "proxy-key-type", "proxy-pass", "proxy-ciphers", "proxy-pinnedpubkey",
        "proxy-ca-native", "proxy-ssl-auto-client-cert", "proxy-ssl-allow-beast", "proxy-tlsv1", "basic", "digest",
        "ntlm", "negotiate", "anyauth", "delegation", "service-name", "oauth2-bearer", "aws-sigv4", "netrc",
        "netrc-optional", "netrc-file", "login-options", "sasl-authzid", "sasl-ir", "location-trusted",
        "request-target", "list-only", "append", "use-ascii", "range", "form-escape", "disallow-username-in-url",
        "alt-svc", "hsts", "interface", "local-port", "doh-url", "unix-socket", "abstract-unix-socket", "keepalive",
        "keepalive-time", "keepalive-cnt", "continue-at", "max-filesize", "happy-eyeballs-timeout-ms",
        "expect100-timeout", "speed-limit", "speed-time", "time-cond", "junk-session-cookies", "follow", "max-redirs",
        "post301", "post302", "post303", "tr-encoding", "ignore-content-length", "path-as-is", "http0.9", "http1.0",
        "http1.1", "crlf", "write-out", "verbose", "trace", "trace-ascii", "url-query", "telnet-option", "tftp-blksize",
        "mail-from", "mail-rcpt", "mail-auth", "mail-rcpt-allowfails", "upload-flags", "tftp-no-options",
        "ftp-skip-pasv-ip", "ftp-method", "ftp-create-dirs", "ftp-port", "ftp-pasv", "ssl", "ftp-ssl", "ssl-reqd",
        "ftp-ssl-reqd", "ftp-ssl-control", "ftp-ssl-ccc", "ftp-ssl-ccc-mode", "ftp-account", "ftp-alternative-to-user",
        "ftp-pret", "quote", "create-file-mode", "ip-tos", "vlan-priority", "mptcp", "pubkey", "hostpubmd5",
        "hostpubsha256", "compressed-ssh", "proto", "proto-redir", "proto-default", "limit-rate", "parallel", "upload-file",
        "etag-compare",
    ];

    /// <summary>Options curl 8.21.0's Schannel build writes no line for, measured (BL-653, BL-654, BL-1106 and BL-1174 Notes; ADR-0326 for the options the Schannel build refuses).</summary>
    private static readonly string[] WritesNothing =
    [
        "output", "proxy-http3", "remote-name", "include", "fail-with-body", "capath", "crlfile", "tls13-ciphers", "curves", "sigalgs",
        "cert-status", "engine", "sessionid", "socks5-gssapi-service", "proxy-capath", "proxy-tls13-ciphers",
        "proxy-crlfile", "globoff", "show-error", "progress-bar", "buffer", "trace-time", "trace-ids", "trace-config",
        "stderr", "out-null", "remote-name-all", "remote-header-name", "output-dir", "create-dirs", "clobber",
        "skip-existing", "remove-on-error", "dump-header", "etag-save", "tcp-nodelay", "tcp-fastopen", "styled-output",
        "dump-ca-embed", "retry", "retry-delay", "retry-max-time", "retry-all-errors", "retry-connrefused", "rate",
        "xattr", "show-headers", "fail-early", "parallel-immediate", "parallel-max", "parallel-max-host", "config",
        "next", "variable", "version", "help", "manual", "ai-help", "libcurl", "raw", "sslv2", "sslv3", "metalink",
        "npn", "ntlm-wb", "false-start", "egd-file", "random-file", "disable", "doh-insecure", "doh-cert-status",
        "disable-epsv", "epsv", "disable-eprt", "eprt", "krb4", "krb", "ipfs-gateway", "log-level", "log-file",
        "dns-servers", "dns-interface", "dns-ipv4-addr", "dns-ipv6-addr", "knownhosts", "ech", "ssl-sessions", "tlsuser",
        "tlspassword", "tlsauthtype", "proxy-tlsuser", "proxy-tlspassword", "proxy-tlsauthtype", "http2",
        "http2-prior-knowledge", "http3", "http3-only", "proxy-http2",
    ];

    /// <summary>Gets or sets the MSTest context the diagnostics are written to.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void EveryParsedOption_IsWrittenOrKnownToWriteNothing()
    {
        HashSet<string> listed = [.. Written, .. WritesNothing];
        Diagnostics.Arrange("listed option count", listed.Count);

        string[] unlisted = [.. CommandLineOptionTable.Rows.Select(row => row.LongName).Where(name => !listed.Contains(name))];
        Diagnostics.Act("unlisted options", CommandLineParseDiagnostics.QuoteEach(unlisted));

        Diagnostics.Assert("unlisted options", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(unlisted));
        Assert.IsEmpty(unlisted, "Options with no --libcurl entry: " + string.Join(", ", unlisted));
    }

    [TestMethod]
    public void EveryListedOption_IsParsedAndListedOnce()
    {
        string[] listed = [.. Written, .. WritesNothing];
        HashSet<string> parsed = [.. CommandLineOptionTable.Rows.Select(row => row.LongName)];
        Diagnostics.Arrange("listed entry count", listed.Length);
        Diagnostics.Act("parsed option count", parsed.Count);

        Diagnostics.Assert("listed entry count", parsed.Count, listed.Length);
        Diagnostics.Assert("every listed option is parsed", true, listed.All(parsed.Contains));
        Assert.HasCount(parsed.Count, listed);
        Assert.IsTrue(listed.All(parsed.Contains));
    }
}
