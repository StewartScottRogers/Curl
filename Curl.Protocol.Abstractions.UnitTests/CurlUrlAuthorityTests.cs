namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="CurlUrlAuthority.ParseLoginDetails" /> and <see cref="CurlUrlAuthority.SplitPort" /> to
/// curl 8.22.0's <c>Curl_parse_login_details</c> and <c>parse_port</c> (unit1620, unit1653).
/// </summary>
[TestClass]
public sealed class CurlUrlAuthorityTests
{
    [TestMethod]
    [DataRow(":;", "", "")]
    [DataRow(":password;", "", "password")]
    [DataRow("user;", "user", null)]
    public void ParseLoginDetails_EmptyOptions_HasNoOptions(string login, string user, string? password)
    {
        CurlUrlAuthority details = CurlUrlAuthority.ParseLoginDetails(login, hasLoginOptions: true);

        Assert.AreEqual(user, details.User);
        Assert.AreEqual(password, details.Password);
        Assert.IsNull(details.Options);
    }

    [TestMethod]
    public void ParseLoginDetails_OptionsBeforePassword_SplitsAllThree()
    {
        CurlUrlAuthority details = CurlUrlAuthority.ParseLoginDetails("user;options:password", hasLoginOptions: true);

        Assert.AreEqual("user", details.User);
        Assert.AreEqual("password", details.Password);
        Assert.AreEqual("options", details.Options);
    }

    [TestMethod]
    public void Parse_ImapUrlWithEmptyOptions_HasNoOptions()
    {
        CurlUrlAuthority? authority = CurlUrlAuthority.Parse("user:pw;@host", "imap", disallowUser: false, refuseUnconvertibleName: false, out CurlUrlRejection rejection);

        Assert.AreEqual(CurlUrlRejection.None, rejection);
        Assert.IsNotNull(authority);
        Assert.AreEqual("pw", authority.Password);
        Assert.IsNull(authority.Options);
    }

    [TestMethod]
    [DataRow("[fe80::250:56ff;fea7:da15]:808", false, 808)]
    [DataRow("[fe80::1]:", true, null)]
    public void SplitPort_AcceptedHostAndPort_ReturnsThePortWritten(string hostAndPort, bool hasScheme, int? expectedPort)
    {
        CurlUrlRejection rejection = CurlUrlAuthority.SplitPort(hostAndPort, hasScheme, out _, out int? port);

        Assert.AreEqual(CurlUrlRejection.None, rejection);
        Assert.AreEqual(expectedPort, port);
    }

    [TestMethod]
    public void SplitPort_NoSchemeAndNothingAfterTheColon_IsBadPortNumber()
    {
        CurlUrlRejection rejection = CurlUrlAuthority.SplitPort("aaaa:", hasScheme: false, out _, out _);

        Assert.AreEqual(CurlUrlRejection.BadPortNumber, rejection);
    }
}
