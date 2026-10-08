using Curl.Cli;
using Curl.Networking;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the composition hands <c>--haproxy-protocol</c> and <c>--haproxy-clientip</c> to the TCP
/// connector as a <see cref="HaproxyProtocolHeader" /> (BL-616): <c>--haproxy-clientip</c> alone turns
/// the line on, even after <c>--no-haproxy-protocol</c>, as curl 8.21.0 does (measured, BL-616 Notes:
/// <c>--haproxy-clientip 1.2.3.4 --no-haproxy-protocol</c> still sent <c>PROXY TCP4 1.2.3.4 ...</c>).
/// </summary>
[TestClass]
public sealed class CurlCompositionHaproxyProtocolTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void HaproxyProtocolOf_WithNeitherOption_IsNull()
    {
        HaproxyProtocolHeader? header = HaproxyProtocolOf("http://h/");

        Diagnostics.Assert("header is null", true, header is null);
        Assert.IsNull(header);
    }

    [TestMethod]
    public void HaproxyProtocolOf_WithHaproxyProtocolThenItsNegation_IsNull()
    {
        HaproxyProtocolHeader? header = HaproxyProtocolOf("--haproxy-protocol", "--no-haproxy-protocol", "http://h/");

        Diagnostics.Assert("header is null", true, header is null);
        Assert.IsNull(header);
    }

    [TestMethod]
    public void HaproxyProtocolOf_WithHaproxyProtocol_SendsTheConnectionsOwnAddress()
    {
        HaproxyProtocolHeader? header = HaproxyProtocolOf("--haproxy-protocol", "http://h/");

        Diagnostics.Assert("header", new HaproxyProtocolHeader(null), header);
        Assert.AreEqual(new HaproxyProtocolHeader(null), header);
    }

    [TestMethod]
    [DataRow("--haproxy-clientip", "1.2.3.4", "http://h/")]
    [DataRow("--haproxy-protocol", "--haproxy-clientip", "1.2.3.4", "http://h/")]
    [DataRow("--haproxy-clientip", "1.2.3.4", "--no-haproxy-protocol", "http://h/")]
    public void HaproxyProtocolOf_WithAClientIp_SendsTheClientIp(params string[] arguments)
    {
        HaproxyProtocolHeader? header = HaproxyProtocolOf(arguments);

        Diagnostics.Assert("header", new HaproxyProtocolHeader("1.2.3.4"), header);
        Assert.AreEqual(new HaproxyProtocolHeader("1.2.3.4"), header);
    }

    [TestMethod]
    public void CreateTransports_WithHaproxyProtocol_GivesTheTcpConnectorTheHeader()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--haproxy-protocol", "http://h/"));
        Diagnostics.Act("TCP connector header", transports.TcpConnector.HaproxyProtocol);

        Diagnostics.Assert("TCP connector header", new HaproxyProtocolHeader(null), transports.TcpConnector.HaproxyProtocol);
        Assert.AreEqual(new HaproxyProtocolHeader(null), transports.TcpConnector.HaproxyProtocol);
    }

    private HaproxyProtocolHeader? HaproxyProtocolOf(params string[] arguments)
    {
        HaproxyProtocolHeader? header = CurlComposition.HaproxyProtocolOf(Parse(arguments));
        Diagnostics.Act("header", header?.ToString() ?? "(null)");
        return header;
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
