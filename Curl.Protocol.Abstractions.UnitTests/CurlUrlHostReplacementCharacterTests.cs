namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// BL-3946: a host holding <c>U+FFFD</c>, the replacement character, cannot be converted, as
/// UTS #46 and libidn2 disallow it. Off Windows a config file's bytes that are not UTF-8 are
/// read as <c>U+FFFD</c>, so upstream test 1034's <c>invalid-utf8-%hex[%e2%90]hex%.local</c>
/// reaches the host as <c>invalid-utf8-�.local</c> and curl fails it with exit 3.
/// </summary>
[TestClass]
public sealed class CurlUrlHostReplacementCharacterTests
{
    [TestMethod]
    [DataRow("invalid-utf8-�.local")]
    [DataRow("�")]
    [DataRow("a.ü�")]
    public void TryNormalize_NameWithTheReplacementCharacter_IsAnIdnConversionFailure(string host)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(accepted);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_LabelWithReplacementCharacterNotRefused_IsKeptAsDecoded()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(
            "invalid-utf8-�.local", refuseUnconvertibleName: false, hostAsWindows1252: false, out string normalized, out _, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual("invalid-utf8-�.local", normalized);
    }
}
