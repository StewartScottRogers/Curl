using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="WinLdapResultText" /> against <c>wldap32.dll</c>'s <c>ldap_err2stringW</c>,
/// read on Windows 11 on 2026-09-28 (BL-586).
/// </summary>
[TestClass]
public sealed class WinLdapResultTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(0, "Success")]
    [DataRow(32, "No Such Object")]
    [DataRow(49, "Invalid Credentials")]
    [DataRow(52, "Unavailable")]
    [DataRow(53, "Unwilling To Perform")]
    [DataRow(85, "Timeout")]
    [DataRow(91, "Can't connect to the LDAP server")]
    [DataRow(97, "Referral hop limit exceeded")]
    public void Of_KnownCode_ReturnsWinLdapsText(int resultCode, string expected)
    {
        Diagnostics.Arrange("result code", resultCode);
        string text = WinLdapResultText.Of(resultCode);
        Diagnostics.Act("text", text);
        Diagnostics.Assert("text", expected, text);
        Assert.AreEqual(expected, WinLdapResultText.Of(resultCode));
    }

    [TestMethod]
    [DataRow(14)]
    [DataRow(100)]
    [DataRow(200)]
    public void Of_CodeWinLdapHasNoTextFor_ReturnsEmpty(int resultCode)
    {
        Diagnostics.Arrange("result code", resultCode);
        string text = WinLdapResultText.Of(resultCode);
        Diagnostics.Act("text", text);
        Diagnostics.Assert("text", string.Empty, text);
        Assert.AreEqual(string.Empty, WinLdapResultText.Of(resultCode));
    }

    [TestMethod]
    public void NamedCodes_AreWinLdapsUnavailableAndTimeout()
    {
        Diagnostics.Arrange("Unavailable code", WinLdapResultText.Unavailable);
        Diagnostics.Arrange("Timeout code", WinLdapResultText.Timeout);
        Diagnostics.Act("Unavailable text", WinLdapResultText.Of(WinLdapResultText.Unavailable));
        Diagnostics.Act("Timeout text", WinLdapResultText.Of(WinLdapResultText.Timeout));
        Diagnostics.Assert("Unavailable text", "Unavailable", WinLdapResultText.Of(WinLdapResultText.Unavailable));
        Diagnostics.Assert("Timeout text", "Timeout", WinLdapResultText.Of(WinLdapResultText.Timeout));
        Assert.AreEqual("Unavailable", WinLdapResultText.Of(WinLdapResultText.Unavailable));
        Assert.AreEqual("Timeout", WinLdapResultText.Of(WinLdapResultText.Timeout));
    }
}
