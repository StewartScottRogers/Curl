using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Where a <c>CURLOPT_NOBODY</c> transfer's header lines go (BL-3199, ADR-0779), as curl 8.22.0 measured with
/// <c>-I --no-include</c>: over HTTP, or an FTP directory, the write callback gets nothing; FTP's <c>-I</c> lines for
/// a file go to it whatever <c>CURLOPT_HEADER</c> says, as upstream's <c>client_write_header</c> forces; and under
/// <c>CURLOPT_HEADER</c> an HTTP head goes to it too.
/// </summary>
[TestClass]
public sealed class NobodyWriteCallbackTests
{
    private const string HttpHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: b\r\n\r\n";

    private const string FtpLoggedIn = "220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string FtpFileInfoReplies = FtpLoggedIn + "200 Type set\r\n213 5\r\n350 Restarting at 0\r\n221 Bye\r\n";

    private const string FtpFileInfoLines = "Content-Length: 5\r\nAccept-ranges: bytes\r\n";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PerformAsync_HttpNobody_GivesTheWriteCallbackNoBytes(bool headerCallback)
    {
        (string body, string headers, CurlCode code) = await PerformAsync("http://h/x", HttpHead, headerCallback, includeHeader: false);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(string.Empty, body);
        Assert.AreEqual(headerCallback ? HttpHead : string.Empty, headers);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PerformAsync_FtpDirectoryNobody_GivesTheWriteCallbackNoBytes(bool headerCallback)
    {
        (string body, _, CurlCode code) = await PerformAsync("ftp://h/d/", FtpLoggedIn + "250 Directory changed\r\n221 Bye\r\n", headerCallback, includeHeader: false);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(string.Empty, body);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PerformAsync_FtpFileNobody_GivesTheWriteCallbackOnlyTheInfoLines(bool headerCallback)
    {
        (string body, string headers, CurlCode code) = await PerformAsync("ftp://h/f.txt", FtpFileInfoReplies, headerCallback, includeHeader: false);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(FtpFileInfoLines, body);
        Assert.AreEqual(
            headerCallback ? FtpLoggedIn + "200 Type set\r\n213 5\r\nContent-Length: 5\r\n350 Restarting at 0\r\nAccept-ranges: bytes\r\n" : string.Empty,
            headers);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PerformAsync_HttpNobodyWithHeaderOption_GivesTheWriteCallbackTheHead(bool headerCallback)
    {
        (string body, string headers, CurlCode code) = await PerformAsync("http://h/x", HttpHead, headerCallback, includeHeader: true);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(HttpHead, body);
        Assert.AreEqual(headerCallback ? HttpHead : string.Empty, headers);
    }

    private static async Task<(string Body, string Headers, CurlCode Code)> PerformAsync(string url, string reply, bool headerCallback, bool includeHeader)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(reply)]);
        await using ManagedCurlEngine engine = new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
        await using IEasyTransfer easy = engine.CreateEasy();
        StringBuilder body = new();
        StringBuilder headers = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Behaviour = new EasyBehaviourOptions { IncludeHeader = includeHeader },
            Transfer = new EasyTransferOptions { NoBody = true },
            Callbacks = new()
            {
                Write = data =>
                {
                    body.Append(Encoding.ASCII.GetString(data));
                    return data.Length;
                },
                Header = headerCallback
                    ? data =>
                    {
                        headers.Append(Encoding.ASCII.GetString(data));
                        return data.Length;
                    }
                : null,
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        return (body.ToString(), headers.ToString(), code);
    }
}
