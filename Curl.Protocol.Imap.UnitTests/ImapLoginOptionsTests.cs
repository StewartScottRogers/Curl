namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how the login options are read, as curl 8.21.0's <c>imap_parse_url_options</c>
/// reads them (BL-554 Notes).
/// </summary>
[TestClass]
public sealed class ImapLoginOptionsTests
{
    [TestMethod]
    [DataRow(null, DisplayName = "no options")]
    [DataRow("", DisplayName = "opt-empty-string")]
    public void Parse_NoOption_AllowsAnyMechanismAndLogin(string? options)
    {
        ImapLoginOptions parsed = Parse(options);

        Assert.IsTrue(parsed.AllowsAnyMechanism);
        Assert.IsEmpty(parsed.NamedMechanisms);
        Assert.IsFalse(parsed.PrefersLogin);
        Assert.IsTrue(parsed.AllowsLogin);
    }

    [TestMethod]
    public void Parse_OneMechanism_AllowsOnlyItAndNoLogin()
    {
        ImapLoginOptions parsed = Parse("AUTH=PLAIN");

        Assert.IsFalse(parsed.AllowsAnyMechanism);
        Assert.IsFalse(parsed.AllowsLogin);
        CollectionAssert.AreEqual(new[] { "plain" }, parsed.AllowedAmong(["LOGIN", "plain", "XOAUTH2"]));
    }

    [TestMethod]
    public void Parse_Star_AllowsAnyAgainAndDropsTheMechanismsBeforeIt()
    {
        ImapLoginOptions parsed = Parse("AUTH=EXTERNAL;AUTH=*");

        Assert.IsTrue(parsed.AllowsAnyMechanism);
        Assert.IsEmpty(parsed.NamedMechanisms);
        Assert.IsTrue(parsed.AllowsLogin);
    }

    [TestMethod]
    public void Parse_MechanismAfterStar_IsNamedBesideAny()
    {
        ImapLoginOptions parsed = Parse("AUTH=*;AUTH=EXTERNAL");

        Assert.IsTrue(parsed.AllowsAnyMechanism);
        Assert.Contains("external", parsed.NamedMechanisms);
    }

    [TestMethod]
    public void Parse_PlusLogin_AllowsOnlyLogin()
    {
        ImapLoginOptions parsed = Parse("AUTH=PLAIN;AUTH=+LOGIN");

        Assert.IsTrue(parsed.PrefersLogin);
        Assert.IsTrue(parsed.AllowsLogin);
        Assert.IsEmpty(parsed.AllowedAmong(["PLAIN", "LOGIN"]));
    }

    [TestMethod]
    public void Parse_MechanismAfterPlusLogin_AllowsOnlyThatMechanism()
    {
        ImapLoginOptions parsed = Parse("AUTH=+LOGIN;AUTH=PLAIN");

        Assert.IsFalse(parsed.PrefersLogin);
        Assert.IsFalse(parsed.AllowsLogin);
        CollectionAssert.AreEqual(new[] { "PLAIN" }, parsed.AllowedAmong(["PLAIN", "LOGIN"]));
    }

    [TestMethod]
    [DataRow("AUTH=BOGUS")]
    [DataRow("FOO=1")]
    [DataRow("AUTH=")]
    [DataRow(";AUTH=LOGIN")]
    [DataRow("AUTH=LOGIN;;")]
    [DataRow("AUTH=*x")]
    [DataRow("AUTH=LOGIN=X")]
    public void Parse_OptionCurlRejects_ReturnsNull(string options)
    {
        Assert.IsNull(ImapLoginOptions.Parse(options));
    }

    private static ImapLoginOptions Parse(string? options) =>
        ImapLoginOptions.Parse(options) ?? throw new AssertFailedException("The options were rejected.");
}
