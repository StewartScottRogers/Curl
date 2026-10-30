using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque;

/// <summary>Checks URI template expansion against RFC 9298 section 2, RFC 9484 section 3 and RFC 6570.</summary>
[TestClass]
public sealed class MasqueUriTemplateTests
{
    private const string UdpWellKnown = "https://example.org/.well-known/masque/udp/{target_host}/{target_port}/";

    [TestMethod]
    public void ExpandConnectUdp_WellKnownTemplate_PutsHostAndPortInThePath() =>
        Assert.AreEqual("https://example.org/.well-known/masque/udp/192.0.2.6/443/", MasqueUriTemplate.ExpandConnectUdp(UdpWellKnown, "192.0.2.6", 443).AbsoluteUri);

    [TestMethod]
    [DataRow("::1")]
    [DataRow("[::1]")]
    public void ExpandConnectUdp_IPv6Host_IsPercentEncodedWithoutBrackets(string host) =>
        Assert.AreEqual("https://example.org/.well-known/masque/udp/%3A%3A1/53/", MasqueUriTemplate.ExpandConnectUdp(UdpWellKnown, host, 53).AbsoluteUri);

    [TestMethod]
    public void ExpandConnectUdp_FormQuery_GivesNameValuePairs() =>
        Assert.AreEqual(
            "https://proxy.example.org:4443/masque?target_host=example.com&target_port=53",
            MasqueUriTemplate.ExpandConnectUdp("https://proxy.example.org:4443/masque{?target_host,target_port}", "example.com", 53).AbsoluteUri);

    [TestMethod]
    public void ExpandConnectUdp_FormContinuation_AppendsToTheQuery() =>
        Assert.AreEqual(
            "https://proxy.example.org/masque?user=bob&target_host=a.example&target_port=1",
            MasqueUriTemplate.ExpandConnectUdp("https://proxy.example.org/masque?user=bob{&target_host,target_port}", "a.example", 1).AbsoluteUri);

    [TestMethod]
    public void ExpandConnectUdp_SimpleList_JoinsWithCommas() =>
        Assert.AreEqual("http://proxy.example/u/a.example,53", MasqueUriTemplate.ExpandConnectUdp("http://proxy.example/u/{target_host,target_port}", "a.example", 53).AbsoluteUri);

    [TestMethod]
    [DataRow("https://example.org/masque/{target_host}/")]
    [DataRow("https://example.org/masque/")]
    public void ExpandConnectUdp_TemplateMissingEitherOfThePair_IsRefused(string template)
    {
        var exception = Assert.ThrowsExactly<MasqueException>(() => MasqueUriTemplate.ExpandConnectUdp(template, "a.example", 53));
        StringAssert.Contains(exception.Message, "must name target_host and target_port");
        Assert.IsNull(exception.StatusCode);
    }

    [TestMethod]
    public void ExpandConnectUdp_UnknownVariable_IsRefused() =>
        StringAssert.Contains(
            Assert.ThrowsExactly<MasqueException>(() => MasqueUriTemplate.ExpandConnectUdp("https://example.org/{target}/{target_host}/{target_port}/", "a", 1)).Message,
            "unknown variable 'target'");

    [TestMethod]
    public void ExpandConnectUdp_UnclosedExpression_IsRefused() =>
        StringAssert.Contains(
            Assert.ThrowsExactly<MasqueException>(() => MasqueUriTemplate.ExpandConnectUdp("https://example.org/{target_host}/{target_port", "a", 1)).Message,
            "unclosed expression");

    [TestMethod]
    [DataRow("/masque/{target_host}/{target_port}/")]
    [DataRow("ftp://example.org/{target_host}/{target_port}/")]
    public void ExpandConnectUdp_NotAnHttpUri_IsRefused(string template) =>
        StringAssert.Contains(
            Assert.ThrowsExactly<MasqueException>(() => MasqueUriTemplate.ExpandConnectUdp(template, "a", 1)).Message,
            "does not expand to an http or https URI");

    [TestMethod]
    public void ExpandConnectIp_WellKnownTemplate_EncodesThePrefixAndTheWildcard() =>
        Assert.AreEqual(
            "https://example.org/.well-known/masque/ip/192.0.2.0%2F24/%2A/",
            MasqueUriTemplate.ExpandConnectIp("https://example.org/.well-known/masque/ip/{target}/{ipproto}/", "192.0.2.0/24", "*").AbsoluteUri);

    [TestMethod]
    public void ExpandConnectIp_OnlyIpproto_IsEnough() =>
        Assert.AreEqual("https://example.org/ip?ipproto=17", MasqueUriTemplate.ExpandConnectIp("https://example.org/ip{?ipproto}", "*", "17").AbsoluteUri);

    [TestMethod]
    public void ExpandConnectIp_TemplateNamingNeither_IsRefused() =>
        StringAssert.Contains(
            Assert.ThrowsExactly<MasqueException>(() => MasqueUriTemplate.ExpandConnectIp("https://masque.example.org/?user=bob", "*", "*")).Message,
            "must name target or ipproto");

    [TestMethod]
    public void ExpandConnectIp_ConnectUdpTemplate_IsRefused() =>
        StringAssert.Contains(
            Assert.ThrowsExactly<MasqueException>(() => MasqueUriTemplate.ExpandConnectIp(UdpWellKnown, "*", "*")).Message,
            "unknown variable 'target_host'");
}
