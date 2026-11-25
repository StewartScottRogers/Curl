using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport;

/// <summary>Checks that a CONNECT the server refuses (draft-ietf-webtrans-http3-16 section 3.2) fails the transfer (BL-5295).</summary>
[TestClass]
public sealed class WebTransportRefusedConnectTests
{
    private const int HangTimeoutMilliseconds = 60_000;

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Status401ThenFin_FailsWithExit22NamingTheRefusalAndWritesNothing()
    {
        ScriptedHttp3Connection connection = new() { ConnectAnswer = ScriptedHttp3Connection.Headers("401"), EndsConnectAfterAnswer = true };

        (TransferResult result, string output) = await RunAsync(connection, "hello refused");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The server refused the WebTransport session: HTTP status 401", result.ErrorMessage);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Status200_StillExitsZeroAndEchoes()
    {
        ScriptedHttp3Connection connection = new() { ConnectAnswer = ScriptedHttp3Connection.Headers("200"), AnswerStream = WebTransportProtocolHandlerTests.Echo };

        (TransferResult result, string output) = await RunAsync(connection, "hello accepted");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello accepted", output);
    }

    private static async Task<(TransferResult Result, string Output)> RunAsync(IMultiplexedConnection connection, string data)
    {
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("webtransport://example.com/"), PostData = Encoding.UTF8.GetBytes(data), Output = output };
        TransferResult result = await new WebTransportProtocolHandler((_, _) => ValueTask.FromResult(connection)).ExecuteAsync(context);
        return (result, Encoding.UTF8.GetString(output.ToArray()));
    }
}
