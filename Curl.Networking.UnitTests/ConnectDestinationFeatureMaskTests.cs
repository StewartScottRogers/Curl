using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="ConnectToMappings" /> parses an IPv6 literal destination when the
/// run-time feature mask hides IPv6: curl 8.22.0 <c>parse_connect_to_host_port</c> without
/// <c>USE_IPV6</c> answers <c>CURLE_NOT_BUILT_IN</c> (upstream case 1454).
/// </summary>
[TestClass]
public sealed class ConnectDestinationFeatureMaskTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Map_WithIpv6HiddenAndBracketedDestination_FailsWithNotBuiltIn()
    {
        Diagnostics.Arrange("mapping", "localhost:80:[::1]:80");
        Diagnostics.Arrange("hidesIpv6", true);

        var destination = new ConnectToMappings(["localhost:80:[::1]:80"], hidesIpv6: true).Map("localhost", 80);

        Diagnostics.Act("parse error", destination.ParseError);
        Diagnostics.Act("parse error code", destination.ParseErrorCode);
        Diagnostics.Assert("parse error", ConnectToMappings.Ipv6NotBuiltInMessage, destination.ParseError);
        Diagnostics.Assert("parse error code", CurlExitCode.NotBuiltIn, destination.ParseErrorCode);
        Assert.AreEqual("Use of IPv6 in *_CONNECT_TO without IPv6 support built-in", destination.ParseError);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, destination.ParseErrorCode);
        Assert.IsFalse(destination.IsMapped);
    }

    [TestMethod]
    public void Map_WithIpv6HiddenAndUnclosedBracket_FailsWithNotBuiltInBeforeTheBracketCheck()
    {
        Diagnostics.Arrange("mapping", "localhost:80:[::1:80");

        var destination = new ConnectToMappings(["localhost:80:[::1:80"], hidesIpv6: true).Map("localhost", 80);

        Diagnostics.Act("parse error code", destination.ParseErrorCode);
        Diagnostics.Assert("parse error code", CurlExitCode.NotBuiltIn, destination.ParseErrorCode);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, destination.ParseErrorCode);
        Assert.AreEqual(ConnectToMappings.Ipv6NotBuiltInMessage, destination.ParseError);
    }

    [TestMethod]
    public void Map_WithIpv6HiddenAndNameDestination_MapsAsBefore()
    {
        Diagnostics.Arrange("mapping", "localhost:80:127.0.0.1:8080");

        var destination = new ConnectToMappings(["localhost:80:127.0.0.1:8080"], hidesIpv6: true).Map("localhost", 80);

        Diagnostics.Act("destination", destination);
        Diagnostics.Assert("destination", new ConnectDestination("127.0.0.1", 8080, IsMapped: true, ParseError: null), destination);
        Assert.AreEqual(new ConnectDestination("127.0.0.1", 8080, IsMapped: true, ParseError: null), destination);
    }

    [TestMethod]
    public void Map_WithoutTheMaskAndBracketedDestination_MapsToTheIpv6Literal()
    {
        Diagnostics.Arrange("mapping", "localhost:80:[::1]:80");
        Diagnostics.Arrange("hidesIpv6", false);

        var destination = new ConnectToMappings(["localhost:80:[::1]:80"], hidesIpv6: false).Map("localhost", 80);

        Diagnostics.Act("destination", destination);
        Diagnostics.Assert("destination", new ConnectDestination("::1", 80, IsMapped: true, ParseError: null), destination);
        Assert.AreEqual(new ConnectDestination("::1", 80, IsMapped: true, ParseError: null), destination);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, destination.ParseErrorCode);
    }

    [TestMethod]
    public void Map_WithTheDefaultConstructorAndBracketedDestination_MapsToTheIpv6Literal()
    {
        Diagnostics.Arrange("mapping", "localhost:80:[::1]:80");

        var destination = new ConnectToMappings(["localhost:80:[::1]:80"]).Map("localhost", 80);

        Diagnostics.Act("destination", destination);
        Diagnostics.Assert("destination", new ConnectDestination("::1", 80, IsMapped: true, ParseError: null), destination);
        Assert.AreEqual(new ConnectDestination("::1", 80, IsMapped: true, ParseError: null), destination);
    }
}
