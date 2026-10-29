namespace Curl.Engine;

/// <summary>
/// CGR-283: a <c>--doh-url</c> holding a space or a TAB is refused, so no DoH request is sent.
/// </summary>
[TestClass]
public sealed class Cgr283Tests
{
    [TestMethod]
    [DataRow("https://127.0.0.1:21800/dns query")]
    [DataRow("https://127.0.0.1:21800/a\tb")]
    [DataRow("127.0.0.1/dns query")]
    public void DohUrlOf_AValueWithASpaceOrControlCharacter_IsNone(string value)
    {
        Assert.IsNull(EngineComposition.DohUrlOf(value));
        Assert.IsFalse(EngineComposition.IsHttpsDohUrl(value));
    }
}
