using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Drives every branch of <see cref="TcpConnector" /> through fakes: resolve failure
/// (exit 6), dial failure (exit 7), address order, TLS hand-off and cancellation.
/// </summary>
[TestClass]
public sealed partial class TcpConnectorTests
{
    private static readonly IPAddress Loopback = IPAddress.Parse("127.0.0.1");

    public TestContext TestContext { get; set; } = null!;

    /// <summary>Gets the running test's diagnostics.</summary>
    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("target", "null");
        var connector = CreateConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await connector.ConnectAsync(null!, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "target", exception.ParamName);
        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_WithHostOf300Bytes_FailsWithCouldntResolveHostCutTo255Characters()
    {
        // curl 8.21.0 (Schannel): curl http://<300 a's>/ -> exit 6,
        // curl: (6) Could not resolve host: <first 231 a's> (measured 2026-09-27, BL-377).
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(new SystemDnsResolver(), dialer, new FakeTlsProvider(), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget(new string('a', 300), 80, UseTls: false));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: " + new string('a', 231), result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenResolverReturnsNoAddresses_FailsWithCouldntResolveHostAndNeverDials()
    {
        var resolver = new FakeDnsResolver();
        var dialer = new FakeTcpDialer();
        var connector = CreateConnector(resolver, dialer, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("nonexistent.invalid", 2628, UseTls: false));

        Diagnostics.Assert("dialed end points", 0, dialer.DialedEndPoints.Count);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        CollectionAssert.AreEqual(new[] { "nonexistent.invalid" }, resolver.ResolvedHosts);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenEveryAddressFailsToDial_FailsWithCouldntConnectAndElapsedMilliseconds()
    {
        var timeProvider = new ManualTimeProvider();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                timeProvider.Advance(2013);
                throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), timeProvider);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false));

        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server",
            result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAForwardProxyFailsToDial_NamesTheProxyInTheCouldntConnectMessage()
    {
        // curl 8.21.0 (Schannel): curl -sS -x http://127.0.0.1:1 http://example.com/ -> exit 7,
        // curl: (7) Failed to connect to 127.0.0.1:1 over proxy 127.0.0.1 after 2028 ms: Could not connect to server
        // (measured with Record-CurlExchange.ps1 2026-10-01, BL-1024).
        var timeProvider = new ManualTimeProvider();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                timeProvider.Advance(2028);
                throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), timeProvider);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false) { IsForwardProxy = true });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to 127.0.0.1:1 over proxy 127.0.0.1 after 2028 ms: Could not connect to server",
            result.ErrorMessage);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAForwardProxyCannotBindTheInterface_NamesTheProxyInTheInterfaceFailedMessage()
    {
        // curl 8.21.0 (Schannel): curl -sS --interface bogus0 -x http://127.0.0.1:47599 http://example.com/ -> exit 45,
        // curl: (45) Failed to connect to 127.0.0.1:47599 over proxy 127.0.0.1 after 2760 ms: Failed binding local connection end
        // (measured with Record-CurlExchange.ps1 2026-10-01, BL-1024).
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = LocalBindingConnector(dialer, new LocalBinding("bogus0", null, null, 0, 1), Interfaces(("lo", [IPAddress.Loopback])));
        Diagnostics.Arrange("interface", "bogus0");

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47599, UseTls: false) { IsForwardProxy = true });

        Diagnostics.Assert("exit code", CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to 127.0.0.1:47599 over proxy 127.0.0.1 after 0 ms: Failed binding local connection end",
            result.ErrorMessage);
        Assert.IsEmpty(dialer.BoundDials);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheLastAddressFailsForAnotherReason_IsNotMarkedRefused()
    {
        // curl 8.21.0 (Schannel): curl --retry 1 --retry-connrefused http://0.0.0.0:1/ -> exit 7,
        // not retried, because CURLINFO_OS_ERRNO is not ECONNREFUSED (measured 2026-09-27, BL-317).
        var refused = IPAddress.Parse("192.0.2.1");
        var dialer = new FakeTcpDialer
        {
            DialOutcome = endPoint => throw new System.Net.Sockets.SocketException(endPoint.Address.Equals(refused)
                ? (int)System.Net.Sockets.SocketError.ConnectionRefused
                : (int)System.Net.Sockets.SocketError.AddressNotAvailable),
        };
        var connector = CreateConnector(new FakeDnsResolver(refused, IPAddress.Any), dialer, new FakeTlsProvider());
        Diagnostics.Arrange("addresses", "192.0.2.1 refuses, 0.0.0.0 not available");

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("0.0.0.0", 1, UseTls: false));

        Diagnostics.Assert("connection refused", false, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheLastAddressRefusesAfterAnotherFailure_IsMarkedRefused()
    {
        var unavailable = IPAddress.Parse("192.0.2.1");
        var dialer = new FakeTcpDialer
        {
            DialOutcome = endPoint => throw new System.Net.Sockets.SocketException(endPoint.Address.Equals(unavailable)
                ? (int)System.Net.Sockets.SocketError.AddressNotAvailable
                : (int)System.Net.Sockets.SocketError.ConnectionRefused),
        };
        var connector = CreateConnector(new FakeDnsResolver(unavailable, Loopback), dialer, new FakeTlsProvider());
        Diagnostics.Arrange("addresses", "192.0.2.1 not available, 127.0.0.1 refuses");

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 1, UseTls: false));

        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ConnectAsync_TriesAddressesInResolverOrderAndReturnsTheFirstSuccess()
    {
        var first = IPAddress.Parse("192.0.2.1");
        var second = IPAddress.Parse("192.0.2.2");
        var third = IPAddress.Parse("192.0.2.3");
        var secondConnection = new FakeConnection();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = endPoint => endPoint.Address.Equals(first)
                ? throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused)
                : secondConnection,
        };
        var connector = CreateConnector(new FakeDnsResolver(first, second, third), dialer, new FakeTlsProvider());
        Diagnostics.Arrange("resolver order", "192.0.2.1 (refuses), 192.0.2.2, 192.0.2.3");

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 80, UseTls: false));

        Diagnostics.Act("dialed end points", string.Join(", ", dialer.DialedEndPoints));
        Diagnostics.Assert("dial count", 2, dialer.DialedEndPoints.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(secondConnection, result.Connection);
        Assert.IsNull(result.UnixSocketPath);
        CollectionAssert.AreEqual(
            new[] { new IPEndPoint(first, 80), new IPEndPoint(second, 80) },
            dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_PassesConnectionAndHostToTlsProviderAndReturnsItsResult()
    {
        var plaintext = new FakeConnection();
        var tlsProvider = new FakeTlsProvider();
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => plaintext },
            tlsProvider);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 636, UseTls: true));

        Diagnostics.Assert("TLS target host", "example.com", tlsProvider.ReceivedTargetHost);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        Assert.AreSame(plaintext, tlsProvider.ReceivedPlaintext);
        Assert.AreEqual("example.com", tlsProvider.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_PassesTheTargetsEventsToAHandshakeReportingProvider()
    {
        var events = new RecordingTransferEvents();
        var tlsProvider = new FakeTlsProvider();
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider);

        await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { Events = events });

        Diagnostics.Assert("events passed on", true, ReferenceEquals(events, tlsProvider.ReceivedEvents));
        Assert.AreSame(events, tlsProvider.ReceivedEvents);
        CollectionAssert.AreEqual(new[] { false }, tlsProvider.ReceivedIsProxy);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsTargetTheHttpHandlerPools_OffersHttp11ThroughAlpn()
    {
        // curl -v -k https://127.0.0.1:P/ says ALPN: curl offers http/1.1 (8.21.0 Schannel, measured, BL-490).
        var tlsProvider = new FakeTlsProvider();
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider);

        await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { PoolScheme = "https" });

        Diagnostics.Assert("ALPN offers", "http/1.1", OfferedProtocols(tlsProvider));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, Assert.ContainsSingle(tlsProvider.ReceivedApplicationProtocols).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_BuiltWithH2ThenHttp11_OffersThemToAnHttpsTargetTheHttpHandlerPools()
    {
        // curl -v --http2 https://www.google.com/ says ALPN: curl offers h2,http/1.1 (nghttp2 builds, ADR-0141).
        var tlsProvider = new FakeTlsProvider();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider,
            TimeProvider.System,
            httpOverTlsApplicationProtocols: HttpApplicationProtocols.H2ThenHttp11);

        await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { PoolScheme = "https" });

        Diagnostics.Assert("ALPN offers", "h2,http/1.1", OfferedProtocols(tlsProvider));
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, Assert.ContainsSingle(tlsProvider.ReceivedApplicationProtocols).ToArray());
    }

    [TestMethod]
    [DataRow("h2")]
    [DataRow(null)]
    public async Task ConnectAsync_OverTls_GivesTheProtocolTheServerAcceptedThroughAlpn(string? accepted)
    {
        // curl -v --http2 https://example.com/ says ALPN: server accepted h2, then using HTTP/2 (BL-660 Notes).
        Diagnostics.Arrange("server accepts", accepted);
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider { ApplicationProtocolToReturn = accepted },
            TimeProvider.System,
            httpOverTlsApplicationProtocols: HttpApplicationProtocols.H2ThenHttp11);

        var connect = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { PoolScheme = "https" });

        Diagnostics.Assert("application protocol", accepted, connect.ApplicationProtocol);
        Assert.AreEqual(accepted, connect.ApplicationProtocol);
    }

    [TestMethod]
    public void HttpOverTlsApplicationProtocols_NotGiven_IsHttp11Only()
    {
        Diagnostics.Arrange("httpOverTlsApplicationProtocols", "not given");
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer(), new FakeTlsProvider(), TimeProvider.System);

        Diagnostics.Act("protocols", string.Join(",", connector.HttpOverTlsApplicationProtocols));
        Diagnostics.Assert("is Http11Only", true, ReferenceEquals(HttpApplicationProtocols.Http11Only, connector.HttpOverTlsApplicationProtocols));
        Assert.AreSame(HttpApplicationProtocols.Http11Only, connector.HttpOverTlsApplicationProtocols);
    }

    [TestMethod]
    [DataRow("https", false, true)]
    [DataRow("HTTPS", false, true)]
    [DataRow("http", false, false)]
    [DataRow(null, false, false)]
    public void ApplicationProtocolsFor_OffersTheHttpListOnlyForHttpOverTlsToTheOrigin(string? poolScheme, bool isForwardProxy, bool offersHttpList)
    {
        var target = new ConnectTarget("example.com", 443, UseTls: true) { PoolScheme = poolScheme, IsForwardProxy = isForwardProxy };
        Diagnostics.Arrange("pool scheme", poolScheme);
        Diagnostics.Arrange("forward proxy", isForwardProxy);

        var offered = string.Join(",", TcpConnector.ApplicationProtocolsFor(target, HttpApplicationProtocols.H2Only).ToArray());
        Diagnostics.Act("offered", offered);

        Diagnostics.Assert("offered", offersHttpList ? "h2" : string.Empty, offered);
        CollectionAssert.AreEqual(
            offersHttpList ? new[] { "h2" } : Array.Empty<string>(),
            TcpConnector.ApplicationProtocolsFor(target, HttpApplicationProtocols.H2Only).ToArray());
    }

    [TestMethod]
    [DataRow("https")]
    [DataRow("http")]
    [DataRow(null)]
    public void ApplicationProtocolsFor_AnHttpsForwardProxy_OffersHttp11WhateverTheHttpList(string? poolScheme)
    {
        // curl -v --http2 --proxy-insecure -x https://proxy http://example.test/ says
        // ALPN: curl offers http/1.1 for the proxy's handshake (8.21.0 Schannel and OpenSSL, measured, BL-753).
        var target = new ConnectTarget("proxy.example", 443, UseTls: true) { PoolScheme = poolScheme, IsForwardProxy = true };
        Diagnostics.Arrange("pool scheme", poolScheme);

        var offered = string.Join(",", TcpConnector.ApplicationProtocolsFor(target, HttpApplicationProtocols.H2ThenHttp11).ToArray());
        Diagnostics.Act("offered", offered);

        Diagnostics.Assert("offered", "http/1.1", offered);
        CollectionAssert.AreEqual(
            new[] { "http/1.1" },
            TcpConnector.ApplicationProtocolsFor(target, HttpApplicationProtocols.H2ThenHttp11).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsForwardProxy_OffersHttp11ThroughAlpnUnderHttp2()
    {
        // curl -v --http2 --proxy-insecure -x https://proxy http://example.test/ says
        // ALPN: curl offers http/1.1 (8.21.0 Schannel and OpenSSL, measured, BL-753).
        var tlsProvider = new FakeTlsProvider();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider,
            TimeProvider.System,
            httpOverTlsApplicationProtocols: HttpApplicationProtocols.H2ThenHttp11);

        await ConnectLoggedAsync(connector, new ConnectTarget("proxy.example", 443, UseTls: true) { IsForwardProxy = true });

        Diagnostics.Assert("ALPN offers", "http/1.1", OfferedProtocols(tlsProvider));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, Assert.ContainsSingle(tlsProvider.ReceivedApplicationProtocols).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxy_OffersHttp11ThroughAlpnInTheProxysHandshakeUnderHttp2()
    {
        // curl -v --http2 -p --proxy-insecure -x https://proxy http://example.test/ says
        // ALPN: curl offers http/1.1 before CONNECT (8.21.0 Schannel and OpenSSL, measured, BL-753).
        var tlsProvider = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "x") };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider,
            TimeProvider.System,
            httpOverTlsApplicationProtocols: HttpApplicationProtocols.H2ThenHttp11);
        Diagnostics.Arrange("proxy", "https://proxy.example:443");

        await ConnectLoggedAsync(
            connector,
            new ConnectTarget("example.com", 443, UseTls: true)
            {
                PoolScheme = "https",
                Proxy = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null),
            });

        Diagnostics.Assert("ALPN offers", "http/1.1", OfferedProtocols(tlsProvider));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, Assert.ContainsSingle(tlsProvider.ReceivedApplicationProtocols).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsForwardProxy_ReportsItsHandshakeAsTheProxys()
    {
        // curl -sv --proxy-insecure -x https://proxy http://example.test/ says Proxy certificate:
        // for the forward proxy's handshake (curl 8.21.0 OpenSSL, measured, BL-405).
        var events = new RecordingTransferEvents();
        var tlsProvider = new FakeTlsProvider();
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider);

        await ConnectLoggedAsync(connector, new ConnectTarget("proxy.example", 443, UseTls: true) { Events = events, IsForwardProxy = true });

        Diagnostics.Assert("handshake is the proxy's", "True", string.Join(",", tlsProvider.ReceivedIsProxy));
        Assert.AreSame(events, tlsProvider.ReceivedEvents);
        CollectionAssert.AreEqual(new[] { true }, tlsProvider.ReceivedIsProxy);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxy_ReportsTheProxysHandshakeOnTheTargetsEventsAsTheProxys()
    {
        var events = new RecordingTransferEvents();
        var tlsProvider = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "x") };
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider);
        Diagnostics.Arrange("proxy", "https://proxy.example:443");

        await ConnectLoggedAsync(
            connector,
            new ConnectTarget("example.com", 443, UseTls: true)
            {
                Events = events,
                Proxy = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null),
            });

        Diagnostics.Assert("TLS target host", "proxy.example", tlsProvider.ReceivedTargetHost);
        Assert.AreSame(events, tlsProvider.ReceivedEvents);
        CollectionAssert.AreEqual(new[] { true }, tlsProvider.ReceivedIsProxy);
        Assert.AreEqual("proxy.example", tlsProvider.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_WhenHandshakeFails_ReturnsTheProvidersFailureUnchanged()
    {
        var failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "x");
        var tlsProvider = new FakeTlsProvider { FailureToReturn = failure };
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            tlsProvider);
        Diagnostics.Arrange("handshake failure", "SslConnectError, x");

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true));

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("x", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(1, tlsProvider.HandshakeCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WithoutUseTls_NeverCallsTlsProvider()
    {
        var plaintext = new FakeConnection();
        var tlsProvider = new FakeTlsProvider();
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => plaintext },
            tlsProvider);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 389, UseTls: false));

        Diagnostics.Assert("handshake count", 0, tlsProvider.HandshakeCount);
        Assert.AreSame(plaintext, result.Connection);
        Assert.AreEqual(0, tlsProvider.HandshakeCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenDialIsCancelled_ThrowsOperationCanceledException()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new OperationCanceledException() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        Diagnostics.Arrange("dial", "throws OperationCanceledException");

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false)));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(OperationCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenResolveIsCancelled_ThrowsOperationCanceledExceptionAndNeverDials()
    {
        var dialer = new FakeTcpDialer();
        var resolver = new FakeDnsResolver { ExceptionToThrow = new OperationCanceledException() };
        var connector = CreateConnector(resolver, dialer, new FakeTlsProvider());
        Diagnostics.Arrange("resolve", "throws OperationCanceledException");

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 80, UseTls: false)));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("dialed end points", 0, dialer.DialedEndPoints.Count);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    private static TcpConnector CreateConnector(FakeDnsResolver resolver, FakeTcpDialer dialer, FakeTlsProvider tlsProvider) =>
        new(resolver, dialer, tlsProvider, new ManualTimeProvider());

    /// <summary>
    /// Writes <paramref name="target" /> as ARRANGE, connects it inside a <c>connect</c> PHASE,
    /// and writes the exit code and error message as ACT.
    /// </summary>
    private async Task<ConnectResult> ConnectLoggedAsync(TcpConnector connector, ConnectTarget target)
    {
        Diagnostics.Arrange("target", $"{target.Host}:{target.Port}, TLS {target.UseTls}, forward proxy {target.IsForwardProxy}");
        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await connector.ConnectAsync(target, CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        return result;
    }

    /// <summary>The ALPN lists the TLS provider was offered, comma-joined, one list per line.</summary>
    private static string OfferedProtocols(FakeTlsProvider tlsProvider) =>
        string.Join(" | ", tlsProvider.ReceivedApplicationProtocols.Select(list => string.Join(",", list)));
}
