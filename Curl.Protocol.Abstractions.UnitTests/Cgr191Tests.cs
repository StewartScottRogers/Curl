namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-191: an IPv6 zone id longer than 15 characters is a bad IPv6 address, as curl 8.22.0 says,
/// not a bad host name.
/// </summary>
[TestClass]
public sealed class Cgr191Tests
{
    [TestMethod]
    public void ToCurlMessage_ZoneIdOf16Characters_IsBadIPv6Address()
    {
        bool parsed = CurlUrl.TryParse("http://[::1%25zzzzzzzzzzzzzzzz]:20200/a", pathAsIs: false, driveLetters: true, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlUrlRejection.BadIPv6, rejection);
        Assert.AreEqual("Bad IPv6 address", rejection.ToCurlMessage());
    }

    [TestMethod]
    [DataRow("http://[::1g]/a")]
    [DataRow("http://[::1%25]/a")]
    [DataRow("http://[::1%25abc]/a")]
    public void TryParse_ShortOrMalformedZoneId_IsDecidedWithoutTheLengthRule(string url)
    {
        bool parsed = CurlUrl.TryParse(url, pathAsIs: false, driveLetters: true, out _, out CurlUrlRejection rejection);

        Assert.AreEqual(url.Contains("abc", StringComparison.Ordinal), parsed);
        Assert.AreEqual(parsed ? CurlUrlRejection.None : CurlUrlRejection.BadIPv6, rejection);
    }

    [TestMethod]
    public void ToCurlMessage_ZoneIdOver15CharactersWithControlCharacter_IsBadHostname()
    {
        bool parsed = CurlUrl.TryParse("http://[::1%25zzzzzzzzzzzzzz%0101]:20200/a", pathAsIs: false, driveLetters: true, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual("Bad hostname", rejection.ToCurlMessage());
    }
}
