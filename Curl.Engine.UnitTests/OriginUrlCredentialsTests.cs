using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="OriginUrlCredentials.Strip" /> to every row of curl 8.22.0's
/// <c>tests/tunit/tool1621.c</c> table, all build conditions on: an unsupported scheme or a bad host
/// name gives <see langword="null" />, and otherwise the user name and password go and the URL comes
/// back normalised (ADR-0482).
/// </summary>
[TestClass]
public sealed class OriginUrlCredentialsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("pop3s://foo@example.com", "pop3s://example.com/")]
    [DataRow("ldap://foo@example.com", "ldap://example.com/")]
    [DataRow("https://foo@example.com", "https://example.com/")]
    [DataRow("https://localhost:45", "https://localhost:45/")]
    [DataRow("https://foo@localhost:45", "https://localhost:45/")]
    [DataRow("https://user:pass@localhost:45", "https://localhost:45/")]
    [DataRow("http://daniel:password@localhost", "http://localhost/")]
    [DataRow("http://daniel@localhost", "http://localhost/")]
    [DataRow("http://localhost/", "http://localhost/")]
    [DataRow("http://host/@path/", "http://host/@path/")]
    [DataRow("http://emptypw:@host/", "http://host/")]
    [DataRow("http://:emptyuser@host/", "http://host/")]
    [DataRow("http://odd%40user@host/", "http://host/")]
    [DataRow("http://odder%3auser@host/", "http://host/")]
    public void Strip_Tool1621Row_RemovesTheCredentialsAndNormalises(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string? actual = OriginUrlCredentials.Strip(url);
        Diagnostics.Act("stripped", actual ?? "(null)");

        Diagnostics.Diff("stripped", expected, actual ?? "(null)");
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("ninja://foo@example.com")]
    [DataRow("http://odd%40host/")]
    [DataRow("http://user@odd%40host/")]
    [DataRow("http://only%40one%40host/")]
    public void Strip_Tool1621RowCurlRejects_ReturnsNull(string url)
    {
        Diagnostics.Arrange("url", url);

        string? actual = OriginUrlCredentials.Strip(url);
        Diagnostics.Act("stripped", actual ?? "(null)");

        Diagnostics.Assert("stripped", "(null)", actual ?? "(null)");
        Assert.IsNull(actual);
    }
}
