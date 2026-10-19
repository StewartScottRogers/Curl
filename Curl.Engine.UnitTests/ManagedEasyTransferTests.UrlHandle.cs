using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Api.Options.Http;
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
    public async Task PerformAsync_UrlHandleFollowedThenPerformedAgain_StartsAgainFromTheHandlesUrl()
    {
        string redirect = "HTTP/1.1 301 Moved\r\nLocation: /next\r\nContent-Length: 0\r\n\r\n";
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(redirect), Encoding.ASCII.GetBytes(Response), Encoding.ASCII.GetBytes(redirect), Encoding.ASCII.GetBytes(Response)]) { ReusesTheFirstConnection = true };
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        using ManagedCurlUrlHandle handle = new();
        handle.Set(CurlUrlPart.Url, "http://127.0.0.1:8990/1567", CurlUrlFlags.None);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { UrlHandle = handle }, Http = new EasyHttpOptions { FollowLocation = FollowLocation.All } };

        CurlCode first = await easy.PerformAsync(CancellationToken.None);
        CurlCode second = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, first);
        Assert.AreEqual(CurlCode.Ok, second);
        string[] requestLines = [.. Encoding.ASCII.GetString(connector.Written).Split("\r\n").Where(line => line.StartsWith("GET ", StringComparison.Ordinal))];
        CollectionAssert.AreEqual(new[] { "GET /1567 HTTP/1.1", "GET /next HTTP/1.1", "GET /1567 HTTP/1.1", "GET /next HTTP/1.1" }, requestLines);
        Assert.AreEqual(CurlUrlCode.Ok, handle.Get(CurlUrlPart.Path, CurlUrlFlags.None, out string? path));
        Assert.AreEqual("/1567", path);
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
