using Curl.Extended.Protocol.DnsQuery.Wire;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests;

[TestClass]
public sealed class DnsJsonTxtEscapingTests
{
    [TestMethod]
    [DataRow("hello", "hello")]
    [DataRow("world \\\"q\\\" \\\\", "world \"q\" \\")]
    [DataRow(@"\255x", "\u00FFx")]
    [DataRow("trailing\\", "trailing\\")]
    [DataRow(@"\12", "12")]
    public void RawCharacterString_PresentationText_GivesRawText(string presentation, string expected)
    {
        Assert.AreEqual(expected, DnsPresentation.RawCharacterString(presentation));
    }
}
