using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the h2c upgrade <c>--http2</c> asks for over cleartext (BL-716) against curl.se's
/// nghttp2 build of curl 8.18.0 through <c>Record-CurlExchange.ps1</c>: the request bytes it
/// sent with <c>-A curl/8.18.0</c>, and what it wrote for a <c>101</c> followed by HTTP/2
/// frames and for a server that ignores the upgrade (BL-716 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string UpgradeRequest =
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:48717\r\nUser-Agent: curl/8.18.0\r\nAccept: */*\r\n"
        + "Upgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\nConnection: Upgrade, HTTP2-Settings\r\n\r\n";

    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";

    /// <summary>The server's empty SETTINGS, then <c>:status 200</c> and <c>hi\n</c> on stream 1, as the recording sent them.</summary>
    private const string UpgradedResponseFrames =
        "000000040000000000" + "00000101040000000188" + "000003000100000001" + "68690a";

    /// <summary>The acknowledgement of the server's SETTINGS curl sent after its preface.</summary>
    private const string SettingsAcknowledgement = "000000040100000000";

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeAnswered101_SendsThePrefaceAndReadsTheResponseFromStream1()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] response = [.. Encoding.Latin1.GetBytes(SwitchingProtocolsHead), .. Convert.FromHexString(UpgradedResponseFrames)];
            ScriptedConnection connection = new(response, chunkSize);
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", output, headerOutput, events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(SwitchingProtocolsHead + "HTTP/2 200 \r\n\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hi\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode);
            Assert.AreEqual(new Version(2, 0), result.Report.HttpVersion);
            StringAssert.StartsWith(
                Convert.ToHexString(connection.Written),
                Convert.ToHexString(Encoding.Latin1.GetBytes(UpgradeRequest)) + Http2Preface.ToUpperInvariant() + SettingsAcknowledgement,
                $"Chunk size {chunkSize}");
            Assert.IsFalse(connection.IsMarkedReusable, "an upgraded connection is never pooled without its session");
            CollectionAssert.Contains(events.Info, HttpConnectionInfoLines.SwitchingToHttp2);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeAnswered101WithFramesInTheSameRead_ReportsTheBytesCopied()
    {
        byte[] response = [.. Encoding.Latin1.GetBytes(SwitchingProtocolsHead), .. Convert.FromHexString(UpgradedResponseFrames)];
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", new MemoryStream(), null, events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        int switching = events.Info.IndexOf(HttpConnectionInfoLines.SwitchingToHttp2);
        Assert.AreEqual("Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=31", events.Info[switching + 1]);
        int http2Head = events.Events.IndexOf("< HTTP/2 200 \r\n");
        Assert.IsGreaterThan(events.Events.IndexOf("* " + HttpConnectionInfoLines.SwitchingToHttp2), http2Head);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeIgnored_ReadsTheHttp11ResponseAndKeepsTheConnection()
    {
        // curl --http2 -v http://127.0.0.1:48716/ against a server answering 200 over HTTP/1.1.
        const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhi\n";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(Response, chunkSize, UpgradeRequest.Replace("48717", "48716", StringComparison.Ordinal));
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(UpgradeContext("http://127.0.0.1:48716/", output, headerOutput, events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hi\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion);
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}: curl leaves the connection intact");
            CollectionAssert.DoesNotContain(events.Info, HttpConnectionInfoLines.SwitchingToHttp2);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2OverTls_SendsNoUpgrade()
    {
        const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";
        const string Request = "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.18.0\r\nAccept: */*\r\n\r\n";
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Response), 65536, Encoding.Latin1.GetBytes(Request)) { IsSecure = true };
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UpgradeContext("https://example.com/", output, null, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeAnswered101ThenClosed_FailsAsHttp2FramingError()
    {
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(SwitchingProtocolsHead), 65536);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", new MemoryStream(), null, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
    }

    private static TransferContext UpgradeContext(string url, Stream output, Stream? headerOutput, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = headerOutput,
            Events = events,
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2, UserAgent = MeasuredUserAgent },
        };
}
