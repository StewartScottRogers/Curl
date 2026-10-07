using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Testing;
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

    /// <summary>Gets or sets the test's context, which carries its diagnostics (BL-1457).</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("QUIC dialer", "none");

        var result = await ConnectMultiplexedAsync(connector, Target());

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithANullTarget_ThrowsArgumentNullException()
    {
        var connector = Connector(new QuicServerChannelOpener(), new ManualTimeProvider());
        Diagnostics.Arrange("target", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.ConnectMultiplexedAsync(null!, CancellationToken.None).AsTask());

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
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
        Diagnostics.Arrange("clock", "stepping from 100 ms");

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        ActEvents(events);
        Diagnostics.Act("timings", result.Timings);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Diagnostics.Assert("application protocol", "h3", connection.ApplicationProtocol);
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
        Diagnostics.Arrange("writes HTTP/3 connection lines", true);
        Diagnostics.Arrange("server's initial_max_streams_bidi", 100);

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        string[] lines = [.. events.Info.Skip(4)];
        Diagnostics.Assert("HTTP/3 line count", 4, lines.Length);
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

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target());

        await using var connection = result.Connection!;
        var serverName = opener.Opened.Single().Server!.Tls!.ClientHello!.Extensions.Single(extension => extension.Type == TlsExtensionType.ServerName);
        Diagnostics.Bytes("server_name extension", serverName.Data);
        Diagnostics.Assert("server name ends with", "quic.test", System.Text.Encoding.ASCII.GetString(serverName.Data));
        Assert.EndsWith("quic.test", System.Text.Encoding.ASCII.GetString(serverName.Data));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheChannelHasNoLocalEndPoint_ReportsTheUnspecifiedOne()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), LocalEndPoint = null };
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("channel's local end point", "null");

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target(events));

        await using var connection = result.Connection!;
        Diagnostics.Assert("opened local end point", new IPEndPoint(IPAddress.Any, 0), events.Opened.Single().LocalEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Any, 0), events.Opened.Single().LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_OverARealUdpSocketBoundToAnyAddress_ReportsTheAddressItSendsFrom()
    {
        // curl.se's ngtcp2 build writes "from 192.168.1.174 port 51486", not 0.0.0.0 (BL-734,
        // BL-1051): a loopback peer is reached from 127.0.0.1.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), ReportsARealUdpSocketsLocalEndPoint = true };
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("reports a real UDP socket's local end point", true);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target(events));

        await using var connection = result.Connection!;
        var local = events.Opened.Single().LocalEndPoint;
        Diagnostics.Assert("local address", IPAddress.Loopback, local.Address);
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
        Diagnostics.Arrange("TCP connections before", 1);

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        await using var connection = result.Connection!;
        Diagnostics.Assert("connection number", 1, events.Opened.Single().ConnectionNumber);
        Assert.AreEqual(1, events.Opened.Single().ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAConnectToMapping_DialsTheMappedHostAndPort()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var resolver = new FakeDnsResolver(IPAddress.Loopback);
        var connector = Connector(opener, new ManualTimeProvider(), resolver: resolver, connectToMappings: new ConnectToMappings(["quic.test:443:mapped.test:8443"]));
        Diagnostics.Arrange("connect-to", "quic.test:443:mapped.test:8443");

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        await using var connection = result.Connection!;
        Diagnostics.Act("resolved hosts", string.Join(", ", resolver.ResolvedHosts));
        Diagnostics.Assert("dialled end point", new IPEndPoint(IPAddress.Loopback, 8443), opener.Opened.Single().ServerEndPoint);
        CollectionAssert.AreEqual(new[] { "mapped.test" }, resolver.ResolvedHosts);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 8443), opener.Opened.Single().ServerEndPoint);
        Assert.AreEqual("mapped.test", events.Opened.Single().HostName);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithAResolveEntryThatDoesNotParse_FailsWithExit49()
    {
        var opener = new QuicServerChannelOpener();
        var connector = Connector(opener, new ManualTimeProvider(), resolveOverrides: ResolveOverrides.Parse(["bad"]));
        Diagnostics.Arrange("resolve entry", "bad");

        var result = await ConnectMultiplexedAsync(connector, Target());

        Diagnostics.Assert("exit code", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'bad'", result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHostDoesNotResolve_FailsWithExit6()
    {
        var opener = new QuicServerChannelOpener();
        var connector = Connector(opener, new ManualTimeProvider(), resolver: new FakeDnsResolver());
        Diagnostics.Arrange("resolver addresses", "none");

        var result = await ConnectMultiplexedAsync(connector, Target());

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
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
        Diagnostics.Arrange("connect timeout", "1000 ms, then the clock advances 1000 ms");

        var connecting = connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        clock.Advance(1000);
        var result = await connecting;

        ActResult(result.ExitCode, result.ErrorMessage);
        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
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
        Diagnostics.Arrange("connect timeout", "none, then the clock advances 10000 ms");

        var connecting = connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        clock.Advance(10000);
        var result = await connecting;

        ActResult(result.ExitCode, result.ErrorMessage);
        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
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
        Diagnostics.Arrange("server closes after ClientHello with", "0x2");

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target(events));

        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
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
        Diagnostics.Arrange("resolver addresses", "127.0.0.1, 127.0.0.2");

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
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
        Diagnostics.Arrange("insecure", false);

        // The certificate failure is in the platform's own words, so neither it nor the lines are written.
        var result = await ConnectMultiplexedAsync(connector, Target(events), writesErrorMessage: false);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
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
        Diagnostics.Arrange("CA certificate file", "no-such-dir-bl728/ca.pem under the temporary folder");

        var result = await ConnectMultiplexedAsync(connector, Target(), writesErrorMessage: false);

        Diagnostics.Assert("exit code", CurlExitCode.SslCacertBadfile, result.ExitCode);
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
        Diagnostics.Arrange("receive failure", SocketError.ConnectionReset);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target(events), writesErrorMessage: false);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
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
        Diagnostics.Arrange("receive failure", SocketError.ConnectionRefused);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target(), writesErrorMessage: false);

        Diagnostics.Assert("error message ends with", "; Connection refused)", result.ErrorMessage?[result.ErrorMessage.LastIndexOf(';')..]);
        Assert.AreEqual($"QUIC: recvfrom() unexpectedly returned -1 (errno={failure.ErrorCode}; Connection refused)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenAReceiveFailsInTheOpenSslBuild_UsesTheSystemsMessage()
    {
        var failure = new SocketException((int)SocketError.ConnectionReset);
        var opener = new QuicServerChannelOpener { ReceiveFailure = failure };
        Diagnostics.Arrange("receive failure", SocketError.ConnectionReset);
        Diagnostics.Arrange("Schannel build", false);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider(), matchesSchannelBuild: false), Target(), writesErrorMessage: false);

        // The errno and the system's message differ by platform, so only whether they match is written.
        Diagnostics.Assert("error message is the system's", true, result.ErrorMessage == $"QUIC: recvfrom() unexpectedly returned -1 (errno={failure.ErrorCode}; {failure.Message})");
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
        Diagnostics.Arrange("resolver addresses", "127.0.0.1 (cannot open a socket), 127.0.0.2");

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        await using var connection = result.Connection!;
        Diagnostics.Act("info line 5", events.Info[5]);
        Diagnostics.Assert("remote end point", new IPEndPoint(SecondAddress, 443), connection.RemoteEndPoint);
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
        Diagnostics.Arrange("connect timeout", "1000 ms; each failed open advances the clock 2000 ms");

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        Diagnostics.Act("info line count", events.Info.Count);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
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
        Diagnostics.Arrange("cancelled", "while waiting to receive");

        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => connecting);
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("channel disposed", true, opener.Opened.Single().IsDisposed);
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task PoolingConnector_ConnectMultiplexedAsync_AsksTheInnerConnector()
    {
        await using var pool = new PoolingConnector(
            new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()),
            new ManualTimeProvider());
        Diagnostics.Arrange("inner connector", "TCP connector without a QUIC dialer");

        var result = await ConnectMultiplexedAsync(pool, Target());

        Diagnostics.Assert("error message", "QUIC is not available on this connector", result.ErrorMessage);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public void QuicDialer_Constructor_WithANullArgument_ThrowsArgumentNullException()
    {
        var opener = new QuicServerChannelOpener();
        var options = new TlsClientOptions();
        var clock = new ManualTimeProvider();
        var random = SystemTlsRandomSource.Instance;
        Diagnostics.Arrange("null argument", "each of the four in turn");

        var exceptions = new[]
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(null!, options, true, clock, random)),
            Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(opener, null!, true, clock, random)),
            Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(opener, options, true, null!, random)),
            Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDialer(opener, options, true, clock, null!)),
        };

        Diagnostics.Act("parameter names", string.Join(", ", exceptions.Select(exception => exception.ParamName)));
        Diagnostics.Assert("exceptions", 4, exceptions.Length);
    }

    [TestMethod]
    public void QuicDialer_PublicConstructor_CreatesADialer()
    {
        Diagnostics.Arrange("local address", IPAddress.Loopback);

        var dialer = new QuicDialer(new TlsClientOptions(), TimeProvider.System, IPAddress.Loopback, 0);

        Diagnostics.Act("dialer", dialer.GetType().Name);
        Diagnostics.Assert("dialer is null", false, dialer is null);
        Assert.IsNotNull(dialer);
    }

    [TestMethod]
    public async Task UdpChannelOpener_Open_BindsTheLocalAddressGiven()
    {
        Diagnostics.Arrange("local address", IPAddress.Loopback);

        await using var channel = new UdpChannelOpener(IPAddress.Loopback).Open(new IPEndPoint(IPAddress.Loopback, 9));

        var local = (IPEndPoint)channel.LocalEndPoint!;
        Diagnostics.Act("local address", local.Address);
        Diagnostics.Assert("local address", IPAddress.Loopback, local.Address);
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

    /// <summary>
    /// Connects <paramref name="target" /> through <paramref name="connector" /> as the tests did
    /// directly, writing the target as ARRANGE, the resolve, connect and handshake as one PHASE,
    /// and the exit code and, unless <paramref name="writesErrorMessage" /> is false because it
    /// holds a platform's own words, the error message as ACT.
    /// </summary>
    private async Task<MultiplexedConnectResult> ConnectMultiplexedAsync(IConnector connector, ConnectTarget target, bool writesErrorMessage = true)
    {
        Diagnostics.Arrange("target", $"{target.Host}:{target.Port}");
        MultiplexedConnectResult result;
        using (Diagnostics.Phase("resolve, connect and handshake"))
        {
            result = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);
        }

        ActResult(result.ExitCode, writesErrorMessage ? result.ErrorMessage : "(not written: it holds the platform's own words)");
        return result;
    }

    /// <summary>As <see cref="ConnectMultiplexedAsync(IConnector, ConnectTarget, bool)" />, for a TCP connect.</summary>
    private async Task<ConnectResult> ConnectAsync(IConnector connector, ConnectTarget target)
    {
        Diagnostics.Arrange("target", $"{target.Host}:{target.Port}");
        ConnectResult result;
        using (Diagnostics.Phase("resolve, connect and handshake"))
        {
            result = await connector.ConnectAsync(target, CancellationToken.None);
        }

        ActResult(result.ExitCode, result.ErrorMessage);
        return result;
    }

    private void ActResult(CurlExitCode exitCode, string? errorMessage)
    {
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("error message", errorMessage);
    }

    private void ActEvents(RecordingTransferEvents events) =>
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));

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
