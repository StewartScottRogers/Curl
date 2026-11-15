using System.Text;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// The built-in progress meter is finished with the transfer's own success: a done transfer draws more than a failed one (BL-5245, AF-0794).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_MeterOnAndTransferSucceeds_FinishesTheMeterWithMoreDrawsThanAFailedTransfer()
    {
        int succeededDraws = await MeterDrawsOfAsync("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789");
        int failedDraws = await MeterDrawsOfAsync("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123");

        Assert.IsGreaterThan(failedDraws, succeededDraws);
    }

    private static async Task<int> MeterDrawsOfAsync(string response)
    {
        using MemoryStream standardError = new();
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false, StandardError = standardError },
        };

        await easy.PerformAsync(CancellationToken.None);

        return Encoding.UTF8.GetString(standardError.ToArray()).Count(c => c == '\r');
    }
}
