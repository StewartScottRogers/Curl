using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-4</c> / <c>--ipv4</c> and <c>-6</c> / <c>--ipv6</c>: each sets
/// <see cref="CommandLineOptions.IpAddressFamily"/>, the last one wins without a warning, and neither
/// has a <c>--no-</c> form. Measured with curl 8.21.0 on 2026-09-28: <c>curl -4 -6 -v file:///nonexist</c>
/// prints no warning, <c>curl -s4</c> and <c>curl -6s</c> are accepted, and <c>curl --no-ipv4</c>
/// exits 2 as not reversible.
/// </summary>
[TestClass]
public sealed class CommandLineIpAddressFamilyOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";
    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NeitherOption_LeavesEitherFamily()
    {
        IpAddressFamilyChoice family = Accept().IpAddressFamily;

        Diagnostics.Assert("ip address family", IpAddressFamilyChoice.Either, family);
        Assert.AreEqual(IpAddressFamilyChoice.Either, family);
    }

    [TestMethod]
    [DataRow(new[] { "-4" }, IpAddressFamilyChoice.IPv4Only)]
    [DataRow(new[] { "--ipv4" }, IpAddressFamilyChoice.IPv4Only)]
    [DataRow(new[] { "-6" }, IpAddressFamilyChoice.IPv6Only)]
    [DataRow(new[] { "--ipv6" }, IpAddressFamilyChoice.IPv6Only)]
    [DataRow(new[] { "-s4" }, IpAddressFamilyChoice.IPv4Only)]
    [DataRow(new[] { "-6s" }, IpAddressFamilyChoice.IPv6Only)]
    [DataRow(new[] { "-4", "-6" }, IpAddressFamilyChoice.IPv6Only)]
    [DataRow(new[] { "-6", "-4" }, IpAddressFamilyChoice.IPv4Only)]
    [DataRow(new[] { "--ipv6", "--ipv4" }, IpAddressFamilyChoice.IPv4Only)]
    public void Parse_AddressFamilyOption_LastOneWins(string[] arguments, IpAddressFamilyChoice expected)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ip address family", expected, result.Options?.IpAddressFamily);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.IpAddressFamily);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-s4", true)]
    [DataRow("-6s", true)]
    [DataRow("-4", false)]
    public void Parse_BundledWithSilent_SetsSilentToo(string bundle, bool silent)
    {
        bool actualSilent = Accept(bundle).Silent;

        Diagnostics.Assert("silent", silent, actualSilent);
        Assert.AreEqual(silent, actualSilent);
    }

    [TestMethod]
    [DataRow("--no-ipv4")]
    [DataRow("--no-ipv6")]
    [DataRow("--no-ipv4=x")]
    public void Parse_NoSpelling_IsRefusedAsNotReversible(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([$"curl: option {spelling}: {CannotBeReversed}", TryHelp]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelling}: {CannotBeReversed}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NextAfterIpv4_StartsTheNextGroupWithEitherFamily()
    {
        CommandLineParseResult result = Parse(["-4", Url, "--next", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("group 0 ip address family", IpAddressFamilyChoice.IPv4Only, result.Groups.Count > 0 ? result.Groups[0].IpAddressFamily : null);
        Diagnostics.Assert("group 1 ip address family", IpAddressFamilyChoice.Either, result.Groups.Count > 1 ? result.Groups[1].IpAddressFamily : null);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(IpAddressFamilyChoice.IPv4Only, result.Groups[0].IpAddressFamily);
        Assert.AreEqual(IpAddressFamilyChoice.Either, result.Groups[1].IpAddressFamily);
    }

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
