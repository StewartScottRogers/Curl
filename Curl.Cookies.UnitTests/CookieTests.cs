namespace Curl.Cookies;

[TestClass]
public sealed class CookieTests
{
    [TestMethod]
    public void With_EveryField_ReplacesEachOne()
    {
        Cookie original = new("a", "1", "example.com", false, "/", false, false, 0);

        Cookie replaced = original with
        {
            Name = "b",
            Value = "2",
            Domain = "example.org",
            IncludesSubdomains = true,
            Path = "/p",
            IsSecure = true,
            IsHttpOnly = true,
            ExpiresUnixSeconds = 5,
        };

        Assert.AreEqual(new Cookie("b", "2", "example.org", true, "/p", true, true, 5), replaced);
    }

    [TestMethod]
    [DataRow(0L, true)]
    [DataRow(1L, false)]
    public void IsSessionCookie_IsTrueOnlyWithoutAnExpiry(long expires, bool expected)
    {
        Cookie cookie = new("a", "1", "example.com", false, "/", false, false, expires);

        Assert.AreEqual(expected, cookie.IsSessionCookie);
    }
}
