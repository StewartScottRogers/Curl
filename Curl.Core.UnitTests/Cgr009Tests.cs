namespace Curl.Core;

/// <summary>
/// Pins CGR-009 of the 2026-10-25 gap report: an exemption entry with an empty dotted label
/// (<c>.1</c>, <c>.0.0.1</c>, <c>foo,.x</c>) does not match an IPv4 literal host, as curl
/// 8.22.0 sends such a transfer through the proxy, where the port used to throw
/// <see cref="IndexOutOfRangeException" /> and exit 2.
/// </summary>
[TestClass]
public sealed class Cgr009Tests
{
    [TestMethod]
    [DataRow(".1")]
    [DataRow(".0.0.1")]
    [DataRow("foo,.x")]
    [DataRow("127..0.1")]
    [DataRow("127.0.0.")]
    [DataRow(".0.0.1/8")]
    public void Matches_EntryWithAnEmptyDottedLabel_DoesNotMatchAnIPv4Host(string noProxy)
    {
        bool matches = NoProxyMatcher.Matches("127.0.0.1", noProxy);

        Assert.IsFalse(matches);
    }
}
