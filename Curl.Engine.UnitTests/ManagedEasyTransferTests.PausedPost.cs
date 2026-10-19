using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// A read-callback POST paused until the write callback unpauses it, answered early with a 401 (BL-2646, upstream test1533).
public sealed partial class ManagedEasyTransferTests
{
    private const string EarlyUnauthorizedResponse = "HTTP/1.1 401 Unauthorized\r\nContent-Length: 15\r\n\r\nAccess denied!\n";

    [TestMethod]
    public async Task PerformAsync_PostPausedUntilTheWriteCallbackUnpausesWithKeepSendingOnError_SendsTheBodyAndFinishes()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(EarlyUnauthorizedResponse)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        (CurlCode code, string received) = await PerformPausedPostAsync(easy, keepSendingOnError: true);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("Access denied!\n", received);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.StartsWith(written, "POST /p HTTP/1.1\r\n");
        StringAssert.EndsWith(written, "Content-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\naaa");
    }

    [TestMethod]
    public async Task PerformAsync_PostPausedUntilTheWriteCallbackUnpausesWithoutKeepSendingOnError_StopsSendingAndFinishes()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(EarlyUnauthorizedResponse)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        (CurlCode code, string received) = await PerformPausedPostAsync(easy, keepSendingOnError: false);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("Access denied!\n", received);
        StringAssert.EndsWith(Encoding.ASCII.GetString(connector.Written), "Content-Type: application/x-www-form-urlencoded\r\n\r\n");
    }

    // lib1533's t1533_read_cb and t1533_write_cb: the read pauses until the response arrives, the write unpauses it.
    private static async Task<(CurlCode Code, string Received)> PerformPausedPostAsync(ManagedEasyTransfer easy, bool keepSendingOnError)
    {
        bool responseReceived = false;
        bool paused = false;
        int remaining = 3;
        MemoryStream received = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/p" },
            Http = new EasyHttpOptions { Post = true, PostFieldSize = 3 },
            Behaviour = new EasyBehaviourOptions { KeepSendingOnError = keepSendingOnError },
            Callbacks = new EasyCallbackOptions
            {
                Read = buffer =>
                {
                    if (!responseReceived)
                    {
                        paused = true;
                        return ReadCallbackResult.Pause;
                    }

                    int count = Math.Min(remaining, buffer.Length);
                    buffer[..count].Fill((byte)'a');
                    remaining -= count;
                    return count;
                },
                Write = data =>
                {
                    responseReceived = true;
                    received.Write(data);
                    if (paused)
                    {
                        paused = false;
                        _ = easy.Pause(CurlPause.Continue);
                    }

                    return data.Length;
                },
            },
        };

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        CurlCode code = await easy.PerformAsync(timeout.Token);
        return (code, Encoding.ASCII.GetString(received.ToArray()));
    }
}
