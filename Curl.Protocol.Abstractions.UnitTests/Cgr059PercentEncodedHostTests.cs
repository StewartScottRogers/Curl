namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-059: a host name that is not ASCII is converted label by label as libidn2 does for curl
/// 8.22.0 (measured with the reference build on 2026-10-26): an empty label is kept, a label
/// with a hyphen first, last or third and fourth is refused, bidirectional text is not checked,
/// and on Windows a byte that is not UTF-8 is read as Windows-1252.
/// </summary>
[TestClass]
public sealed class Cgr059PercentEncodedHostTests
{
    [TestMethod]
    [DataRow("a%FFb", "xn--ab-6ka")]
    [DataRow("a%E9b", "xn--ab-bja")]
    [DataRow("%FC.example", "xn--tda.example")]
    [DataRow("x%FC%D7", "xn--x-9ea4i")]
    [DataRow("a%80b", "xn--ab-8fu")]
    public void TryNormalize_StrayBytesAsWindows1252_ReadsThemInThatCodePage(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: true, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }

    [TestMethod]
    [DataRow("a%81b")]
    [DataRow("invalid-utf8-%e2%90.local")]
    [DataRow("%F0%90%82%80a")]
    public void TryNormalize_ByteWindows1252LeavesUndefined_IsRefused(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: true, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalize_StrayByteInAUtf8Locale_IsRefused()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("a%FFb", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    [DataRow("%C3%BC..example", "xn--tda..example")]
    [DataRow("a%D7%90.example", "xn--a-0hc.example")]
    [DataRow("%C3%BC-x.example", "xn---x-wka.example")]
    [DataRow("a.%C3%BC-x.example", "a.xn---x-wka.example")]
    [DataRow("xn--tda.%C3%BC", "xn--tda.xn--tda")]
    [DataRow("%C3%BC.", "xn--tda.")]
    [DataRow("%C3%BC%E3%80%82a", "xn--tda.a")]
    public void TryNormalize_NameInAUtf8Locale_ConvertsEachLabel(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }

    [TestMethod]
    [DataRow("-%C3%BC.example")]
    [DataRow("%C3%BC-.example")]
    [DataRow("-a.%C3%BC.example")]
    [DataRow("ab--c.%C3%BC")]
    [DataRow("a%C2%90b")]
    [DataRow("b---c.%C3%BC")]
    [DataRow("%C3%BCaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("%C3%BC.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC%C3%BC")]
    public void TryNormalize_LabelIdnaRefuses_IsAnIdnConversionFailure(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalize_UnconvertibleNameNotRefused_KeepsTheName()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("-%C3%BC.example", refuseUnconvertibleName: false, hostAsWindows1252: false, out string normalized, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("-ü.example", normalized);
        Assert.AreEqual("-ü.example", idnHost);
    }

    [TestMethod]
    public void ToCurlMessage_IdnConversionFailed_IsCurlsMalformatText() =>
        Assert.AreEqual("URL using bad/illegal format or missing URL", CurlUrlRejection.IdnConversionFailed.ToCurlMessage());
}
