using System.Text;
using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Libcurl.UnitTests.Info;

/// <summary>
/// <c>CURLINFO_SIZE_UPLOAD_T</c> after a <c>CURLOPT_POST</c> of 1 alone, which sends an empty chunked body,
/// counts the terminating chunk's five bytes, as libcurl 8.22.0 does (easytest.py's post-flag-only, BL-3339).
/// </summary>
[TestClass]
public sealed class EasyInfoSizeUploadOfEmptyChunkedPostTests
{
    [TestMethod]
    public async Task GetInfo_SizeUploadTAfterPostFlagOnly_IsTheFiveBytesOfTheTerminatingChunk()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/echo");
        easy.SetOption(CurlOption.Post, 1L);

        CurlCode code = easy.Perform();

        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(written, "\r\nTransfer-Encoding: chunked\r\n");
        StringAssert.EndsWith(written, "\r\n\r\n0\r\n\r\n");
        Assert.AreEqual(CurlCode.Ok, easy.GetInfo(CurlInfo.SizeUploadT, out var uploaded));
        Assert.AreEqual(5L, uploaded);
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
