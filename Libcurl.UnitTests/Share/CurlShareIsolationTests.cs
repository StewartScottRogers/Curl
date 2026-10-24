using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Curl.Core;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.Share;

namespace Libcurl.UnitTests.Share;

/// <summary>
/// Pins share-handle isolation through the public API (BL-3273, ADR-0789): two easy handles of one library share no
/// cookie or connection unless <c>CURLSHOPT_SHARE</c> links them, and then only for the kind it names; once cookies are
/// unshared a new cookie stays with the handle that received it.
/// </summary>
[TestClass]
public sealed class CurlShareIsolationTests
{
    private const string Url = "http://h/x";

    private const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nContent-Length: 0\r\n\r\n";

    private const string SetsSecondCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: z=9\r\nContent-Length: 0\r\n\r\n";

    private const string Echo = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task Perform_TwoUnsharedHandles_ShareNoCookieAndNoConnection()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(kind: null);

        Assert.DoesNotContain("Cookie:", secondRequest);
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1L, secondConnects);
    }

    [TestMethod]
    public async Task Perform_CookieOnlyShare_CrossesTheCookieButNotTheConnection()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(CurlLockData.Cookie);

        StringAssert.Contains(secondRequest, "Cookie: a=1\r\n");
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1L, secondConnects);
    }

    [TestMethod]
    public async Task Perform_ConnectionOnlyShare_ReusesTheConnectionButCrossesNoCookie()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(CurlLockData.Connect);

        Assert.DoesNotContain("Cookie:", secondRequest);
        Assert.HasCount(1, connector.Targets);
        Assert.AreEqual(0L, secondConnects);
    }

    [TestMethod]
    public async Task Perform_DnsOnlyShare_CrossesNeitherCookieNorConnection()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(CurlLockData.Dns);

        Assert.DoesNotContain("Cookie:", secondRequest);
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1L, secondConnects);
    }

    [TestMethod]
    public async Task Perform_CookiesUnshared_ANewCookieTheFirstReceivesNoLongerReachesTheSecond()
    {
        ScriptedConnector connector = new([Bytes(SetsCookie), Bytes(Echo), Bytes(SetsSecondCookie), Bytes(Echo)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        var share = library.ShareInit();
        share.SetOption(CurlShareOption.Share, CurlLockData.Cookie);
        using var first = CookieHandle(library, share);
        using var second = CookieHandle(library, share);
        first.Perform();
        second.Perform();

        first.SetOption(CurlOption.Share, null);
        second.SetOption(CurlOption.Share, null);
        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.Unshare, CurlLockData.Cookie));
        first.SetOption(CurlOption.Share, share);
        second.SetOption(CurlOption.Share, share);
        first.Perform();
        int written = connector.Written.Length;
        second.Perform();

        Assert.DoesNotContain("z=9", Encoding.ASCII.GetString(connector.Written[written..]));
    }

    private static async Task<(ScriptedConnector Connector, string SecondRequest, long SecondConnects)> PerformTwiceAsync(CurlLockData? kind)
    {
        ScriptedConnector connector = new([Bytes(SetsCookie), Bytes(Echo)]) { ReusesTheFirstConnection = kind == CurlLockData.Connect };
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        CurlShare? share = null;
        if (kind is { } shared)
        {
            share = library.ShareInit();
            Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.Share, shared));
        }

        using var first = CookieHandle(library, share);
        using var second = CookieHandle(library, share);
        Assert.AreEqual(CurlCode.Ok, first.Perform());
        int written = connector.Written.Length;
        Assert.AreEqual(CurlCode.Ok, second.Perform());
        Assert.AreEqual(CurlCode.Ok, second.GetInfo(CurlInfo.NumConnects, out object? connects));
        return (connector, Encoding.ASCII.GetString(connector.Written[written..]), (long)connects!);
    }

    private static CurlEasy CookieHandle(CurlLibrary library, CurlShare? share)
    {
        var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, Url);
        easy.SetOption(CurlOption.Cookiefile, string.Empty);
        easy.SetOption(CurlOption.Share, share);
        return easy;
    }

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                new PoolingConnector(connector, shared.Connections, configuration: null),
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
