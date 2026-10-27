using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasySmtpOptionsTests
{
    private static readonly EasySmtpOptions Defaults = new();

    [TestMethod]
    public void New_EveryOption_DefaultsToLibcurlDefault()
    {
        Assert.IsNull(Defaults.MailFrom);
        Assert.IsEmpty(Defaults.MailRecipients);
        Assert.IsNull(Defaults.MailAuth);
        Assert.IsFalse(Defaults.MailRecipientAllowFails);
        Assert.AreEqual((byte)16, Defaults.UploadFlags);
    }

    [TestMethod]
    public void With_OneOption_ChangesOnlyThatOption()
    {
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { MailFrom = "a@example.com" }, o => o with { MailFrom = Defaults.MailFrom });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { MailRecipients = ["b@example.com"] }, o => o with { MailRecipients = Defaults.MailRecipients });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { MailAuth = "a@example.com" }, o => o with { MailAuth = Defaults.MailAuth });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { MailRecipientAllowFails = true }, o => o with { MailRecipientAllowFails = Defaults.MailRecipientAllowFails });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { UploadFlags = 1 }, o => o with { UploadFlags = Defaults.UploadFlags });
        Assert.HasCount(5, typeof(EasySmtpOptions).GetProperties());
    }
}
