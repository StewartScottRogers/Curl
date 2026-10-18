using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <c>CURLOPT_COOKIELIST</c> applied at perform (BL-2520, ADR-0657): each item, in the order set, changes the
/// cookies the request sends and <c>CURLINFO_COOKIELIST</c> as curl 8.22.0's <c>cookielist</c> in <c>lib/setopt.c</c>
/// does - <c>ALL</c>, <c>SESS</c>, <c>FLUSH</c>, <c>RELOAD</c>, a Netscape cookie-file line and a <c>Set-Cookie:</c> line.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private const string NoCookies = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string ListedSessionLine = "example.com\tFALSE\t/\tFALSE\t0\tlisted\t1";

    private const string ListedLastingLine = "example.com\tFALSE\t/\tFALSE\t4102444800\tlasting\t2";

    [TestMethod]
    public async Task PerformAsync_CookieListNetscapeLine_SendsTheCookieAndListsIt()
    {
        (ScriptedConnector connector, IEasyTransfer easy) = CookieListEasy(NoCookies, new EasyHttpOptions { CookieList = [ListedSessionLine] });
        await using IEasyTransfer disposed = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: listed=1\r\n");
        Assert.AreEqual(ListedSessionLine, easy.Info.CookieList.Single());
    }

    [TestMethod]
    public async Task PerformAsync_CookieListSetCookieLine_SendsTheCookieAndListsIt()
    {
        EasyHttpOptions http = new() { CookieList = ["Set-Cookie: header=3; domain=example.com; path=/"] };
        (ScriptedConnector connector, IEasyTransfer easy) = CookieListEasy(NoCookies, http);
        await using IEasyTransfer disposed = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: header=3\r\n");
        StringAssert.EndsWith(easy.Info.CookieList.Single(), "\theader\t3");
    }

    [TestMethod]
    public async Task PerformAsync_CookieListAllAfterAnEarlierPerformsCookie_SendsOnlyTheCookiesListedAfterIt()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(NoCookies), Encoding.ASCII.GetBytes(NoCookies)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = CookieListOptions(new EasyHttpOptions { CookieList = [ListedSessionLine] });
        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));
        easy.Options = CookieListOptions(new EasyHttpOptions { CookieList = ["all", ListedLastingLine] });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: lasting=2\r\n");
        Assert.AreEqual(ListedLastingLine, easy.Info.CookieList.Single());
    }

    [TestMethod]
    public async Task PerformAsync_CookieListSess_SendsOnlyTheCookieWithAnExpiry()
    {
        EasyHttpOptions http = new() { CookieList = [ListedSessionLine, ListedLastingLine, "SESS"] };
        (ScriptedConnector connector, IEasyTransfer easy) = CookieListEasy(NoCookies, http);
        await using IEasyTransfer disposed = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: lasting=2\r\n");
        Assert.AreEqual(ListedLastingLine, easy.Info.CookieList.Single());
    }

    [TestMethod]
    public async Task PerformAsync_CookieListFlushThenAll_WritesTheJarHoldingTheCookieBeforeItIsRemoved()
    {
        MemoryStream printed = new();
        EasyHttpOptions http = new() { CookieJar = "-", CookieList = [ListedSessionLine, "FLUSH", "ALL"] };
        (ScriptedConnector connector, IEasyTransfer easy) = CookieListEasy(NoCookies, http, new MemoryFileSystem(), printed);
        await using IEasyTransfer disposed = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(printed.ToArray()), "\tlisted\t1\n");
        Assert.DoesNotContain("Cookie:", Encoding.ASCII.GetString(connector.Written));
        Assert.IsEmpty(easy.Info.CookieList);
    }

    [TestMethod]
    public async Task PerformAsync_CookieListFlushWithNoJar_WritesNothingAndKeepsTheCookie()
    {
        MemoryStream printed = new();
        EasyHttpOptions http = new() { CookieList = [ListedSessionLine, "flush"] };
        (ScriptedConnector connector, IEasyTransfer easy) = CookieListEasy(NoCookies, http, new MemoryFileSystem(), printed);
        await using IEasyTransfer disposed = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0, printed.Length);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: listed=1\r\n");
        Assert.AreEqual(ListedSessionLine, easy.Info.CookieList.Single());
    }

    [TestMethod]
    public async Task PerformAsync_CookieListReloadThenFlush_WritesTheFilesCookiesBeforeTheTransfer()
    {
        MemoryStream printed = new();
        MemoryFileSystem files = new() { Files = { ["in.txt"] = Encoding.ASCII.GetBytes(CookieFileText) } };
        EasyHttpOptions http = new() { CookieFiles = ["in.txt"], CookieJar = "-", CookieList = ["Reload", "FLUSH"] };
        (ScriptedConnector connector, IEasyTransfer easy) = CookieListEasy(SetsCookie, http, files, printed);
        await using IEasyTransfer disposed = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        string jars = Encoding.ASCII.GetString(printed.ToArray());
        Assert.AreEqual(2, jars.Split("\tsent\t1\n").Length - 1);
        Assert.AreEqual(1, jars.Split("\tgot\t2\n").Length - 1);
        Assert.AreEqual(2, files.Opened.Count(opened => opened == "read in.txt"));
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: sent=1\r\n");
        Assert.HasCount(2, easy.Info.CookieList);
    }

    [TestMethod]
    public async Task PerformAsync_CookieListOfCommandsAlone_LeavesTheCookieEngineOff()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(NoCookies), Encoding.ASCII.GetBytes(NoCookies)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = CookieListOptions(new EasyHttpOptions { CookieList = [ListedSessionLine] });
        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));
        easy.Options = CookieListOptions(new EasyHttpOptions { Cookie = "given=1", CookieList = ["ALL", "SESS", "FLUSH", "RELOAD"] });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        easy.Options = CookieListOptions(new EasyHttpOptions { CookieList = [ListedLastingLine] });
        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Cookie: given=1\r\n");
        Assert.HasCount(3, easy.Info.CookieList);
    }

    private static (ScriptedConnector Connector, IEasyTransfer Easy) CookieListEasy(string response, EasyHttpOptions http) =>
        CookieListEasy(response, http, new MemoryFileSystem(), Stream.Null);

    private static (ScriptedConnector Connector, IEasyTransfer Easy) CookieListEasy(string response, EasyHttpOptions http, MemoryFileSystem files, Stream printed)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(response)]);
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = CookieListOptions(http);
        easy.Host = new EasyTransferHost(files, Stream.Null, () => printed, NoDiagnosticLog.Instance, RunsOnWindows: false, AltSvcUsesOverHttp: false);
        return (connector, easy);
    }

    private static EasyOptions CookieListOptions(EasyHttpOptions http) =>
        new() { Network = new EasyNetworkOptions { Url = "http://example.com/x" }, Http = http };
}
