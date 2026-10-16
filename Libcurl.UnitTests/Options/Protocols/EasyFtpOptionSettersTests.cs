using Curl.Api.Options.Protocols;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Protocols.ProtocolOptionAssert;

namespace Libcurl.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyFtpOptionSettersTests
{
    [TestMethod]
    public void SetOption_Ftpport_SetsTheActiveAddressAndNullClearsIt()
    {
        Text(CurlOption.Ftpport, p => p.Ftp.ActiveAddress);
    }

    [TestMethod]
    public void SetOption_Quote_StoresACopyOfTheCommandsAndNullEmptiesThem()
    {
        List(CurlOption.Quote, p => p.Ftp.Quote);
    }

    [TestMethod]
    public void SetOption_Prequote_StoresACopyOfTheCommandsAndNullEmptiesThem()
    {
        List(CurlOption.Prequote, p => p.Ftp.PreQuote);
    }

    [TestMethod]
    public void SetOption_Postquote_StoresACopyOfTheCommandsAndNullEmptiesThem()
    {
        List(CurlOption.Postquote, p => p.Ftp.PostQuote);
    }

    [TestMethod]
    public void SetOption_AppendAndItsAliasFtpappend_AreOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.Append, p => p.Ftp.Append);
        Switch(CurlOption.Ftpappend, p => p.Ftp.Append);
    }

    [TestMethod]
    public void SetOption_FtpUseEprt_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.FtpUseEprt, p => p.Ftp.UseEprt);
    }

    [TestMethod]
    public void SetOption_FtpUseEpsv_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.FtpUseEpsv, p => p.Ftp.UseEpsv);
    }

    [TestMethod]
    public void SetOption_FtpUsePret_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.FtpUsePret, p => p.Ftp.UsePret);
    }

    [TestMethod]
    public void SetOption_FtpCreateMissingDirs_TakesZeroToTwoAndRefusesTheRest()
    {
        Assert.AreEqual(FtpCreateMissingDirectories.CreateAndRetry, SetOk(CurlOption.FtpCreateMissingDirs, 2L).Ftp.CreateMissingDirectories);
        Assert.AreEqual(FtpCreateMissingDirectories.None, SetOk(CurlOption.FtpCreateMissingDirs, 0).Ftp.CreateMissingDirectories);
        Refused(CurlOption.FtpCreateMissingDirs, 3L);
        Refused(CurlOption.FtpCreateMissingDirs, -1L);
    }

    [TestMethod]
    public void SetOption_FtpAlternativeToUser_SetsTheCommandAndNullClearsIt()
    {
        Text(CurlOption.FtpAlternativeToUser, p => p.Ftp.AlternativeToUser);
    }

    [TestMethod]
    public void SetOption_FtpSkipPasvIp_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.FtpSkipPasvIp, p => p.Ftp.SkipPasvIp);
    }

    [TestMethod]
    public void SetOption_Ftpsslauth_TakesZeroToTwoAndRefusesTheRest()
    {
        Assert.AreEqual(FtpSslAuthentication.Tls, SetOk(CurlOption.Ftpsslauth, FtpSslAuthentication.Tls).Ftp.SslAuthentication);
        Assert.AreEqual(FtpSslAuthentication.Ssl, SetOk(CurlOption.Ftpsslauth, 1L).Ftp.SslAuthentication);
        Refused(CurlOption.Ftpsslauth, 3L);
        Refused(CurlOption.Ftpsslauth, -1L);
    }

    [TestMethod]
    public void SetOption_FtpSslCcc_TakesZeroToTwoAndRefusesTheRest()
    {
        Assert.AreEqual(FtpClearCommandChannel.Active, SetOk(CurlOption.FtpSslCcc, 2L).Ftp.ClearCommandChannel);
        Assert.AreEqual(FtpClearCommandChannel.Passive, SetOk(CurlOption.FtpSslCcc, 1).Ftp.ClearCommandChannel);
        Refused(CurlOption.FtpSslCcc, 3L);
        Refused(CurlOption.FtpSslCcc, -1L);
    }

    [TestMethod]
    public void SetOption_FtpAccount_SetsTheAccountAndNullClearsIt()
    {
        Text(CurlOption.FtpAccount, p => p.Ftp.Account);
    }

    [TestMethod]
    public void SetOption_FtpFilemethod_TakesZeroToThreeAndRefusesTheRest()
    {
        Assert.AreEqual(FtpFileMethod.SingleCwd, SetOk(CurlOption.FtpFilemethod, 3L).Ftp.FileMethod);
        Assert.AreEqual(FtpFileMethod.Default, SetOk(CurlOption.FtpFilemethod, 0L).Ftp.FileMethod);
        Refused(CurlOption.FtpFilemethod, 4L);
        Refused(CurlOption.FtpFilemethod, -1L);
    }
}
