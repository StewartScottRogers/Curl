using System.Text;
using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Libcurl.UnitTests.Info;

/// <summary>
/// Pins <c>CURLINFO_REFERER</c> on a handle that performed through the managed engine and its real HTTP handler over a
/// scripted connection: the <c>CURLOPT_REFERER</c> value, and under <c>CURLOPT_AUTOREFERER</c> the URL a followed
/// redirect came from, as libcurl 8.22.0's <c>data->state.referer</c> (BL-3257).
/// </summary>
[TestClass]
public sealed class EasyInfoAuthAndRefererTests
{
    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi";

    private const string Redirect = "HTTP/1.1 302 Found\r\nLocation: /second\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task GetInfo_RefererAfterAPerformWithCurlOptReferer_IsTheValueSet()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Ok)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/first");
        easy.SetOption(CurlOption.Referer, "http://example.com/from");
        easy.SetOption(CurlOption.Writedata, Stream.Null);

        CurlCode performed = easy.Perform();
        var code = easy.GetInfo(CurlInfo.Referer, out var value);

        Assert.AreEqual(CurlCode.Ok, performed);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("http://example.com/from", value);
        Assert.Contains("Referer: http://example.com/from\r\n", Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task GetInfo_RefererAfterAFollowedRedirectUnderAutoReferer_IsTheUrlRedirectedFrom()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Redirect), Encoding.ASCII.GetBytes(Ok)]) { ReusesTheFirstConnection = true };
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/first");
        easy.SetOption(CurlOption.Followlocation, 1L);
        easy.SetOption(CurlOption.Autoreferer, 1L);
        easy.SetOption(CurlOption.Writedata, Stream.Null);

        CurlCode performed = easy.Perform();
        var code = easy.GetInfo(CurlInfo.Referer, out var value);

        Assert.AreEqual(CurlCode.Ok, performed);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("http://127.0.0.1:8990/first", value);
        Assert.Contains("\r\nReferer: http://127.0.0.1:8990/first\r\n", Encoding.ASCII.GetString(connector.Written));
    }

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
