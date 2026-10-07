using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--ipfs-gateway</c>: the last value is kept verbatim, an empty one is refused as blank,
/// and a malformed one is accepted, because curl 8.21.0 checks the gateway only when it rewrites an
/// <c>ipfs://</c> or <c>ipns://</c> URL. Every line was measured with <c>/mingw64/bin/curl</c> 8.21.0
/// against <c>http://127.0.0.1:1/</c> on 2026-09-27: <c>--ipfs-gateway :::</c> and
/// <c>--ipfs-gateway foo://h:1/</c> there go on to connect and exit 7.
/// </summary>
[TestClass]
public sealed class CommandLineIpfsGatewayOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_WithoutIpfsGateway_LeavesTheGatewayUnset()
    {
        string? gateway = Accept().IpfsGateway;

        Diagnostics.Assert("ipfs gateway", null, gateway);
        Assert.IsNull(gateway);
    }

    [TestMethod]
    public void Parse_IpfsGateway_RecordsTheGatewayVerbatim()
    {
        string? gateway = Accept("--ipfs-gateway", "127.0.0.1:1").IpfsGateway;

        Diagnostics.Assert("ipfs gateway", "127.0.0.1:1", gateway);
        Assert.AreEqual("127.0.0.1:1", gateway);
    }

    [TestMethod]
    public void Parse_IpfsGatewayWithAttachedValue_RecordsTheGateway()
    {
        string? gateway = Accept("--ipfs-gateway=http://gw.example/").IpfsGateway;

        Diagnostics.Assert("ipfs gateway", "http://gw.example/", gateway);
        Assert.AreEqual("http://gw.example/", gateway);
    }

    [TestMethod]
    public void Parse_IpfsGatewayGivenTwice_KeepsTheLast()
    {
        string? gateway = Accept("--ipfs-gateway", "http://first/", "--ipfs-gateway", "http://second/").IpfsGateway;

        Diagnostics.Assert("ipfs gateway", "http://second/", gateway);
        Assert.AreEqual("http://second/", gateway);
    }

    [TestMethod]
    [DataRow(":::")]
    [DataRow("foo://h:1/")]
    public void Parse_MalformedIpfsGateway_IsAcceptedForTheRewriterToRefuse(string gateway)
    {
        string? actualGateway = Accept("--ipfs-gateway", gateway).IpfsGateway;

        Diagnostics.Assert("ipfs gateway", gateway, actualGateway);
        Assert.AreEqual(gateway, actualGateway);
    }

    [TestMethod]
    public void Parse_EmptyIpfsGateway_IsRefusedAsBlank()
    {
        CommandLineParseResult result = Parse(["--ipfs-gateway", "", Url]);

        AssertRefused(result, "curl: option --ipfs-gateway: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_IpfsGatewayLast_IsRefusedAsNeedingParameter()
    {
        CommandLineParseResult result = Parse([Url, "--ipfs-gateway"]);

        AssertRefused(result, "curl: option --ipfs-gateway: requires parameter");
    }

    [TestMethod]
    public void Parse_NegatedIpfsGateway_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = Parse(["--no-ipfs-gateway", "x", Url]);

        AssertRefused(result, "curl: option --no-ipfs-gateway: the given option cannot be reversed with a --no- prefix");
    }

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([optionLine, TryHelp]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
