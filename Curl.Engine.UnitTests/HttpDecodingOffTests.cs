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
/// Pins <c>CURLOPT_HTTP_CONTENT_DECODING</c> and <c>CURLOPT_HTTP_TRANSFER_DECODING</c> through the easy API:
/// each set to 0 delivers its coding undecoded on its own - a gzip, broken gzip or unknown Content-Encoding
/// as received with exit 0, a chunked body with its chunk framing through the zero-length chunk and its
/// trailers, as curl 8.22.0 writes it under <c>--raw</c> (BL-3162 Notes) - while at 1 they still decode,
/// and a broken gzip body still fails with 61 (ADR-0780, BL-3192).
/// </summary>
[TestClass]
public sealed class HttpDecodingOffTests
{
    private const string ChunkedBody = "6\r\nhello \r\n5\r\nworld\r\n0\r\nX-T: v\r\n\r\n";

    private static readonly byte[] BrokenGzip = [0x1F, 0x8B, 0x08, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x03, 0xDE, 0xAD, 0xBE, 0xEF];

    [TestMethod]
    public async Task PerformAsync_ContentDecodingOffOverGzip_WritesTheGzipBytes()
    {
        byte[] gzip = Gzip("hello, decoded");

        (CurlCode code, byte[] body) = await PerformAsync(EncodedResponse("gzip", gzip), contentDecoding: false, transferDecoding: true);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(gzip, body);
    }

    [TestMethod]
    public async Task PerformAsync_ContentDecodingOnOverGzip_WritesTheDecodedBody()
    {
        (CurlCode code, byte[] body) = await PerformAsync(EncodedResponse("gzip", Gzip("hello, decoded")), contentDecoding: true, transferDecoding: true);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("hello, decoded", Encoding.ASCII.GetString(body));
    }

    [TestMethod]
    public async Task PerformAsync_ContentDecodingOffOverBrokenGzip_WritesTheBytesAndSucceeds()
    {
        (CurlCode code, byte[] body) = await PerformAsync(EncodedResponse("gzip", BrokenGzip), contentDecoding: false, transferDecoding: true);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(BrokenGzip, body);
    }

    [TestMethod]
    public async Task PerformAsync_ContentDecodingOnOverBrokenGzip_FailsWithBadContentEncoding()
    {
        (CurlCode code, _) = await PerformAsync(EncodedResponse("gzip", BrokenGzip), contentDecoding: true, transferDecoding: true);

        Assert.AreEqual(CurlCode.BadContentEncoding, code);
    }

    [TestMethod]
    public async Task PerformAsync_ContentDecodingOffOverAnUnknownEncoding_WritesTheBytesAndSucceeds()
    {
        byte[] encoded = Encoding.ASCII.GetBytes("not decoded");

        (CurlCode code, byte[] body) = await PerformAsync(EncodedResponse("x-unknown", encoded), contentDecoding: false, transferDecoding: true);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(encoded, body);
    }

    [TestMethod]
    public async Task PerformAsync_ContentDecodingOnOverAnUnknownEncoding_FailsWithBadContentEncoding()
    {
        (CurlCode code, _) = await PerformAsync(EncodedResponse("x-unknown", Encoding.ASCII.GetBytes("not decoded")), contentDecoding: true, transferDecoding: true);

        Assert.AreEqual(CurlCode.BadContentEncoding, code);
    }

    [TestMethod]
    public async Task PerformAsync_TransferDecodingOffOverChunked_WritesTheChunkFraming()
    {
        (CurlCode code, byte[] body) = await PerformAsync(ChunkedResponse(), contentDecoding: true, transferDecoding: false);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(ChunkedBody, Encoding.ASCII.GetString(body));
        Assert.HasCount(34, body);
    }

    [TestMethod]
    public async Task PerformAsync_TransferDecodingOnOverChunked_WritesTheDecodedBody()
    {
        (CurlCode code, byte[] body) = await PerformAsync(ChunkedResponse(), contentDecoding: true, transferDecoding: true);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("hello world", Encoding.ASCII.GetString(body));
    }

    [TestMethod]
    public async Task PerformAsync_TransferDecodingOffOverChunkedGzip_DecodesTheGzipInsideTheChunks()
    {
        byte[] gzip = Gzip("hi");
        byte[] chunked = [.. Encoding.ASCII.GetBytes($"{gzip.Length:x}\r\n"), .. gzip, .. Encoding.ASCII.GetBytes("\r\n0\r\n\r\n")];
        byte[] response = [.. Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nTransfer-Encoding: chunked\r\n\r\n"), .. chunked];

        (CurlCode code, byte[] body) = await PerformAsync(response, contentDecoding: false, transferDecoding: false);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(chunked, body);
    }

    private static byte[] EncodedResponse(string contentEncoding, byte[] body) =>
        [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Encoding: {contentEncoding}\r\nContent-Length: {body.Length}\r\n\r\n"), .. body];

    private static byte[] ChunkedResponse() =>
        Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n" + ChunkedBody);

    private static async Task<(CurlCode Code, byte[] Body)> PerformAsync(byte[] response, bool contentDecoding, bool transferDecoding)
    {
        ScriptedConnector connector = new([response]);
        MemoryStream body = new();
        IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { AcceptEncoding = string.Empty, HttpContentDecoding = contentDecoding, HttpTransferDecoding = transferDecoding },
            Callbacks = new EasyCallbackOptions { Write = bytes => { body.Write(bytes); return bytes.Length; } },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        await easy.DisposeAsync();
        return (code, body.ToArray());
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
