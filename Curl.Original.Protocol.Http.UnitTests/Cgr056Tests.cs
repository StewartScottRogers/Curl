using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-056 (BL-3585): the absolute-form target a forward proxy is sent keeps an IPv6 host's
/// zone id as <c>%25</c> and the id, while the <c>Host</c> line leaves it out, as curl 8.22.0 sends
/// (measured: <c>GET http://[fe80::1%25eth0]:8080/x HTTP/1.1</c> with <c>Host: [fe80::1]:8080</c>).
/// </summary>
[TestClass]
public sealed class Cgr056Tests
{
    [TestMethod]
    [DataRow("http://[fe80::1%25eth0]:8080/x", "GET http://[fe80::1%25eth0]:8080/x HTTP/1.1", "Host: [fe80::1]:8080")]
    [DataRow("http://[::1%25lo]:8080/a", "GET http://[::1%25lo]:8080/a HTTP/1.1", "Host: [::1]:8080")]
    [DataRow("http://[::1%25lo]/a", "GET http://[::1%25lo]/a HTTP/1.1", "Host: [::1]")]
    [DataRow("http://[::1]:8080/a", "GET http://[::1]:8080/a HTTP/1.1", "Host: [::1]:8080")]
    public void Format_ForwardProxy_KeepsTheZoneIdInTheTargetButNotInHost(string url, string requestLine, string host)
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(url), new HttpRequestOptions(), forwardProxy: true);

        string[] lines = Encoding.Latin1.GetString(head).Split("\r\n");
        Assert.AreEqual(requestLine, lines[0]);
        Assert.AreEqual(host, lines[1]);
    }

    [TestMethod]
    public void Format_Origin_SendsOnlyThePathForAZonedHost()
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://[fe80::1%25eth0]:8080/x"), new HttpRequestOptions());

        Assert.StartsWith("GET /x HTTP/1.1\r\nHost: [fe80::1]:8080\r\n", Encoding.Latin1.GetString(head));
    }
}
