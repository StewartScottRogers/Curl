namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="ConnectToMappings" /> matches and parses <c>--connect-to</c> mappings
/// against curl 8.21.0 (measured; the commands are in BL-214's Notes).
/// </summary>
[TestClass]
public sealed class ConnectToMappingsTests
{
    [TestMethod]
    public void Constructor_WithNullMappings_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new ConnectToMappings(null!));

        Assert.AreEqual("mappings", exception.ParamName);
    }

    [TestMethod]
    public void Map_WithNullHost_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ConnectToMappings.None.Map(null!, 80));

        Assert.AreEqual("host", exception.ParamName);
    }

    [TestMethod]
    public void Map_WithNoMappings_KeepsTheHostAndPortUnmapped()
    {
        Assert.AreEqual(
            new ConnectDestination("a", 80, IsMapped: false, ParseError: null),
            ConnectToMappings.None.Map("a", 80));
    }

    [TestMethod]
    [DataRow("a:80:127.0.0.1:9", "127.0.0.1", 9)] // Trying 127.0.0.1:9
    [DataRow("A:80:127.0.0.1:9", "127.0.0.1", 9)] // Trying 127.0.0.1:9
    [DataRow("::127.0.0.1:", "127.0.0.1", 80)] // Trying 127.0.0.1:80
    [DataRow("a::[::1]:9", "::1", 9)] // Trying [::1]:9
    [DataRow("a:80::9", "a", 9)] // Host a:9 was resolved
    [DataRow("a:80:127.0.0.8", "127.0.0.8", 80)] // Trying 127.0.0.8:80
    [DataRow("a:80:127.0.0.8:", "127.0.0.8", 80)] // Trying 127.0.0.8:80
    [DataRow("a:80:127.0.0.8:0", "127.0.0.8", 0)] // Trying 127.0.0.8:0
    [DataRow("a:80:127.0.0.8:0080", "127.0.0.8", 80)] // Trying 127.0.0.8:80
    [DataRow("a:80:127.0.0.8:9x", "127.0.0.8", 9)] // Trying 127.0.0.8:9
    [DataRow("a:80:127.0.0.8:65535", "127.0.0.8", 65535)] // Trying 127.0.0.8:65535
    [DataRow("a:80:[::1]", "::1", 80)] // Trying [::1]:80
    [DataRow("a:80:[::1]x:9", "::1", 9)] // Trying [::1]:9
    [DataRow("a:80::", "a", 80)] // resolves a
    [DataRow("a:080:b:9", "b", 9)]
    [DataRow("a:0000000000080:127.0.0.8:9", "127.0.0.8", 9)] // Trying 127.0.0.8:9
    [DataRow("a:80:127.0.0.8:0000000000080", "127.0.0.8", 80)] // Trying 127.0.0.8:80
    [DataRow("a:80:127.0.0.8:000", "127.0.0.8", 0)]
    public void Map_WithAMatchingMapping_ReturnsItsDestination(string mapping, string host, int port)
    {
        // curl --connect-to <mapping> http://a/
        var mappings = new ConnectToMappings([mapping]);

        Assert.AreEqual(new ConnectDestination(host, port, IsMapped: true, ParseError: null), mappings.Map("a", 80));
    }

    [TestMethod]
    [DataRow("a:81:127.0.0.1:9")]
    [DataRow("b:80:127.0.0.1:9")]
    [DataRow("a:x:127.0.0.1:9")] // resolves a
    [DataRow("a:80")] // resolves a
    [DataRow("garbage")]
    [DataRow("")]
    [DataRow("[::1:80:127.0.0.1:9")]
    [DataRow("b:80:c:x")] // resolves a: a mapping that does not match is never parsed
    public void Map_WithAMappingThatDoesNotMatch_KeepsTheHostAndPortUnmapped(string mapping)
    {
        var mappings = new ConnectToMappings([mapping]);

        Assert.AreEqual(new ConnectDestination("a", 80, IsMapped: false, ParseError: null), mappings.Map("a", 80));
    }

    [TestMethod]
    [DataRow("[::1]")]
    [DataRow("::1")]
    public void Map_WithABracketedIPv6Host_MatchesTheUrlsHost(string urlHost)
    {
        // curl --connect-to [::1]:80:127.0.0.9:9 http://[::1]/ -> Trying 127.0.0.9:9
        var mappings = new ConnectToMappings(["[::1]:80:127.0.0.9:9"]);

        Assert.AreEqual(new ConnectDestination("127.0.0.9", 9, IsMapped: true, ParseError: null), mappings.Map(urlHost, 80));
    }

    [TestMethod]
    public void Map_UsesTheFirstMatchingMapping()
    {
        // curl --connect-to a:81:127.0.0.1:9 --connect-to a:80:127.0.0.6:9 --connect-to a:80:127.0.0.7:9 http://a/ -> Trying 127.0.0.6:9
        var mappings = new ConnectToMappings(["a:81:127.0.0.1:9", "a:80:127.0.0.6:9", "a:80:127.0.0.7:9"]);

        Assert.AreEqual(new ConnectDestination("127.0.0.6", 9, IsMapped: true, ParseError: null), mappings.Map("a", 80));
    }

    [TestMethod]
    public void Map_AMatchingMappingThatChangesNothing_StillStopsTheSearch()
    {
        // curl --connect-to a:80:: --connect-to a:80:127.0.0.7:9 http://a/ -> resolves a
        var mappings = new ConnectToMappings(["a:80::", "a:80:127.0.0.7:9"]);

        Assert.AreEqual(new ConnectDestination("a", 80, IsMapped: true, ParseError: null), mappings.Map("a", 80));
    }

    [TestMethod]
    [DataRow("a:80:b:x", "No valid port number in 'b:x'")]
    [DataRow("a:80:b:99999", "No valid port number in 'b:99999'")]
    [DataRow("a:80:127.0.0.8:-1", "No valid port number in '127.0.0.8:-1'")]
    [DataRow("a:80:127.0.0.8:+0", "No valid port number in '127.0.0.8:+0'")]
    [DataRow("a:80:127.0.0.8:-0", "No valid port number in '127.0.0.8:-0'")]
    [DataRow("a:80:127.0.0.8:65536", "No valid port number in '127.0.0.8:65536'")]
    [DataRow("a:80:127.0.0.8:1234567890", "No valid port number in '127.0.0.8:1234567890'")] // measured
    [DataRow("a:80:127.0.0.8:100000", "No valid port number in '127.0.0.8:100000'")]
    [DataRow("a:80:[::1:9", "Invalid IPv6 address format in '[::1:9'")]
    public void Map_WhenTheMatchingDestinationDoesNotParse_ReportsCurlsExit49Message(string mapping, string message)
    {
        // curl --connect-to <mapping> http://a/ -> curl: (49) <message>
        var mappings = new ConnectToMappings([mapping]);

        Assert.AreEqual(message, mappings.Map("a", 80).ParseError);
    }
}
