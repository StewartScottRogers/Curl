using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="RedirectDiagnosticLog" /> takes a password out of a redirect target before
/// logging it (ADR-0222, decision 7; BL-921).
/// </summary>
[TestClass]
public sealed class RedirectDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://user:s3cret@host/p?q#f", "http://host/p?q#f")]
    [DataRow("http://user:s3cret@host", "http://host")]
    [DataRow("user:s3cret@host/p", "host/p")]
    [DataRow("http://host/p@q", "http://host/p@q")]
    [DataRow("http://a@b:c@host:80/", "http://host:80/")]
    public void WithoutUserInformation_Url_DropsTheUserInformationOfTheAuthorityOnly(string url, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);

        string logged = RedirectDiagnosticLog.WithoutUserInformation(url);

        diagnostics.Act("logged", logged);
        diagnostics.Diff("logged", expected, logged);
        diagnostics.Assert("logged", expected, logged);
        Assert.AreEqual(expected, logged);
    }
}
