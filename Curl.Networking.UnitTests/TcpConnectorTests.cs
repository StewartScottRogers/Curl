using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives every branch of <see cref="TcpConnector" /> through fakes: resolve failure
/// (exit 6), dial failure (exit 7), address order, TLS hand-off and cancellation.
/// </summary>
[TestClass]
public sealed class TcpConnectorTests
{
    private static readonly IPAddress Loopback = IPAddress.Parse("127.0.0.1");

    [TestMethod]
    public async Task ConnectAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        var connector = CreateConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await connector.ConnectAsync(null!, CancellationToken.None));

        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenResolverReturnsNoAddresses_FailsWithCouldntResolveHostAndNeverDials()
    {
        var resolver = new FakeDnsResolver();
        var dialer = new FakeTcpDialer();
        var connector = CreateConnector(resolver, dialer, new FakeTlsProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget("nonexistent.invalid", 2628, UseTls: false),
            CancellationToken.None);

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

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server",
            result.ErrorMessage);
        Assert.IsNull(result.Connection);
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

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(secondConnection, result.Connection);
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

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 636, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        Assert.AreSame(plaintext, tlsProvider.ReceivedPlaintext);
        Assert.AreEqual("example.com", tlsProvider.ReceivedTargetHost);
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

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true), CancellationToken.None);

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

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 389, UseTls: false), CancellationToken.None);

        Assert.AreSame(plaintext, result.Connection);
        Assert.AreEqual(0, tlsProvider.HandshakeCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenDialIsCancelled_ThrowsOperationCanceledException()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new OperationCanceledException() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false), CancellationToken.None));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenResolveIsCancelled_ThrowsOperationCanceledExceptionAndNeverDials()
    {
        var dialer = new FakeTcpDialer();
        var resolver = new FakeDnsResolver { ExceptionToThrow = new OperationCanceledException() };
        var connector = CreateConnector(resolver, dialer, new FakeTlsProvider());

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(new ConnectTarget("example.com", 80, UseTls: false), CancellationToken.None));

        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    private static TcpConnector CreateConnector(FakeDnsResolver resolver, FakeTcpDialer dialer, FakeTlsProvider tlsProvider) =>
        new(resolver, dialer, tlsProvider, new ManualTimeProvider());
}
