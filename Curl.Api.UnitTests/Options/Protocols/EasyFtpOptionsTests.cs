using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyFtpOptionsTests
{
    private static readonly EasyFtpOptions Defaults = new();

    [TestMethod]
    public void New_StringOptions_DefaultToNull()
    {
        Assert.IsNull(Defaults.ActiveAddress);
        Assert.IsNull(Defaults.AlternativeToUser);
        Assert.IsNull(Defaults.Account);
    }

    [TestMethod]
    public void New_CommandLists_DefaultToEmpty()
    {
        Assert.IsEmpty(Defaults.Quote);
        Assert.IsEmpty(Defaults.PreQuote);
        Assert.IsEmpty(Defaults.PostQuote);
    }

    [TestMethod]
    public void New_SwitchOptions_DefaultToLibcurlDefaults()
    {
        Assert.IsFalse(Defaults.Append);
        Assert.IsTrue(Defaults.UseEprt);
        Assert.IsTrue(Defaults.UseEpsv);
        Assert.IsFalse(Defaults.UsePret);
        Assert.IsTrue(Defaults.SkipPasvIp);
    }

    [TestMethod]
    public void New_EnumeratedOptions_DefaultToLibcurlDefaults()
    {
        Assert.AreEqual(FtpCreateMissingDirectories.None, Defaults.CreateMissingDirectories);
        Assert.AreEqual(FtpSslAuthentication.Default, Defaults.SslAuthentication);
        Assert.AreEqual(FtpClearCommandChannel.None, Defaults.ClearCommandChannel);
        Assert.AreEqual(FtpFileMethod.MultiCwd, Defaults.FileMethod);
    }

    [TestMethod]
    public void Enums_CarryLibcurlNumbers()
    {
        Assert.AreEqual(0, (int)FtpCreateMissingDirectories.None);
        Assert.AreEqual(1, (int)FtpCreateMissingDirectories.Create);
        Assert.AreEqual(2, (int)FtpCreateMissingDirectories.CreateAndRetry);
        Assert.AreEqual(0, (int)FtpSslAuthentication.Default);
        Assert.AreEqual(1, (int)FtpSslAuthentication.Ssl);
        Assert.AreEqual(2, (int)FtpSslAuthentication.Tls);
        Assert.AreEqual(0, (int)FtpClearCommandChannel.None);
        Assert.AreEqual(1, (int)FtpClearCommandChannel.Passive);
        Assert.AreEqual(2, (int)FtpClearCommandChannel.Active);
        Assert.AreEqual(0, (int)FtpFileMethod.Default);
        Assert.AreEqual(1, (int)FtpFileMethod.MultiCwd);
        Assert.AreEqual(2, (int)FtpFileMethod.NoCwd);
        Assert.AreEqual(3, (int)FtpFileMethod.SingleCwd);
    }

    public static IEnumerable<object[]> SingleOptionChanges =>
    [
        Change("ActiveAddress", o => o with { ActiveAddress = "-" }, o => o with { ActiveAddress = Defaults.ActiveAddress }),
        Change("Quote", o => o with { Quote = ["NOOP"] }, o => o with { Quote = Defaults.Quote }),
        Change("PreQuote", o => o with { PreQuote = ["NOOP"] }, o => o with { PreQuote = Defaults.PreQuote }),
        Change("PostQuote", o => o with { PostQuote = ["NOOP"] }, o => o with { PostQuote = Defaults.PostQuote }),
        Change("Append", o => o with { Append = true }, o => o with { Append = Defaults.Append }),
        Change("UseEprt", o => o with { UseEprt = false }, o => o with { UseEprt = Defaults.UseEprt }),
        Change("UseEpsv", o => o with { UseEpsv = false }, o => o with { UseEpsv = Defaults.UseEpsv }),
        Change("UsePret", o => o with { UsePret = true }, o => o with { UsePret = Defaults.UsePret }),
        Change("CreateMissingDirectories", o => o with { CreateMissingDirectories = FtpCreateMissingDirectories.CreateAndRetry }, o => o with { CreateMissingDirectories = Defaults.CreateMissingDirectories }),
        Change("AlternativeToUser", o => o with { AlternativeToUser = "SITE AUTH" }, o => o with { AlternativeToUser = Defaults.AlternativeToUser }),
        Change("SkipPasvIp", o => o with { SkipPasvIp = false }, o => o with { SkipPasvIp = Defaults.SkipPasvIp }),
        Change("SslAuthentication", o => o with { SslAuthentication = FtpSslAuthentication.Tls }, o => o with { SslAuthentication = Defaults.SslAuthentication }),
        Change("ClearCommandChannel", o => o with { ClearCommandChannel = FtpClearCommandChannel.Active }, o => o with { ClearCommandChannel = Defaults.ClearCommandChannel }),
        Change("Account", o => o with { Account = "acct" }, o => o with { Account = Defaults.Account }),
        Change("FileMethod", o => o with { FileMethod = FtpFileMethod.NoCwd }, o => o with { FileMethod = Defaults.FileMethod }),
    ];

    [TestMethod]
    [DynamicData(nameof(SingleOptionChanges))]
    public void With_OneOption_ChangesOnlyThatOption(
        string option,
        Func<EasyFtpOptions, EasyFtpOptions> set,
        Func<EasyFtpOptions, EasyFtpOptions> restore)
    {
        EasyFtpOptions changed = set(Defaults);

        Assert.AreNotEqual(Defaults, changed, $"setting {option} left the record unchanged");
        Assert.AreEqual(Defaults, restore(changed), $"setting {option} changed another option");
    }

    [TestMethod]
    public void SingleOptionChanges_CoverEveryProperty()
    {
        int properties = typeof(EasyFtpOptions).GetProperties().Length;

        Assert.HasCount(properties, SingleOptionChanges);
    }

    private static object[] Change(
        string option,
        Func<EasyFtpOptions, EasyFtpOptions> set,
        Func<EasyFtpOptions, EasyFtpOptions> restore) => [option, set, restore];
}
