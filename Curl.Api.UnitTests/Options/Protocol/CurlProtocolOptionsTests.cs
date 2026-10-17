using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;

namespace Curl.Api.UnitTests.Options.Protocol;

[TestClass]
public sealed class CurlProtocolOptionsTests
{
    private static readonly CurlClientOptions ClientOptions = new()
    {
        UserAgent = "client-agent",
        Http = new CurlHttpOptions { FollowLocation = FollowLocation.All, Referer = "http://client-referer/" },
        Authentication = new CurlAuthenticationOptions { UserName = "client-user", HttpAuthentication = HttpAuthenticationMethods.Digest },
        Ftp = new CurlFtpOptions { Account = "client-account" },
        Ssh = new CurlSshOptions { KnownHostsFile = "client-hosts" },
        Mail = new CurlMailOptions { MailFrom = "<client@example>" },
        Tftp = new CurlTftpOptions { BlockSize = 1024 },
        Telnet = new CurlTelnetOptions { TelnetOptions = ["TTYPE=client"] },
        Rtsp = new CurlRtspOptions { SessionId = "client-session" },
    };

    private static async Task<EasyOptions> PerformedOptions(CurlRequest request)
    {
        ScriptedEasyTransfer easy = new();
        await using CurlClient client = new(new ScriptedCurlEngine(easy), ClientOptions, ownsEngine: true);
        await client.SendAsync(request, CancellationToken.None);
        return easy.PerformedOptions!;
    }

    [TestMethod]
    public async Task RequestWithoutRecords_KeepsTheClientRecords()
    {
        EasyOptions options = await PerformedOptions(new CurlRequest(HttpMethod.Get, "http://example/"));

        Assert.AreEqual(FollowLocation.All, options.Http.FollowLocation);
        Assert.AreEqual("http://client-referer/", options.Http.Referer);
        Assert.AreEqual("client-agent", options.Http.UserAgent);
        Assert.AreEqual("client-user", options.Authentication.UserName);
        Assert.AreEqual(HttpAuthenticationMethods.Digest, options.Authentication.HttpAuthentication);
        Assert.AreEqual("client-account", options.Protocols.Ftp.Account);
        Assert.AreEqual("client-hosts", options.Protocols.Ssh.KnownHostsFile);
        Assert.AreEqual("<client@example>", options.Protocols.Smtp.MailFrom);
        Assert.AreEqual(1024, options.Protocols.Tftp.BlockSize);
        CollectionAssert.AreEqual(new[] { "TTYPE=client" }, options.Protocols.Telnet.TelnetOptions.ToArray());
        Assert.AreEqual("client-session", options.Protocols.Rtsp.SessionId);
    }

    [TestMethod]
    public async Task RequestRecords_OverrideTheClientRecordsWhole()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/")
        {
            Http = new CurlHttpOptions { Referer = "http://request-referer/" },
            Authentication = new CurlAuthenticationOptions { UserName = "request-user" },
            Ftp = new CurlFtpOptions { Append = true },
            Ssh = new CurlSshOptions { Compression = true },
            Mail = new CurlMailOptions { MailAuth = "<auth@example>" },
            Tftp = new CurlTftpOptions { NoOptions = true },
            Telnet = new CurlTelnetOptions { TelnetOptions = ["TTYPE=request"] },
            Rtsp = new CurlRtspOptions { Transport = "RTP/AVP" },
        };

        EasyOptions options = await PerformedOptions(request);

        Assert.AreEqual(FollowLocation.None, options.Http.FollowLocation);
        Assert.AreEqual("http://request-referer/", options.Http.Referer);
        Assert.AreEqual("client-agent", options.Http.UserAgent);
        Assert.AreEqual("request-user", options.Authentication.UserName);
        Assert.AreEqual(HttpAuthenticationMethods.Basic, options.Authentication.HttpAuthentication);
        Assert.IsNull(options.Protocols.Ftp.Account);
        Assert.IsTrue(options.Protocols.Ftp.Append);
        Assert.IsNull(options.Protocols.Ssh.KnownHostsFile);
        Assert.IsTrue(options.Protocols.Ssh.Compression);
        Assert.IsNull(options.Protocols.Smtp.MailFrom);
        Assert.AreEqual("<auth@example>", options.Protocols.Smtp.MailAuth);
        Assert.AreEqual(512, options.Protocols.Tftp.BlockSize);
        Assert.IsTrue(options.Protocols.Tftp.NoOptions);
        CollectionAssert.AreEqual(new[] { "TTYPE=request" }, options.Protocols.Telnet.TelnetOptions.ToArray());
        Assert.IsNull(options.Protocols.Rtsp.SessionId);
        Assert.AreEqual("RTP/AVP", options.Protocols.Rtsp.Transport);
    }

    [TestMethod]
    public async Task RequestHttpRecord_LeavesTheRequestHeadersAndBodyToTheRequest()
    {
        CurlRequest request = new(HttpMethod.Post, "http://example/")
        {
            Headers = [new KeyValuePair<string, string>("X-Test", "1")],
            Http = new CurlHttpOptions { UserAgent = "request-agent" },
        };

        EasyOptions options = await PerformedOptions(request);

        Assert.AreEqual("request-agent", options.Http.UserAgent);
        CollectionAssert.AreEqual(new[] { "X-Test: 1" }, options.Http.HttpHeaders.ToArray());
        Assert.IsTrue(options.Http.Post);
    }

    [TestMethod]
    public void Apply_WithNoRecords_ReturnsTheOptionsUnchanged()
    {
        EasyOptions options = new();

        Assert.AreEqual(options, CurlProtocolOptions.Apply(options, default));
    }
}
