using Libcurl.Options;
using static Libcurl.UnitTests.Options.Protocols.ProtocolOptionAssert;

namespace Libcurl.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasySmtpTftpTelnetOptionSettersTests
{
    [TestMethod]
    public void SetOption_MailFrom_SetsTheSenderAndNullClearsIt()
    {
        Text(CurlOption.MailFrom, p => p.Smtp.MailFrom);
    }

    [TestMethod]
    public void SetOption_MailRcpt_StoresACopyOfTheRecipientsAndNullEmptiesThem()
    {
        List(CurlOption.MailRcpt, p => p.Smtp.MailRecipients);
    }

    [TestMethod]
    public void SetOption_MailAuth_SetsTheIdentityAndNullClearsIt()
    {
        Text(CurlOption.MailAuth, p => p.Smtp.MailAuth);
    }

    [TestMethod]
    public void SetOption_MailRcptAllowfailsAndItsMisspeltAlias_AreOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.MailRcptAllowfails, p => p.Smtp.MailRecipientAllowFails);
        Switch(CurlOption.MailRcptAlllowfails, p => p.Smtp.MailRecipientAllowFails);
    }

    [TestMethod]
    public void SetOption_TftpBlksize_KeepsEightTo65464AndMapsBelowToTheDefaultAndAboveToTheLargest()
    {
        Assert.AreEqual(8, SetOk(CurlOption.TftpBlksize, 8L).Tftp.BlockSize);
        Assert.AreEqual(65464, SetOk(CurlOption.TftpBlksize, 65464L).Tftp.BlockSize);
        Assert.AreEqual(512, SetOk(CurlOption.TftpBlksize, 7L).Tftp.BlockSize);
        Assert.AreEqual(65464, SetOk(CurlOption.TftpBlksize, 65465L).Tftp.BlockSize);
    }

    [TestMethod]
    public void SetOption_TftpNoOptions_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.TftpNoOptions, p => p.Tftp.NoOptions);
    }

    [TestMethod]
    public void SetOption_Telnetoptions_StoresACopyOfTheEntriesAndNullEmptiesThem()
    {
        List(CurlOption.Telnetoptions, p => p.Telnet.TelnetOptions);
    }
}
