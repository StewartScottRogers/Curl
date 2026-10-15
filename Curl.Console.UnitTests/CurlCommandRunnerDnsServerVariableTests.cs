using Curl.Engine;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>CURL_DNS_SERVER</c> on the dialing runner (ADR-0492, BL-2248): with the debug seam on,
/// names are asked of that server through the run's <see cref="IDatagramConnector" />, as curl's
/// debug build's <c>override-dns</c> asks; with it off, as in the shipped executable, the variable
/// changes nothing.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDnsServerVariableTests
{
    private readonly MemoryStream standardError = new();

    [TestMethod]
    public async Task RunAsync_WithTheSeamOn_AsksTheVariablesServerThroughTheDatagramConnectorAndDialsItsAnswer()
    {
        DnsStandIn dns = new(IPAddress.Parse("10.0.0.7"));
        RefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dns, dialer, new FixedDnsResolver(IPAddress.Parse("10.0.0.9")), honours: true, "-s", "http://dns.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEquivalent(new[] { "127.0.0.1:5353", "127.0.0.1:5353" }, dns.Opened);
        CollectionAssert.AreEquivalent(new[] { 1, 28 }, dns.QueryTypes);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("10.0.0.7"), 47114) }, dialer.Dialled);
    }

    [TestMethod]
    public async Task RunAsync_WithTheSeamOnUnderIPv4Only_AsksForTheARecordAlone()
    {
        DnsStandIn dns = new(IPAddress.Parse("10.0.0.7"));

        await RunAsync(dns, new RefusingTcpDialer(), new FixedDnsResolver(IPAddress.Parse("10.0.0.9")), honours: true, "-s", "-4", "http://dns.example:47114/");

        CollectionAssert.AreEqual(new[] { 1 }, dns.QueryTypes);
    }

    [TestMethod]
    public async Task RunAsync_WithTheSeamOnAndANameTheServerDoesNotKnow_FailsWithCouldNotResolveHost()
    {
        DnsStandIn dns = new(address: null);
        RefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dns, dialer, new FixedDnsResolver(IPAddress.Parse("10.0.0.9")), honours: true, "-sS", "http://unknown.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntResolveHost, exitCode);
        StringAssert.StartsWith(Encoding.ASCII.GetString(standardError.ToArray()), "curl: (6) Could not resolve host: unknown.example");
        Assert.IsEmpty(dialer.Dialled);
    }

    [TestMethod]
    public async Task RunAsync_WithTheSeamOff_IgnoresTheVariable()
    {
        DnsStandIn dns = new(IPAddress.Parse("10.0.0.7"));
        RefusingTcpDialer dialer = new();

        int exitCode = await RunAsync(dns, dialer, new FixedDnsResolver(IPAddress.Parse("10.0.0.9")), honours: false, "-s", "http://dns.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Assert.IsEmpty(dns.Opened);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("10.0.0.9"), 47114) }, dialer.Dialled);
    }

    [TestMethod]
    public void DnsServerVariableResolverOf_WithAnEmptyValue_KeepsTheGivenResolver()
    {
        FixedDnsResolver given = new(IPAddress.Loopback);

        IDnsResolver chosen = EngineComposition.DnsServerVariableResolverOf(EasyOptionsFromCommandLine.NetworkOf(Curl.Cli.CommandLineParser.Parse(["http://dns.example/"], _ => true).Options!), string.Empty, given, new DnsStandIn(null));

        Assert.AreSame(given, chosen);
    }

    private async Task<int> RunAsync(DnsStandIn dns, RefusingTcpDialer dialer, IDnsResolver resolver, bool honours, params string[] arguments)
    {
        Dictionary<string, string> environment = new() { [EngineComposition.DnsServerVariableName] = "127.0.0.1:5353" };
        return await CurlComposition.CreateRunner(
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                dialer,
                resolver,
                dns,
                readEnvironmentVariable: name => environment.GetValueOrDefault(name),
                honoursDnsServerVariable: honours)
            .RunAsync(arguments);
    }

    /// <summary>A resolver answering every name with one address.</summary>
    private sealed class FixedDnsResolver(IPAddress address) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([address]);
    }

    /// <summary>A dialer that records each address and refuses it.</summary>
    private sealed class RefusingTcpDialer : ITcpDialer
    {
        public List<IPEndPoint> Dialled { get; } = [];

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            Dialled.Add(endPoint);
            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }

    /// <summary>
    /// An in-memory DNS server behind a datagram connector: it answers an A query with
    /// <c>address</c>, an AAAA query with no data, and every query with NXDOMAIN when
    /// <c>address</c> is <see langword="null" />.
    /// </summary>
    private sealed class DnsStandIn(IPAddress? address) : IDatagramConnector
    {
        public List<string> Opened { get; } = [];

        public List<int> QueryTypes { get; } = [];

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            lock (Opened)
            {
                Opened.Add($"{host}:{port}");
            }

            return ValueTask.FromResult(DatagramOpenResult.Opened(new Channel(this, new IPEndPoint(IPAddress.Parse(host), port))));
        }

        private byte[] Answer(ReadOnlySpan<byte> query)
        {
            int questionEnd = 12;
            while (query[questionEnd] != 0)
            {
                questionEnd += 1 + query[questionEnd];
            }

            questionEnd += 5;
            int type = BinaryPrimitives.ReadUInt16BigEndian(query[(questionEnd - 4)..]);
            lock (QueryTypes)
            {
                QueryTypes.Add(type);
            }

            bool answers = address is not null && type == 1;
            byte[] answer = answers ? [0xC0, 12, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, .. address!.GetAddressBytes()] : [];
            byte[] reply = [.. query[..questionEnd], .. answer];
            reply[2] = 0x81;
            reply[3] = (byte)(address is null ? 0x83 : 0x80);
            BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(6), (ushort)(answers ? 1 : 0));
            BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(10), 0);
            return reply;
        }

        private sealed class Channel(DnsStandIn server, IPEndPoint endPoint) : IDatagramChannel
        {
            private byte[] _reply = [];

            public EndPoint ServerEndPoint => endPoint;

            public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
            {
                _reply = server.Answer(datagram.Span);
                return ValueTask.CompletedTask;
            }

            public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                _reply.CopyTo(buffer);
                return ValueTask.FromResult(new DatagramReceived(_reply.Length, endPoint));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
