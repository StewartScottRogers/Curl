using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives every branch of <see cref="UdpDatagramConnector" /> through fakes: resolve
/// failure (exit 6), open failure (exit 7), endpoint selection and cancellation.
/// </summary>
[TestClass]
public sealed class UdpDatagramConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_WithNullHost_ThrowsArgumentNullException()
    {
        var connector = CreateConnector(new FakeDnsResolver(), OpenFake([]));

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await connector.OpenAsync(null!, 69, CancellationToken.None));

        Assert.AreEqual("host", exception.ParamName);
    }

    [TestMethod]
    public async Task OpenAsync_WhenResolverReturnsNoAddresses_FailsWithCouldntResolveHostAndOpensNothing()
    {
        var resolver = new FakeDnsResolver();
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(resolver, OpenFake(opened));

        var result = await connector.OpenAsync("nonexistent.invalid", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
        Assert.IsNull(result.Channel);
        CollectionAssert.AreEqual(new[] { "nonexistent.invalid" }, resolver.ResolvedHosts);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenOpenSucceeds_ReportsTheFirstResolvedAddressWithTheRequestedPort()
    {
        var first = IPAddress.Parse("192.0.2.1");
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(new FakeDnsResolver(first, IPAddress.Parse("192.0.2.2")), OpenFake(opened));

        var result = await connector.OpenAsync("tftp.example", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(result.Channel);
        Assert.AreEqual(new IPEndPoint(first, 6969), result.Channel.ServerEndPoint);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(first, 6969) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheFirstAddressCannotBeOpened_UsesTheNextAddress()
    {
        var first = IPAddress.Parse("2001:db8::1");
        var second = IPAddress.Parse("192.0.2.2");
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(
            new FakeDnsResolver(first, second),
            endPoint =>
            {
                opened.Add(endPoint);
                return endPoint.Address.Equals(first)
                    ? throw new SocketException((int)SocketError.AddressFamilyNotSupported)
                    : new FakeDatagramChannel(endPoint);
            });

        var result = await connector.OpenAsync("tftp.example", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        Assert.AreEqual(new IPEndPoint(second, 69), result.Channel.ServerEndPoint);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(first, 69), new IPEndPoint(second, 69) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenNoAddressCanBeOpened_FailsWithCouldntConnectAndElapsedMilliseconds()
    {
        var timeProvider = new ManualTimeProvider();
        var connector = new UdpDatagramConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            timeProvider,
            _ =>
            {
                timeProvider.Advance(3);
                throw new SocketException((int)SocketError.AddressFamilyNotSupported);
            });

        var result = await connector.OpenAsync("127.0.0.1", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:69 after 3 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsNull(result.Channel);
    }

    [TestMethod]
    public async Task OpenAsync_WhenResolveIsCancelled_ThrowsOperationCanceledException()
    {
        var connector = CreateConnector(
            new FakeDnsResolver { ExceptionToThrow = new OperationCanceledException() },
            OpenFake([]));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connector.OpenAsync("tftp.example", 69, CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenAsync_ThroughThePublicConstructor_OpensAUdpChannelToTheResolvedEndPoint()
    {
        var connector = new UdpDatagramConnector(new FakeDnsResolver(IPAddress.Loopback), TimeProvider.System);

        var result = await connector.OpenAsync("localhost", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        await using var channel = result.Channel;
        Assert.IsInstanceOfType<UdpDatagramChannel>(channel);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 69), channel.ServerEndPoint);
    }

    private static UdpDatagramConnector CreateConnector(IDnsResolver resolver, Func<IPEndPoint, IDatagramChannel> openChannel) =>
        new(resolver, new ManualTimeProvider(), openChannel);

    private static Func<IPEndPoint, IDatagramChannel> OpenFake(List<IPEndPoint> opened) =>
        endPoint =>
        {
            opened.Add(endPoint);
            return new FakeDatagramChannel(endPoint);
        };
}
