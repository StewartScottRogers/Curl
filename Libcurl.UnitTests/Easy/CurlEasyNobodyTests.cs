using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// Pins where a <c>CURLOPT_NOBODY</c> handle's header lines go through the public options (BL-3199, ADR-0779), as
/// libcurl 8.22.0: nothing reaches <c>CURLOPT_WRITEFUNCTION</c> over HTTP or for an FTP directory, with or without
/// <c>CURLOPT_HEADERFUNCTION</c>; FTP's <c>-I</c> lines for a file always do; and <c>CURLOPT_HEADER</c> 1 sends an
/// HTTP head there too.
/// </summary>
[TestClass]
public sealed class CurlEasyNobodyTests
{
    private const string HttpHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: b\r\n\r\n";

    private const string FtpLoggedIn = "220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Perform_HttpNobody_GivesTheWriteFunctionNoBytes(bool headerFunction)
    {
        (string body, string headers, CurlCode code) = await PerformAsync("http://127.0.0.1:8990/x", HttpHead, headerFunction, header: 0L);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(string.Empty, body);
        Assert.AreEqual(headerFunction ? HttpHead : string.Empty, headers);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Perform_FtpDirectoryNobody_GivesTheWriteFunctionNoBytes(bool headerFunction)
    {
        (string body, _, CurlCode code) = await PerformAsync("ftp://127.0.0.1:8990/d/", FtpLoggedIn + "250 Directory changed\r\n221 Bye\r\n", headerFunction, header: 0L);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(string.Empty, body);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Perform_FtpFileNobody_GivesTheWriteFunctionOnlyTheInfoLines(bool headerFunction)
    {
        (string body, _, CurlCode code) = await PerformAsync(
            "ftp://127.0.0.1:8990/f.txt",
            FtpLoggedIn + "200 Type set\r\n213 5\r\n350 Restarting at 0\r\n221 Bye\r\n",
            headerFunction,
            header: 0L);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("Content-Length: 5\r\nAccept-ranges: bytes\r\n", body);
    }

    [TestMethod]
    public async Task Perform_HttpNobodyWithHeaderOne_GivesTheWriteFunctionTheHead()
    {
        (string body, string headers, CurlCode code) = await PerformAsync("http://127.0.0.1:8990/x", HttpHead, headerFunction: true, header: 1L);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(HttpHead, body);
        Assert.AreEqual(HttpHead, headers);
    }

    private static async Task<(string Body, string Headers, CurlCode Code)> PerformAsync(string url, string reply, bool headerFunction, long header)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(reply)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        StringBuilder body = new();
        StringBuilder headers = new();
        WriteCallback write = data =>
        {
            body.Append(Encoding.ASCII.GetString(data));
            return data.Length;
        };
        HeaderCallback headerCallback = data =>
        {
            headers.Append(Encoding.ASCII.GetString(data));
            return data.Length;
        };
        easy.SetOption(CurlOption.Url, url);
        easy.SetOption(CurlOption.Nobody, 1L);
        easy.SetOption(CurlOption.Header, header);
        easy.SetOption(CurlOption.Writefunction, write);
        if (headerFunction)
        {
            easy.SetOption(CurlOption.Headerfunction, headerCallback);
        }

        CurlCode code = easy.Perform();

        return (body.ToString(), headers.ToString(), code);
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
