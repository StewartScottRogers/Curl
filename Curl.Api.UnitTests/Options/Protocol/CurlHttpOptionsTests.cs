using Curl.Api.Client;
using Curl.Api.Options.Http;

namespace Curl.Api.UnitTests.Options.Protocol;

[TestClass]
public sealed class CurlHttpOptionsTests
{
    private static async Task<EasyHttpOptions> ReceivedByEngine(CurlClientOptions options)
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine, options, ownsEngine: true);
        client.CreateEasy();
        return engine.CreatedEasyHandles.Single().Options.Http;
    }

    [TestMethod]
    public async Task EveryMember_ReachesTheEngineUnderItsEasyName()
    {
        CurlHttpOptions http = new()
        {
            UserAgent = "agent/1",
            Referer = "http://referer/",
            AutoReferer = true,
            AcceptEncoding = "gzip",
            TransferEncoding = true,
            HttpContentDecoding = false,
            HttpTransferDecoding = false,
            FollowLocation = FollowLocation.All,
            MaxRedirects = -1,
            PostRedirect = PostRedirect.All,
            HttpVersion = HttpVersion.Http2,
            Http09Allowed = true,
            IgnoreContentLength = true,
            Expect100Timeout = TimeSpan.FromMilliseconds(250),
            Http200Aliases = ["ICY 200 OK"],
            RequestTarget = "*",
            Cookie = "a=1",
            CookieFiles = ["cookies.txt"],
            CookieJar = "jar.txt",
            CookieSession = true,
            CookieList = ["FLUSH"],
            AltSvc = "altsvc.txt",
            AltSvcControl = AltSvcControl.H2,
            Hsts = "hsts.txt",
            HstsControl = HstsControl.Enable,
            PipeWait = true,
            StreamWeight = 256,
            SslEnableAlpn = false,
        };

        EasyHttpOptions easy = await ReceivedByEngine(new CurlClientOptions { Http = http });

        Assert.AreEqual("agent/1", easy.UserAgent);
        Assert.AreEqual("http://referer/", easy.Referer);
        Assert.IsTrue(easy.AutoReferer);
        Assert.AreEqual("gzip", easy.AcceptEncoding);
        Assert.IsTrue(easy.TransferEncoding);
        Assert.IsFalse(easy.HttpContentDecoding);
        Assert.IsFalse(easy.HttpTransferDecoding);
        Assert.AreEqual(FollowLocation.All, easy.FollowLocation);
        Assert.AreEqual(-1, easy.MaxRedirects);
        Assert.AreEqual(PostRedirect.All, easy.PostRedirect);
        Assert.AreEqual(HttpVersion.Http2, easy.HttpVersion);
        Assert.IsTrue(easy.Http09Allowed);
        Assert.IsTrue(easy.IgnoreContentLength);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), easy.Expect100Timeout);
        CollectionAssert.AreEqual(new[] { "ICY 200 OK" }, easy.Http200Aliases.ToArray());
        Assert.AreEqual("*", easy.RequestTarget);
        Assert.AreEqual("a=1", easy.Cookie);
        CollectionAssert.AreEqual(new[] { "cookies.txt" }, easy.CookieFiles.ToArray());
        Assert.AreEqual("jar.txt", easy.CookieJar);
        Assert.IsTrue(easy.CookieSession);
        CollectionAssert.AreEqual(new[] { "FLUSH" }, easy.CookieList.ToArray());
        Assert.AreEqual("altsvc.txt", easy.AltSvc);
        Assert.AreEqual(AltSvcControl.H2, easy.AltSvcControl);
        Assert.AreEqual("hsts.txt", easy.Hsts);
        Assert.AreEqual(HstsControl.Enable, easy.HstsControl);
        Assert.IsTrue(easy.PipeWait);
        Assert.AreEqual(256, easy.StreamWeight);
        Assert.IsFalse(easy.SslEnableAlpn);
    }

    [TestMethod]
    public async Task DefaultRecord_LeavesTheEngineAtLibcurlDefaults()
    {
        EasyHttpOptions easy = await ReceivedByEngine(new CurlClientOptions { Http = new CurlHttpOptions() });

        Assert.AreEqual(new EasyHttpOptions(), easy);
    }

    [TestMethod]
    public async Task RecordWithoutUserAgent_KeepsTheClientUserAgent()
    {
        EasyHttpOptions easy = await ReceivedByEngine(new CurlClientOptions { UserAgent = "client/1", Http = new CurlHttpOptions() });

        Assert.AreEqual("client/1", easy.UserAgent);
    }

    [TestMethod]
    public void InvalidValues_ThrowAsTheyAreSet()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlHttpOptions { FollowLocation = (FollowLocation)4 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlHttpOptions { MaxRedirects = -2 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlHttpOptions { HttpVersion = (HttpVersion)6 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlHttpOptions { Expect100Timeout = TimeSpan.FromMilliseconds(-1) });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlHttpOptions { Http200Aliases = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlHttpOptions { CookieFiles = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlHttpOptions { CookieList = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlHttpOptions { StreamWeight = 0 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlHttpOptions { StreamWeight = 257 });
    }
}
