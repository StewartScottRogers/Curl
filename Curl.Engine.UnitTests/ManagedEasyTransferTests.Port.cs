using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_PortOptionOverAUrlWithNoPort_ConnectsToItAndSendsItInTheHostHeader()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1/522", Port = 8990 } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(8990, connector.Targets.Single().Port);
        Assert.AreEqual("GET /522 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nAccept: */*\r\n\r\n", Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual("http://127.0.0.1/522", easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task PerformAsync_UrlWithNoSchemeAndDotSegments_ReportsTheGuessedSchemeAndNormalisedPathAsEffectiveUrl()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "127.0.0.1:8990/hello/../1907" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("http://127.0.0.1:8990/1907", easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task PerformAsync_PortOptionOverAUrlsPortThroughAProxy_SendsItInTheRequestTargetAndTheHostHeader()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://www.example.com:999/523", Port = 19999 },
            Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128" },
        };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, toolSettings: null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.StartsWith(
            Encoding.ASCII.GetString(written),
            "GET http://www.example.com:19999/523 HTTP/1.1\r\nHost: www.example.com:19999\r\n");
    }
}
