using System.Net;
using System.Net.Sockets;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the dialing runner sends its <c>--http3</c> and <c>--http3-only</c> QUIC datagrams through
/// the run's injected <see cref="IDatagramConnector" />, not a real UDP socket, so an in-process HTTP/3
/// server answers them (BL-2260).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHttp3DatagramConnectorTests
{
    [TestMethod]
    public async Task RunAsync_WithHttp3Only_OpensTheQuicChannelThroughTheDatagramConnectorAndSendsTheInitialThroughIt()
    {
        RecordingDatagramConnector datagrams = new();

        int exitCode = await CurlComposition.CreateRunner(
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                new RefusingTcpDialer(),
                new LoopbackDnsResolver(),
                datagrams,
                parsesAsWindowsBuild: false)
            .RunAsync(["-s", "--http3-only", "https://127.0.0.1:47260/"]);

        Assert.AreEqual((int)CurlExitCode.RecvError, exitCode);
        CollectionAssert.AreEqual(new[] { "127.0.0.1:47260" }, datagrams.Opened);
        Assert.IsNotEmpty(datagrams.Sent);
        Assert.IsGreaterThanOrEqualTo(1200, datagrams.Sent[0].Length);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 47260), datagrams.SentTo[0]);
    }

    [TestMethod]
    public void CreateDatagramConnectorQuicDialer_UnderTraceConfigHttp3_WritesTheHttp3ConnectionLines()
    {
        Curl.Cli.CommandLineOptions options = Curl.Cli.CommandLineParser.Parse(["--trace-config", "http/3", "https://h3.example/"], _ => true).Options!;

        QuicDialer dialer = CurlComposition.CreateDatagramConnectorQuicDialer(options, new TlsClientOptions(), new RecordingDatagramConnector());

        Assert.IsTrue(dialer.WritesHttp3ConnectionLines);
    }

    /// <summary>A dialer that refuses every address, so no TCP connection is made.</summary>
    private sealed class RefusingTcpDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }

    /// <summary>
    /// A datagram connector that records each open and every datagram sent, and whose channel's
    /// receive fails as a reset socket does.
    /// </summary>
    private sealed class RecordingDatagramConnector : IDatagramConnector
    {
        public List<string> Opened { get; } = [];

        public List<byte[]> Sent { get; } = [];

        public List<EndPoint> SentTo { get; } = [];

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            lock (Opened)
            {
                Opened.Add($"{host}:{port}");
            }

            return ValueTask.FromResult(DatagramOpenResult.Opened(new Channel(this, new IPEndPoint(IPAddress.Parse(host), port))));
        }

        private sealed class Channel(RecordingDatagramConnector connector, IPEndPoint endPoint) : IDatagramChannel
        {
            public EndPoint ServerEndPoint => endPoint;

            public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
            {
                lock (connector.Sent)
                {
                    connector.Sent.Add(datagram.ToArray());
                    connector.SentTo.Add(destination);
                }

                return ValueTask.CompletedTask;
            }

            public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
                ValueTask.FromException<DatagramReceived>(new SocketException((int)SocketError.ConnectionReset));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
