using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_WithoutIpfsGateway_LeavesTheGatewayUnset()
    {
        Assert.IsNull(Accept().IpfsGateway);
    }

    [TestMethod]
    public void Parse_IpfsGateway_RecordsTheGatewayVerbatim()
    {
        Assert.AreEqual("127.0.0.1:1", Accept("--ipfs-gateway", "127.0.0.1:1").IpfsGateway);
    }

    [TestMethod]
    public void Parse_IpfsGatewayWithAttachedValue_RecordsTheGateway()
    {
        Assert.AreEqual("http://gw.example/", Accept("--ipfs-gateway=http://gw.example/").IpfsGateway);
    }

    [TestMethod]
    public void Parse_IpfsGatewayGivenTwice_KeepsTheLast()
    {
        Assert.AreEqual("http://second/", Accept("--ipfs-gateway", "http://first/", "--ipfs-gateway", "http://second/").IpfsGateway);
    }

    [TestMethod]
    [DataRow(":::")]
    [DataRow("foo://h:1/")]
    public void Parse_MalformedIpfsGateway_IsAcceptedForTheRewriterToRefuse(string gateway)
    {
        Assert.AreEqual(gateway, Accept("--ipfs-gateway", gateway).IpfsGateway);
    }

    [TestMethod]
    public void Parse_EmptyIpfsGateway_IsRefusedAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ipfs-gateway", "", Url]);

        AssertRefused(result, "curl: option --ipfs-gateway: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_IpfsGatewayLast_IsRefusedAsNeedingParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--ipfs-gateway"]);

        AssertRefused(result, "curl: option --ipfs-gateway: requires parameter");
    }

    [TestMethod]
    public void Parse_NegatedIpfsGateway_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-ipfs-gateway", "x", Url]);

        AssertRefused(result, "curl: option --no-ipfs-gateway: the given option cannot be reversed with a --no- prefix");
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
