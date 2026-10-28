namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-059: the hyphen and empty-label rules libidn2 applies to a host typed as it is, not
/// percent-encoded, as curl 8.22.0 does (measured with the reference build on 2026-10-28):
/// <c>http://-ü.example/</c> fails with exit 3 and <c>http://ü..example/</c> goes out as
/// <c>Host: xn--tda..example</c>.
/// </summary>
[TestClass]
public sealed class Cgr059LabelRulesTests
{
    [TestMethod]
    [DataRow("-ü.example")]
    [DataRow("ü-.example")]
    [DataRow("a.-ü")]
    public void TryNormalize_LabelWithAHyphenFirstOrLast_IsAnIdnConversionFailure(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingStrayBytes(host, refuseUnconvertibleName: true, strayBytesAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    [DataRow("ü..example", "xn--tda..example")]
    [DataRow("a..ü", "a..xn--tda")]
    public void TryNormalize_NameWithAnEmptyLabel_KeepsTheEmptyLabel(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingStrayBytes(host, refuseUnconvertibleName: true, strayBytesAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }
}
