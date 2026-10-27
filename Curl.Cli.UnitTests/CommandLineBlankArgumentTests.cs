using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how every option that takes an argument treats an empty one (<c>''</c>), as curl 8.22.0 does
/// (BL-3183). Each option in <c>curl --help all</c> that shows an argument was run once as
/// <c>curl -m 5 --&lt;name&gt; '' http://127.0.0.1:1/</c> through <c>Record-CurlExchange.ps1 -NoServer</c>
/// on 2026-10-22 (the lists are quoted in BL-3183's Notes): some refuse it as blank, some accept it and go
/// on to the transfer, and the rest refuse it for another reason (not a number, not a file, not supported).
/// </summary>
[TestClass]
public sealed class CommandLineBlankArgumentTests
{
    private const string Url = "http://127.0.0.1:1/";

    private static readonly string[] RefusedAsBlank =
    [
        "abstract-unix-socket", "capath", "cert", "cert-type", "ciphers", "cookie-jar", "curves", "dump-header",
        "engine", "etag-compare", "etag-save", "ftp-account", "ftp-alternative-to-user", "ftp-port",
        "haproxy-clientip", "hostpubmd5", "hostpubsha256", "interface", "ipfs-gateway", "key", "key-type",
        "libcurl", "mail-auth", "mail-from", "oauth2-bearer", "output", "output-dir", "pass", "pinnedpubkey",
        "preproxy", "proto-default", "proxy-capath", "proxy-cert", "proxy-cert-type", "proxy-ciphers",
        "proxy-key-type", "proxy-pinnedpubkey", "proxy-service-name", "proxy-tls13-ciphers", "proxy1.0", "pubkey",
        "range", "request", "request-target", "sasl-authzid", "service-name", "sigalgs", "socks4", "socks4a",
        "socks5", "socks5-hostname", "tls13-ciphers", "trace", "trace-ascii", "unix-socket", "url",
    ];

    // Refused as not supported, blank or not, by every reference build, none of which has c-ares, as Cgr041Tests
    // pins (ADR-0837); a c-ares build refuses an empty one as blank, as CommandLineDnsOptionTests pins.
    private static readonly string[] RefusedWithoutCAres =
    [
        "dns-interface", "dns-ipv4-addr", "dns-ipv6-addr", "dns-servers",
    ];

    // Curl's own options, unknown to curl 8.22.0, so not measured.
    private static readonly string[] CurlOnly =
    [
        "log-level", "log-file", "httpsig-algo", "httpsig-key", "httpsig-keyid", "httpsig-headers",
    ];

    private static readonly string[] AcceptedWhenBlank =
    [
        "alt-svc", "aws-sigv4", "connect-to", "cookie", "data", "data-ascii", "data-binary", "data-raw",
        "data-urlencode", "delegation", "doh-url", "egd-file", "ftp-method", "ftp-ssl-ccc-mode", "header", "help",
        "hsts", "json", "krb", "krb4", "login-options", "mail-rcpt", "noproxy", "proto", "proto-redir", "proxy",
        "proxy-header", "proxy-key", "proxy-pass", "proxy-tlsauthtype", "proxy-tlspassword", "proxy-tlsuser",
        "proxy-user", "quote", "random-file", "referer", "resolve", "socks5-gssapi-service", "stderr",
        "telnet-option", "time-cond", "tlsauthtype", "tlspassword", "tlsuser", "trace-config", "upload-file",
        "url-query", "user", "user-agent", "variable", "write-out",
    ];

    private static readonly string[] RefusedForAnotherReason =
    [
        "cacert", "config", "connect-timeout", "continue-at", "create-file-mode", "crlfile",
        "expect100-timeout", "form", "form-string",
        "happy-eyeballs-timeout-ms", "ip-tos", "keepalive-cnt", "keepalive-time", "knownhosts", "limit-rate",
        "local-port", "max-filesize", "max-redirs", "max-time", "netrc-file", "parallel-max", "parallel-max-host",
        "proxy-cacert", "proxy-crlfile", "rate", "retry", "retry-delay", "retry-max-time", "speed-limit",
        "speed-time", "tftp-blksize", "tls-max", "upload-flags", "vlan-priority",
    ];

    // The Schannel build has no TLS session export, so it refuses --ssl-sessions as not supported whatever
    // the value; the OpenSSL build on Linux and macOS reads it with getstr(..., DENY_BLANK) and refuses an
    // empty one as blank (BL-3203). Each platform's answer is pinned in its own test.
    private const string SslSessions = "ssl-sessions";

