using System.IO.Compression;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the <c>Accept-Encoding</c> line each <c>CURLOPT_ACCEPT_ENCODING</c> sends: the string exactly as given,
/// every supported encoding for the empty string and no line for NULL, with <c>CURLINFO_REQUEST_SIZE</c> the
/// recorded request's length, and a gzip body still decoded under <c>"gzip"</c> (ADR-0777, BL-3191).
/// </summary>
[TestClass]
public sealed class AcceptEncodingWireTests
{
    private const string EmptyResponse = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    [DataRow("gzip", "gzip")]
    [DataRow("identity", "identity")]
    [DataRow("", "deflate, gzip, br, zstd")]
    public async Task PerformAsync_AcceptEncodingSet_SendsThatAcceptEncodingLine(string acceptEncoding, string sent)
    {
        (ScriptedConnector connector, IEasyTransfer easy) = await PerformAsync(acceptEncoding, Encoding.ASCII.GetBytes(EmptyResponse), _ => { });

        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.Contains(written, $"\r\nAccept-Encoding: {sent}\r\n");
        Assert.AreEqual(connector.Written.Length, easy.Info.RequestSize);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task PerformAsync_AcceptEncodingNull_SendsNoAcceptEncodingLine()
    {
        (ScriptedConnector connector, IEasyTransfer easy) = await PerformAsync(null, Encoding.ASCII.GetBytes(EmptyResponse), _ => { });

        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.DoesNotContain("Accept-Encoding", written);
        Assert.AreEqual(connector.Written.Length, easy.Info.RequestSize);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task PerformAsync_AcceptEncodingGzipAndGzipBody_WritesTheDecodedBody()
    {
        byte[] compressed = Gzip("hello, decoded");
        byte[] response = [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: {compressed.Length}\r\n\r\n"), .. compressed];
        StringBuilder body = new();

        (_, IEasyTransfer easy) = await PerformAsync("gzip", response, text => body.Append(text));

        Assert.AreEqual("hello, decoded", body.ToString());
        await easy.DisposeAsync();
    }

    private static async Task<(ScriptedConnector Connector, IEasyTransfer Easy)> PerformAsync(string? acceptEncoding, byte[] response, Action<string> onBody)
    {
        ScriptedConnector connector = new([response]);
        IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { AcceptEncoding = acceptEncoding },
            Callbacks = new EasyCallbackOptions { Write = bytes => { onBody(Encoding.ASCII.GetString(bytes)); return bytes.Length; } },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        return (connector, easy);
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
