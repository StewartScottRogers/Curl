namespace Curl.Extension.Dane;

[TestClass]
public sealed class DaneResolverAddressTests
{
    [TestMethod]
    [DataRow("192.0.2.53", "192.0.2.53", 53)]
    [DataRow("192.0.2.53:5353", "192.0.2.53", 5353)]
    [DataRow("resolver.example", "resolver.example", 53)]
    [DataRow("resolver.example:54", "resolver.example", 54)]
    [DataRow("2001:db8::53", "2001:db8::53", 53)]
    [DataRow("[2001:db8::53]", "2001:db8::53", 53)]
    [DataRow("[2001:db8::53]:5353", "2001:db8::53", 5353)]
    public void Parse_HostAndOptionalPort_GivesTheAddress(string text, string host, int port)
    {
        Assert.AreEqual(new DaneResolverAddress(host, port), DaneResolverAddress.Parse(text));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("resolver.example/path")]
    [DataRow("resolver.example?query")]
    [DataRow("resolver.example#fragment")]
    [DataRow("user@resolver.example")]
    [DataRow("resolver.example:99999")]
    [DataRow("a:b")]
    public void Parse_NotAHostAndPort_GivesNull(string text)
    {
        Assert.IsNull(DaneResolverAddress.Parse(text));
    }
}
