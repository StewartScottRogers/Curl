namespace Curl.Protocol.Abstractions.UnitTests;

[TestClass]
public sealed class CurlUrlHostTests
{
    [TestMethod]
    [DataRow("[::1")]
    [DataRow("[")]
    public void TryNormalize_BracketNotClosed_IsBadIPv6(string host)
    {
        bool accepted = CurlUrlHost.TryNormalize(host, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.BadIPv6, rejection);
    }

    [TestMethod]
    public void TryNormalize_BracketedAddressWithZone_KeepsAddressAndZone()
    {
        bool accepted = CurlUrlHost.TryNormalize("[fe80::1%25eth0]", out string normalized, out _, out string? zoneId, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("[fe80::1]", normalized);
        Assert.AreEqual("eth0", zoneId);
    }
}
