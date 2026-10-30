using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// Checks <see cref="VirtualMachineSocketConnector" /> against vsock(7) "Description" and Hyper-V sockets: a
/// <c>SOCK_STREAM</c> socket connected to the address carries a byte stream with no framing of its own.
/// </summary>
[TestClass]
public sealed class VirtualMachineSocketConnectorTests
{
    private static readonly VsockAddress Vsock = new(3, 5000);

    [TestMethod]
    public async Task ConnectAsync_StreamSocket_CarriesTheRequestAndResponseBytesUnchanged()
    {
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        FakeVirtualMachineSocketOpener opener = new() { ServerBytes = response };
        byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");

        ConnectResult result = await new VirtualMachineSocketConnector(Vsock, opener).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await using IConnection connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        byte[] read = new byte[response.Length + 8];
        int count = await connection.ReadAsync(read, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(request, opener.LastSocket!.Written.ToArray());
        Assert.AreEqual(1, opener.LastSocket.FlushCount);
        CollectionAssert.AreEqual(response, read[..count]);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_AnyHostAndPort_OpensTheOneAddress()
    {
        FakeVirtualMachineSocketOpener opener = new();
        VirtualMachineSocketConnector connector = new(Vsock, opener);

        await (await connector.ConnectAsync(new ConnectTarget("example.com", 8080, UseTls: false), CancellationToken.None)).Connection!.DisposeAsync();
        await (await connector.ConnectAsync(new ConnectTarget("other.test", 80, UseTls: false), CancellationToken.None)).Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { Vsock, Vsock }, opener.Opened);
        Assert.IsTrue(opener.LastSocket!.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTarget_IsRefusedAsNotBuiltInWithoutOpeningTheSocket()
    {
        FakeVirtualMachineSocketOpener opener = new();

        ConnectResult result = await new VirtualMachineSocketConnector(Vsock, opener).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual("TLS over the vsock 3:5000 is not built in", result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetWithCurlsTlsProvider_RunsTheHandshakeOverTheSocket()
    {
        byte[] clientHello = [0x16, 0x03, 0x01, 0x00, 0x04, 0x01, 0x00, 0x00, 0x00];
        byte[] serverHello = [0x16, 0x03, 0x03, 0x00, 0x04, 0x02, 0x00, 0x00, 0x00];
        FakeVirtualMachineSocketOpener opener = new() { ServerBytes = serverHello };
        RecordedHandshakeTlsProvider tls = new(clientHello, serverHello.Length);
        DistinctTransferEvents events = new();

        ConnectResult result = await new VirtualMachineSocketConnector(Vsock, opener, tls).ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { Events = events, TlsServerName = "vm.test" }, CancellationToken.None);
        await using IConnection connection = result.Connection!;

        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual("vm.test", tls.TargetHost);
        Assert.AreSame(events, tls.Events);
        CollectionAssert.AreEqual(serverHello, tls.ServerHello);
        CollectionAssert.AreEqual(clientHello, opener.LastSocket!.Written.ToArray());
        Assert.IsEmpty(tls.ApplicationProtocols!);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetWithoutServerName_VerifiesTheUrlsHost()
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);

        ConnectResult result = await new VirtualMachineSocketConnector(Vsock, new FakeVirtualMachineSocketOpener(), tls).ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.AreEqual("example.com", tls.TargetHost);
    }

    [TestMethod]
    [DataRow(null, new[] { "h2", "http/1.1" }, DisplayName = "curl's list")]
    [DataRow(new[] { "h2" }, new[] { "h2" }, DisplayName = "the target's own list")]
    public async Task ConnectAsync_HttpsTarget_OffersTheListThroughAlpn(string[]? targetList, string[] offered)
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);
        VirtualMachineSocketConnector connector = new(Vsock, new FakeVirtualMachineSocketOpener(), tls, ["h2", "http/1.1"]);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { PoolScheme = "HTTPS", ApplicationProtocols = targetList }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(offered, tls.ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    public void HttpOverTlsApplicationProtocols_WithoutCurlsList_IsHttp11Alone() =>
        CollectionAssert.AreEqual(new[] { "http/1.1" }, new VirtualMachineSocketConnector(Vsock, new FakeVirtualMachineSocketOpener()).HttpOverTlsApplicationProtocols.ToArray());

    [TestMethod]
    [DataRow(typeof(SocketException), DisplayName = "no such family, refused or reset")]
    [DataRow(typeof(IOException), DisplayName = "an I/O failure")]
    [DataRow(typeof(TimeoutException), DisplayName = "timed out")]
    [DataRow(typeof(UnauthorizedAccessException), DisplayName = "access denied")]
    public async Task ConnectAsync_SocketThatCannotBeOpened_FailsAsCouldNotConnect(Type failureType)
    {
        Exception failure = failureType == typeof(SocketException) ? new SocketException((int)SocketError.AddressFamilyNotSupported) : (Exception)Activator.CreateInstance(failureType, "the reason")!;
        HyperVSocketAddress hyperV = new(HyperVSocketAddress.Parent, new Guid("3049197c-facb-11e6-bd58-64006a7986d3"));

        ConnectResult result = await new VirtualMachineSocketConnector(hyperV, new FakeVirtualMachineSocketOpener(failure)).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual($"Failed to connect to the {hyperV}: {failure.Message}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_Cancelled_LetsTheCancellationThrough() =>
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await new VirtualMachineSocketConnector(Vsock, new FakeVirtualMachineSocketOpener(new OperationCanceledException())).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None));

    [TestMethod]
    public async Task ConnectAsync_NullTarget_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await new VirtualMachineSocketConnector(Vsock, new FakeVirtualMachineSocketOpener()).ConnectAsync(null!, CancellationToken.None));
}
