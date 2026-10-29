using System.Net;
using System.Net.Sockets;

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-034 <c>C_65</c> and <c>C_21</c>: a refused connect tells the socket callback the connecting socket with
/// <c>CURL_POLL_OUT</c>, then its removal, and the timer -1, ending with <c>CURLE_COULDNT_CONNECT</c>; three transfers
/// with <c>curl_multi_assign</c> each get their own real socket and the value assigned to it back in every later socket
/// callback. The stand-ins they drive are <see cref="Cgr034Tests" />'s.
/// </summary>
public sealed partial class Cgr034Tests
{
    [TestMethod]
    public async Task SocketAction_RefusedConnect_TellsOutOnTheConnectingSocketThenRemoveWithCouldntConnect()
    {
        SocketDialer dialer = new(Response) { Held = true, Refuses = true };
        List<string> calls = [];
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        CurlSocket? socket = null;
        multi.Options = new MultiOptions
        {
            Socket = (_, s, poll, _) =>
            {
                calls.Add($"S={(int)poll}");
                socket = s;
                return 0;
            },
            Timer = milliseconds =>
            {
                calls.Add($"T{milliseconds}");
                return 0;
            },
        };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        multi.Add(easy);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        CurlSocket connecting = socket!.Value;
        dialer.Release.SetResult();
        (CurlMultiCode code, int running) = (CurlMultiCode.Ok, 1);
        for (int step = 0; running > 0 && step < 50; step++)
        {
            (code, running) = await multi.SocketActionAsync(connecting, CurlSocketSelect.Out, CancellationToken.None);
        }

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(0, running);
        Assert.AreEqual(new CurlSocket(Handle), connecting);
        CollectionAssert.AreEqual(new[] { "T0", "S=2", "T-1", "S=4" }, calls, string.Join(" ", calls));
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.CouldntConnect, message.Result);
    }

    [TestMethod]
    public async Task SocketAction_ThreeTransfersAssigned_TellsEachItsOwnSocketWithTheValueAssigned()
    {
        SocketDialer dialer = new(Response);
        Dictionary<CurlSocket, CurlPoll> waiting = [];
        List<string> calls = [];
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        multi.Options = new MultiOptions
        {
            Socket = (_, s, poll, state) =>
            {
                calls.Add($"S{s.Handle}={(int)poll}:{state ?? "-"}");
                waiting[s] = poll;
                return 0;
            },
        };
        for (int index = 0; index < 3; index++)
        {
            IEasyTransfer easy = engine.CreateEasy();
            easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };
            multi.Add(easy);
        }

        (_, int running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        for (int step = 0; running > 0 && step < 20; step++)
        {
            foreach ((CurlSocket s, CurlPoll poll) in waiting.ToArray())
            {
                Assert.AreEqual(CurlMultiCode.Ok, multi.Assign(s, $"a{s.Handle}"));
                waiting.Remove(s);
                (_, running) = await multi.SocketActionAsync(s, (CurlSocketSelect)(int)poll, CancellationToken.None);
            }
        }

        Assert.AreEqual(0, running);
        foreach (nint handle in new nint[] { Handle, Handle + 1, Handle + 2 })
        {
            CollectionAssert.AreEqual(
                new[] { $"S{handle}=2:-", $"S{handle}=1:a{handle}", $"S{handle}=4:a{handle}" },
                calls.Where(call => call.StartsWith($"S{handle}=", StringComparison.Ordinal)).ToArray(),
                string.Join(" ", calls));
        }

        for (int index = 0; index < 3; index++)
        {
            Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
            Assert.AreEqual(CurlCode.Ok, message.Result);
        }
    }
}
