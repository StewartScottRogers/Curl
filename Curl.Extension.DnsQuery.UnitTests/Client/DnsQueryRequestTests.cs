using Curl.Extension.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Client;

/// <summary>Checks how a <c>dns:</c> URL is read (RFC 4501 section 2).</summary>
[TestClass]
public sealed class DnsQueryRequestTests
{
    [TestMethod]
    public void Of_ServerAndName_QueriesTypeAClassInOnPort53()
    {
        DnsQueryRequest request = DnsQueryRequest.Of(CurlUrl.Parse("dns://192.0.2.53/www.example.com"));

        Assert.AreEqual(new DnsQueryRequest("192.0.2.53", 53, "www.example.com", 1, 1), request);
    }

    [TestMethod]
    public void Of_PortInTheAuthority_QueriesThatPort() =>
        Assert.AreEqual(5353, DnsQueryRequest.Of(CurlUrl.Parse("dns://ns.example.net:5353/example.com")).Port);

    [TestMethod]
    public void Of_NoPortInTheAuthority_QueriesTheTransportsDefaultPort() =>
        Assert.AreEqual(853, DnsQueryRequest.Of(CurlUrl.Parse("dns://ns.example.net/example.com"), 853).Port);

    [TestMethod]
    public void Of_PortInTheAuthority_QueriesThatPortOverTheTransportsDefault() =>
        Assert.AreEqual(5353, DnsQueryRequest.Of(CurlUrl.Parse("dns://ns.example.net:5353/example.com"), 853).Port);

    [TestMethod]
    [DataRow("dns://h/example.com?type=AAAA", (ushort)28, (ushort)1)]
    [DataRow("dns://h/example.com?TYPE=mx;CLASS=ch", (ushort)15, (ushort)3)]
    [DataRow("dns://h/example.com?class=IN;type=TYPE65", (ushort)65, (ushort)1)]
    [DataRow("dns://h/example.com?type=TXT&class=CLASS4", (ushort)16, (ushort)4)]
    [DataRow("dns://h/example.com?", (ushort)1, (ushort)1)]
    public void Of_QueryElements_SetTheTypeAndClass(string url, ushort type, ushort recordClass)
    {
        DnsQueryRequest request = DnsQueryRequest.Of(CurlUrl.Parse(url));

        Assert.AreEqual((type, recordClass), (request.Type, request.Class));
    }

    [TestMethod]
    public void Of_PercentEncodedName_IsDecoded() =>
        Assert.AreEqual(@"a\.b.example.com", DnsQueryRequest.Of(CurlUrl.Parse("dns://h/a%5C.b.example.com")).Name);

    [TestMethod]
    public void Of_NoAuthority_NamesNoServer()
    {
        Assert.IsTrue(CurlUrl.TryParseAllowingEmptyHost("dns:///example.com", false, false, out CurlUrl? url, out _));

        DnsQueryRequest request = DnsQueryRequest.Of(url);

        Assert.IsNull(request.Server);
        Assert.AreEqual("example.com", request.Name);
    }

    [TestMethod]
    [DataRow("dns://h/")]
    [DataRow("dns://h/example.com?type=NOPE")]
    [DataRow("dns://h/example.com?class=NOPE")]
    [DataRow("dns://h/example.com?type")]
    [DataRow("dns://h/example.com?ttl=5")]
    public void Of_NoNameOrAnUnknownQueryElement_FailsWithExit3(string url) =>
        Assert.AreEqual(CurlExitCode.UrlMalformat, Assert.ThrowsExactly<DnsQueryException>(() => DnsQueryRequest.Of(CurlUrl.Parse(url))).ExitCode);
}
