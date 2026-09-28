namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="MailRequestOptions" />: a new instance holds curl's "not given" values, and
/// every member set in the initializer reads back unchanged (ADR-0121).
/// </summary>
[TestClass]
public sealed class MailRequestOptionsTests
{
    [TestMethod]
    public void MailRequestOptions_NothingSet_HoldsCurlsNotGivenValues()
    {
        var options = new MailRequestOptions();

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
        string[] recipients = ["a@example.com", "b@example.com"];
        string[] uploadFlags = ["answered", "flagged"];

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
        var without = new MailRequestOptions { From = "a@example.com" };
        var with = without with { SaslInitialResponse = true };

        Assert.AreNotEqual(without, with);
        Assert.AreEqual(without, new MailRequestOptions { From = "a@example.com" });
    }
}
