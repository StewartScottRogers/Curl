using System.Net;
using System.Net.Sockets;
using Curl.Cli;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--doh-url</c> on the dialing runner (BL-2928): the DoH POSTs dial through the runner's
/// <see cref="ITcpDialer" />, the DoH server's name resolving through the runner's resolver, so the
/// conformance harness's in-memory servers answer them; without it the runner's resolver answers every name.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDialingDohTests
{
    [TestMethod]
    public async Task RunAsync_WithADohUrl_DialsTheDohServerThroughTheGivenDialerAndResolver()
    {
        RecordingRefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dialer, "-s", "--doh-url", "http://doh.example:47115/dns-query", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntResolveHost, exitCode);
        Assert.IsNotEmpty(dialer.Dialled);
        Assert.IsTrue(dialer.Dialled.TrueForAll(endPoint => endPoint.Equals(new IPEndPoint(IPAddress.Parse("10.0.0.9"), 47115))));
    }

    [TestMethod]
    public async Task RunAsync_WithoutADohUrl_DialsTheAddressTheGivenResolverAnswers()
    {
        RecordingRefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dialer, "-s", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("10.0.0.9"), 47114) }, dialer.Dialled);
    }

    [TestMethod]
    public async Task RunAsync_WithADohUrl_ClosesTheKeptDohConnectionsWhenTheGroupEnds()
    {
        AnsweringTcpDialer dialer = new();

        int exitCode = await RunAsync(dialer, "-s", "--doh-url", "http://doh.example:47115/dns-query", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        List<AnsweringConnection> dohConnections = [.. dialer.Dialled.Where(connection => connection.Port == 47115)];
        Assert.HasCount(2, dohConnections);
        Assert.IsTrue(dohConnections.TrueForAll(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task CreateTransferDispatch_OverTransports_ClosesTheirKeptDohConnections()
    {
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Options()), EasyOptionsFromCommandLine.NetworkOf(Options()), EasyOptionsFromCommandLine.TlsOf(Options()), EasyOptionsFromCommandLine.HttpOf(Options()), EasyOptionsFromCommandLine.ProxyOf(Options()), EasyOptionsFromCommandLine.AuthenticationOf(Options()));

        await using TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        Assert.AreSame(transports, dispatch.KeptDohConnections);
    }

    private static CommandLineOptions Options() =>
        CommandLineParser.Parse(["--doh-url", "https://doh.example/dns-query", "http://site.example/"], _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess).Options!;

    // Answers the DoH server's port with one A answer (example.test, 127.0.0.1) and every other port with an empty 200.
    private sealed class AnsweringTcpDialer : ITcpDialer
    {
        private static readonly byte[] DohReply =
        [
            .. "HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: 46\r\n\r\n"u8,
            0x00, 0x00, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x07, .. "example"u8, 0x04, .. "test"u8, 0x00, 0x00, 0x01, 0x00, 0x01,
            0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01,
        ];

        public List<AnsweringConnection> Dialled { get; } = [];

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            AnsweringConnection connection = new(endPoint.Port, endPoint.Port == 47115 ? DohReply : "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray());
            lock (Dialled)
            {
                Dialled.Add(connection);
            }

            return ValueTask.FromResult(new DialedTcpConnection(connection, new IPEndPoint(IPAddress.Loopback, 50000)));
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }

    private sealed class AnsweringConnection(int port, byte[] reply) : IConnection
    {
        private ReadOnlyMemory<byte> unread = reply;

        public int Port => port;

        public bool IsDisposed { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(unread.Length, buffer.Length);
            unread[..count].CopyTo(buffer);
            unread = unread[count..];
            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<int> RunAsync(ITcpDialer dialer, params string[] arguments) =>
        await CurlComposition.CreateRunner(
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                dialer,
                new FixedDnsResolver(IPAddress.Parse("10.0.0.9")),
                new NoDatagramConnector())
            .RunAsync(arguments);

    /// <summary>A resolver answering every name with one address.</summary>
    private sealed class FixedDnsResolver(IPAddress address) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([address]);
    }

    /// <summary>A datagram connector no test reaches.</summary>
    private sealed class NoDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No datagram channel is opened.");
    }

    /// <summary>A dialer that records each address and refuses it.</summary>
    private sealed class RecordingRefusingTcpDialer : ITcpDialer
    {
        public List<IPEndPoint> Dialled { get; } = [];

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            lock (Dialled)
            {
                Dialled.Add(endPoint);
            }

            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }
}
