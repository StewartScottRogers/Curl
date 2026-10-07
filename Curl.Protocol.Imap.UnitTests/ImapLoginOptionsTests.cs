using Curl.Testing;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how the login options are read, as curl 8.21.0's <c>imap_parse_url_options</c>
/// reads them (BL-554 Notes).
/// </summary>
[TestClass]
public sealed class ImapLoginOptionsTests
{
    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(null, DisplayName = "no options")]
    [DataRow("", DisplayName = "opt-empty-string")]
    public void Parse_NoOption_AllowsAnyMechanismAndLogin(string? options)
    {
        Diagnostics.Arrange("options", DiagnosticText.Escape(options));
        ImapLoginOptions parsed = Parse(options);
        Describe(parsed);

        Diagnostics.Assert("AllowsAnyMechanism", true, parsed.AllowsAnyMechanism);
        Diagnostics.Assert("PrefersLogin", false, parsed.PrefersLogin);
        Diagnostics.Assert("AllowsLogin", true, parsed.AllowsLogin);
        Assert.IsTrue(parsed.AllowsAnyMechanism);
        Assert.IsEmpty(parsed.NamedMechanisms);
        Assert.IsFalse(parsed.PrefersLogin);
        Assert.IsTrue(parsed.AllowsLogin);
    }

    [TestMethod]
    public void Parse_OneMechanism_AllowsOnlyItAndNoLogin()
    {
        Diagnostics.Arrange("options", "AUTH=PLAIN");
        ImapLoginOptions parsed = Parse("AUTH=PLAIN");
        Describe(parsed);
        string[] allowed = parsed.AllowedAmong(["LOGIN", "plain", "XOAUTH2"]).ToArray();
        Diagnostics.Act("allowed among LOGIN, plain, XOAUTH2", string.Join(",", allowed));

        Diagnostics.Assert("AllowsAnyMechanism", false, parsed.AllowsAnyMechanism);
        Diagnostics.Assert("AllowsLogin", false, parsed.AllowsLogin);
        Diagnostics.Diff("allowed", "plain", string.Join(",", allowed));
        Assert.IsFalse(parsed.AllowsAnyMechanism);
        Assert.IsFalse(parsed.AllowsLogin);
        CollectionAssert.AreEqual(new[] { "plain" }, parsed.AllowedAmong(["LOGIN", "plain", "XOAUTH2"]));
    }

    [TestMethod]
    public void Parse_Star_AllowsAnyAgainAndDropsTheMechanismsBeforeIt()
    {
        Diagnostics.Arrange("options", "AUTH=EXTERNAL;AUTH=*");
        ImapLoginOptions parsed = Parse("AUTH=EXTERNAL;AUTH=*");
        Describe(parsed);

        Diagnostics.Assert("AllowsAnyMechanism", true, parsed.AllowsAnyMechanism);
        Diagnostics.Assert("AllowsLogin", true, parsed.AllowsLogin);
        Assert.IsTrue(parsed.AllowsAnyMechanism);
        Assert.IsEmpty(parsed.NamedMechanisms);
        Assert.IsTrue(parsed.AllowsLogin);
    }

    [TestMethod]
    public void Parse_MechanismAfterStar_IsNamedBesideAny()
    {
        Diagnostics.Arrange("options", "AUTH=*;AUTH=EXTERNAL");
        ImapLoginOptions parsed = Parse("AUTH=*;AUTH=EXTERNAL");
        Describe(parsed);

        Diagnostics.Assert("AllowsAnyMechanism", true, parsed.AllowsAnyMechanism);
        Diagnostics.Assert("names external", true, parsed.NamedMechanisms.Contains("external"));
        Assert.IsTrue(parsed.AllowsAnyMechanism);
        Assert.Contains("external", parsed.NamedMechanisms);
    }

    [TestMethod]
    public void Parse_PlusLogin_AllowsOnlyLogin()
    {
        Diagnostics.Arrange("options", "AUTH=PLAIN;AUTH=+LOGIN");
        ImapLoginOptions parsed = Parse("AUTH=PLAIN;AUTH=+LOGIN");
        Describe(parsed);
        int allowedCount = parsed.AllowedAmong(["PLAIN", "LOGIN"]).Count();
        Diagnostics.Act("allowed among PLAIN, LOGIN (count)", allowedCount);

        Diagnostics.Assert("PrefersLogin", true, parsed.PrefersLogin);
        Diagnostics.Assert("AllowsLogin", true, parsed.AllowsLogin);
        Diagnostics.Assert("allowed count", 0, allowedCount);
        Assert.IsTrue(parsed.PrefersLogin);
        Assert.IsTrue(parsed.AllowsLogin);
        Assert.IsEmpty(parsed.AllowedAmong(["PLAIN", "LOGIN"]));
    }

    [TestMethod]
    public void Parse_MechanismAfterPlusLogin_AllowsOnlyThatMechanism()
    {
        Diagnostics.Arrange("options", "AUTH=+LOGIN;AUTH=PLAIN");
        ImapLoginOptions parsed = Parse("AUTH=+LOGIN;AUTH=PLAIN");
        Describe(parsed);
        string allowed = string.Join(",", parsed.AllowedAmong(["PLAIN", "LOGIN"]));
        Diagnostics.Act("allowed among PLAIN, LOGIN", allowed);

        Diagnostics.Assert("PrefersLogin", false, parsed.PrefersLogin);
        Diagnostics.Assert("AllowsLogin", false, parsed.AllowsLogin);
        Diagnostics.Diff("allowed", "PLAIN", allowed);
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
        Diagnostics.Arrange("options", DiagnosticText.Escape(options));
        ImapLoginOptions? parsed = ImapLoginOptions.Parse(options);
        Diagnostics.Act("parsed", parsed is null ? "null" : "not null");

        Diagnostics.Assert("parsed is null", true, parsed is null);
        Assert.IsNull(ImapLoginOptions.Parse(options));
    }

    private static ImapLoginOptions Parse(string? options) =>
        ImapLoginOptions.Parse(options) ?? throw new AssertFailedException("The options were rejected.");

    private void Describe(ImapLoginOptions parsed) =>
        Diagnostics.Act(
            "parsed",
            $"any {parsed.AllowsAnyMechanism}, named [{string.Join(",", parsed.NamedMechanisms)}], prefersLogin {parsed.PrefersLogin}, allowsLogin {parsed.AllowsLogin}");
}
