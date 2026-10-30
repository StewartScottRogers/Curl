namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-238: off Windows, under a locale that is not UTF-8 (<c>LC_ALL=C</c>) libidn2 cannot convert a
/// host name that is not ASCII, so curl fails the transfer URL with exit 3.
/// </summary>
[TestClass]
public sealed class Cgr238Tests
{
    [TestMethod]
    [DataRow("LC_ALL", "C", false)]
    [DataRow("LC_ALL", "POSIX", false)]
    [DataRow("LC_ALL", "C.UTF-8", true)]
    [DataRow("LC_CTYPE", "en_US.utf8", true)]
    [DataRow("LANG", "de_DE.UTF-8", true)]
    [DataRow("LANG", "de_DE.ISO-8859-1", false)]
    public void LocaleIsUtf8_NamedLocale_SaysWhetherItIsUtf8(string variable, string value, bool expected)
    {
        bool utf8 = CurlUrlHost.LocaleIsUtf8(name => name == variable ? value : null);

        Assert.AreEqual(expected, utf8);
    }

    [TestMethod]
    public void LocaleIsUtf8_NothingSet_IsTheCLocale() =>
        Assert.IsFalse(CurlUrlHost.LocaleIsUtf8(_ => null));

    [TestMethod]
    public void LocaleIsUtf8_LcAllWinsOverLang_AndEmptyIsSkipped()
    {
        Assert.IsFalse(CurlUrlHost.LocaleIsUtf8(name => name == "LC_ALL" ? "C" : "en_US.UTF-8"));
        Assert.IsTrue(CurlUrlHost.LocaleIsUtf8(name => name == "LC_ALL" ? string.Empty : "en_US.UTF-8"));
    }

    [TestMethod]
    public void TryNormalizeReadingHost_NonAsciiHostInLocaleThatIsNotUtf8_IsIdnConversionFailed()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("bücher.example", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection, localeRejectsNonAsciiName: true);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_AsciiHostInLocaleThatIsNotUtf8_IsAccepted()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("example.com", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _, localeRejectsNonAsciiName: true);

        Assert.IsTrue(accepted);
        Assert.AreEqual("example.com", idnHost);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_NonAsciiHostInUtf8Locale_IsConverted()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("bücher.example", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _, localeRejectsNonAsciiName: false);

        Assert.IsTrue(accepted);
        Assert.AreEqual("xn--bcher-kva.example", idnHost);
    }

    [TestMethod]
    [DataRow(true, "C", false)]
    [DataRow(false, "C", true)]
    [DataRow(false, "C.UTF-8", false)]
    public void LocaleRejectsNonAsciiNameOn_PlatformAndLocale_RejectsOnlyOffWindowsOutsideUtf8(bool isWindows, string locale, bool expected) =>
        Assert.AreEqual(expected, CurlUrlHost.LocaleRejectsNonAsciiNameOn(isWindows, _ => locale));
}
