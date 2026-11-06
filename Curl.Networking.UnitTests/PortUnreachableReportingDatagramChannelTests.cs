using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class PortUnreachableReportingDatagramChannelTests
{
    private static readonly IPEndPoint Server = new(IPAddress.Loopback, 1);

    // curl_easy_strerror's words for CurlExitCode.CouldntConnect (exit 7). This layer only carries the text;
    // the engine maps the exception to CurlExitCode.CouldntConnect, which EndPointReportingProtocolHandlerTests pins.
    private const string CouldntConnectWords = "Could not connect to server";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionRefused)]
    public async Task ReceiveAsync_PortClosed_ThrowsCouldntConnectTextWithTheElapsedTime(SocketError socketError)
    {
        Diagnostics.Arrange("socket error on receive", socketError);
        var timeProvider = new ManualTimeProvider();
        var inner = new FailingChannel(new SocketException((int)socketError));
        var connector = new UdpDatagramConnector(new FakeDnsResolver(IPAddress.Loopback), timeProvider, _ => inner);
        var opened = await connector.OpenAsync("localhost", 1, CancellationToken.None);
        await using var channel = opened.Channel!;
        timeProvider.Advance(5);

        var refused = await Assert.ThrowsExactlyAsync<DatagramPortUnreachableException>(
            async () => await channel.ReceiveAsync(new byte[16], CancellationToken.None));
        Diagnostics.Act("message", refused.Message);

        Diagnostics.Assert("message", "Failed to connect to localhost:1 after 5 ms: Could not connect to server", refused.Message);
        Diagnostics.Assert("socket error kept", socketError, refused.SocketErrorCode);
        Assert.AreEqual("Failed to connect to localhost:1 after 5 ms: Could not connect to server", refused.Message);
        Assert.EndsWith(CouldntConnectWords, refused.Message);
        Assert.AreEqual(socketError, refused.SocketErrorCode);
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionRefused)]
    public async Task SendAsync_PortClosed_ThrowsCouldntConnectText(SocketError socketError)
    {
        Diagnostics.Arrange("socket error on send", socketError);
        var timeProvider = new ManualTimeProvider();
        var inner = new FailingChannel(new SocketException((int)socketError));
        var connector = new UdpDatagramConnector(new FakeDnsResolver(IPAddress.Loopback), timeProvider, _ => inner);
        var opened = await connector.OpenAsync("localhost", 1, CancellationToken.None);
        await using var channel = opened.Channel!;
        timeProvider.Advance(7);

        var refused = await Assert.ThrowsExactlyAsync<DatagramPortUnreachableException>(
            async () => await channel.SendAsync(new byte[1], Server, CancellationToken.None));
        Diagnostics.Act("message", refused.Message);

        Diagnostics.Assert("message", "Failed to connect to localhost:1 after 7 ms: Could not connect to server", refused.Message);
        Diagnostics.Assert("socket error kept", socketError, refused.SocketErrorCode);
        Assert.AreEqual("Failed to connect to localhost:1 after 7 ms: Could not connect to server", refused.Message);
        Assert.EndsWith(CouldntConnectWords, refused.Message);
        Assert.AreEqual(socketError, refused.SocketErrorCode);
    }

    [TestMethod]
    public async Task ReceiveAndSend_OtherSocketError_PassesThrough()
    {
        Diagnostics.Arrange("socket error", SocketError.MessageSize);
        var failure = new SocketException((int)SocketError.MessageSize);
        await using var channel = new PortUnreachableReportingDatagramChannel(new FailingChannel(failure), () => "unused");

        var received = await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.ReceiveAsync(new byte[1], CancellationToken.None));
        var sent = await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.SendAsync(new byte[1], Server, CancellationToken.None));
        Diagnostics.Act("same exception", ReferenceEquals(failure, received) && ReferenceEquals(failure, sent));

        Diagnostics.Assert("same exception", true, ReferenceEquals(failure, received) && ReferenceEquals(failure, sent));
        Assert.AreSame(failure, received);
        Assert.AreSame(failure, sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_InnerAlreadyReportsTheRefusal_PassesItThrough()
    {
        var failure = new DatagramPortUnreachableException(SocketError.ConnectionReset, "inner text");
        Diagnostics.Arrange("inner failure", failure.Message);
        await using var channel = new PortUnreachableReportingDatagramChannel(new FailingChannel(failure), () => "outer text");

        var refused = await Assert.ThrowsExactlyAsync<DatagramPortUnreachableException>(
            async () => await channel.ReceiveAsync(new byte[1], CancellationToken.None));
        Diagnostics.Act("message", refused.Message);

        Diagnostics.Assert("message", "inner text", refused.Message);
        Assert.AreSame(failure, refused);
    }

    [TestMethod]
    public async Task Members_InnerAnswers_ForwardsEveryCall()
    {
        var inner = new AnsweringChannel();
        Diagnostics.Arrange("inner server", Server);
        var channel = new PortUnreachableReportingDatagramChannel(inner, () => "unused");

        await channel.SendAsync(new byte[] { 1, 2 }, Server, CancellationToken.None);
        var received = await channel.ReceiveAsync(new byte[16], CancellationToken.None);
        await channel.DisposeAsync();
        Diagnostics.Act("server end point", channel.ServerEndPoint);

        Diagnostics.Assert("server end point", Server, channel.ServerEndPoint);
        Assert.AreEqual(Server, channel.ServerEndPoint);
        Assert.AreEqual(AnsweringChannel.Local, channel.LocalEndPoint);
        Assert.AreEqual(2, received.Length);
        Assert.IsTrue(inner.Sent);
        Assert.IsTrue(inner.Disposed);
    }

    private sealed class AnsweringChannel : IDatagramChannel
    {
        public static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

        public bool Sent { get; private set; }

        public bool Disposed { get; private set; }

        public EndPoint ServerEndPoint => Server;

        public EndPoint? LocalEndPoint => Local;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            Sent = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new DatagramReceived(2, Server));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AnswerThenFailChannel(Exception failure) : IDatagramChannel
    {
        private bool _answered;

        public EndPoint ServerEndPoint => Server;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw failure;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (_answered)
            {
                throw failure;
            }

            _answered = true;
            return new DatagramReceived(2, Server);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingChannel(Exception failure) : IDatagramChannel
    {
        public EndPoint ServerEndPoint => Server;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            ValueTask.FromException(failure);

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.FromException<DatagramReceived>(failure);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
