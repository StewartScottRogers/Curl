using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NoProtocolOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
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
        CommandLineParseResult result = CommandLineParser.Parse(["--proto", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
        AssertSchemes(expected ?? EveryKnownScheme, result.Options.AllowedProtocols);
    }

    [TestMethod]
    public void Parse_ProtoRedir_AllowsTheSchemesAsProtoDoes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto-redir", "-all,+https", Url]);

        Assert.IsTrue(result.IsAccepted);
        AssertSchemes(["https"], result.Options.AllowedRedirectProtocols);
        Assert.IsNull(result.Options.AllowedProtocols);
    }

    [TestMethod]
    public void Parse_ProtoGivenTwice_StartsAgainFromEverySchemeEachTime()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto", "-http", "--proto", "+ftp", Url]);

        Assert.IsTrue(result.IsAccepted);
        AssertSchemes(EveryKnownScheme, result.Options.AllowedProtocols);
    }

    [TestMethod]
    [DataRow("http,bogus", new[] { "Warning: unrecognized protocol 'bogus'" }, DisplayName = "unknown name")]
    [DataRow("+bogus,-nope", new[] { "Warning: unrecognized protocol 'bogus'", "Warning: unrecognized protocol 'nope'" }, DisplayName = "modifier not repeated")]
    [DataRow("++http", new[] { "Warning: unrecognized protocol '+http'" }, DisplayName = "only one modifier")]
    [DataRow("+", new[] { "Warning: unrecognized protocol ''" }, DisplayName = "modifier alone")]
    [DataRow("*", new[] { "Warning: unrecognized protocol '*'" }, DisplayName = "star")]
    [DataRow(" http , bogus", new[] { "Warning: unrecognized protocol ' http '", "Warning: unrecognized protocol ' bogus'" }, DisplayName = "spaces kept")]
    [DataRow("ipfs,ws,wss,rtmp,scp", new[] { "Warning: unrecognized protocol 'ipfs'", "Warning: unrecognized protocol 'rtmp'" }, DisplayName = "ipfs and rtmp unknown")]
    [DataRow("smb", new[] { "Warning: unrecognized protocol 'smb'" }, DisplayName = "smb unknown")]
    [DataRow("-bogus", new[] { "Warning: unrecognized protocol 'bogus'" }, DisplayName = "removing unknown")]
    [DataRow("http,aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", new[] { "Warning: unrecognized protocol 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'" }, DisplayName = "name cut to 31 characters")]
    [DataRow("+bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", new[] { "Warning: unrecognized protocol 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'" }, DisplayName = "cut after the modifier")]
    public void Parse_ProtoNamingUnknownSchemes_WarnsAndKeepsEveryScheme(string value, string[] expectedWarnings)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(expectedWarnings, result.WarningLines.ToArray());
        AssertSchemes(EveryKnownScheme, result.Options.AllowedProtocols);
    }

    [TestMethod]
    [DataRow("=bogus,http", DisplayName = "=unknown empties, then http")]
    [DataRow("=,http", DisplayName = "= alone empties, then http")]
    public void Parse_ProtoSettingUnknownThenKnown_WarnsAndAllowsOnlyTheKnown(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(1, result.WarningLines);
        AssertSchemes(["http"], result.Options.AllowedProtocols);
    }

    [TestMethod]
    public void Parse_ProtoNamingUnknownAfterSilent_DropsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--proto", "http,bogus", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--proto", "-all", new string[0], DisplayName = "-all")]
    [DataRow("--proto", "=http,-http", new string[0], DisplayName = "set then remove")]
    [DataRow("--proto", "=bogus", new[] { "Warning: unrecognized protocol 'bogus'" }, DisplayName = "=unknown")]
    [DataRow("--proto", "=", new[] { "Warning: unrecognized protocol ''" }, DisplayName = "= alone")]
    [DataRow("--proto-redir", "=bogus", new[] { "Warning: unrecognized protocol 'bogus'" }, DisplayName = "proto-redir =unknown")]
    public void Parse_ProtoLeavingNoScheme_RefusesAsBadlyUsedAfterItsWarnings(string option, string value, string[] expectedWarnings)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {option}: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
        CollectionAssert.AreEqual(expectedWarnings, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_ProtoLeavingNoSchemeAfterAnAcceptedOne_StillRefuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto", "http", "--proto", "-all", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --proto: is badly used here", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    [DataRow("ldap", "ldap")]
    [DataRow("HTTPS", "https")]
    [DataRow("ftp", "ftp")]
    public void Parse_ProtoDefaultNamingKnownScheme_RecordsItLowercase(string value, string expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto-default", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.DefaultProtocol);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("all")]
    [DataRow("smb")]
    [DataRow("ipfs")]
    public void Parse_ProtoDefaultNamingUnknownScheme_RefusesAsUnsupportedWithExitOne(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--proto-default", value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --proto-default: a specified protocol is unsupported by libcurl", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ProtoDefaultEmpty_RefusesAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proto-default", "", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --proto-default: blank argument where content is expected", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void UnsupportedProtocol_NullSpelledOption_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.UnsupportedProtocol(null!));
    }

    private static void AssertSchemes(string[] expected, IReadOnlySet<string>? actual)
    {
        Assert.IsNotNull(actual);
        CollectionAssert.AreEquivalent(expected, actual.ToArray());
    }
}
