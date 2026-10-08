using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="MailRequestOptions" />: a new instance holds curl's "not given" values, and
/// every member set in the initializer reads back unchanged (ADR-0121).
/// </summary>
[TestClass]
public sealed class MailRequestOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void MailRequestOptions_NothingSet_HoldsCurlsNotGivenValues()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "new MailRequestOptions()");

        var options = new MailRequestOptions();

        diagnostics.Act("from", options.From);
        diagnostics.Act("recipient count", options.Recipients.Count);
        diagnostics.Act("recipient allow fails", options.RecipientAllowFails);
        diagnostics.Act("sasl initial response", options.SaslInitialResponse);
        diagnostics.Assert("from", null, options.From);
        diagnostics.Assert("recipient count", 0, options.Recipients.Count);
        Assert.IsNull(options.From);
        Assert.IsEmpty(options.Recipients);
        Assert.IsNull(options.Auth);
        Assert.IsFalse(options.RecipientAllowFails);
        Assert.IsEmpty(options.UploadFlags);
        Assert.IsNull(options.CustomCommand);
        Assert.IsNull(options.LoginOptions);
        Assert.IsNull(options.SaslAuthorizationIdentity);
        Assert.IsFalse(options.SaslInitialResponse);
        Assert.IsNull(options.BearerToken);
        Assert.IsNull(options.ServiceName);
    }

    [TestMethod]
    public void MailRequestOptions_EveryMemberSet_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] recipients = ["a@example.com", "b@example.com"];
        string[] uploadFlags = ["answered", "flagged"];
        diagnostics.Arrange("recipients", string.Join(",", recipients));
        diagnostics.Arrange("upload flags", string.Join(",", uploadFlags));

        var options = new MailRequestOptions
        {
            From = "sender@example.com",
            Recipients = recipients,
            Auth = "auth@example.com",
            RecipientAllowFails = true,
            UploadFlags = uploadFlags,
            CustomCommand = "VRFY a",
            LoginOptions = "AUTH=PLAIN",
            SaslAuthorizationIdentity = "zid",
            SaslInitialResponse = true,
            BearerToken = "token",
            ServiceName = "custom",
        };

        diagnostics.Act("from", options.From);
        diagnostics.Act("auth", options.Auth);
        diagnostics.Act("custom command", options.CustomCommand);
        diagnostics.Act("login options", options.LoginOptions);
        diagnostics.Act("service name", options.ServiceName);
        diagnostics.Assert("from", "sender@example.com", options.From);
        diagnostics.Assert("service name", "custom", options.ServiceName);
        Assert.AreEqual("sender@example.com", options.From);
        Assert.AreSame(recipients, options.Recipients);
        Assert.AreEqual("auth@example.com", options.Auth);
        Assert.IsTrue(options.RecipientAllowFails);
        Assert.AreSame(uploadFlags, options.UploadFlags);
        Assert.AreEqual("VRFY a", options.CustomCommand);
        Assert.AreEqual("AUTH=PLAIN", options.LoginOptions);
        Assert.AreEqual("zid", options.SaslAuthorizationIdentity);
        Assert.IsTrue(options.SaslInitialResponse);
        Assert.AreEqual("token", options.BearerToken);
        Assert.AreEqual("custom", options.ServiceName);
    }

    [TestMethod]
    public void Equals_ForOptionsDifferingOnlyInInitialResponse_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var without = new MailRequestOptions { From = "a@example.com" };
        var with = without with { SaslInitialResponse = true };
        diagnostics.Arrange("without", without);
        diagnostics.Arrange("with", with);

        bool differ = !without.Equals(with);
        bool sameContentEqual = without.Equals(new MailRequestOptions { From = "a@example.com" });

        diagnostics.Act("differ", differ);
        diagnostics.Act("same content equal", sameContentEqual);
        diagnostics.Assert("differ", true, differ);
        diagnostics.Assert("same content equal", true, sameContentEqual);
        Assert.AreNotEqual(without, with);
        Assert.AreEqual(without, new MailRequestOptions { From = "a@example.com" });
    }
}
