namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>An <c>oci://</c> hop's host is matched against the no-proxy list as curl 8.21.0's <c>Curl_check_noproxy</c> matches it (ADR-1290).</summary>
[TestClass]
public sealed class HopNoProxyMatcherTests
{
    [TestMethod]
    [DataRow("*", "anything.example")]
    [DataRow("example.com", "example.com")]
    [DataRow("example.com", "www.example.com")]
    [DataRow(".example.com", "www.example.com")]
    [DataRow("example.com.", "example.com")]
    [DataRow("example.com", "example.com.")]
    [DataRow("EXAMPLE.com", "Example.COM")]
    [DataRow(" other.test , example.com", "example.com")]
    [DataRow(",,example.com", "example.com")]
    [DataRow("127.0.0.1", "127.0.0.1")]
    [DataRow("10.0.0.0/8", "10.1.2.3")]
    [DataRow("192.168.0.0/23", "192.168.1.9")]
    [DataRow("::1", "[::1]")]
    [DataRow("2001:db8::/32", "2001:db8::5")]
    [DataRow("10.0.0.0/0", "10.0.0.0")]
    public void Matches_HostTheListExempts_IsTrue(string noProxy, string host) =>
        Assert.IsTrue(HopNoProxyMatcher.Matches(host, noProxy));

    [TestMethod]
    [DataRow(null, "example.com")]
    [DataRow("", "example.com")]
    [DataRow("*,example.org", "example.com")]
    [DataRow("example.com", "nonexample.com")]
    [DataRow("www.example.com", "example.com")]
    [DataRow("foo example.com", "example.com")]
    [DataRow("localhost", "127.0.0.1")]
    [DataRow("127.0.0.1", "127.0.0.2")]
    [DataRow("10.0.0.0/x", "10.0.0.1")]
    [DataRow("10.0.0.0/33", "10.0.0.1")]
    [DataRow("192.168.0.0/23", "192.168.2.1")]
    [DataRow("::1", "127.0.0.1")]
    [DataRow("[::1]", "[::1]")]
    [DataRow("fe80::1%eth0", "fe80::1")]
    [DataRow("127.0.0.1", "127.000.0.1")]
    [DataRow("127.0.0.1", "1270.0.0.1")]
    [DataRow("127.0.0.1", "127..0.1")]
    [DataRow("127.0.0.1", "127.0.0.256")]
    public void Matches_HostTheListDoesNotExempt_IsFalse(string? noProxy, string host) =>
        Assert.IsFalse(HopNoProxyMatcher.Matches(host, noProxy));
}
