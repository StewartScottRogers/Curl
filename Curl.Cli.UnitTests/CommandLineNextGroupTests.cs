using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-:</c> / <c>--next</c>, which ends one option group and starts the next. Measured with the
/// local curl 8.21.0 on 2026-09-28 through <c>Record-CurlExchange.ps1 -Connections 2</c> against
/// <c>http://127.0.0.1:45808/a</c> and <c>/b</c> with <c>-q</c> first; every case, with the bytes curl
/// printed, is in BL-508's Notes.
/// </summary>
[TestClass]
public sealed class CommandLineNextGroupTests
{
    private const string FirstUrl = "http://127.0.0.1:45808/a";

    private const string SecondUrl = "http://127.0.0.1:45808/b";

    private const string ThirdUrl = "http://127.0.0.1:45808/c";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    /// <summary>
    /// The per-group options curl 8.21.0 keeps in each <c>OperationConfig</c>: every row not in
    /// <see cref="CommandLineOptionTable.GlobalOptionLongNames"/>. A new row must be added to one list or
    /// the other, so its place among the groups is decided rather than guessed.
    /// </summary>
    private static readonly string[] PerGroupOptionLongNames =
    [
        "url", "globoff", "buffer", "output", "upload-file", "remote-name", "out-null", "remote-name-all",
        "remote-header-name", "output-dir", "create-dirs", "clobber", "skip-existing", "remove-on-error",
        "write-out", "data", "data-ascii", "data-binary", "data-raw", "data-urlencode", "json", "form",
        "form-string", "form-escape", "disallow-username-in-url", "get", "url-query", "dump-header", "etag-save", "etag-compare", "alt-svc", "hsts", "user", "basic", "digest", "ntlm", "negotiate",
        "anyauth", "oauth2-bearer", "aws-sigv4", "netrc", "netrc-optional", "netrc-file","proxy", "socks4", "socks4a", "socks5", "socks5-hostname", "proxy1.0", "preproxy", "socks5-basic", "socks5-gssapi", "socks5-gssapi-service", "socks5-gssapi-nec", "haproxy-protocol", "haproxy-clientip", "suppress-connect-headers", "proxy-user", "proxy-basic", "proxy-digest", "proxy-ntlm", "proxy-negotiate", "proxy-anyauth",
        "noproxy", "proxytunnel", "telnet-option", "tftp-blksize", "mail-from", "mail-rcpt", "mail-auth",
        "mail-rcpt-allowfails", "upload-flags", "login-options", "sasl-authzid", "sasl-ir", "resolve", "connect-to", "interface", "local-port", "dns-servers", "dns-interface", "dns-ipv4-addr", "dns-ipv6-addr", "doh-url", "doh-insecure", "doh-cert-status", "unix-socket", "abstract-unix-socket", "tftp-no-options",
        "disable-epsv", "epsv", "ftp-skip-pasv-ip", "ftp-method", "ftp-create-dirs", "ftp-port", "ftp-pasv",
        "disable-eprt", "eprt", "ssl", "ftp-ssl", "ssl-reqd", "ftp-ssl-reqd", "ftp-ssl-control", "ftp-ssl-ccc",
        "ftp-ssl-ccc-mode", "ftp-account", "ftp-alternative-to-user", "ftp-pret", "list-only", "use-ascii", "crlf", "append",
        "quote", "create-file-mode", "insecure", "ssl-no-revoke", "ssl-revoke-best-effort", "ssl-allow-beast",
        "ca-native", "alpn", "sessionid", "tcp-nodelay", "keepalive", "keepalive-time", "keepalive-cnt", "ip-tos", "vlan-priority", "tcp-fastopen", "mptcp", "cacert", "capath", "crlfile",
        "pinnedpubkey", "cert-status", "ssl-auto-client-cert", "proxy-insecure", "proxy-http2", "proxy-http3",
        "proxy-cacert", "proxy-capath", "proxy-cert", "proxy-key", "proxy-cert-type", "proxy-key-type", "proxy-pass",
        "proxy-ciphers", "proxy-tls13-ciphers", "proxy-crlfile", "proxy-pinnedpubkey", "proxy-ca-native",
        "proxy-ssl-auto-client-cert", "proxy-ssl-allow-beast", "proxy-tlsuser", "proxy-tlspassword", "proxy-tlsauthtype", "cert", "key", "cert-type", "key-type", "pass", "pubkey", "knownhosts",
        "hostpubmd5", "hostpubsha256", "compressed-ssh", "tlsv1", "tlsv1.0",
        "tlsv1.1", "tlsv1.2", "tlsv1.3", "tls-max", "proxy-tlsv1", "proto", "proto-redir", "proto-default",
        "ciphers", "tls13-ciphers", "curves", "sigalgs", "tls-earlydata", "ech", "engine", "tlsuser",
        "tlspassword", "tlsauthtype", "range", "continue-at", "max-filesize", "connect-timeout", "happy-eyeballs-timeout-ms", "max-time", "expect100-timeout",
        "retry", "retry-delay", "retry-max-time", "retry-all-errors", "retry-connrefused", "limit-rate",
        "speed-limit", "speed-time", "remote-time", "xattr", "time-cond", "request", "header", "proxy-header",
        "user-agent", "referer", "cookie", "cookie-jar", "junk-session-cookies", "follow", "location", "location-trusted",
        "max-redirs", "post301", "post302", "post303", "show-headers", "include", "head", "fail",
        "fail-with-body", "compressed", "raw", "tr-encoding", "ignore-content-length", "path-as-is", "http0.9",
        "request-target", "ipfs-gateway", "http1.0", "http1.1", "http2", "http2-prior-knowledge", "http3",
        "http3-only", "ipv4", "ipv6", "sslv2", "sslv3", "metalink", "npn", "ntlm-wb", "false-start", "egd-file", "random-file",
        "krb4", "krb", "delegation", "service-name", "proxy-service-name",
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce()
    {
        HashSet<string> perGroup = new(PerGroupOptionLongNames, StringComparer.Ordinal);
        Diagnostics.Arrange("per-group long names", perGroup.Count);
        Diagnostics.Arrange("global long names", CommandLineOptionTable.GlobalOptionLongNames.Count);

        string[] unclassified = [.. CommandLineOptionTable.Rows
            .Where(row => !(CommandLineOptionTable.GlobalOptionLongNames.Contains(row.LongName) ^ perGroup.Contains(row.LongName)))
            .Select(row => row.LongName)];
        Diagnostics.Act("rows", CommandLineOptionTable.Rows.Count);
        Diagnostics.Assert("rows not in exactly one list", "[]", CommandLineParseDiagnostics.QuoteEach(unclassified));
        Diagnostics.Assert("rows", CommandLineOptionTable.Rows.Count, CommandLineOptionTable.GlobalOptionLongNames.Count + perGroup.Count);
        foreach (CommandLineOption row in CommandLineOptionTable.Rows)
        {
            Assert.IsTrue(
                CommandLineOptionTable.GlobalOptionLongNames.Contains(row.LongName) ^ perGroup.Contains(row.LongName),
                $"--{row.LongName} must be in exactly one of GlobalOptionLongNames and PerGroupOptionLongNames.");
        }

        Assert.HasCount(CommandLineOptionTable.Rows.Count, CommandLineOptionTable.GlobalOptionLongNames.Concat(perGroup));
    }

    [TestMethod]
    public void Parse_NoNext_ReturnsTheOptionsAsTheOnlyGroup()
    {
        CommandLineParseResult result = Parse([FirstUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 1, result.Groups.Count);
        Assert.HasCount(1, result.Groups);
        Assert.AreSame(result.Options, result.Groups[0]);
        Diagnostics.Assert("refusal after groups", null, result.RefusalAfterGroups);
        Assert.IsNull(result.RefusalAfterGroups);
    }

    [TestMethod]
    [DataRow("--next")]
    [DataRow("-:")]
    [DataRow("--next=x")]
    public void Parse_Next_StartsASecondGroupWithItsOwnUrls(string next)
    {
        CommandLineParseResult result = Parse([FirstUrl, next, SecondUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Diagnostics.Assert("group 0 urls", CommandLineParseDiagnostics.QuoteEach([FirstUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[0].Urls));
        CollectionAssert.AreEqual(new[] { FirstUrl }, result.Groups[0].Urls.ToArray());
        Diagnostics.Assert("group 1 urls", CommandLineParseDiagnostics.QuoteEach([SecondUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[1].Urls));
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_PerGroupOptionsBeforeNext_DoNotReachTheNextGroup()
    {
        CommandLineParseResult result = Parse(["-w", "[w]", "-o", "f508a", "-H", "X-A: 1", "-d", "x", FirstUrl, "--next", SecondUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions first = result.Groups[0];
        CommandLineOptions second = result.Groups[1];
        Diagnostics.Assert("group 0 write-out", "[w]", first.WriteOut);
        Diagnostics.Assert("group 1 write-out", null, second.WriteOut);
        Diagnostics.Assert("group 0 output files", "[\"f508a\"]", CommandLineParseDiagnostics.QuoteEach(first.OutputFiles));
        Diagnostics.Assert("group 1 headers", "[]", CommandLineParseDiagnostics.QuoteEach(second.Headers));
        Assert.AreEqual("[w]", first.WriteOut);
        Assert.IsNull(second.WriteOut);
        CollectionAssert.AreEqual(new[] { "f508a" }, first.OutputFiles.ToArray());
        Assert.IsEmpty(second.OutputFiles);
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, first.Headers.ToArray());
        Assert.IsEmpty(second.Headers);
        Assert.IsNotNull(first.PostData);
        Assert.IsNull(second.PostData);
    }

    [TestMethod]
    public void Parse_GlobalOptionAfterNext_AppliesToEveryGroup()
    {
        CommandLineParseResult result = Parse([FirstUrl, "--next", "-v", "-s", "--fail-early", SecondUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        foreach (CommandLineOptions group in result.Groups)
        {
            Diagnostics.Assert("trace", TraceKind.Verbose, group.Trace);
            Diagnostics.Assert("silent", true, group.Silent);
            Diagnostics.Assert("fail early", true, group.FailEarly);
            Assert.AreEqual(TraceKind.Verbose, group.Trace);
            Assert.IsTrue(group.Silent);
            Assert.IsTrue(group.FailEarly);
        }
    }

    [TestMethod]
    public void Parse_GlobalOptionBeforeNext_AppliesToEveryGroup()
    {
        CommandLineParseResult result = Parse(["--trace-ascii", "t.txt", "--trace-time", "-S", "--no-progress-meter", FirstUrl, "--next", SecondUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions second = result.Groups[1];
        Diagnostics.Assert("group 1 trace", TraceKind.AsciiDump, second.Trace);
        Diagnostics.Assert("group 1 trace file", "t.txt", second.TraceFile);
        Diagnostics.Assert("group 1 trace time, show error, progress meter off", "True, True, True", $"{second.TraceTime}, {second.ShowError}, {second.ProgressMeterOff}");
        Assert.AreEqual(TraceKind.AsciiDump, second.Trace);
        Assert.AreEqual("t.txt", second.TraceFile);
        Assert.IsTrue(second.TraceTime);
        Assert.IsTrue(second.ShowError);
        Assert.IsTrue(second.ProgressMeterOff);
    }

    [TestMethod]
    public void Parse_NextInABundle_EndsTheBundleAndItsLettersAfterAreIgnored()
    {
        CommandLineParseResult result = Parse([FirstUrl, "-:s", SecondUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Diagnostics.Assert("silent", false, CommandLineParseDiagnostics.Peek(result.Options)?.Silent);
        Assert.IsFalse(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_NextAfterALetterOfABundle_AppliesTheLetterFirst()
    {
        CommandLineParseResult result = Parse([FirstUrl, "-s:A", SecondUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Diagnostics.Assert("group 1 silent", true, result.Groups[1].Silent);
        Assert.IsTrue(result.Groups[1].Silent);
        Diagnostics.Assert("group 1 urls", CommandLineParseDiagnostics.QuoteEach([SecondUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[1].Urls));
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--next", FirstUrl }, "--next")]
    [DataRow(new[] { FirstUrl, "--next", "--next", SecondUrl }, "--next")]
    [DataRow(new[] { "-:", FirstUrl }, "-:")]
    [DataRow(new[] { "-sS", "--next", FirstUrl }, "--next")]
    public void Parse_NextWithNoUrlInItsGroup_IsRefusedWithAllThreeLines(string[] arguments, string spelled)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Diagnostics.Assert("group count", 0, result.Groups.Count);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Diagnostics.Assert("found at transfer setup", false, CommandLineParseDiagnostics.Peek(result.Refusal)?.FoundAtTransferSetup);
        Assert.IsEmpty(result.Groups);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        Assert.IsFalse(result.Refusal.FoundAtTransferSetup);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { "curl: missing URL before --next", $"curl: option {spelled}: is badly used here", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { "curl: missing URL before --next", $"curl: option {spelled}: is badly used here", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-s", "--next", FirstUrl }, "--next")]
    [DataRow(new[] { "-s:", FirstUrl }, "-s:")]
    public void Parse_NextWithNoUrlWhileSilent_HidesTheMissingUrlLine(string[] arguments, string spelled)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { $"curl: option {spelled}: is badly used here", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelled}: is badly used here", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoNext_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = Parse([FirstUrl, "--no-next", SecondUrl]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { "curl: option --no-next: the given option cannot be reversed with a --no- prefix", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { "curl: option --no-next: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(false, new[] { "curl: (2) no URL specified", TryHelp })]
    [DataRow(true, new[] { "curl: (2) no URL specified", TryHelp })]
    public void Parse_LastGroupWithNoUrl_RunsTheGroupsBeforeItThenRefusesNoUrl(bool silent, string[] refusalLines)
    {
        CommandLineParseResult result = Parse(silent ? ["-s", FirstUrl, "--next"] : [FirstUrl, "--next"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 1, result.Groups.Count);
        Assert.HasCount(1, result.Groups);
        Diagnostics.Assert("group 0 urls", CommandLineParseDiagnostics.QuoteEach([FirstUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[0].Urls));
        CollectionAssert.AreEqual(new[] { FirstUrl }, result.Groups[0].Urls.ToArray());
        Diagnostics.Assert("refusal after groups exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.RefusalAfterGroups)?.ExitCode);
        Diagnostics.Assert("refusal after groups stderr lines", CommandLineParseDiagnostics.QuoteEach(refusalLines), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.RefusalAfterGroups)?.StandardErrorLines ?? []));
        Assert.IsNotNull(result.RefusalAfterGroups);
        Assert.AreEqual(CurlExitCode.FailedInit, result.RefusalAfterGroups.ExitCode);
        CollectionAssert.AreEqual(refusalLines, result.RefusalAfterGroups.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_FormAndDataInTheFirstGroup_RefusesTheCommandLine()
    {
        CommandLineParseResult result = Parse(["-s", "-F", "a=b", "-d", "z", FirstUrl, "--next", SecondUrl]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Diagnostics.Assert("found at transfer setup", true, CommandLineParseDiagnostics.Peek(result.Refusal)?.FoundAtTransferSetup);
        Diagnostics.Assert("stderr lines", "[]", CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        Assert.IsTrue(result.Refusal.FoundAtTransferSetup);
        Assert.IsEmpty(result.Refusal.StandardErrorLines);
    }

    [TestMethod]
    public void Parse_FormAndDataInALaterGroup_RunsTheGroupsBeforeItThenRefuses()
    {
        CommandLineParseResult result = Parse([FirstUrl, "--next", SecondUrl, "--next", "-F", "a=b", "-d", "z", ThirdUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Assert.IsNotNull(result.RefusalAfterGroups);
        CommandLineParseResult alone = Parse(["-F", "a=b", "-d", "z", ThirdUrl]);
        Diagnostics.Assert("refusal after groups stderr lines", CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(alone.Refusal)?.StandardErrorLines ?? []), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.RefusalAfterGroups)?.StandardErrorLines ?? []));
        Assert.IsNotEmpty(alone.Refusal!.StandardErrorLines);
        CollectionAssert.AreEqual(alone.Refusal.StandardErrorLines.ToArray(), result.RefusalAfterGroups.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-o", "nul", "-o", "nul2", FirstUrl, "--next", SecondUrl, "--next", ThirdUrl }, 3)]
    [DataRow(new[] { FirstUrl, "--next", "-o", "nul", "-o", "nul2", SecondUrl, "--next", ThirdUrl }, 2)]
    [DataRow(new[] { "-s", "-o", "nul", "-o", "nul2", FirstUrl, "--next", SecondUrl }, 0)]
    public void Parse_OutputLeftOverInAGroup_WarnsOnceForItAndEachGroupAfterIt(string[] arguments, int warnings)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("warning lines after transfers", warnings, result.WarningLinesAfterTransfers.Count);
        CollectionAssert.AreEqual(
            Enumerable.Repeat(CommandLineWarning.MoreOutputOptionsThanUrls, warnings).ToArray(),
            result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileNextLine_StartsTheNextGroup()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "k1.txt"], ("k1.txt", $"url = \"{FirstUrl}\"\nnext\nurl = \"{SecondUrl}\"\n"));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Diagnostics.Assert("group 0 urls", CommandLineParseDiagnostics.QuoteEach([FirstUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[0].Urls));
        CollectionAssert.AreEqual(new[] { FirstUrl }, result.Groups[0].Urls.ToArray());
        Diagnostics.Assert("group 1 urls", CommandLineParseDiagnostics.QuoteEach([SecondUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[1].Urls));
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileNextLineWithNoUrlBeforeIt_IsIgnored()
    {
        CommandLineParseResult result = Parse(["-K", "k2.txt"], ("k2.txt", $"next\nurl = \"{SecondUrl}\"\n"));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 1, result.Groups.Count);
        Assert.HasCount(1, result.Groups);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([SecondUrl]), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Options)?.Urls ?? []));
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Options.Urls.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ArgumentsAfterAConfigFileNextLine_GoToTheGroupItStarted()
    {
        CommandLineParseResult result = Parse(["-s", FirstUrl, "-K", "k4.txt", SecondUrl], ("k4.txt", "-d x\n-:\n-H \"X-B: 2\"\n"));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Diagnostics.Assert("group 1 headers", "[\"X-B: 2\"]", CommandLineParseDiagnostics.QuoteEach(result.Groups[1].Headers));
        Assert.IsNotNull(result.Groups[0].PostData);
        Assert.IsEmpty(result.Groups[0].Headers);
        CollectionAssert.AreEqual(new[] { "X-B: 2" }, result.Groups[1].Headers.ToArray());
        Diagnostics.Assert("group 1 urls", CommandLineParseDiagnostics.QuoteEach([SecondUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[1].Urls));
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NextLinesInNestedConfigFiles_StartAGroupOnlyAfterAUrl()
    {
        CommandLineParseResult result = Parse(
            ["-K", "outer.txt", SecondUrl],
            ("outer.txt", "-K inner.txt\nnext\n"),
            ("inner.txt", $"url = \"{FirstUrl}\"\nnext\n"));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Diagnostics.Assert("group 1 urls", CommandLineParseDiagnostics.QuoteEach([SecondUrl]), CommandLineParseDiagnostics.QuoteEach(result.Groups[1].Urls));
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NextOnTheCommandLineAfterAConfigFile_IsStillRefusedWithoutAUrl()
    {
        CommandLineParseResult result = Parse(["-K", "k.txt", "--next", FirstUrl], ("k.txt", "silent\n"));

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { "curl: option --next: is badly used here", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(new[] { "curl: option --next: is badly used here", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_UserWithoutPasswordInSeveralGroups_PromptsForEachWithItsUrlNumber()
    {
        RecordingPasswordPrompt prompt = new("pw");
        string[] arguments = ["-u", "bob", FirstUrl, "--next", "-U", "pat;opts", SecondUrl];
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("prompt answer", "pw");

        CommandLineParseResult result = CommandLineParser.Parse(
            arguments,
            _ => true,
            prompt,
            new RecordingDataFileReader());
        ActGroups(result);
        Diagnostics.Act("prompts", CommandLineParseDiagnostics.QuoteEach(prompt.Prompts));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert(
            "prompts",
            "[\"Enter host password for user 'bob' on URL #1:\", \"Enter proxy password for user 'pat' on URL #2:\"]",
            CommandLineParseDiagnostics.QuoteEach(prompt.Prompts));
        Diagnostics.Assert("group passwords", "pw, pw", $"{result.Groups.ElementAtOrDefault(0)?.Credentials?.Password}, {result.Groups.ElementAtOrDefault(1)?.ProxyCredentials?.Password}");
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Enter host password for user 'bob' on URL #1:", "Enter proxy password for user 'pat' on URL #2:" },
            prompt.Prompts);
        Assert.AreEqual("pw", result.Groups[0].Credentials!.Password);
        Assert.AreEqual("pw", result.Groups[1].ProxyCredentials!.Password);
    }

    [TestMethod]
    public void Parse_VersionAfterNext_EndsParsingWithTheGroupsReadSoFar()
    {
        CommandLineParseResult result = Parse([FirstUrl, "--next", "-V", "--bogus"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("version requested", true, CommandLineParseDiagnostics.Peek(result.Options)?.VersionRequested);
        Assert.IsTrue(result.Options.VersionRequested);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
    }

    [TestMethod]
    public void NextGroup_NullLongName_Throws()
    {
        Diagnostics.Arrange("long name", null);
        Diagnostics.Arrange("short name", ':');

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineOption.NextGroup(null!, ':'));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "longName", exception.ParamName);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, params (string Name, string Contents)[] files)
    {
        Diagnostics.ArrangeArguments(arguments);
        RecordingDataFileReader reader = new();
        foreach ((string name, string contents) in files)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            Diagnostics.Bytes("config file " + name, bytes);
            reader.Files[name] = bytes;
        }

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new RecordingPasswordPrompt(string.Empty), reader);
        ActGroups(result);
        return result;
    }

    private void ActGroups(CommandLineParseResult result)
    {
        Diagnostics.ActParse(result);
        Diagnostics.Act("group count", result.Groups.Count);
        for (int index = 0; index < result.Groups.Count; index++)
        {
            Diagnostics.Act($"group {index} urls", CommandLineParseDiagnostics.QuoteEach(result.Groups[index].Urls));
        }

        if (result.RefusalAfterGroups is { } refusal)
        {
            Diagnostics.Act("refusal after groups exit code", $"{(int)refusal.ExitCode} ({refusal.ExitCode})");
            foreach (string line in refusal.StandardErrorLines)
            {
                Diagnostics.Act("refusal after groups stderr", line);
            }
        }
    }

    private sealed class RecordingPasswordPrompt(string answer) : IPasswordPrompt
    {
        public List<string> Prompts { get; } = [];

        public string ReadPassword(string prompt)
        {
            Prompts.Add(prompt);
            return answer;
        }
    }
}
