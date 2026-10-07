using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the four c-ares options <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and
/// <c>--dns-ipv6-addr</c> on every platform: the last value is kept verbatim, an empty one is refused as
/// blank, and a malformed one is accepted, because a c-ares build of curl checks the value only when it
/// resolves a host name (exit 43 there). Measured on 2026-09-28 with Alpine edge's curl 8.22.0 built with
/// c-ares 1.34.8 through <c>Record-CurlExchange.ps1 -NoServer</c> against <c>http://127.0.0.1:1/</c>;
/// every case is in BL-643's Notes.
/// </summary>
[TestClass]
public sealed class CommandLineDnsOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_WithoutDnsOptions_LeavesThemUnset()
    {
        CommandLineOptions options = Accept();

        TestDiagnostics.For(TestContext).Assert("dns servers", null, options.DnsServers);
        Assert.IsNull(options.DnsServers);
        Assert.IsNull(options.DnsInterface);
        Assert.IsNull(options.DnsIPv4Address);
        Assert.IsNull(options.DnsIPv6Address);
    }

    [TestMethod]
    [DataRow("192.168.0.1,192.168.0.2")]
    [DataRow("10.0.0.1:53")]
    [DataRow("[::1]:53,1.2.3.4")]
    public void Parse_DnsServers_RecordsTheListVerbatim(string servers)
    {
        Assert.AreEqual(servers, Accept("--dns-servers", servers).DnsServers);
    }

    [TestMethod]
    public void Parse_DnsInterface_RecordsTheName()
    {
        Assert.AreEqual("eth0", Accept("--dns-interface", "eth0").DnsInterface);
    }

    [TestMethod]
    public void Parse_DnsIPv4Address_RecordsTheAddress()
    {
        Assert.AreEqual("10.1.2.3", Accept("--dns-ipv4-addr", "10.1.2.3").DnsIPv4Address);
    }

    [TestMethod]
    public void Parse_DnsIPv6Address_RecordsTheAddress()
    {
        Assert.AreEqual("2a04:4e42::561", Accept("--dns-ipv6-addr", "2a04:4e42::561").DnsIPv6Address);
    }

    [TestMethod]
    public void Parse_DnsServersWithAttachedValue_RecordsTheList()
    {
        Assert.AreEqual("10.0.0.1", Accept("--dns-servers=10.0.0.1").DnsServers);
    }

    [TestMethod]
    public void Parse_DnsOptionsGivenTwice_KeepTheLast()
    {
        CommandLineOptions options = Accept(
            "--dns-servers", "1.1.1.1", "--dns-servers", "8.8.8.8",
            "--dns-interface", "eth0", "--dns-interface", "eth1",
            "--dns-ipv4-addr", "10.0.0.1", "--dns-ipv4-addr", "10.0.0.2",
            "--dns-ipv6-addr", "::1", "--dns-ipv6-addr", "::2");

        TestDiagnostics.For(TestContext).Assert("dns servers", "8.8.8.8", options.DnsServers);
        Assert.AreEqual("8.8.8.8", options.DnsServers);
        Assert.AreEqual("eth1", options.DnsInterface);
        Assert.AreEqual("10.0.0.2", options.DnsIPv4Address);
        Assert.AreEqual("::2", options.DnsIPv6Address);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("1.2.3.4:99999")]
    [DataRow("1.2.3.4,")]
    [DataRow("1.2.3.4, 5.6.7.8")]
    public void Parse_MalformedDnsServers_IsAcceptedForTheResolverToRefuse(string servers)
    {
        Assert.AreEqual(servers, Accept("--dns-servers", servers).DnsServers);
    }

    [TestMethod]
    public void Parse_NonexistentDnsInterface_IsAccepted()
    {
        Assert.AreEqual("nosuchif0", Accept("--dns-interface", "nosuchif0").DnsInterface);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("::1")]
    public void Parse_MalformedDnsIPv4Address_IsAcceptedForTheResolverToRefuse(string address)
    {
        Assert.AreEqual(address, Accept("--dns-ipv4-addr", address).DnsIPv4Address);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("1.2.3.4")]
    public void Parse_MalformedDnsIPv6Address_IsAcceptedForTheResolverToRefuse(string address)
    {
        Assert.AreEqual(address, Accept("--dns-ipv6-addr", address).DnsIPv6Address);
    }

    [TestMethod]
    [DataRow("--dns-servers")]
    [DataRow("--dns-interface")]
    [DataRow("--dns-ipv4-addr")]
    [DataRow("--dns-ipv6-addr")]
    public void Parse_EmptyValue_IsRefusedAsBlank(string option)
    {
        CommandLineParseResult result = Parse([option, "", Url]);

        AssertRefused(result, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--dns-servers")]
    [DataRow("--dns-interface")]
    [DataRow("--dns-ipv4-addr")]
    [DataRow("--dns-ipv6-addr")]
    public void Parse_OptionLast_IsRefusedAsNeedingParameter(string option)
    {
        CommandLineParseResult result = Parse([Url, option]);

        AssertRefused(result, $"curl: option {option}: requires parameter");
    }

    [TestMethod]
    [DataRow("dns-servers")]
    [DataRow("dns-interface")]
    [DataRow("dns-ipv4-addr")]
    [DataRow("dns-ipv6-addr")]
    public void Parse_NegatedOption_IsRefusedAsNotReversible(string longName)
    {
        CommandLineParseResult result = Parse([$"--no-{longName}", "x", Url]);

        AssertRefused(result, $"curl: option --no-{longName}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_DnsServersBeforeNext_DoesNotReachTheNextGroup()
    {
        CommandLineParseResult result = Parse(["--dns-servers", "1.1.1.1", Url, "--next", Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        diagnostics.Assert("group 0 dns servers", "1.1.1.1", result.Groups[0].DnsServers);
        diagnostics.Assert("group 1 dns servers", null, result.Groups[1].DnsServers);
        Assert.AreEqual("1.1.1.1", result.Groups[0].DnsServers);
        Assert.IsNull(result.Groups[1].DnsServers);
    }

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        TestDiagnostics.For(TestContext).Act(
            "dns options",
            $"servers {Quote(result.Options.DnsServers)}, interface {Quote(result.Options.DnsInterface)}, ipv4 {Quote(result.Options.DnsIPv4Address)}, ipv6 {Quote(result.Options.DnsIPv6Address)}");
        return result.Options;
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        diagnostics.ActParse(result);
        return result;
    }

    private static string Quote(string? value) => value is null ? "null" : "\"" + value + "\"";

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", false, result.IsAccepted);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        diagnostics.Assert("first stderr line", optionLine, result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
