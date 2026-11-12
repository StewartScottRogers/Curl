using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>Checks that <c>-T</c> on <c>nats://</c> publishes the uploaded file as <c>-d</c> publishes its data (BL-4951).</summary>
[TestClass]
public sealed class NatsUploadTests
{
    private const string Info = "INFO {\"server_id\":\"x\",\"max_payload\":64}\r\n";

    private const string Connect = "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":false,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1}\r\n";

    [TestMethod]
    public async Task ExecuteAsync_AnUpload_PublishesTheFilesBytesAndNeverSubscribes()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "PONG\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, new TransferContext { Url = CurlUrl.Parse("nats://example.com/foo"), Output = output, Upload = new MemoryStream("filebody"u8.ToArray()) });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
        Assert.AreEqual(Connect + "PUB foo 8\r\nfilebody\r\nPING\r\n", connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataAndAnUpload_PublishesTheData()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "PONG\r\n");

        TransferResult result = await ExecuteAsync(connection, new TransferContext { Url = CurlUrl.Parse("nats://example.com/foo"), Output = new MemoryStream(), PostData = Encoding.UTF8.GetBytes("data"), Upload = new MemoryStream("filebody"u8.ToArray()) });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Connect + "PUB foo 4\r\ndata\r\nPING\r\n", connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnUploadThatFailsToRead_ReportsReadErrorNotConnectionReset()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "PONG\r\n");

        TransferResult result = await ExecuteAsync(connection, new TransferContext { Url = CurlUrl.Parse("nats://example.com/foo"), Output = new MemoryStream(), Upload = new FailingStream() });

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.Contains("disk gone", result.ErrorMessage ?? string.Empty);
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("disk gone");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("disk gone");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedConnection connection, TransferContext context) =>
        new NatsProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), () => "_INBOX.t").ExecuteAsync(context);
}
