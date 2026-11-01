using System.Net;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-280 (BL-3809): a header block that does not decode fails the transfer with exit 16 and
/// closes the connection with GOAWAY COMPRESSION_ERROR (code 9), not the NO_ERROR <c>shutdown</c> GOAWAY.
/// </summary>
[TestClass]
public sealed class Cgr280Tests
{
    // GOAWAY: length 8, type 7, no flags, stream 0; last stream 0, COMPRESSION_ERROR, no debug data.
    private const string CompressionErrorGoAway = "000008070000000000" + "00000000" + "00000009";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_HeaderBlockDoesNotDecode_FailsWithExit16AndSendsGoAwayCompressionError()
    {
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new byte[] { 0xFF, 0xFF, 0xFF }, isEndStream: true, isEndHeaders: true)),
        ];
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1 with the undecodable block FF FF FF");
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync("GET /x HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), CancellationToken.None);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () =>
        {
            byte[] buffer = new byte[256];
            while (await stream.ReadAsync(buffer, CancellationToken.None) > 0)
            {
            }
        });

        string written = Convert.ToHexString(connection.Written);
        Diagnostics.Act("exit code", failure.ExitCode);
        Diagnostics.Act("written ends", written[^CompressionErrorGoAway.Length..]);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, failure.ExitCode);
        Diagnostics.Assert("written ends", CompressionErrorGoAway, written[^CompressionErrorGoAway.Length..]);
        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
        Assert.EndsWith(CompressionErrorGoAway, written);
    }

    [TestMethod]
    public async Task ReadAsync_HeaderBlockDoesNotDecodeAndTheGoAwayCannotBeWritten_StillFailsWithExit16()
    {
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new byte[] { 0xFF, 0xFF, 0xFF }, isEndStream: true, isEndHeaders: true)),
        ];
        Diagnostics.Arrange("server", "SETTINGS, HEADERS 1 undecodable; every GOAWAY write is reset");
        Http2Session session = new(new WriteFailsAfterReadConnection(scripted));
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync("GET /x HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), CancellationToken.None);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(async () =>
        {
            byte[] buffer = new byte[256];
            while (await stream.ReadAsync(buffer, CancellationToken.None) > 0)
            {
            }
        });

        Diagnostics.Act("exit code", failure.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.Http2, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
    }

    /// <summary>A connection that replays <paramref name="response" /> and resets every GOAWAY write.</summary>
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
            buffer.Span is [_, _, _, 7, ..] ? throw new IOException("Connection reset.") : ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }}
