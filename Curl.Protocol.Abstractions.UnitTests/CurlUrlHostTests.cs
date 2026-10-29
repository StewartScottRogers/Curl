namespace Curl.Protocol.Abstractions.UnitTests;

[TestClass]
public sealed class CurlUrlHostTests
{
    [TestMethod]
    [DataRow(true, "xn--ab-pia8903akua.test")]
    [DataRow(false, "aa/cb.test")]
    public void ReadingHostAs_ChosenRule_DecidesHowTheHostIsRead_AndDisposeRestoresTheDefault(bool hostAsWindows1252, string expectedIdnHost)
    {
        string defaultIdnHost = ConvertAccountOfHost();

        string chosenIdnHost;
        using (CurlUrlHost.ReadingHostAs(hostAsWindows1252))
        {
            chosenIdnHost = ConvertAccountOfHost();
        }

        Assert.AreEqual(expectedIdnHost, chosenIdnHost);
        Assert.AreEqual(defaultIdnHost, ConvertAccountOfHost());
    }

    private static string ConvertAccountOfHost()
    {
        CurlUrlHost.TryNormalize("a℀b.test", refuseUnconvertibleName: false, out _, out string idnHost, out _, out _);

        return idnHost;
    }

    [TestMethod]
    [DataRow("[::1")]
    [DataRow("[")]
    public void TryNormalize_BracketNotClosed_IsBadIPv6(string host)
    {
        bool accepted = CurlUrlHost.TryNormalize(host, refuseUnconvertibleName: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.BadIPv6, rejection);
    }

    [TestMethod]
    public void TryNormalize_BracketedAddressWithZone_KeepsAddressAndZone()
    {
        bool accepted = CurlUrlHost.TryNormalize("[fe80::1%25eth0]", refuseUnconvertibleName: false, out string normalized, out _, out string? zoneId, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("[fe80::1]", normalized);
        Assert.AreEqual("eth0", zoneId);
    }

    [TestMethod]
    [DataRow("a\u0090ü.example")]
    [DataRow("aü�.example")]
    public void TryNormalizeReadingHost_LabelWithARuneIdnaRefuses_IsAnIdnConversionFailure(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_LabelWithALoneLowSurrogate_IsAnIdnConversionFailure()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("a\uDC80b.example", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_LabelWithALoneHighSurrogate_IsAnIdnConversionFailure()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("\uD800ü.example", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_LabelOfEncodableRunes_IsPunycode()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("ü.example", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("xn--tda.example", idnHost);
    }
}
