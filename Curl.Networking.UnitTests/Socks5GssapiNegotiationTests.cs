using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class Socks5GssapiNegotiationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("rcmd", "rcmd", "proxy.example", DisplayName = "A service is joined to the proxy's host")]
    [DataRow("", "", "proxy.example", DisplayName = "An empty service too")]
    [DataRow("socks/gw.example", "socks", "gw.example", DisplayName = "A service with a slash is the whole target")]
    [DataRow("a/b/c", "a", "b/c", DisplayName = "Split at the first slash")]
    public void RequestFor_NamesTheAcceptorAsCurlDoes(string service, string serviceName, string hostName)
    {
        Diagnostics.Arrange("proxy host, service", $"proxy.example, {service}");

        var request = Socks5GssapiNegotiation.RequestFor("proxy.example", Socks5AuthenticationOptions.Default with { GssapiServiceName = service });

        Diagnostics.Act("service name, host name", (request.ServiceName, request.HostName));
        Diagnostics.Assert("service name, host name", (serviceName, hostName), (request.ServiceName, request.HostName));

        Assert.AreEqual((serviceName, hostName), (request.ServiceName, request.HostName));
    }
}
