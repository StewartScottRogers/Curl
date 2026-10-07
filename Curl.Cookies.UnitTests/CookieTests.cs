using Curl.Testing;

namespace Curl.Cookies;

[TestClass]
public sealed class CookieTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void With_EveryField_ReplacesEachOne()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Cookie original = new("a", "1", "example.com", false, "/", false, false, 0);
        diagnostics.Arrange("original", original);

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
        diagnostics.Act("replaced", replaced);

        Cookie expected = new("b", "2", "example.org", true, "/p", true, true, 5);
        diagnostics.Assert("replaced", expected, replaced);
        Assert.AreEqual(expected, replaced);
    }

    [TestMethod]
    [DataRow(0L, true)]
    [DataRow(1L, false)]
    public void IsSessionCookie_IsTrueOnlyWithoutAnExpiry(long expires, bool expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expires (Unix seconds)", expires);
        Cookie cookie = new("a", "1", "example.com", false, "/", false, false, expires);

        diagnostics.Act("IsSessionCookie", cookie.IsSessionCookie);

        diagnostics.Assert("IsSessionCookie", expected, cookie.IsSessionCookie);
        Assert.AreEqual(expected, cookie.IsSessionCookie);
    }
}
