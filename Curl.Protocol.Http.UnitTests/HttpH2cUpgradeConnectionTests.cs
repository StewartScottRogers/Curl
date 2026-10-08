using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpH2cUpgradeConnection" />: a response that is not <c>HTTP/1.1 101</c> is
/// read through unchanged, and a <c>101</c> head is given whole before the reads switch to
/// HTTP/2 stream 1, wherever the head ends and whatever follows it.
/// </summary>
[TestClass]
public sealed class HttpH2cUpgradeConnectionTests
{
    private static readonly int[] ChunkSizes = [1, 7, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    /// <summary>The server's empty SETTINGS, then <c>:status 200</c> and a body on stream 1.</summary>
    private static byte[] Frames(string body) =>
    [
        .. Convert.FromHexString("000000040000000000" + "00000101040000000188"),
        .. Convert.FromHexString("0000" + body.Length.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + "000100000001"),
        .. Encoding.Latin1.GetBytes(body),
    ];

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", DisplayName = "200")]
    [DataRow("HTTP/1.0 101 Switching Protocols\r\n\r\n", DisplayName = "HTTP/1.0 101")]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\n\r\n", DisplayName = "100 first")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n", DisplayName = "101 closed before its empty line")]
    [DataRow("HTTP/1.1", DisplayName = "Closed inside the status line")]
    public async Task ReadAsync_NotAWhole101Head_GivesTheResponseUnchanged(string response)
    {
        Diagnostics.Bytes("response", Encoding.Latin1.GetBytes(response));
        foreach (int chunkSize in ChunkSizes)
        {
            HttpH2cUpgradeConnection connection = new(new ScriptedConnection(Encoding.Latin1.GetBytes(response), chunkSize), new RecordingTransferEvents(), "http");
            Diagnostics.Arrange("chunk size", chunkSize);

            string read = await ReadAllAsync(connection);

            Diagnostics.Act("upgraded", connection.IsUpgraded);
            Diagnostics.Diff("read", response, read);
            Assert.AreEqual(response, read, $"Chunk size {chunkSize}");
            Assert.IsFalse(connection.IsUpgraded, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\n", "hi\n", DisplayName = "CRLF head")]
    [DataRow("HTTP/1.1 101 Switching Protocols\nUpgrade: h2c\n\n", "hi\n", DisplayName = "LF head")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\n", "a\n\nb", DisplayName = "CRLF head, LF empty line in the body")]
    [DataRow("HTTP/1.1 101 Switching Protocols\nUpgrade: h2c\n\n", "a\n\r\nb", DisplayName = "LF head, CRLF empty line in the body")]
    public async Task ReadAsync_Switching101_GivesTheHeadThenStream1(string head, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection transport = new([.. Encoding.Latin1.GetBytes(head), .. Frames(body)], chunkSize);
            RecordingTransferEvents events = new();
            HttpH2cUpgradeConnection connection = new(transport, events, "http");
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Bytes("head", Encoding.Latin1.GetBytes(head));
            Diagnostics.Bytes("body", Encoding.Latin1.GetBytes(body));

            string read = await ReadAllAsync(connection);

            Diagnostics.Act("upgraded", connection.IsUpgraded);
            Diagnostics.Act("info lines", string.Join(" | ", events.Info));
            Diagnostics.Diff("read", head + "HTTP/2 200 \r\n\r\n" + body, read);
            Assert.AreEqual(head + "HTTP/2 200 \r\n\r\n" + body, read, $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsUpgraded, $"Chunk size {chunkSize}");
            Assert.AreEqual(HttpConnectionInfoLines.SwitchingToHttp2, events.Info[0], $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_GoToTheTransport()
    {
        ScriptedConnection transport = new([], 1);
        HttpH2cUpgradeConnection connection = new(transport, new RecordingTransferEvents(), "http");
        Diagnostics.Arrange("written", "GET");

        await connection.WriteAsync("GET"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        Diagnostics.Bytes("transport written", transport.Written);
        Diagnostics.Act("transport written", Encoding.Latin1.GetString(transport.Written));
        Diagnostics.Diff("transport written", "GET"u8, transport.Written);
        CollectionAssert.AreEqual("GET"u8.ToArray(), transport.Written);
    }

    [TestMethod]
    public async Task Properties_AreTheTransports_AndDisposingLeavesItOpen()
    {
        ScriptedConnection transport = new([], 1) { IsSecure = true };
        HttpH2cUpgradeConnection connection = new(transport, new RecordingTransferEvents(), "http");
        Diagnostics.Arrange("transport", "secure, no end points");

        await connection.DisposeAsync();

        Diagnostics.Act("secure, transport disposed", $"{connection.IsSecure}, {transport.IsDisposed}");
        Diagnostics.Assert("secure, transport disposed", "True, False", $"{connection.IsSecure}, {transport.IsDisposed}");
        Assert.IsTrue(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsNull(connection.LocalEndPoint);
        Assert.IsFalse(transport.IsDisposed);
    }

    private static async Task<string> ReadAllAsync(IConnection connection)
    {
        MemoryStream read = new();
        byte[] buffer = new byte[5];
        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            read.Write(buffer, 0, count);
        }

        return Encoding.Latin1.GetString(read.ToArray());
    }
}
