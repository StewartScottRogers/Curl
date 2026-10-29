using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-263: an HTTP/1.0 upload of unknown size is refused before any request is sent, so
/// <c>CURLINFO_REQUEST_SIZE</c> answers 0, as libcurl 8.22.0 does.
/// </summary>
[TestClass]
public sealed class Cgr263Tests
{
    [TestMethod]
    public async Task PerformAsync_Http10UploadOfUnknownSize_AnswersARequestSizeOfZero()
    {
        ScriptedConnector connector = new([]) { ReportsTheConnectionOpened = true };
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:21410/up" },
            Transfer = new EasyTransferOptions { Upload = true },
            Http = new EasyHttpOptions { HttpVersion = HttpVersion.Http10 },
            Callbacks = new() { Read = _ => 0 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UploadFailed, code);
        Assert.AreEqual(0, easy.Info.RequestSize);
    }

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(TimeProvider.System, (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)), readEnvironmentVariable: _ => null);
}
