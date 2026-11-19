using Curl.Api.EngineContracts.WebSocket;
using Libcurl.Easy;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Libcurl.UnitTests.WebSocket;

/// <summary>Compiles and runs the receive loop that <c>Libcurl/README.md</c> shows under "Waiting for a WebSocket frame".</summary>
[TestClass]
public sealed class ReadmeWebSocketWaitSampleTests
{
    private static async Task<int> ReceiveLoopAsync(CurlEasy easy, CancellationToken cancellationToken)
    {
        var frames = 0;
        var buffer = new byte[4096];
        while (true)
        {
            var code = easy.WebSocketRecv(buffer, out var received, out var meta);
            if (code == CurlCode.Again)
            {
                await easy.WebSocketWaitAsync(cancellationToken);
                continue;
            }

            if (code != CurlCode.Ok)
            {
                break;
            }

            Console.WriteLine($"{received} bytes, flags {meta?.Flags}");
            frames++;
        }

        return frames;
    }

    [TestMethod]
    public async Task ReceiveLoop_OnAnEngineThatAnswersGotNothing_EndsWithoutWaiting()
    {
        var transfer = new FakeWebSocketTransfer();
        transfer.FakeWebSocket.ReceiveResult = new CurlWebSocketReceiveResult(CurlCode.GotNothing, 0, default);
        var easy = new CurlEasy(transfer, new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>()));

        Assert.AreEqual(0, await ReceiveLoopAsync(easy, CancellationToken.None));
    }
}
