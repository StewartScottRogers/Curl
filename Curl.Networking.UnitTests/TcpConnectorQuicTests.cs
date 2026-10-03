using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <see cref="TcpConnector.ConnectMultiplexedAsync" /> with a <see cref="QuicDialer" /> (BL-728):
/// resolves as a TCP connect does, then runs a real QUIC handshake against the in-memory
/// server through <see cref="QuicServerChannelOpener" />, and reports curl's <c>-v</c> lines,
/// timings and exit codes (ADR-0144, ADR-0180).
/// </summary>
[TestClass]
public sealed partial class TcpConnectorQuicTests
{
    private static readonly IPAddress SecondAddress = IPAddress.Parse("127.0.0.2");

    private readonly List<QuicTestServer> _servers = [];

    [TestCleanup]
    public void DisposeServers()
    {
        foreach (var server in _servers)
        {
            server.Dispose();
        }

        DeleteCertificateFiles();
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithoutAQuicDialer_FailsAsTheInterfaceDoes()
    {
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithANullTarget_ThrowsArgumentNullException()
    {
        var connector = Connector(new QuicServerChannelOpener(), new ManualTimeProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.ConnectMultiplexedAsync(null!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHandshakeCompletes_ReturnsTheConnectionWithCurlsLinesAndTimings()
    {
        // curl.se's ngtcp2 build, --http3 -v https://www.google.com/ (ADR-0144): the resolve
        // lines, Trying, the trust anchors, the TLS lines with no ALPN line, then Established.
        var clock = new SteppingTimeProvider(100);
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, clock);

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Assert.AreEqual("h3", connection.ApplicationProtocol);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), connection.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50123), connection.LocalEndPoint);
        CollectionAssert.AreEqual(
            new[] { "Host quic.test:443 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", "  Trying 127.0.0.1:443..." },
            events.Info);
        Assert.HasCount(2, events.TlsEvents);
        Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        var handshake = events.Handshakes.Single();
        Assert.AreEqual(SslProtocols.Tls13, handshake.ProtocolVersion);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_128_GCM_SHA256, handshake.CipherSuite);
        Assert.AreEqual("h3", handshake.NegotiatedApplicationProtocol);
        Assert.IsEmpty(handshake.OfferedApplicationProtocols);
        Assert.AreEqual("CN=localhost", handshake.ServerCertificate!.Subject);
        Assert.IsNull(handshake.VerifiedHostName);
        var opened = events.Opened.Single();
        Assert.AreEqual("quic.test", opened.HostName);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), opened.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50123), opened.LocalEndPoint);
        Assert.AreEqual(0, opened.ConnectionNumber);
        var timings = result.Timings!;
        Assert.AreEqual(100, timings.Started);
        Assert.IsTrue(timings.NameResolved > timings.Started);
        Assert.IsTrue(timings.Connected > timings.NameResolved);
        Assert.AreEqual(timings.Connected, timings.TlsHandshakeCompleted);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WritingHttp3ConnectionLines_WritesCurlsLinesBetweenTheTlsLinesAndEstablished()
    {
        // curl 8.18.0's ngtcp2 build, -v --trace-config http/3 against cloudflare-quic.com (BL-1208 Notes).
        var clock = new SteppingTimeProvider(100);
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(initialMaxStreamsBidi: 100) };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, clock, writesHttp3ConnectionLines: true);

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        string[] lines = [.. events.Info.Skip(4)];
        Assert.HasCount(4, lines, string.Join('\n', events.Info));
        Assert.StartsWith("[HTTP/3] handshake complete after ", lines[0]);
        Assert.EndsWith("ms, remote transport[max_udp_payload=65527, initial_max_data=1048576]", lines[0]);
        CollectionAssert.AreEqual(new[] { "[HTTP/3] max bidi streams now 100, used 0", "[HTTP/3] peer verified", "[HTTP/3] connect -> 0, done=1" }, lines[1..]);
        Assert.HasCount(1, events.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ToAHostName_SendsItAsTheServerName()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        var serverName = opener.Opened.Single().Server!.Tls!.ClientHello!.Extensions.Single(extension => extension.Type == TlsExtensionType.ServerName);
        Assert.EndsWith("quic.test", System.Text.Encoding.ASCII.GetString(serverName.Data));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheChannelHasNoLocalEndPoint_ReportsTheUnspecifiedOne()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), LocalEndPoint = null };
        var events = new RecordingTransferEvents();

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(new IPEndPoint(IPAddress.Any, 0), events.Opened.Single().LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_OverARealUdpSocketBoundToAnyAddress_ReportsTheAddressItSendsFrom()
    {
        // curl.se's ngtcp2 build writes "from 192.168.1.174 port 51486", not 0.0.0.0 (BL-734,
        // BL-1051): a loopback peer is reached from 127.0.0.1.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), ReportsARealUdpSocketsLocalEndPoint = true };
        var events = new RecordingTransferEvents();

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        var local = events.Opened.Single().LocalEndPoint;
        Assert.AreEqual(IPAddress.Loopback, local.Address);
        Assert.AreNotEqual(0, local.Port);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_NumbersTheConnectionAfterTheTcpConnectionsBeforeIt()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider());
        _ = await connector.ConnectAsync(new ConnectTarget("quic.test", 443, UseTls: false), CancellationToken.None);

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(1, events.Opened.Single().ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAConnectToMapping_DialsTheMappedHostAndPort()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var resolver = new FakeDnsResolver(IPAddress.Loopback);
        var connector = Connector(opener, new ManualTimeProvider(), resolver: resolver, connectToMappings: new ConnectToMappings(["quic.test:443:mapped.test:8443"]));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(new[] { "mapped.test" }, resolver.ResolvedHosts);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 8443), opener.Opened.Single().ServerEndPoint);
        Assert.AreEqual("mapped.test", events.Opened.Single().HostName);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithAResolveEntryThatDoesNotParse_FailsWithExit49()
    {
        var opener = new QuicServerChannelOpener();
        var connector = Connector(opener, new ManualTimeProvider(), resolveOverrides: ResolveOverrides.Parse(["bad"]));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'bad'", result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHostDoesNotResolve_FailsWithExit6()
    {
        var opener = new QuicServerChannelOpener();
        var connector = Connector(opener, new ManualTimeProvider(), resolver: new FakeDnsResolver());

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: quic.test", result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheConnectTimeoutPasses_FailsWithExit28()
    {
        // curl.se's build, --http3-only --connect-timeout 1 against a silent UDP peer:
        // curl: (28) Connection timed out after 1008 milliseconds (ADR-0144).
        var clock = new ManualTimeProvider();
        var opener = new QuicServerChannelOpener();
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, clock, connectTimeout: TimeSpan.FromSeconds(1));

        var connecting = connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        clock.Advance(1000);
        var result = await connecting;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", result.ErrorMessage);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", events.Info[^1]);
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithoutAConnectTimeout_FailsWithExit55AfterTenSeconds()
    {
        // curl.se's build, --http3-only against a silent UDP peer (ADR-0144): the expiry line,
        // no "QUIC connect to" line, then "Failed to connect to ... port".
        var clock = new ManualTimeProvider();
        var opener = new QuicServerChannelOpener();
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, clock, connectTimeout: TimeSpan.Zero);

        var connecting = connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        clock.Advance(10000);
        var result = await connecting;

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT",
                "Failed to connect to quic.test port 443 after 10000 ms: Failed sending data to the peer",
            },
            events.Info.Skip(4).ToArray());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheServerRefusesTheConnection_FailsWithExit8()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(closeAfterClientHello: 0x2) };
        var events = new RecordingTransferEvents();

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                result.ErrorMessage,
                "QUIC connect to 127.0.0.1 port 443 failed: Weird server reply",
                "Failed to connect to quic.test port 443 after 0 ms: Weird server reply",
            },
            events.Info.Skip(4).ToArray());
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenEveryAddressFails_TriesEachAndReturnsTheLastFailure()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(closeAfterClientHello: 0x1) };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider(), resolver: new FakeDnsResolver(IPAddress.Loopback, SecondAddress));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { new IPEndPoint(IPAddress.Loopback, 443), new IPEndPoint(SecondAddress, 443) },
            opener.Opened.Select(channel => channel.ServerEndPoint).ToArray());
        Assert.AreEqual("  Trying 127.0.0.2:443...", events.Info[7]);
        Assert.AreEqual("QUIC connect to 127.0.0.2 port 443 failed: Could not connect to server", events.Info[^2]);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheCertificateIsRejected_FailsAsTheTcpPathDoes()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider(), new TlsClientOptions());

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));
        CollectionAssert.AreEqual(
            new[]
            {
                result.ErrorMessage,
                "QUIC connect to 127.0.0.1 port 443 failed: SSL peer certificate or SSH remote key was not OK",
                "Failed to connect to quic.test port 443 after 0 ms: SSL peer certificate or SSH remote key was not OK",
            },
            events.Info.Skip(4).ToArray());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheCaCertificateFileCannotBeRead_FailsWithExit77()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var connector = Connector(opener, new ManualTimeProvider(), new TlsClientOptions(CaCertificateFile: Path.Combine(Path.GetTempPath(), "no-such-dir-bl728", "ca.pem")));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.ExitCode);
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenAReceiveFailsInTheSchannelBuild_FailsWithExit56AndCurlsWinsockWords()
    {
        // curl.se's Windows build, --http3-only with nothing on UDP (ADR-0144): errno=10054 there;
        // the code is the platform's own, so it is read from the exception.
        var failure = new SocketException((int)SocketError.ConnectionReset);
        var opener = new QuicServerChannelOpener { ReceiveFailure = failure };
        var events = new RecordingTransferEvents();

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual($"QUIC: recvfrom() unexpectedly returned -1 (errno={failure.ErrorCode}; Connection was reset)", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                result.ErrorMessage,
                "QUIC connect to 127.0.0.1 port 443 failed: Failure when receiving data from the peer",
                "Failed to connect to quic.test port 443 after 0 ms: Failure when receiving data from the peer",
            },
            events.Info.Skip(4).ToArray());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenAReceiveFailsWithAnotherErrorInTheSchannelBuild_UsesCurlsWordsForIt()
    {
        var failure = new SocketException((int)SocketError.ConnectionRefused);
        var opener = new QuicServerChannelOpener { ReceiveFailure = failure };

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual($"QUIC: recvfrom() unexpectedly returned -1 (errno={failure.ErrorCode}; Connection refused)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenAReceiveFailsInTheOpenSslBuild_UsesTheSystemsMessage()
    {
        var failure = new SocketException((int)SocketError.ConnectionReset);
        var opener = new QuicServerChannelOpener { ReceiveFailure = failure };

        var result = await Connector(opener, new ManualTimeProvider(), matchesSchannelBuild: false).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual($"QUIC: recvfrom() unexpectedly returned -1 (errno={failure.ErrorCode}; {failure.Message})", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenASocketCannotBeOpened_MovesOnToTheNextAddress()
    {
        var opener = new QuicServerChannelOpener
        {
            ServerFor = _ => Server(),
            OpenOutcome = endPoint =>
            {
                if (endPoint.Address.Equals(IPAddress.Loopback))
                {
                    throw new SocketException((int)SocketError.AddressFamilyNotSupported);
                }
            },
        };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider(), resolver: new FakeDnsResolver(IPAddress.Loopback, SecondAddress));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(new IPEndPoint(SecondAddress, 443), connection.RemoteEndPoint);
        Assert.AreEqual("QUIC connect to 127.0.0.1 port 443 failed: Could not connect to server", events.Info[5]);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheConnectTimeoutHasPassedBeforeTheNextAddress_FailsWithExit28()
    {
        var clock = new ManualTimeProvider();
        var opener = new QuicServerChannelOpener
        {
            OpenOutcome = _ =>
            {
                clock.Advance(2000);
                throw new SocketException((int)SocketError.AddressFamilyNotSupported);
            },
        };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, clock, resolver: new FakeDnsResolver(IPAddress.Loopback, SecondAddress), connectTimeout: TimeSpan.FromSeconds(1));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 2000 milliseconds", result.ErrorMessage);
        Assert.DoesNotContain("  Trying 127.0.0.2:443...", events.Info);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenCancelled_DisposesTheChannelAndThrows()
    {
        var opener = new QuicServerChannelOpener();
        using var cancellation = new CancellationTokenSource();
        var connecting = Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(), cancellation.Token).AsTask();
        await opener.WaitingToReceive.WaitAsync();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => connecting);
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task PoolingConnector_ConnectMultiplexedAsync_AsksTheInnerConnector()
    {
        await using var pool = new PoolingConnector(
            new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()),
            new ManualTimeProvider());

        var result = await pool.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public void QuicDialer_Constructor_WithANullArgument_ThrowsArgumentNullException()
    {
        var opener = new QuicServerChannelOpener();
        var options = new TlsClientOptions();
        var clock = new ManualTimeProvider();
        var random = SystemTlsRandomSource.Instance;

        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(null!, options, true, clock, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(opener, null!, true, clock, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(opener, options, true, null!, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(opener, options, true, clock, null!));
    }

    [TestMethod]
    public void QuicDialer_PublicConstructor_CreatesADialer()
    {
        Assert.IsNotNull(new QuicDialer(new TlsClientOptions(), TimeProvider.System, IPAddress.Loopback, 0));
    }

    [TestMethod]
    public async Task UdpChannelOpener_Open_BindsTheLocalAddressGiven()
    {
        await using var channel = new UdpChannelOpener(IPAddress.Loopback).Open(new IPEndPoint(IPAddress.Loopback, 9));

        var local = (IPEndPoint)channel.LocalEndPoint!;
        Assert.AreEqual(IPAddress.Loopback, local.Address);
        Assert.AreNotEqual(0, local.Port);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 9), channel.ServerEndPoint);
    }

    private static ConnectTarget Target(ITransferEvents? events = null) =>
        new("quic.test", 443, UseTls: true) { Events = events ?? NoTransferEvents.Instance, PoolScheme = "https" };

    private static TcpConnector Connector(
        QuicServerChannelOpener opener,
        TimeProvider clock,
        TlsClientOptions? options = null,
        bool matchesSchannelBuild = true,
        FakeDnsResolver? resolver = null,
        ResolveOverrides? resolveOverrides = null,
        ConnectToMappings? connectToMappings = null,
        TimeSpan? connectTimeout = null,
        bool writesHttp3ConnectionLines = false) =>
        new(
            resolver ?? new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            clock,
            resolveOverrides: resolveOverrides,
            connectToMappings: connectToMappings,
            connectTimeout: connectTimeout,
            quicDialer: new QuicDialer(opener, options ?? new TlsClientOptions(Insecure: true), matchesSchannelBuild, clock, SystemTlsRandomSource.Instance) { WritesHttp3ConnectionLines = writesHttp3ConnectionLines });

    private QuicTestServer Server(ulong? closeAfterClientHello = null, bool requestClientCertificate = false, ushort cipherSuite = 0x1301, ulong initialMaxStreamsBidi = 0)
    {
        var server = new QuicTestServer
        {
            ConfigureTransportParameters = parameters => parameters with { InitialMaxStreamsBidi = initialMaxStreamsBidi },
            CloseAfterClientHello = closeAfterClientHello,
            RequestClientCertificate = requestClientCertificate,
            CipherSuite = cipherSuite,
        };
        lock (_servers)
        {
            _servers.Add(server);
        }

        return server;
    }
}
