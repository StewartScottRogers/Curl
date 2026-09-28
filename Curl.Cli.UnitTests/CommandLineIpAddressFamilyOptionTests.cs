using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NeitherOption_LeavesEitherFamily()
    {
        Assert.AreEqual(IpAddressFamilyChoice.Either, Accept().IpAddressFamily);
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
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

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
        Assert.AreEqual(silent, Accept(bundle).Silent);
    }

    [TestMethod]
    [DataRow("--no-ipv4")]
    [DataRow("--no-ipv6")]
    [DataRow("--no-ipv4=x")]
    public void Parse_NoSpelling_IsRefusedAsNotReversible(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelling}: {CannotBeReversed}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NextAfterIpv4_StartsTheNextGroupWithEitherFamily()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-4", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(IpAddressFamilyChoice.IPv4Only, result.Groups[0].IpAddressFamily);
        Assert.AreEqual(IpAddressFamilyChoice.Either, result.Groups[1].IpAddressFamily);
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }
}
