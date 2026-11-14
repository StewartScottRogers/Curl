using System.Net;
using Curl.Extended.Protocol.WebTransport.Session;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport;

/// <summary>Checks <c>-v</c>'s connection and TLS events for a WebTransport dial (CurlSurface:verbose-webtransport-1).</summary>
[TestClass]
public sealed class WebTransportVerboseTests
{
    private const int HangTimeoutMilliseconds = 120_000;

    private static readonly WebTransportRequest Request = new("wt.example", 4433, "wt.example:4433", "/", WebTransportMode.Bidirectional, ReadOnlyMemory<byte>.Empty);

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task DialAsync_VerboseEvents_RaisesTheTlsHandshakeThenTheOpenedConnection()
    {
        using QuicTestServer server = new();
        RecordingEvents events = new();

        await using IMultiplexedConnection connection = await Dialer(new FakeDatagramConnector(new QuicTestChannel(server))).DialAsync(Request, events, CancellationToken.None);

        Assert.AreEqual("h3", events.Handshakes.Single().NegotiatedApplicationProtocol);
        Assert.IsTrue(events.Handshakes.Single().IsQuic);
        ConnectionOpenedEvent opened = events.Opened.Single();
        Assert.AreEqual("wt.example", opened.HostName);
        Assert.AreEqual(IPAddress.Loopback, opened.RemoteEndPoint.Address);
        CollectionAssert.AreEqual(new[] { "handshake", "opened" }, events.Order);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task DialAsync_ChannelKnowsItsLocalAddress_ReportsItAndFallsBackOnAnUnresolvedServer()
    {
        using QuicTestServer server = new();
        RecordingEvents events = new();
        IPEndPoint local = new(IPAddress.Loopback, 5555);

        await using IMultiplexedConnection connection = await Dialer(new FakeDatagramConnector(new AddressedChannel(new QuicTestChannel(server), new DnsEndPoint("wt.example", 4433), local))).DialAsync(Request, events, CancellationToken.None);

        Assert.AreEqual(local, events.Opened.Single().LocalEndPoint);
        Assert.AreEqual(4433, events.Opened.Single().RemoteEndPoint.Port);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task DialAsync_VerboseOff_RaisesNothing()
    {
        using QuicTestServer server = new();
        RecordingEvents unattached = new();

        await using IMultiplexedConnection connection = await Dialer(new FakeDatagramConnector(new QuicTestChannel(server))).DialAsync(Request, CancellationToken.None);

        Assert.IsEmpty(unattached.Order);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_PassesTheTransfersEventsToTheDial()
    {
        RecordingEvents events = new();
        ITransferEvents? received = null;
        WebTransportProtocolHandler handler = new((_, passed, _) =>
        {
            received = passed;
            return ValueTask.FromException<IMultiplexedConnection>(new WebTransportException(CurlExitCode.CouldntConnect, "stop"));
        });

        await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("webtransport://wt.example/"), Output = new MemoryStream(), Events = events });

        Assert.AreSame(events, received);
    }

    private static WebTransportQuicDialer Dialer(FakeDatagramConnector connector) =>
        new(connector, _ => new QuicTestVerifier(), TimeProvider.System);

    private sealed class AddressedChannel(IDatagramChannel inner, EndPoint server, EndPoint local) : IDatagramChannel
    {
        public EndPoint ServerEndPoint => server;

        public EndPoint? LocalEndPoint => local;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) => inner.SendAsync(datagram, inner.ServerEndPoint, cancellationToken);

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            new((await inner.ReceiveAsync(buffer, cancellationToken)).Length, server);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class RecordingEvents : ITransferEvents
    {
        public List<string> Order { get; } = [];

        public List<TlsHandshakeEvent> Handshakes { get; } = [];

        public List<ConnectionOpenedEvent> Opened { get; } = [];

        public void ReportInfo(string text) => Order.Add("info");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
            Order.Add("opened");
            Opened.Add(opened);
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
            Order.Add("handshake");
            Handshakes.Add(handshake);
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
