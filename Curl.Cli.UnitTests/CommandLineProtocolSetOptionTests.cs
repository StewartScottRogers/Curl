using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser reads <c>--proto</c> and <c>--proto-redir</c> into the schemes they allow and
/// <c>--proto-default</c> into one scheme: each syntax form, the order items apply in, <c>all</c>, and the
/// warning and refusal texts, byte for byte as measured with <c>Record-CurlExchange.ps1</c> against the
/// Windows curl 8.21.0 on 2026-09-28 (BL-522).
/// </summary>
[TestClass]
public sealed class CommandLineProtocolSetOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private static readonly string[] EveryKnownScheme =
    [
        "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
        "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smtp", "smtps", "telnet", "tftp", "ws", "wss",
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoProtocolOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("allowed protocols given", false, result.Options.AllowedProtocols is not null);
        Diagnostics.Assert("allowed redirect protocols given", false, result.Options.AllowedRedirectProtocols is not null);
        Diagnostics.Assert("default protocol", "null", result.Options.DefaultProtocol ?? "null");
        Assert.IsNull(result.Options.AllowedProtocols);
        Assert.IsNull(result.Options.AllowedRedirectProtocols);
        Assert.IsNull(result.Options.DefaultProtocol);
    }

    [TestMethod]
    [DataRow("=http,https", new[] { "http", "https" }, DisplayName = "= sets the list")]
    [DataRow("-all,+http", new[] { "http" }, DisplayName = "-all then +http")]
    [DataRow("-ALL,+http", new[] { "http" }, DisplayName = "ALL in any case")]
    [DataRow("=HtTp", new[] { "http" }, DisplayName = "names in any case, stored lowercase")]
    [DataRow("=https,ftp", new[] { "https", "ftp" }, DisplayName = "no modifier adds")]
    [DataRow("=https,+ftp", new[] { "https", "ftp" }, DisplayName = "+ adds")]
    [DataRow("=http,https,-http", new[] { "https" }, DisplayName = "- removes")]
    [DataRow("-http,=ftp", new[] { "ftp" }, DisplayName = "a later = replaces what came before")]
    [DataRow("=ftp,all", null, DisplayName = "all adds every scheme")]
    [DataRow("=all", null, DisplayName = "=all is every scheme")]
    [DataRow("", null, DisplayName = "empty value keeps every scheme")]
    [DataRow(",", null, DisplayName = "empty items are skipped")]
    [DataRow("=ftp,,+ws,", new[] { "ftp", "ws" }, DisplayName = "empty items between others are skipped")]
    public void Parse_ProtoInEachSyntaxForm_AllowsTheSchemesLeftToRight(string value, string[]? expected)
    {
        CommandLineParseResult result = Parse(["--proto", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
        AssertSchemes(expected ?? EveryKnownScheme, result.Options.AllowedProtocols);
    }

    [TestMethod]
    public void Parse_ProtoRedir_AllowsTheSchemesAsProtoDoes()
    {
        CommandLineParseResult result = Parse(["--proto-redir", "-all,+https", Url]);

        Assert.IsTrue(result.IsAccepted);
        AssertSchemes(["https"], result.Options.AllowedRedirectProtocols);
        Assert.IsNull(result.Options.AllowedProtocols);
    }

    [TestMethod]
    public void Parse_ProtoGivenTwice_StartsAgainFromEverySchemeEachTime()
    {
        CommandLineParseResult result = Parse(["--proto", "-http", "--proto", "+ftp", Url]);

        Assert.IsTrue(result.IsAccepted);
        AssertSchemes(EveryKnownScheme, result.Options.AllowedProtocols);
    }

    [TestMethod]
    [DataRow("--proto", "x", "x", DisplayName = "unknown name alone")]
    [DataRow("--proto", "http,bogus", "bogus", DisplayName = "unknown after a known one")]
    [DataRow("--proto", "+bogus,-nope", "bogus", DisplayName = "stops at the first unknown, modifier not repeated")]
    [DataRow("--proto", "++http", "+http", DisplayName = "only one modifier")]
    [DataRow("--proto", "+", "", DisplayName = "modifier alone")]
    [DataRow("--proto", "=", "", DisplayName = "= alone")]
    [DataRow("--proto", "=,http", "", DisplayName = "= alone, then http")]
    [DataRow("--proto", "=bogus", "bogus", DisplayName = "=unknown")]
    [DataRow("--proto", "=bogus,http", "bogus", DisplayName = "=unknown, then http")]
    [DataRow("--proto", "-bogus", "bogus", DisplayName = "removing unknown")]
    [DataRow("--proto", "x,-all", "x", DisplayName = "unknown before -all")]
    [DataRow("--proto", "*", "*", DisplayName = "star")]
    [DataRow("--proto", " http , bogus", " http ", DisplayName = "spaces kept")]
    [DataRow("--proto", "ws,ipfs,rtmp", "ipfs", DisplayName = "ipfs unknown")]
    [DataRow("--proto", "rtmp", "rtmp", DisplayName = "rtmp unknown")]
    [DataRow("--proto", "http,aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DisplayName = "name cut to 31 characters")]
    [DataRow("--proto", "+bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", DisplayName = "cut after the modifier")]
    [DataRow("--proto-redir", "x", "x", DisplayName = "proto-redir unknown")]
    [DataRow("--proto-redir", "=bogus", "bogus", DisplayName = "proto-redir =unknown")]
    public void Parse_ProtoNamingUnknownScheme_RefusesAsBadlyUsedAfterNamingIt(string option, string value, string expectedName)
    {
        CommandLineParseResult result = Parse([option, value, Url]);

        string[] expectedLines =
        [
            $"curl: unrecognized protocol '{expectedName}'",
            $"curl: option {option}: is badly used here",
            CommandLineRefusal.TryHelpLine,
        ];
        AssertRefusal(CurlExitCode.FailedInit, expectedLines, result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(expectedLines, result.Refusal.StandardErrorLines.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ProtoNamingUnknownAfterSilent_RefusesWithoutNamingIt()
    {
        CommandLineParseResult result = Parse(["-s", "--proto", "http,bogus", Url]);

        AssertRefusal(CurlExitCode.FailedInit, ["curl: option --proto: is badly used here", CommandLineRefusal.TryHelpLine], result);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option --proto: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ProtoNamingUnknownAfterSilentAndShowError_RefusesNamingIt()
    {
        CommandLineParseResult result = Parse(["-sS", "--proto", "bogus", Url]);

        AssertRefusal(
            CurlExitCode.FailedInit,
            ["curl: unrecognized protocol 'bogus'", "curl: option --proto: is badly used here", CommandLineRefusal.TryHelpLine],
            result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: unrecognized protocol 'bogus'", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    [DataRow("--proto", "-all", DisplayName = "-all")]
    [DataRow("--proto", "=http,-http", DisplayName = "set then remove")]
    [DataRow("--proto-redir", "-all", DisplayName = "proto-redir -all")]
    public void Parse_ProtoLeavingNoScheme_RefusesAsBadlyUsed(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url]);

        AssertRefusal(CurlExitCode.FailedInit, [$"curl: option {option}: is badly used here", CommandLineRefusal.TryHelpLine], result);
        AssertWarnings([], result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {option}: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ProtoLeavingNoSchemeAfterAnAcceptedOne_StillRefuses()
    {
        CommandLineParseResult result = Parse(["--proto", "http", "--proto", "-all", Url]);

        Diagnostics.Assert(
            "first stderr line",
            "curl: option --proto: is badly used here",
            CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines.FirstOrDefault());
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --proto: is badly used here", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    [DataRow("ldap", "ldap")]
    [DataRow("HTTPS", "https")]
    [DataRow("ftp", "ftp")]
    [DataRow("DICT", "dict")]
    public void Parse_ProtoDefaultNamingKnownScheme_RecordsItLowercase(string value, string expected)
    {
        CommandLineParseResult result = Parse(["--proto-default", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("default protocol", expected, result.Options.DefaultProtocol);
        Assert.AreEqual(expected, result.Options.DefaultProtocol);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("all")]
    [DataRow("ipfs")]
    public void Parse_ProtoDefaultNamingUnknownScheme_RefusesAsUnsupportedWithExitOne(string value)
    {
        CommandLineParseResult result = Parse(["-s", "--proto-default", value, Url]);

        AssertRefusal(
            CurlExitCode.UnsupportedProtocol,
            ["curl: option --proto-default: a specified protocol is unsupported by libcurl", CommandLineRefusal.TryHelpLine],
            result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --proto-default: a specified protocol is unsupported by libcurl", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ProtoDefaultEmpty_RefusesAsBlank()
    {
        CommandLineParseResult result = Parse(["--proto-default", "", Url]);

        AssertRefusal(
            CurlExitCode.FailedInit,
            ["curl: option --proto-default: blank argument where content is expected", CommandLineRefusal.TryHelpLine],
            result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --proto-default: blank argument where content is expected", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void UnsupportedProtocol_NullSpelledOption_Throws()
    {
        Diagnostics.Arrange("spelled option", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.UnsupportedProtocol(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertWarnings(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "warnings",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertRefusal(CurlExitCode expectedExitCode, string[] expectedLines, CommandLineParseResult result)
    {
        CommandLineRefusal? refusal = CommandLineParseDiagnostics.Peek(result.Refusal);
        Diagnostics.Assert("exit code", expectedExitCode, refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
    }

    private void AssertSchemes(string[] expected, IReadOnlySet<string>? actual)
    {
        Diagnostics.Assert(
            "schemes (sorted)",
            CommandLineParseDiagnostics.QuoteEach(expected.Order(StringComparer.Ordinal)),
            actual is null ? "null" : CommandLineParseDiagnostics.QuoteEach(actual.Order(StringComparer.Ordinal)));
        Assert.IsNotNull(actual);
        CollectionAssert.AreEquivalent(expected, actual.ToArray());
    }
}
