namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-059: libidn2 does not check bidirectional text in a host curl looks up, so a label that
/// mixes a Latin letter with a right-to-left one is converted, not refused: in a UTF-8 locale
/// curl 8.18.0 on Linux (measured on 2026-10-28) sends <c>http://aא.example/</c> as
/// <c>Host: xn--a-0hc.example</c> with exit 0.
/// </summary>
[TestClass]
public sealed class Cgr059BidiTests
{
    [TestMethod]
    [DataRow("aא.example", "xn--a-0hc.example")]
    [DataRow("aא", "xn--a-0hc")]
    public void TryNormalize_LabelMixingLatinAndRightToLeftInAUtf8Locale_IsConverted(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }
}