    // Refused as not supported, blank or not, by every reference build, as Cgr042Tests pins (ADR-0822).
    private const string Ech = "ech";

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> RefusedAsBlankOptions => RefusedAsBlank.Select(name => new object[] { name });

    public static IEnumerable<object[]> AcceptedWhenBlankOptions => AcceptedWhenBlank.Select(name => new object[] { name });

    public static IEnumerable<object[]> RefusedForAnotherReasonOptions => RefusedForAnotherReason.Select(name => new object[] { name });

    [TestMethod]
    [DynamicData(nameof(RefusedAsBlankOptions))]
    public void Parse_BlankArgument_IsRefusedAsBlankWhereCurlRefusesIt(string longName)
    {
        CommandLineParseResult result = ParseWithBlankArgument(longName);

        string[] expected = [$"curl: option --{longName}: blank argument where content is expected", CommandLineRefusal.TryHelpLine];
        TestDiagnostics.For(TestContext).Assert("stderr", string.Join(" | ", expected), string.Join(" | ", result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(expected, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(AcceptedWhenBlankOptions))]
    public void Parse_BlankArgument_IsAcceptedWhereCurlAcceptsIt(string longName)
    {
        CommandLineParseResult result = ParseWithBlankArgument(longName);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted, string.Join(" | ", result.Refusal?.StandardErrorLines ?? []));
    }

    [TestMethod]
    [DynamicData(nameof(RefusedForAnotherReasonOptions))]
    public void Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason(string longName)
    {
        CommandLineParseResult result = ParseWithBlankArgument(longName);

        string blankLine = $"curl: option --{longName}: blank argument where content is expected";
        TestDiagnostics.For(TestContext).Assert("not refused as blank", blankLine, result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.IsFalse(result.IsAccepted, "curl refuses an empty --" + longName + " for another reason");
        Assert.IsNotEmpty(result.Refusal.StandardErrorLines);
        CollectionAssert.DoesNotContain(result.Refusal.StandardErrorLines.ToArray(), blankLine);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Parse_BlankSslSessionsOnWindows_IsNotRefusedAsBlankAsTheSchannelBuildRefusesItAsUnsupported()
    {
        CommandLineParseResult result = ParseWithBlankArgument(SslSessions);

        string[] expected = ["curl: option --ssl-sessions: the installed libcurl version does not support this", CommandLineRefusal.TryHelpLine];
        TestDiagnostics.For(TestContext).Assert("stderr", string.Join(" | ", expected), string.Join(" | ", result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(expected, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Parse_BlankSslSessionsOnLinuxAndMacOs_IsRefusedAsBlankAsTheOpenSslBuildRefusesIt()
    {
        Parse_BlankArgument_IsRefusedAsBlankWhereCurlRefusesIt(SslSessions);
    }

    [TestMethod]
    public void Parse_BlankProxyKey_IsAcceptedAsCurlAcceptsIt()
    {
        CommandLineParseResult result = ParseWithBlankArgument("proxy-key");

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted, string.Join(" | ", result.Refusal?.StandardErrorLines ?? []));
    }

    [TestMethod]
    public void Lists_EveryArgumentTakingRowInTheTable_IsMeasured()
    {
        string[] measured = [.. RefusedAsBlank, .. RefusedWithoutCAres, .. AcceptedWhenBlank, .. RefusedForAnotherReason, .. CurlOnly, SslSessions, Ech];
        string[] unmeasured = CommandLineOptionTable.Rows
            .Where(row => row.TakesValue && !measured.Contains(row.LongName))
            .Select(row => row.LongName)
            .ToArray();

        TestDiagnostics.For(TestContext).Assert("unmeasured", string.Empty, string.Join(", ", unmeasured));
        Assert.IsEmpty(unmeasured, string.Join(", ", unmeasured));
    }

    private static CommandLineParseResult ParseWithBlankArgument(string longName) =>
        CommandLineParser.Parse(["-m", "5", $"--{longName}", string.Empty, Url], _ => false, new BlankPasswordPrompt(), new NoDataFileReader());

    private sealed class BlankPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => string.Empty;
    }

    private sealed class NoDataFileReader : IDataFileReader
    {
        public bool TryReadFile(string path, out byte[] contents)
        {
            contents = [];
            return false;
        }

        public byte[] ReadStandardInput() => [];

        public bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason)
        {
            modificationTime = default;
            failureReason = "CreateFile failed: GetLastError 0x00000003";
            return false;
        }
    }
}
