namespace Curl.Core;

/// <summary>
/// Pins how <see cref="RedirectDiagnosticLog" /> takes a password out of a redirect target before
/// logging it (ADR-0222, decision 7; BL-921).
/// </summary>
[TestClass]
public sealed class RedirectDiagnosticLogTests
{
    [TestMethod]
    [DataRow("http://user:s3cret@host/p?q#f", "http://host/p?q#f")]
    [DataRow("http://user:s3cret@host", "http://host")]
    [DataRow("user:s3cret@host/p", "host/p")]
    [DataRow("http://host/p@q", "http://host/p@q")]
    [DataRow("http://a@b:c@host:80/", "http://host:80/")]
    public void WithoutUserInformation_Url_DropsTheUserInformationOfTheAuthorityOnly(string url, string expected)
    {
        string logged = RedirectDiagnosticLog.WithoutUserInformation(url);

        Assert.AreEqual(expected, logged);
    }
}
