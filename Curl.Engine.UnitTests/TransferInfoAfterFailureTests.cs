using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the info a transfer keeps when it fails after the response began: libcurl 8.22.0 still reports the
/// response code, header size, HTTP version and the peer's address and port the transfer reached. Measured on
/// 2026-10-22 with curl 8.22.0 (mingw, Schannel) through <c>Record-CurlExchange.ps1</c> on 127.0.0.1:47131 with
/// <c>-w '%{http_code} %{size_header} %{http_version} %{remote_ip} %{remote_port}'</c> (BL-3195 Notes).
/// </summary>
[TestClass]
public sealed class TransferInfoAfterFailureTests
{
    private const string Url = "http://h:47131/";

    /// <summary><c>CURL_HTTP_VERSION_1_1</c>.</summary>
    private const long Http11 = 2;

    /// <summary>curl 8.22.0: exit 8 <c>Header without colon</c>, then <c>200 36 1.1 127.0.0.1 47131</c>.</summary>
    [TestMethod]
    public async Task Perform_HeaderLineWithoutColon_KeepsTheResponseReached()
    {
        (CurlCode code, TransferInfo info) = await PerformAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nBad header line\r\n\r\nhello");

        AssertKept(CurlCode.WeirdServerReply, 200, 36, code, info);
    }

    /// <summary>curl 8.22.0: exit 100 <c>A value or data field grew larger than allowed</c>, then <c>200 36 1.1 127.0.0.1 47131</c>.</summary>
    [TestMethod]
    public async Task Perform_HeaderLineOf400Kilobytes_KeepsTheResponseReached()
    {
        (CurlCode code, TransferInfo info) = await PerformAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Big: " + new string('a', 400_000) + "\r\n\r\nhello");

        AssertKept(CurlCode.TooLarge, 200, 36, code, info);
    }

    /// <summary>curl 8.22.0: exit 8 <c>Multiple Location headers</c>, then <c>302 34 1.1 127.0.0.1 47131</c>.</summary>
    [TestMethod]
    public async Task Perform_TwoLocationHeaders_KeepsTheResponseReached()
    {
        (CurlCode code, TransferInfo info) = await PerformAsync("HTTP/1.1 302 Found\r\nLocation: /a\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n");

        AssertKept(CurlCode.WeirdServerReply, 302, 34, code, info);
    }

    /// <summary>curl 8.22.0 after a refused connect: exit 7 and <c>%{remote_port}</c> <c>-1</c>.</summary>
    [TestMethod]
    public async Task Perform_ConnectRefused_AnswersPrimaryPortMinusOne()
    {
        await using IEasyTransfer easy = CreateEngine(new RefusingConnector()).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = Url } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual(-1, easy.Info.PrimaryPort);
    }

    private static void AssertKept(CurlCode expectedCode, long responseCode, long headerSize, CurlCode code, TransferInfo info)
    {
        Assert.AreEqual(expectedCode, code);
        Assert.AreEqual(responseCode, info.ResponseCode);
        Assert.AreEqual(headerSize, info.HeaderSize);
        Assert.AreEqual(Http11, info.HttpVersion);
        Assert.AreEqual("127.0.0.1", info.PrimaryIp);
        Assert.AreEqual(47131, info.PrimaryPort);
    }

    private static async Task<(CurlCode Code, TransferInfo Info)> PerformAsync(string response)
    {
        EndPointScriptedConnector connector = new(new EndPointScriptedConnector.Script(
            new IPEndPoint(IPAddress.Loopback, 50000), new IPEndPoint(IPAddress.Loopback, 47131), Encoding.Latin1.GetBytes(response)));
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = Url } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        return (code, easy.Info);
    }

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Failed to connect"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);

    /// <summary>A connector whose every connect the peer refuses.</summary>
    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused("Failed to connect"));
    }
}
