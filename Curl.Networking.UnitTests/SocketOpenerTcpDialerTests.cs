using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// A connect's <see cref="ConnectTarget.SocketOpener" /> is asked before each TCP attempt, as curl 8.22.0's
/// <c>cf_socket_open</c> calls <c>CURLOPT_OPENSOCKETFUNCTION</c> before the connect (ADR-0903, CGR-036). The
/// loopback listeners here are only connected to, never sent a byte.
/// </summary>
[TestClass]
public sealed class SocketOpenerTcpDialerTests
{
    [TestMethod]
    public async Task ConnectAsync_WithAnOpenerRefusingEveryAddress_FailsWithExit7AndTheListenerAcceptsNothing()
    {
        using var listener = Listen();
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Refuse);

        var result = await ConnectAsync(PortOf(listener), opener);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsFalse(listener.Poll(100_000, SelectMode.SelectRead), "the listener has a connection waiting");
        Assert.HasCount(1, opener.Opened);
        Assert.IsEmpty(opener.Closed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnOpenerSupplyingAConnectedSocket_UsesThatSocketsConnectionNotTheTargets()
    {
        using var target = Listen();
        using var other = Listen();
        using var supplied = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await supplied.ConnectAsync(other.LocalEndPoint!);
        using var accepted = await other.AcceptAsync();
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Supplied(supplied.Handle, alreadyConnected: true));

        var result = await ConnectAsync(PortOf(target), opener);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Assert.AreEqual(accepted.RemoteEndPoint, connection.LocalEndPoint);
        Assert.IsFalse(target.Poll(100_000, SelectMode.SelectRead), "the target's listener has a connection waiting");
        Assert.IsEmpty(opener.Closed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7()
    {
        using var unlistened = BoundUnlistenedSocket();
        var port = PortOf(unlistened);
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Proceed);

        var result = await ConnectAsync(port, opener);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        var expected = new IPEndPoint(IPAddress.Loopback, port);
        CollectionAssert.AreEqual(new[] { expected }, opener.Opened);
        CollectionAssert.AreEqual(new[] { expected }, opener.Closed);
    }

    [TestMethod]
    public async Task DialAsync_WithASuppliedSocketNotYetConnected_ConnectsItToTheAddress()
    {
        using var listener = Listen();
        using var supplied = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Supplied(supplied.Handle, alreadyConnected: false));
        var dialer = new SocketOpenerTcpDialer(new FakeTcpDialer(), opener);

        var dialed = await dialer.DialAsync((IPEndPoint)listener.LocalEndPoint!, CancellationToken.None);
        await using var connection = dialed.Connection;

        using var accepted = await listener.AcceptAsync();
        Assert.AreEqual(accepted.RemoteEndPoint, dialed.LocalEndPoint);
        Assert.IsEmpty(opener.Closed);
    }

    [TestMethod]
    public async Task DialAsync_WithASuppliedSocketWhoseConnectFails_ThrowsAndTellsTheOpenerItClosed()
    {
        using var unlistened = BoundUnlistenedSocket();
        var endPoint = (IPEndPoint)unlistened.LocalEndPoint!;
        using var supplied = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Supplied(supplied.Handle, alreadyConnected: false));
        var dialer = new SocketOpenerTcpDialer(new FakeTcpDialer(), opener);

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await dialer.DialAsync(endPoint, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { endPoint }, opener.Closed);
    }

    [TestMethod]
    public async Task DialAsync_WhenTheOpenerProceeds_DialsThroughTheInnerDialer()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Proceed);
        var dialer = new SocketOpenerTcpDialer(inner, opener);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { endPoint }, inner.DialedEndPoints);
        Assert.IsEmpty(opener.Closed);
    }

    [TestMethod]
    public async Task DialAsync_WithNoEndPoint_Throws()
    {
        var dialer = new SocketOpenerTcpDialer(new FakeTcpDialer(), new RecordingSocketOpener(_ => TcpSocketOpenDecision.Proceed));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await dialer.DialAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task OtherDials_GoStraightToTheInnerDialerWithoutTheOpener()
    {
        var refusal = new SocketException((int)SocketError.ProtocolNotSupported);
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection(), UnixSocketDialOutcome = _ => new FakeConnection(), SocketOpenOutcome = _ => refusal };
        var opener = new RecordingSocketOpener(_ => TcpSocketOpenDecision.Refuse);
        var dialer = new SocketOpenerTcpDialer(inner, opener);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var unixSocket = new UnixSocketAddress("/tmp/curl.sock", IsAbstract: false);

        await dialer.DialFromAsync(endPoint, new IPEndPoint(IPAddress.Loopback, 0), 1, CancellationToken.None);
        await dialer.DialUnixSocketAsync(unixSocket, CancellationToken.None);

        Assert.HasCount(1, inner.BoundDials);
        CollectionAssert.AreEqual(new[] { unixSocket }, inner.DialedUnixSockets);
        Assert.AreSame(refusal, dialer.FailureToOpenSocket(AddressFamily.InterNetwork));
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public void Decisions_SayWhatWasDecided()
    {
        Assert.IsFalse(TcpSocketOpenDecision.Proceed.IsRefused);
        Assert.IsNull(TcpSocketOpenDecision.Proceed.SuppliedHandle);
        Assert.IsTrue(TcpSocketOpenDecision.Refuse.IsRefused);
        Assert.IsNull(TcpSocketOpenDecision.Refuse.SuppliedHandle);
        var supplied = TcpSocketOpenDecision.Supplied(42, alreadyConnected: true);
        Assert.IsFalse(supplied.IsRefused);
        Assert.AreEqual((nint)42, supplied.SuppliedHandle);
        Assert.IsTrue(supplied.AlreadyConnected);
    }

    private static Task<ConnectResult> ConnectAsync(int port, ITcpSocketOpener opener)
    {
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), new TcpDialer(), new FakeTlsProvider(), TimeProvider.System);
        return connector.ConnectAsync(new ConnectTarget("127.0.0.1", port, UseTls: false) { SocketOpener = opener }, CancellationToken.None).AsTask();
    }

    private static Socket Listen()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();
        return listener;
    }

    private static int PortOf(Socket listener) => ((IPEndPoint)listener.LocalEndPoint!).Port;

    /// <summary>
    /// A loopback socket bound to a port the system handed it and never listening, so a connect to that port is
    /// refused; held for the whole test, so a test running beside it cannot be handed the port and accept the
    /// connect (BL-4368).
    /// </summary>
    private static Socket BoundUnlistenedSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }

    private sealed class RecordingSocketOpener(Func<IPEndPoint, TcpSocketOpenDecision> decide) : ITcpSocketOpener
    {
        public List<IPEndPoint> Opened { get; } = [];

        public List<IPEndPoint> Closed { get; } = [];

        public TcpSocketOpenDecision Open(IPEndPoint remoteEndPoint)
        {
            Opened.Add(remoteEndPoint);
            return decide(remoteEndPoint);
        }

        public void SocketClosed(IPEndPoint remoteEndPoint) => Closed.Add(remoteEndPoint);
    }
}
