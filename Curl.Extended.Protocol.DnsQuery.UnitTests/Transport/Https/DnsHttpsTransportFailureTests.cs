using System.Net;
using System.Net.Sockets;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Https;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tls;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Https;

/// <summary>
/// Checks how a DNS over HTTPS query whose connection fails ends, over HTTP/1.1 and HTTP/2: exit 55 or 56 with curl's
/// words, never .NET's exception text.
/// </summary>
[TestClass]
public sealed class DnsHttpsTransportFailureTests
{
    private const string Http11 = "http/1.1";
    private const string Http2 = "h2";

    private static readonly byte[] Query = new DnsWire().Header(0, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(Http11)]
    [DataRow(Http2)]
    public async Task ExchangeAsync_ResetWhileReadingOnWindows_FailsWithExit56AndCurlsRecvFailure(string protocol)
    {
        DnsQueryException failure = await FailureOfAsync(protocol, new FailingConnection { ReadFailure = Reset() });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", failure.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(Http11)]
    [DataRow(Http2)]
    public async Task ExchangeAsync_ResetWhileReadingOffWindows_FailsWithExit56AndTheSocketErrorsWords(string protocol)
    {
        IOException reset = Reset();

        DnsQueryException failure = await FailureOfAsync(protocol, new FailingConnection { ReadFailure = reset });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual($"Recv failure: {reset.InnerException!.Message}", failure.Message);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: 64\r\n\r\nab")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\n\r\nab")]
    public async Task ExchangeAsync_ReadFailsPartWayThroughAnHttp11Response_FailsWithExit56(string received)
    {
        var connection = new FailingConnection { Reply = System.Text.Encoding.ASCII.GetBytes(received), ReadFailure = new IOException(".NET words") };

        DnsQueryException failure = await FailureOfAsync(Http11, connection);

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", failure.Message);
    }

    [TestMethod]
    [DataRow(Http11)]
    [DataRow(Http2)]
    public async Task ExchangeAsync_TlsEndsWithoutCloseNotify_FailsWithExit56AndTheTlsLayersWords(string protocol)
    {
        DnsQueryException failure = await FailureOfAsync(protocol, new FailingConnection { ReadFailure = new MissingCloseNotifyException("missing close_notify") });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("missing close_notify", failure.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(Http11)]
    [DataRow(Http2)]
    public async Task ExchangeAsync_ResetWhileSendingOnWindows_FailsWithExit55AndCurlsSendFailure(string protocol)
    {
        DnsQueryException failure = await FailureOfAsync(protocol, new FailingConnection { WriteFailure = Reset() });

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", failure.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(Http11)]
    [DataRow(Http2)]
    public async Task ExchangeAsync_ResetWhileSendingOffWindows_FailsWithExit55AndTheSocketErrorsWords(string protocol)
    {
        IOException reset = Reset();

        DnsQueryException failure = await FailureOfAsync(protocol, new FailingConnection { WriteFailure = reset });

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual($"Send failure: {reset.InnerException!.Message}", failure.Message);
    }

    [TestMethod]
    [DataRow(Http11)]
    [DataRow(Http2)]
    public async Task ExchangeAsync_FlushFailsWithoutASocketError_FailsWithExit55AndCurlsGeneralWords(string protocol)
    {
        DnsQueryException failure = await FailureOfAsync(protocol, new FailingConnection { FlushFailure = new IOException(".NET words") });

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_GoAwayCannotBeSentAfterAProtocolError_StillFailsWithExit8()
    {
        // DATA on stream 0 breaks RFC 9113 section 6.1; the connection is gone by the time the GOAWAY is written.
        byte[] frames =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(0, new byte[1], isEndStream: false)),
        ];
        var connection = new FailingConnection { Reply = frames, WriteFailure = Reset(), WritesFailOnceTheReplyIsRead = true };

        DnsQueryException failure = await FailureOfAsync(Http2, connection);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    public void DohFailureWordingConnection_IsSecureAndRemoteEndPoint_AreTheWrappedConnections()
    {
        var wrapped = new FailingConnection { IsSecure = true, RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 443) };

        IConnection worded = new DohFailureWordingConnection(wrapped);

        Assert.IsTrue(worded.IsSecure);
        Assert.AreSame(wrapped.RemoteEndPoint, worded.RemoteEndPoint);
    }

    [TestMethod]
    public async Task DohFailureWordingConnection_DisposeAsync_DisposesTheWrappedConnection()
    {
        var wrapped = new FailingConnection();

        await new DohFailureWordingConnection(wrapped).DisposeAsync();

        Assert.IsTrue(wrapped.IsDisposed);
    }

    private static IOException Reset() =>
        new("Unable to read data from the transport connection.", new SocketException((int)SocketError.ConnectionReset));

    private static async Task<DnsQueryException> FailureOfAsync(string protocol, FailingConnection connection)
    {
        var context = new TransferContext { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream() };
        var transport = new DnsHttpsTransport(connection, new HandshakingTlsProvider { ApplicationProtocol = protocol });

        return await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await transport.ExchangeAsync("h", 443, Query, context));
    }

    // A connector giving itself as the connection: it fails the write, flush or read it is told to (a read only once its
    // reply is read, and a write likewise when asked), and otherwise reads its reply and then the end of the stream.
    private sealed class FailingConnection : IConnector, IConnection
    {
        private int position;

        public IOException? WriteFailure { get; init; }

        public bool WritesFailOnceTheReplyIsRead { get; init; }

        public IOException? FlushFailure { get; init; }

        public IOException? ReadFailure { get; init; }

        public byte[] Reply { get; init; } = [];

        public bool IsSecure { get; init; }

        public EndPoint? RemoteEndPoint { get; init; }

        public bool IsDisposed { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(this));

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (position == Reply.Length && ReadFailure is not null)
            {
                return ValueTask.FromException<int>(ReadFailure);
            }

            int length = Math.Min(buffer.Length, Reply.Length - position);
            Reply.AsMemory(position, length).CopyTo(buffer);
            position += length;
            return ValueTask.FromResult(length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            WriteFailure is null || (WritesFailOnceTheReplyIsRead && position < Reply.Length)
                ? ValueTask.CompletedTask
                : ValueTask.FromException(WriteFailure);

        public ValueTask FlushAsync(CancellationToken cancellationToken) =>
            FlushFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(FlushFailure);

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
