namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-059, BL-3953: on Windows curl 8.22.0 (Schannel, libidn2 2.3.8) converts a host name that is
/// not ASCII from its bytes read as Windows-1252, every byte and not only those that are not UTF-8
/// (measured with the reference build on 2026-10-28): a typed character is its ANSI byte, an escape
/// the byte it decodes to.
/// </summary>
[TestClass]
public sealed class Cgr059Windows1252HostTests
{
    [TestMethod]
    [DataRow("%C3%BC.example", "xn--14-ria7423a.example")]
    [DataRow("ü.example", "xn--tda.example")]
    [DataRow("€", "xn--lzg")]
    public void TryNormalize_HostAsWindows1252_ReadsEveryByteInThatCodePage(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: true, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }

    [TestMethod]
    [DataRow("a%D7%90.example")]
    [DataRow("aא.example")]
    [DataRow("\U00010080a")]
    public void TryNormalize_HostAsWindows1252WithAByteItLeavesUndefined_IsRefused(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: true, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    [DataRow("a￿", "a%EF%BF%BF")]
    [DataRow("a\u0085", "a%C2%85")]
    public void TryNormalize_HostAsWindows1252WithACharacterItLacks_ReadsItsUtf8InThatCodePage(string typedHost, string escapedHost)
    {
        CurlUrlHost.TryNormalizeReadingHost(typedHost, refuseUnconvertibleName: true, hostAsWindows1252: true, out _, out string typed, out _, out _);
        CurlUrlHost.TryNormalizeReadingHost(escapedHost, refuseUnconvertibleName: true, hostAsWindows1252: true, out _, out string escaped, out _, out _);

        Assert.AreEqual(escaped, typed);
    }

    [TestMethod]
    public void TryNormalize_HostAsWindows1252_KeepsTheNameAsDecodedFromUtf8()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("%C3%BC", refuseUnconvertibleName: true, hostAsWindows1252: true, out string normalized, out _, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("ü", normalized);
    }
}
