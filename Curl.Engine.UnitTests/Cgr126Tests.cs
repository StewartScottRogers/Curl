using System.IO.Compression;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-126: <c>CURLINFO_SIZE_DELIVERED</c> is the bytes the write callback took - the decoded body under
/// <c>CURLOPT_ACCEPT_ENCODING</c>, the header lines and body under <c>CURLOPT_HEADER</c> - not
/// <c>CURLINFO_SIZE_DOWNLOAD_T</c>, as libcurl 8.22.0 answers (5 and 69 for the issue's repro).
/// </summary>
[TestClass]
public sealed class Cgr126Tests
{
    private const string PlainResponse = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes()
    {
        byte[] compressed = Gzip("hello");
        byte[] response = [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: {compressed.Length}\r\n\r\n"), .. compressed];

        IEasyTransfer easy = await PerformAsync(response, new EasyHttpOptions { AcceptEncoding = "" }, new EasyBehaviourOptions());

        Assert.AreEqual(compressed.Length, easy.Info.SizeDownload);
        Assert.AreEqual(5L, easy.Info.SizeDelivered);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task PerformAsync_IncludeHeader_DeliversTheHeaderLinesAndTheBody()
    {
        IEasyTransfer easy = await PerformAsync(Encoding.ASCII.GetBytes(PlainResponse), new EasyHttpOptions(), new EasyBehaviourOptions { IncludeHeader = true });

        Assert.AreEqual(5L, easy.Info.SizeDownload);
        Assert.AreEqual((long)PlainResponse.Length, easy.Info.SizeDelivered);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task PerformAsync_NoWriteCallback_DeliversTheDownloadedSize()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(PlainResponse)]);
        IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));

        Assert.AreEqual(5L, easy.Info.SizeDelivered);
        await easy.DisposeAsync();
    }

    private static async Task<IEasyTransfer> PerformAsync(byte[] response, EasyHttpOptions http, EasyBehaviourOptions behaviour)
    {
        ScriptedConnector connector = new([response]);
        IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = http,
            Behaviour = behaviour,
            Callbacks = new EasyCallbackOptions { Write = bytes => bytes.Length },
        };

        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));
        return easy;
    }

    private static byte[] Gzip(string text)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(Encoding.ASCII.GetBytes(text));
        }

        return output.ToArray();
    }

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
