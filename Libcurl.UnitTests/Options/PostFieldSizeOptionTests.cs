using System.Text;
using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options;

/// <summary>
/// <c>CURLOPT_POSTFIELDSIZE</c> and <c>CURLOPT_POSTFIELDSIZE_LARGE</c> set through <c>CurlEasy</c> cut a
/// <c>CURLOPT_POSTFIELDS</c> body and its <c>Content-Length</c>, as libcurl 8.22.0 does (BL-3190).
/// </summary>
[TestClass]
public sealed class PostFieldSizeOptionTests
{
    [TestMethod]
    [DataRow(CurlOption.Postfieldsize, DisplayName = "CURLOPT_POSTFIELDSIZE")]
    [DataRow(CurlOption.PostfieldsizeLarge, DisplayName = "CURLOPT_POSTFIELDSIZE_LARGE")]
    public async Task Perform_PostFieldSizeThreeOfSixBytes_SendsThreeBytesWithContentLengthThree(CurlOption option)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/post");
        easy.SetOption(CurlOption.Postfields, "abcdef");
        easy.SetOption(option, 3L);

        CurlCode code = easy.Perform();

        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(written, "\r\nContent-Length: 3\r\n");
        StringAssert.EndsWith(written, "\r\n\r\nabc");
        Assert.AreEqual(CurlCode.Ok, easy.GetInfo(CurlInfo.SizeUploadT, out var uploaded));
        Assert.AreEqual(3L, uploaded);
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
