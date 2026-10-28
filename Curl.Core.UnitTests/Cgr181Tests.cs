using Curl.Protocol.Abstractions;

namespace Curl.Core.UnitTests;

[TestClass]
public sealed class Cgr181Tests
{
    [TestMethod]
    [DataRow(":")]
    [DataRow("::1")]
    [DataRow("::::")]
    public void TryParse_TextStartingWithColon_ReportsNoHostPresent(string text)
    {
        bool parsed = ProxyUrlParser.TryParse(text, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsFalse(parsed);
        Assert.IsNull(proxy);
        Assert.AreEqual($"Unsupported proxy syntax in '{text}': No host present", failure!.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_ZeroHost_IsNormalisedToZeroDotZeroDotZeroDotZero()
    {
        bool parsed = ProxyUrlParser.TryParse("0", out ProxyEndpoint? proxy, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual("0.0.0.0", proxy!.Host);
    }
}
