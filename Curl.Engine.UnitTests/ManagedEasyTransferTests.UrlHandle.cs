using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_UrlHandleSetOverAUrl_TransfersTheHandlesUrl()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        using ManagedCurlUrlHandle handle = new();
        handle.Set(CurlUrlPart.Url, "http://127.0.0.1:8990/658", CurlUrlFlags.None);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://www.example.com", UrlHandle = handle } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(8990, connector.Targets.Single().Port);
        StringAssert.StartsWith(Encoding.ASCII.GetString(connector.Written), "GET /658 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_UrlHandleWithNoHost_FailsWithNoUrlSet()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        using ManagedCurlUrlHandle handle = new();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://www.example.com", UrlHandle = handle } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.AreEqual(ManagedEasyTransfer.NoUrlSetText, easy.ErrorText);
    }
}
