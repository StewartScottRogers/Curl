using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-139 (BL-3668): a server whose first frame is not its SETTINGS fails the transfer
/// with exit 16, and the connection is closed with GOAWAY PROTOCOL_ERROR "SETTINGS expected", as
/// curl 8.18.0 with nghttp2 1.68 measured against a server that never sends SETTINGS.
/// </summary>
[TestClass]
public sealed class Cgr139Tests
{
    // GOAWAY: length 25, type 7, no flags, stream 0; last stream 0, PROTOCOL_ERROR, "SETTINGS expected".
    private const string SettingsExpectedGoAway = "000019070000000000" + "00000000" + "00000001" + "53455454494E4753206578706563746564";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false, DisplayName = "HEADERS first")]
    [DataRow(true, DisplayName = "SETTINGS ACK, then HEADERS")]
    public async Task ReadAsync_ServerSendsNoSettings_FailsWithExit16AndSendsGoAwaySettingsExpected(bool acknowledgesFirst)
    {
        HpackEncoder server = new();
        List<Http2Frame> frames = [];
        if (acknowledgesFirst)
        {
            frames.Add(Http2FrameFactory.CreateSettingsAcknowledgement());
        }

        frames.Add(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true));
        frames.Add(Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true));
        byte[] scripted = [.. frames.SelectMany(Http2FrameCodec.Serialize)];
        Diagnostics.Arrange("server frames", (acknowledgesFirst ? "SETTINGS ACK, " : string.Empty) + "HEADERS 1, DATA 1 hello end; never SETTINGS");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection stream = await StartAsync(session);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadAsync(stream));

        string written = Convert.ToHexString(connection.Written);
        Diagnostics.Act("exit code", failure.ExitCode);
        Diagnostics.Act("written ends", written[^SettingsExpectedGoAway.Length..]);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, failure.ExitCode);
        Diagnostics.Assert("written ends", SettingsExpectedGoAway, written[^SettingsExpectedGoAway.Length..]);
        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
        Assert.AreEqual("Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.", failure.Message);
        Assert.EndsWith(SettingsExpectedGoAway, written);
    }

    [TestMethod]
    public async Task ReadAsync_ServerSendsNoSettingsAndTheGoAwayCannotBeWritten_StillFailsWithExit16()
    {
        HpackEncoder server = new();
        byte[] scripted = Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: true, isEndHeaders: true)).ToArray();
        Diagnostics.Arrange("server", "HEADERS 1 end, never SETTINGS; every write after the first read is reset");
        WriteFailsAfterReadConnection connection = new(scripted);
        Http2Session session = new(connection);
        Http2StreamConnection stream = await StartAsync(session);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadAsync(stream));

        Diagnostics.Act("exit code", failure.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
        Assert.AreEqual(HttpTransferMessages.Http2SettingsExpected, failure.Message);
    }

    [TestMethod]
    public async Task ReadAsync_ServerSendsSettingsFirst_ReadsTheResponse()
    {
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true)),
        ];
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1, DATA 1 hello end");
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection stream = await StartAsync(session);

        string response = await ReadAsync(stream);

        Diagnostics.Act("response", response.Replace("\r\n", "\\r\\n"));
        Diagnostics.Assert("response", "HTTP/2 200 \\r\\n\\r\\nhello", response.Replace("\r\n", "\\r\\n"));
        Assert.AreEqual("HTTP/2 200 \r\n\r\nhello", response);
    }

    private static async Task<Http2StreamConnection> StartAsync(Http2Session session)
    {
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync("GET /x HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), CancellationToken.None);
        return stream;
    }

    private static async Task<string> ReadAsync(Http2StreamConnection stream)
    {
        MemoryStream response = new();
        byte[] buffer = new byte[256];
        int read;
        while ((read = await stream.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            response.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(response.ToArray());
    }

    /// <summary>
    /// A connection that replays <paramref name="response" /> and resets every write made after
    /// its first read, as a server that hung up straight after its reply does.
    /// </summary>
    private sealed class WriteFailsAfterReadConnection(byte[] response) : IConnection
    {
        private int position;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(buffer.Length, response.Length - position);
            response.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            position > 0 ? throw new IOException("Connection reset.") : ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
