namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// BL-3945: in a host that is not ASCII, an <c>xn--</c> label passes as written only when libidn2
/// can decode it, as curl 8.22.0 does: <c>xn--zz.ü</c> fails with exit 3 while <c>xn--tda.ü</c>
/// sends <c>Host: xn--tda.xn--tda</c>.
/// </summary>
[TestClass]
public sealed class CurlUrlHostXnLabelTests
{
    [TestMethod]
    [DataRow("xn--zz.ü", DisplayName = "a number cut short")]
    [DataRow("xn--.ü", DisplayName = "nothing to decode")]
    [DataRow("xn--a.ü", DisplayName = "decodes to the control character U+0080")]
    [DataRow("xn--tda-.ü", DisplayName = "decodes to ASCII only")]
    [DataRow("xn--abc-.ü", DisplayName = "basic code points only")]
    [DataRow("xn--9999999999a.ü", DisplayName = "an index past int.MaxValue")]
    [DataRow("xn--en32g.ü", DisplayName = "decodes past U+10FFFF")]
    [DataRow("xn--ib9b.ü", DisplayName = "decodes to a lone surrogate")]
    [DataRow("xn--t_a.ü", DisplayName = "a digit that is not a letter or decimal digit")]
    public void TryNormalize_XnLabelThatDoesNotDecode_IsAnIdnConversionFailure(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    [DataRow("xn--tda.ü", "xn--tda.xn--tda")]
    [DataRow("XN--TDA.ü", "xn--tda.xn--tda")]
    [DataRow("xn--ab-tda.ü", "xn--ab-tda.xn--tda")]
    public void TryNormalize_XnLabelThatDecodes_IsSentAsWritten(string host, string expectedIdnHost)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expectedIdnHost, idnHost);
    }

    [TestMethod]
    public void TryNormalize_AsciiNameWithXnLabelThatDoesNotDecode_IsKept()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("xn--zz.example", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("xn--zz.example", idnHost);
    }
}
