namespace Curl.Networking;

[TestClass]
public sealed class Socks5GssapiNegotiationTests
{
    [TestMethod]
    [DataRow("rcmd", "rcmd", "proxy.example", DisplayName = "A service is joined to the proxy's host")]
    [DataRow("", "", "proxy.example", DisplayName = "An empty service too")]
    [DataRow("socks/gw.example", "socks", "gw.example", DisplayName = "A service with a slash is the whole target")]
    [DataRow("a/b/c", "a", "b/c", DisplayName = "Split at the first slash")]
    public void RequestFor_NamesTheAcceptorAsCurlDoes(string service, string serviceName, string hostName)
    {
        var request = Socks5GssapiNegotiation.RequestFor("proxy.example", Socks5AuthenticationOptions.Default with { GssapiServiceName = service });

        Assert.AreEqual((serviceName, hostName), (request.ServiceName, request.HostName));
    }
}
