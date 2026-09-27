namespace Curl.Core;

/// <summary>
/// Pins how <see cref="NoProxyMatcher" /> reads an exemption list, each case measured
/// against curl 8.21.0 on 2026-09-26 as
/// <c>http_proxy=http://127.0.0.1:1111 curl -sv --connect-timeout 1 --resolve '*:2222:127.0.0.1' --noproxy "&lt;list&gt;" http://&lt;host&gt;:2222/</c>,
/// reading whether it tried port 1111 (proxy) or 2222 (direct).
/// </summary>
[TestClass]
public sealed class NoProxyMatcherTests
{
    [TestMethod]
    [DataRow("example.com", "example.com")]
    [DataRow("example.com", "www.example.com")]
    [DataRow(".example.com", "example.com")]
    [DataRow(".example.com", "www.example.com")]
    [DataRow("example.com.", "example.com")]
    [DataRow("example.com", "example.com.")]
    [DataRow("EXAMPLE.com", "Example.COM")]
    [DataRow("example.com", "a.b.example.com")]
    [DataRow("com", "example.com")]
    [DataRow(".com", "example.com")]
    [DataRow("localhost", "localhost")]
    [DataRow("localhost.", "localhost")]
    [DataRow("*", "example.com")]
    [DataRow("foo,example.com", "example.com")]
    [DataRow("foo, example.com", "example.com")]
    [DataRow("foo ,example.com", "example.com")]
    [DataRow("foo,,,example.com", "example.com")]
    [DataRow("\texample.com", "example.com")]
    [DataRow("example.com,*", "www.example.com")]
    [DataRow(",example.com", "example.com")]
    [DataRow(" ,example.com", "example.com")]
    [DataRow("example.com,", "example.com")]
    [DataRow("example.com ", "example.com")]
    [DataRow("127.0.0.1", "127.0.0.1")]
    [DataRow("127.0.0.1/32", "127.0.0.1")]
    [DataRow("127.0.0.0/31", "127.0.0.1")]
    [DataRow("127.0.0.0/8", "127.0.0.5")]
    [DataRow("127.0.0.0/08", "127.0.0.5")]
    [DataRow("127.0.0.1/0", "127.0.0.1")]
    [DataRow("127.0.0.0/1", "126.0.0.1")]
    [DataRow("127.0.0.0/1", "1.0.0.1")]
    [DataRow("128.0.0.0/1", "200.0.0.1")]
    [DataRow("::1", "::1")]
    [DataRow("::1/128", "::1")]
    [DataRow("::1/0", "::1")]
    [DataRow("fe80::/10", "fe80::1")]
    [DataRow("fe80::/64", "fe80::1")]
    public void Matches_MeasuredExemption_IsDirect(string noProxy, string hostName)
    {
        Assert.IsTrue(NoProxyMatcher.Matches(hostName, noProxy));
    }

    [TestMethod]
    [DataRow("example.com", "nonexample.com")]
    [DataRow("www.example.com", "example.com")]
    [DataRow("b.example.com", "xb.example.com")]
    [DataRow("*.example.com", "www.example.com")]
    [DataRow("*,foo", "example.com")]
    [DataRow("foo,*", "example.com")]
    [DataRow(" * ", "example.com")]
    [DataRow("foo example.com", "example.com")]
    [DataRow("foo\texample.com", "example.com")]
    [DataRow("foo,", "example.com")]
    [DataRow("", "example.com")]
    [DataRow(".", "example.com")]
    [DataRow("a.test:2222", "a.test")]
    [DataRow("x", "localhost")]
    [DataRow("x", "127.0.0.1")]
    [DataRow("localhost", "127.0.0.1")]
    [DataRow("127.0.0.1", "localhost")]
    [DataRow("127.0.0.1", "127.0.0.2")]
    [DataRow("127.1", "127.0.0.1")]
    [DataRow("0x7f.0.0.1", "127.0.0.1")]
    [DataRow("127.0.0", "127.0.0.5")]
    [DataRow("127.0.0.0/24", "127.0.1.5")]
    [DataRow("127.0.0.0/31", "127.0.0.2")]
    [DataRow("127.0.0.0/33", "127.0.0.5")]
    [DataRow("127.0.0.0/0", "127.0.0.5")]
    [DataRow("127.0.0.1/", "127.0.0.1")]
    [DataRow("127.0.0.0/abc", "127.0.0.0")]
    [DataRow("127.0.0.0/8x", "127.0.0.5")]
    [DataRow("127.0.0.0/-1", "127.0.0.5")]
    [DataRow("127.0.0.0/+8", "127.0.0.5")]
    [DataRow("127.0.0.1/0x8", "127.0.0.1")]
    [DataRow("abc/8", "127.0.0.1")]
    [DataRow("[::1]", "::1")]
    [DataRow("::/0", "::1")]
    [DataRow("::1/abc", "::1")]
    [DataRow("fe80::/129", "fe80::1")]
    [DataRow("fe80::/64", "fe80:0:0:1::1")]
    [DataRow("fe80::1%eth0", "fe80::1")]
    [DataRow("::1", "127.0.0.1")]
    public void Matches_MeasuredNonExemption_UsesProxy(string noProxy, string hostName)
    {
        Assert.IsFalse(NoProxyMatcher.Matches(hostName, noProxy));
    }

    [TestMethod]
    [DataRow("127.0.0.1", "::1")]
    [DataRow("1.2.3.4.5", "1.2.3.4")]
    [DataRow("01.2.3.4", "1.2.3.4")]
    [DataRow("256.0.0.1", "1.2.3.4")]
    [DataRow("1.2.3.4/99999999999", "1.2.3.4")]
    public void Matches_AddressEntryThatCannotMatch_IsFalse(string noProxy, string hostName)
    {
        Assert.IsFalse(NoProxyMatcher.Matches(hostName, noProxy));
    }

    [TestMethod]
    public void Matches_PrefixBitsWithinAByte_ComparesThoseBits()
    {
        Assert.IsTrue(NoProxyMatcher.Matches("10.0.0.130", "10.0.0.128/25"));
        Assert.IsFalse(NoProxyMatcher.Matches("10.0.0.127", "10.0.0.128/25"));
    }

    [TestMethod]
    public void Matches_NullList_IsFalse()
    {
        Assert.IsFalse(NoProxyMatcher.Matches("example.com", null));
    }

    [TestMethod]
    public void Matches_EmptyHost_IsFalse()
    {
        Assert.IsFalse(NoProxyMatcher.Matches(string.Empty, "*"));
    }

    [TestMethod]
    public void Matches_NullHost_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NoProxyMatcher.Matches(null!, "*"));
    }
}
