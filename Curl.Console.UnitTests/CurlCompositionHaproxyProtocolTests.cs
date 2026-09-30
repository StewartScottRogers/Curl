using Curl.Cli;
using Curl.Networking;

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
    [TestMethod]
    public void HaproxyProtocolOf_WithNeitherOption_IsNull() =>
        Assert.IsNull(CurlComposition.HaproxyProtocolOf(Parse("http://h/")));

    [TestMethod]
    public void HaproxyProtocolOf_WithHaproxyProtocolThenItsNegation_IsNull() =>
        Assert.IsNull(CurlComposition.HaproxyProtocolOf(Parse("--haproxy-protocol", "--no-haproxy-protocol", "http://h/")));

    [TestMethod]
    public void HaproxyProtocolOf_WithHaproxyProtocol_SendsTheConnectionsOwnAddress() =>
        Assert.AreEqual(new HaproxyProtocolHeader(null), CurlComposition.HaproxyProtocolOf(Parse("--haproxy-protocol", "http://h/")));

    [TestMethod]
    [DataRow("--haproxy-clientip", "1.2.3.4", "http://h/")]
    [DataRow("--haproxy-protocol", "--haproxy-clientip", "1.2.3.4", "http://h/")]
    [DataRow("--haproxy-clientip", "1.2.3.4", "--no-haproxy-protocol", "http://h/")]
    public void HaproxyProtocolOf_WithAClientIp_SendsTheClientIp(params string[] arguments) =>
        Assert.AreEqual(new HaproxyProtocolHeader("1.2.3.4"), CurlComposition.HaproxyProtocolOf(Parse(arguments)));

    [TestMethod]
    public void CreateTransports_WithHaproxyProtocol_GivesTheTcpConnectorTheHeader()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--haproxy-protocol", "http://h/"));

        Assert.AreEqual(new HaproxyProtocolHeader(null), transports.TcpConnector.HaproxyProtocol);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
