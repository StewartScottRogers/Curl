using System.Text;
using Curl.Protocol.Abstractions;

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
        "form-string", "get", "url-query", "dump-header", "user", "basic", "digest", "ntlm", "negotiate",
        "anyauth", "oauth2-bearer", "proxy", "socks4", "socks4a", "socks5", "socks5-hostname", "proxy-user",
        "noproxy", "proxytunnel", "telnet-option", "tftp-blksize", "mail-from", "mail-rcpt", "mail-auth",
        "mail-rcpt-allowfails", "upload-flags", "login-options", "sasl-authzid", "sasl-ir", "resolve", "connect-to", "tftp-no-options",
        "disable-epsv", "epsv", "ftp-skip-pasv-ip", "ftp-method", "ftp-create-dirs", "ftp-port", "ftp-pasv",
        "disable-eprt", "eprt", "ssl", "ftp-ssl", "ssl-reqd", "ftp-ssl-reqd", "ftp-ssl-control", "list-only",
        "quote", "create-file-mode", "insecure", "ssl-no-revoke", "ssl-revoke-best-effort", "ssl-allow-beast",
        "ca-native", "alpn", "sessionid", "tcp-nodelay", "keepalive", "cacert", "capath", "proxy-insecure",
        "proxy-cacert", "proxy-capath", "cert", "key", "cert-type", "key-type", "pass", "pubkey", "knownhosts",
        "hostpubmd5", "hostpubsha256", "compressed-ssh", "tlsv1", "tlsv1.0",
        "tlsv1.1", "tlsv1.2", "tlsv1.3", "tls-max", "proxy-tlsv1", "proto", "proto-redir", "proto-default",
        "ciphers", "tls13-ciphers", "range", "continue-at", "max-filesize", "connect-timeout", "max-time",
        "retry", "retry-delay", "retry-max-time", "retry-all-errors", "retry-connrefused", "limit-rate",
        "speed-limit", "speed-time", "remote-time", "time-cond", "request", "header", "proxy-header",
        "user-agent", "referer", "cookie", "cookie-jar", "junk-session-cookies", "location", "location-trusted",
        "max-redirs", "post301", "post302", "post303", "show-headers", "include", "head", "fail",
        "fail-with-body", "compressed", "raw", "tr-encoding", "ignore-content-length", "path-as-is", "http0.9",
        "request-target", "ipfs-gateway", "http1.0", "http1.1", "http2", "http2-prior-knowledge", "http3",
        "http3-only", "ipv4", "ipv6", "sslv2", "sslv3", "metalink", "npn", "ntlm-wb", "false-start", "egd-file", "random-file",
        "krb4",
    ];

    [TestMethod]
    public void OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce()
    {
        HashSet<string> perGroup = new(PerGroupOptionLongNames, StringComparer.Ordinal);

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

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(1, result.Groups);
        Assert.AreSame(result.Options, result.Groups[0]);
        Assert.IsNull(result.RefusalAfterGroups);
    }

    [TestMethod]
    [DataRow("--next")]
    [DataRow("-:")]
    [DataRow("--next=x")]
    public void Parse_Next_StartsASecondGroupWithItsOwnUrls(string next)
    {
        CommandLineParseResult result = Parse([FirstUrl, next, SecondUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        CollectionAssert.AreEqual(new[] { FirstUrl }, result.Groups[0].Urls.ToArray());
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_PerGroupOptionsBeforeNext_DoNotReachTheNextGroup()
    {
        CommandLineParseResult result = Parse(["-w", "[w]", "-o", "f508a", "-H", "X-A: 1", "-d", "x", FirstUrl, "--next", SecondUrl]);

        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions first = result.Groups[0];
        CommandLineOptions second = result.Groups[1];
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

        Assert.IsTrue(result.IsAccepted);
        foreach (CommandLineOptions group in result.Groups)
        {
            Assert.AreEqual(TraceKind.Verbose, group.Trace);
            Assert.IsTrue(group.Silent);
            Assert.IsTrue(group.FailEarly);
        }
    }

    [TestMethod]
    public void Parse_GlobalOptionBeforeNext_AppliesToEveryGroup()
    {
        CommandLineParseResult result = Parse(["--trace-ascii", "t.txt", "--trace-time", "-S", "--no-progress-meter", FirstUrl, "--next", SecondUrl]);

        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions second = result.Groups[1];
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

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        Assert.IsFalse(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_NextAfterALetterOfABundle_AppliesTheLetterFirst()
    {
        CommandLineParseResult result = Parse([FirstUrl, "-s:A", SecondUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        Assert.IsTrue(result.Groups[1].Silent);
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

        Assert.IsFalse(result.IsAccepted);
        Assert.IsEmpty(result.Groups);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        Assert.IsFalse(result.Refusal.FoundAtTransferSetup);
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

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelled}: is badly used here", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoNext_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = Parse([FirstUrl, "--no-next", SecondUrl]);

        Assert.IsFalse(result.IsAccepted);
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

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(1, result.Groups);
        CollectionAssert.AreEqual(new[] { FirstUrl }, result.Groups[0].Urls.ToArray());
        Assert.IsNotNull(result.RefusalAfterGroups);
        Assert.AreEqual(CurlExitCode.FailedInit, result.RefusalAfterGroups.ExitCode);
        CollectionAssert.AreEqual(refusalLines, result.RefusalAfterGroups.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_FormAndDataInTheFirstGroup_RefusesTheCommandLine()
    {
        CommandLineParseResult result = Parse(["-s", "-F", "a=b", "-d", "z", FirstUrl, "--next", SecondUrl]);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsTrue(result.Refusal.FoundAtTransferSetup);
        Assert.IsEmpty(result.Refusal.StandardErrorLines);
    }

    [TestMethod]
    public void Parse_FormAndDataInALaterGroup_RunsTheGroupsBeforeItThenRefuses()
    {
        CommandLineParseResult result = Parse([FirstUrl, "--next", SecondUrl, "--next", "-F", "a=b", "-d", "z", ThirdUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        Assert.IsNotNull(result.RefusalAfterGroups);
        CommandLineParseResult alone = Parse(["-F", "a=b", "-d", "z", ThirdUrl]);
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

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            Enumerable.Repeat(CommandLineWarning.MoreOutputOptionsThanUrls, warnings).ToArray(),
            result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileNextLine_StartsTheNextGroup()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "k1.txt"], ("k1.txt", $"url = \"{FirstUrl}\"\nnext\nurl = \"{SecondUrl}\"\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        CollectionAssert.AreEqual(new[] { FirstUrl }, result.Groups[0].Urls.ToArray());
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileNextLineWithNoUrlBeforeIt_IsIgnored()
    {
        CommandLineParseResult result = Parse(["-K", "k2.txt"], ("k2.txt", $"next\nurl = \"{SecondUrl}\"\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(1, result.Groups);
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Options.Urls.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ArgumentsAfterAConfigFileNextLine_GoToTheGroupItStarted()
    {
        CommandLineParseResult result = Parse(["-s", FirstUrl, "-K", "k4.txt", SecondUrl], ("k4.txt", "-d x\n-:\n-H \"X-B: 2\"\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        Assert.IsNotNull(result.Groups[0].PostData);
        Assert.IsEmpty(result.Groups[0].Headers);
        CollectionAssert.AreEqual(new[] { "X-B: 2" }, result.Groups[1].Headers.ToArray());
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NextLinesInNestedConfigFiles_StartAGroupOnlyAfterAUrl()
    {
        CommandLineParseResult result = Parse(
            ["-K", "outer.txt", SecondUrl],
            ("outer.txt", "-K inner.txt\nnext\n"),
            ("inner.txt", $"url = \"{FirstUrl}\"\nnext\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        CollectionAssert.AreEqual(new[] { SecondUrl }, result.Groups[1].Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NextOnTheCommandLineAfterAConfigFile_IsStillRefusedWithoutAUrl()
    {
        CommandLineParseResult result = Parse(["-K", "k.txt", "--next", FirstUrl], ("k.txt", "silent\n"));

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "curl: option --next: is badly used here", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_UserWithoutPasswordInSeveralGroups_PromptsForEachWithItsUrlNumber()
    {
        RecordingPasswordPrompt prompt = new("pw");

        CommandLineParseResult result = CommandLineParser.Parse(
            ["-u", "bob", FirstUrl, "--next", "-U", "pat;opts", SecondUrl],
            _ => true,
            prompt,
            new RecordingDataFileReader());

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

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.VersionRequested);
        Assert.HasCount(2, result.Groups);
    }

    [TestMethod]
    public void NextGroup_NullLongName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineOption.NextGroup(null!, ':'));
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, params (string Name, string Contents)[] files)
    {
        RecordingDataFileReader reader = new();
        foreach ((string name, string contents) in files)
        {
            reader.Files[name] = Encoding.UTF8.GetBytes(contents);
        }

        return CommandLineParser.Parse(arguments, _ => true, new RecordingPasswordPrompt(string.Empty), reader);
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
