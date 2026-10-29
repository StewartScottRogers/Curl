using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;

namespace Curl.Engine;

/// <summary>
/// CGR-034 <c>C_75</c> and <c>C_21</c>: the first socket action after the handles are added returns only once every
/// transfer waits on its real socket, so a connect that completes at once is told as the read wait it moves on to.
/// </summary>
public sealed partial class Cgr034Tests
{
    [TestMethod]
    public async Task SocketAction_ConnectCompletesAtOnceThenReadWaits_KickTellsTheSocketForReadingAndTheDeadline()
    {
        SocketDialer dialer = new(null);
        List<CurlPoll> polls = [];
        long timer = 0;
        CurlSocket? socket = null;
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        multi.Options = new MultiOptions
        {
            Socket = (_, s, poll, _) =>
            {
                socket = s;
                polls.Add(poll);
                return 0;
            },
            Timer = milliseconds =>
            {
                timer = milliseconds;
                return 0;
            },
        };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" },
            // Far enough off that a loaded runner reaches the read wait before it, so the transfer is not timed out
            // (told Remove) while the action lets it go on (BL-4089).
            Transfer = new EasyTransferOptions { Timeout = LongestWait * 2 },
        };

        multi.Add(easy);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        await multi.SocketActionAsync(socket!.Value, CurlSocketSelect.Out, CancellationToken.None);

        Assert.IsNotEmpty(polls);
        Assert.AreEqual(CurlPoll.In, polls[^1], string.Join(" ", polls));
        Assert.IsGreaterThan(0, timer);
    }
}
