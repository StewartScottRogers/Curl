using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    private const string Redirect = "HTTP/1.1 302 Found\r\nLocation: http://other/y\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task Headers_RedirectFollowed_RecordsEachRequestsHeadersInItsOwnRequest()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Redirect), Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { FollowLocation = FollowLocation.All },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        easy.Headers.TryGet("location", 0, CurlHeaderOrigin.Header, 0, out CurlHeader? location);
        easy.Headers.TryGet("Content-Type", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out CurlHeader? contentType);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(new CurlHeader("Location", "http://other/y", 1, 0, CurlHeaderOrigin.Header), location);
        Assert.AreEqual(new CurlHeader("Content-Type", "text/plain", 1, 0, CurlHeaderOrigin.Header), contentType);
        Assert.AreEqual(CurlHeaderCode.Missing, easy.Headers.TryGet("Location", 0, CurlHeaderOrigin.Header, 1, out _));
        Assert.AreEqual(CurlHeaderCode.NoRequest, easy.Headers.TryGet("Location", 0, CurlHeaderOrigin.Header, 2, out _));
    }

    [TestMethod]
    public async Task Headers_InformationalResponseBeforeTheFinalOne_RecordsItsHeadersAs1xxAndPassesEveryLineToTheCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 103 Early Hints\r\nLink: </s.css>\r\n\r\n" + Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        StringBuilder callback = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new()
            {
                Header = header =>
                {
                    callback.Append(Encoding.ASCII.GetString(header));
                    return header.Length;
                },
            },
        };

        await easy.PerformAsync(CancellationToken.None);
        CurlHeader? first = easy.Headers.Next(CurlHeaderOrigin.Informational | CurlHeaderOrigin.Header, 0, null);

        Assert.AreEqual(new CurlHeader("Link", "</s.css>", 1, 0, CurlHeaderOrigin.Informational), first);
        StringAssert.Contains(callback.ToString(), "Content-Type: text/plain\r\n");
    }

    [TestMethod]
    public async Task Headers_NonHttpTransfer_RecordsNone()
    {
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "file:///no/such/file" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlHeaderCode.NoHeaders, easy.Headers.TryGet("Server", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out _));
    }

    [TestMethod]
    public async Task Headers_AfterResetOrTheNextPerform_AreForgotten()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        await easy.PerformAsync(CancellationToken.None);
        CurlHeaderCode afterPerform = easy.Headers.TryGet("Content-Type", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out _);

        easy.Reset();

        Assert.AreEqual(CurlHeaderCode.Ok, afterPerform);
        Assert.AreEqual(CurlHeaderCode.NoHeaders, easy.Headers.TryGet("Content-Type", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out _));
    }
}
