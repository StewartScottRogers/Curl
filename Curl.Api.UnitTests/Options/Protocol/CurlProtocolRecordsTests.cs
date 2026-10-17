using Curl.Api.Client;
using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocol;

[TestClass]
public sealed class CurlProtocolRecordsTests
{
    private static async Task<EasyProtocolOptions> ReceivedByEngine(CurlClientOptions options)
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine, options, ownsEngine: true);
        client.CreateEasy();
        return engine.CreatedEasyHandles.Single().Options.Protocols;
    }

    [TestMethod]
    public async Task FtpMembers_ReachTheEngineUnderTheirEasyNames()
    {
        CurlFtpOptions ftp = new()
        {
            ActiveAddress = "-",
            Quote = ["NOOP"],
            PreQuote = ["SITE A"],
            PostQuote = ["SITE B"],
            Append = true,
            UseEprt = false,
            UseEpsv = false,
            UsePret = true,
            CreateMissingDirectories = FtpCreateMissingDirectories.CreateAndRetry,
            AlternativeToUser = "ALT",
            SkipPasvIp = false,
            SslAuthentication = FtpSslAuthentication.Tls,
            ClearCommandChannel = FtpClearCommandChannel.Active,
            Account = "acct",
            FileMethod = FtpFileMethod.NoCwd,
        };

        EasyFtpOptions easy = (await ReceivedByEngine(new CurlClientOptions { Ftp = ftp })).Ftp;

        Assert.AreEqual("-", easy.ActiveAddress);
        CollectionAssert.AreEqual(new[] { "NOOP" }, easy.Quote.ToArray());
        CollectionAssert.AreEqual(new[] { "SITE A" }, easy.PreQuote.ToArray());
        CollectionAssert.AreEqual(new[] { "SITE B" }, easy.PostQuote.ToArray());
        Assert.IsTrue(easy.Append);
        Assert.IsFalse(easy.UseEprt);
        Assert.IsFalse(easy.UseEpsv);
        Assert.IsTrue(easy.UsePret);
        Assert.AreEqual(FtpCreateMissingDirectories.CreateAndRetry, easy.CreateMissingDirectories);
        Assert.AreEqual("ALT", easy.AlternativeToUser);
        Assert.IsFalse(easy.SkipPasvIp);
        Assert.AreEqual(FtpSslAuthentication.Tls, easy.SslAuthentication);
        Assert.AreEqual(FtpClearCommandChannel.Active, easy.ClearCommandChannel);
        Assert.AreEqual("acct", easy.Account);
        Assert.AreEqual(FtpFileMethod.NoCwd, easy.FileMethod);
    }

    [TestMethod]
    public async Task SshMembers_ReachTheEngineUnderTheirEasyNames()
    {
        CurlSshOptions ssh = new()
        {
            AuthenticationTypes = SshAuthenticationTypes.PublicKey,
            Compression = true,
            HostPublicKeyMd5 = "00112233445566778899aabbccddeeff",
            HostPublicKeySha256 = "c2hhMjU2",
            PublicKeyFile = "id.pub",
            PrivateKeyFile = "id",
            KnownHostsFile = "known_hosts",
        };

        EasySshOptions easy = (await ReceivedByEngine(new CurlClientOptions { Ssh = ssh })).Ssh;

        Assert.AreEqual(SshAuthenticationTypes.PublicKey, easy.AuthenticationTypes);
        Assert.IsTrue(easy.Compression);
        Assert.AreEqual("00112233445566778899aabbccddeeff", easy.HostPublicKeyMd5);
        Assert.AreEqual("c2hhMjU2", easy.HostPublicKeySha256);
        Assert.AreEqual("id.pub", easy.PublicKeyFile);
        Assert.AreEqual("id", easy.PrivateKeyFile);
        Assert.AreEqual("known_hosts", easy.KnownHostsFile);
    }

    [TestMethod]
    public async Task MailTftpTelnetAndRtspMembers_ReachTheEngineUnderTheirEasyNames()
    {
        CurlClientOptions options = new()
        {
            Mail = new CurlMailOptions { MailFrom = "<a@example>", MailRecipients = ["<b@example>"], MailAuth = "<c@example>", MailRecipientAllowFails = true },
            Tftp = new CurlTftpOptions { BlockSize = 1024, NoOptions = true },
            Telnet = new CurlTelnetOptions { TelnetOptions = ["TTYPE=vt100"] },
            Rtsp = new CurlRtspOptions { Request = RtspRequest.Describe, SessionId = "s1", StreamUri = "rtsp://x/", Transport = "RTP/AVP", ClientCSeq = 3, ServerCSeq = 4 },
        };

        EasyProtocolOptions easy = await ReceivedByEngine(options);

        Assert.AreEqual("<a@example>", easy.Smtp.MailFrom);
        CollectionAssert.AreEqual(new[] { "<b@example>" }, easy.Smtp.MailRecipients.ToArray());
        Assert.AreEqual("<c@example>", easy.Smtp.MailAuth);
        Assert.IsTrue(easy.Smtp.MailRecipientAllowFails);
        Assert.AreEqual(1024, easy.Tftp.BlockSize);
        Assert.IsTrue(easy.Tftp.NoOptions);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100" }, easy.Telnet.TelnetOptions.ToArray());
        Assert.AreEqual(RtspRequest.Describe, easy.Rtsp.Request);
        Assert.AreEqual("s1", easy.Rtsp.SessionId);
        Assert.AreEqual("rtsp://x/", easy.Rtsp.StreamUri);
        Assert.AreEqual("RTP/AVP", easy.Rtsp.Transport);
        Assert.AreEqual(3, easy.Rtsp.ClientCSeq);
        Assert.AreEqual(4, easy.Rtsp.ServerCSeq);
    }

    [TestMethod]
    public async Task DefaultRecords_LeaveTheEngineAtLibcurlDefaults()
    {
        CurlClientOptions options = new()
        {
            Ftp = new CurlFtpOptions(),
            Ssh = new CurlSshOptions(),
            Mail = new CurlMailOptions(),
            Tftp = new CurlTftpOptions(),
            Telnet = new CurlTelnetOptions(),
            Rtsp = new CurlRtspOptions(),
        };

        EasyProtocolOptions easy = await ReceivedByEngine(options);

        Assert.AreEqual(new EasyFtpOptions(), easy.Ftp);
        Assert.AreEqual(new EasySshOptions(), easy.Ssh);
        Assert.AreEqual(new EasySmtpOptions(), easy.Smtp);
        Assert.AreEqual(new EasyTftpOptions(), easy.Tftp);
        Assert.AreEqual(new EasyTelnetOptions(), easy.Telnet);
        Assert.AreEqual(new EasyRtspOptions(), easy.Rtsp);
    }

    [TestMethod]
    public void InvalidValues_ThrowAsTheyAreSet()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlFtpOptions { Quote = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlFtpOptions { PreQuote = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlFtpOptions { PostQuote = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlFtpOptions { CreateMissingDirectories = (FtpCreateMissingDirectories)3 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlFtpOptions { SslAuthentication = (FtpSslAuthentication)3 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlFtpOptions { ClearCommandChannel = (FtpClearCommandChannel)3 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlFtpOptions { FileMethod = (FtpFileMethod)4 });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlMailOptions { MailRecipients = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlTftpOptions { BlockSize = 7 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlTftpOptions { BlockSize = 65465 });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlTelnetOptions { TelnetOptions = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlRtspOptions { Request = (RtspRequest)12 });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlRtspOptions { StreamUri = null! });
    }
}
