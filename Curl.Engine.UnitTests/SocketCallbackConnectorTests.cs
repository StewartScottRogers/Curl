using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class SocketCallbackConnectorTests
{
    private static readonly ConnectTarget Target = new("h", 80, false);

    [TestMethod]
    public async Task ConnectAsync_AllCallbacksSet_CallsOpenWithThePeerThenSockoptAndCloseOnceOnDispose()
    {
        List<string> calls = [];
        FakeConnection inner = new() { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80) };
        CurlSocketAddress? seen = null;
        SocketCallbackConnector connector = Create(new FakeConnector(inner), new EasyCallbackOptions
        {
            OpenSocket = (purpose, address) =>
            {
                seen = address;
                calls.Add($"open {purpose}");
                return new CurlSocket(9);
            },
            SocketOption = (socket, _) =>
            {
                calls.Add($"sockopt {socket.Handle}");
                return SocketOptionResult.Ok;
            },
            CloseSocket = socket =>
            {
                calls.Add($"close {socket.Handle}");
                return 0;
            },
        });

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);
        SocketCallbackConnection connection = (SocketCallbackConnection)connect.Connection!;
        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.AreEqual(new CurlSocket(9), connection.Socket);
        Assert.IsTrue(inner.Disposed);
        CollectionAssert.AreEqual(new[] { "open IpConnection", "sockopt 9", "close 9" }, calls);
        Assert.AreEqual(2, seen!.Family);
        Assert.AreEqual(SocketCallbackConnector.StreamSocketType, seen.SocketType);
        Assert.AreEqual(SocketCallbackConnector.TcpProtocol, seen.Protocol);
        Assert.AreEqual(16, seen.Address.Length);
    }

    [TestMethod]
    public async Task ConnectAsync_OpenSocketReturnsBad_DisposesTheConnectionAndFailsWithCouldntConnect()
    {
        int others = 0;
        FakeConnection inner = new();
        SocketCallbackConnector connector = Create(new FakeConnector(inner), new EasyCallbackOptions
        {
            OpenSocket = (_, _) => CurlSocket.Bad,
            SocketOption = (_, _) => (SocketOptionResult)(++others),
            CloseSocket = _ => ++others,
        });

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, connect.ExitCode);
        Assert.AreEqual("Failed to connect to h:80 after 0 ms: Could not connect to server", connect.ErrorMessage);
        Assert.IsTrue(inner.Disposed);
        Assert.AreEqual(0, others);
    }

    [TestMethod]
    public async Task ConnectAsync_SockoptReturnsError_DisposesTheConnectionCallsCloseAndFailsWithAbortedByCallback()
    {
        List<CurlSocket> closed = [];
        FakeConnection inner = new();
        SocketCallbackConnector connector = Create(new FakeConnector(inner), new EasyCallbackOptions
        {
            SocketOption = (_, _) => SocketOptionResult.Error,
            CloseSocket = socket =>
            {
                closed.Add(socket);
                return 0;
            },
        });

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.AbortedByCallback, connect.ExitCode);
        Assert.AreEqual("Failed to connect to h:80 after 0 ms: Operation was aborted by an application callback", connect.ErrorMessage);
        Assert.IsTrue(inner.Disposed);
        CollectionAssert.AreEqual(new[] { new CurlSocket(1) }, closed);
    }

    [TestMethod]
    public async Task ConnectAsync_SockoptReturnsAlreadyConnected_CarriesOnWithTheConnection()
    {
        SocketCallbackConnector connector = Create(new FakeConnector(new FakeConnection()), new EasyCallbackOptions { SocketOption = (_, _) => SocketOptionResult.AlreadyConnected });

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, connect.ExitCode);
        Assert.IsInstanceOfType<SocketCallbackConnection>(connect.Connection);
    }

    [TestMethod]
    public async Task ConnectAsync_NoOpenSocketCallback_NumbersEachConnectionsSocketFromOne()
    {
        List<nint> seen = [];
        SocketCallbackConnector connector = Create(new FakeConnector(new FakeConnection()), new EasyCallbackOptions
        {
            SocketOption = (socket, _) =>
            {
                seen.Add(socket.Handle);
                return SocketOptionResult.Ok;
            },
        });

        await connector.ConnectAsync(Target, CancellationToken.None);
        await connector.ConnectAsync(Target, CancellationToken.None);

        CollectionAssert.AreEqual(new nint[] { 1, 2 }, seen);
    }

    [TestMethod]
    public async Task ConnectAsync_InnerConnectFails_ReturnsItsResultAndCallsNoCallback()
    {
        int calls = 0;
        ConnectResult failed = ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h");
        SocketCallbackConnector connector = Create(new FakeConnector(failed), new EasyCallbackOptions { OpenSocket = (_, _) => new CurlSocket(++calls) });

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(failed, connect);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_Connected_KeepsEveryDetailOfTheInnerResult()
    {
        FakeConnection inner = new();
        IPEndPoint local = new(IPAddress.Loopback, 5000);
        ConnectResult original = ConnectResult.Connected(inner, null, local, 200, [new byte[] { 1 }], true, 7, "h2", "/s", "m", 81, 2, 30);
        SocketCallbackConnector connector = Create(new FakeConnector(original), new EasyCallbackOptions { CloseSocket = _ => 0 });

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(local, connect.LocalEndPoint);
        Assert.AreEqual(200, connect.ProxyConnectResponseCode);
        Assert.AreEqual(1, connect.PeerCertificates.Count);
        Assert.IsTrue(connect.IsReused);
        Assert.AreEqual(7, connect.ConnectionNumber);
        Assert.AreEqual("h2", connect.ApplicationProtocol);
        Assert.AreEqual("/s", connect.UnixSocketPath);
        Assert.AreEqual("m", connect.MappedHost);
        Assert.AreEqual(81, connect.MappedPort);
        Assert.AreEqual(2, connect.ConnectReplyHeadersStored);
        Assert.AreEqual(30, connect.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_AllCallbacksSet_CallsOpenWithADatagramPeerThenSockoptAndCloseOnceOnDispose()
    {
        List<string> calls = [];
        FakeMultiplexedConnection quic = new() { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 443) };
        ConnectTimings timings = new(1, null, 2, 2);
        CurlSocketAddress? seen = null;
        SocketCallbackConnector connector = Create(new FakeConnector(MultiplexedConnectResult.Connected(quic, timings)), new EasyCallbackOptions
        {
            OpenSocket = (purpose, address) =>
            {
                seen = address;
                calls.Add($"open {purpose}");
                return new CurlSocket(9);
            },
            SocketOption = (socket, _) =>
            {
                calls.Add($"sockopt {socket.Handle}");
                return SocketOptionResult.Ok;
            },
            CloseSocket = socket =>
            {
                calls.Add($"close {socket.Handle}");
                return 0;
            },
        });

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);
        SocketCallbackMultiplexedConnection connection = (SocketCallbackMultiplexedConnection)connect.Connection!;
        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.AreSame(timings, connect.Timings);
        Assert.AreEqual(new CurlSocket(9), connection.Socket);
        Assert.IsTrue(quic.Disposed);
        CollectionAssert.AreEqual(new[] { "open IpConnection", "sockopt 9", "close 9" }, calls);
        Assert.AreEqual(2, seen!.Family);
        Assert.AreEqual(SocketCallbackConnector.DatagramSocketType, seen.SocketType);
        Assert.AreEqual(SocketCallbackConnector.UdpProtocol, seen.Protocol);
        Assert.AreEqual(16, seen.Address.Length);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_OpenSocketReturnsBad_DisposesTheConnectionAndFailsWithCouldntConnect()
    {
        int others = 0;
        FakeMultiplexedConnection quic = new();
        SocketCallbackConnector connector = Create(new FakeConnector(MultiplexedConnectResult.Connected(quic, null)), new EasyCallbackOptions
        {
            OpenSocket = (_, _) => CurlSocket.Bad,
            SocketOption = (_, _) => (SocketOptionResult)(++others),
            CloseSocket = _ => ++others,
        });

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, connect.ExitCode);
        Assert.AreEqual("Failed to connect to h:80 after 0 ms: Could not connect to server", connect.ErrorMessage);
        Assert.IsTrue(quic.Disposed);
        Assert.AreEqual(0, others);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_SockoptReturnsError_DisposesTheConnectionCallsCloseAndFailsWithAbortedByCallback()
    {
        List<CurlSocket> closed = [];
        FakeMultiplexedConnection quic = new();
        SocketCallbackConnector connector = Create(new FakeConnector(MultiplexedConnectResult.Connected(quic, null)), new EasyCallbackOptions
        {
            SocketOption = (_, _) => SocketOptionResult.Error,
            CloseSocket = socket =>
            {
                closed.Add(socket);
                return 0;
            },
        });

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.AbortedByCallback, connect.ExitCode);
        Assert.AreEqual("Failed to connect to h:80 after 0 ms: Operation was aborted by an application callback", connect.ErrorMessage);
        Assert.IsTrue(quic.Disposed);
        CollectionAssert.AreEqual(new[] { new CurlSocket(1) }, closed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_InnerConnectFails_ReturnsItsResultAndCallsNoCallback()
    {
        int calls = 0;
        MultiplexedConnectResult failed = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "QUIC failed");
        SocketCallbackConnector connector = Create(new FakeConnector(failed), new EasyCallbackOptions { OpenSocket = (_, _) => new CurlSocket(++calls) });

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreSame(failed, connect);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_Accepted_BuildsTheSessionOverTheWrappedConnection()
    {
        List<string> calls = [];
        FakeMultiplexedConnection quic = new() { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 5000) };
        IMultiplexedConnection? given = null;
        FakeConnection session = new();
        SocketCallbackConnector connector = Create(new FakeConnector(MultiplexedConnectResult.Connected(quic, null)), new EasyCallbackOptions
        {
            SocketOption = (socket, _) =>
            {
                calls.Add($"sockopt {socket.Handle}");
                return SocketOptionResult.Ok;
            },
        });

        ConnectResult connect = await connector.ConnectMultiplexedSessionAsync(
            Target,
            connection =>
            {
                given = connection;
                return session;
            },
            CancellationToken.None);

        Assert.AreSame(session, connect.Connection);
        Assert.AreEqual("h3", connect.ApplicationProtocol);
        Assert.AreSame(quic.LocalEndPoint, connect.LocalEndPoint);
        Assert.AreEqual(new CurlSocket(1), ((SocketCallbackMultiplexedConnection)given!).Socket);
        CollectionAssert.AreEqual(new[] { "sockopt 1" }, calls);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_OpenSocketReturnsBad_FailsWithCouldntConnectAndBuildsNoSession()
    {
        int sessions = 0;
        SocketCallbackConnector connector = Create(new FakeConnector(MultiplexedConnectResult.Connected(new FakeMultiplexedConnection(), null)), new EasyCallbackOptions { OpenSocket = (_, _) => CurlSocket.Bad });

        ConnectResult connect = await connector.ConnectMultiplexedSessionAsync(
            Target,
            _ =>
            {
                sessions++;
                return new FakeConnection();
            },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, connect.ExitCode);
        Assert.AreEqual("Failed to connect to h:80 after 0 ms: Could not connect to server", connect.ErrorMessage);
        Assert.AreEqual(0, sessions);
    }

    [TestMethod]
    public async Task MultiplexedConnection_EveryMember_AnswersAsTheInnerConnection()
    {
        FakeMultiplexedConnection inner = new() { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 1), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 2) };
        SocketCallbackMultiplexedConnection connection = new(inner, new CurlSocketCloser(new CurlSocket(3), null));

        IMultiplexedStream bidirectional = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
        IMultiplexedStream unidirectional = await connection.OpenUnidirectionalStreamAsync(CancellationToken.None);
        IMultiplexedStream accepted = await connection.AcceptUnidirectionalStreamAsync(CancellationToken.None);
        await connection.CloseAsync(0x100, CancellationToken.None);
        await connection.DisposeAsync();

        Assert.AreSame(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(inner.LocalEndPoint, connection.LocalEndPoint);
        Assert.AreEqual("h3", connection.ApplicationProtocol);
        Assert.AreEqual(7L, connection.BidirectionalStreamLimit);
        Assert.AreEqual(TimeSpan.FromSeconds(30), connection.PeerIdleTimeout);
        Assert.AreSame(inner.Bidirectional, bidirectional);
        Assert.AreSame(inner.Unidirectional, unidirectional);
        Assert.AreSame(inner.Accepted, accepted);
        Assert.AreEqual(0x100L, inner.CloseCode);
        Assert.IsTrue(inner.Disposed);
    }

    [TestMethod]
    public void Over_NoSocketCallbackSet_GivesTheConnectorItself()
    {
        FakeConnector inner = new(new FakeConnection());
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);

        Assert.AreSame(inner, SocketCallbackConnector.Over(inner, new EasyCallbackOptions(), shared));
        Assert.IsInstanceOfType<SocketCallbackConnector>(SocketCallbackConnector.Over(inner, new EasyCallbackOptions { OpenSocket = (_, _) => CurlSocket.Bad }, shared));
        Assert.IsInstanceOfType<SocketCallbackConnector>(SocketCallbackConnector.Over(inner, new EasyCallbackOptions { SocketOption = (_, _) => SocketOptionResult.Ok }, shared));
    }

    [TestMethod]
    public void AddressOf_EachKindOfPeer_GivesFamilyTypeProtocolAndBytes()
    {
        CurlSocketAddress none = SocketCallbackConnector.AddressOf(null);
        CurlSocketAddress v6 = SocketCallbackConnector.AddressOf(new IPEndPoint(IPAddress.IPv6Loopback, 443));
        CurlSocketAddress unix = SocketCallbackConnector.AddressOf(new UnixDomainSocketEndPoint("/tmp/s"));

        Assert.AreEqual(0, none.Family);
        Assert.AreEqual(0, none.Address.Length);
        Assert.AreEqual(SocketCallbackConnector.FamilyNumberOf(AddressFamily.InterNetworkV6, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()), v6.Family);
        Assert.AreEqual(SocketCallbackConnector.TcpProtocol, v6.Protocol);
        Assert.AreEqual(28, v6.Address.Length);
        Assert.AreEqual(1, unix.Family);
        Assert.AreEqual(0, unix.Protocol);
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, true, false, 2)]
    [DataRow(AddressFamily.Unix, false, false, 1)]
    [DataRow(AddressFamily.InterNetworkV6, true, false, 23)]
    [DataRow(AddressFamily.InterNetworkV6, false, true, 30)]
    [DataRow(AddressFamily.InterNetworkV6, false, false, 10)]
    public void FamilyNumberOf_EachPlatform_GivesItsNumber(AddressFamily family, bool isWindows, bool isMacOs, int expected) =>
        Assert.AreEqual(expected, SocketCallbackConnector.FamilyNumberOf(family, isWindows, isMacOs));

    [TestMethod]
    public async Task Connection_EveryMember_AnswersAsTheInnerConnection()
    {
        FakeConnection inner = new() { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 1), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 2) };
        SocketCallbackConnection connection = new(inner, new CurlSocketCloser(new CurlSocket(3), null));
        byte[] buffer = new byte[4];

        Assert.AreEqual(3, await connection.ReadAsync(buffer, CancellationToken.None));
        await connection.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        connection.MarkReusable();
        await connection.DisposeAsync();

        Assert.IsTrue(connection.IsSecure);
        Assert.AreSame(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(inner.LocalEndPoint, connection.LocalEndPoint);
        Assert.IsNull(connection.Session);
        Assert.IsFalse(connection.TryHoldSession(null!));
        Assert.IsTrue(connection.IsSharedWithAnotherTransfer);
        Assert.IsTrue(connection.HasPeerClosed);
        Assert.AreEqual(1, inner.Writes);
        Assert.AreEqual(1, inner.Flushes);
        Assert.IsTrue(inner.MarkedReusable);
        Assert.IsTrue(inner.Disposed);
    }

    [TestMethod]
    public async Task ClearTlsAsync_InnerGivesAPlainConnection_WrapsItSoTheSocketClosesOnce()
    {
        int closes = 0;
        FakeConnection plain = new();
        FakeConnection inner = new() { Plain = plain };
        SocketCallbackConnection connection = new(inner, new CurlSocketCloser(new CurlSocket(4), _ => ++closes));

        IConnection? cleared = await connection.ClearTlsAsync(sendCloseNotifyFirst: true, CancellationToken.None);
        await cleared!.DisposeAsync();
        await connection.DisposeAsync();

        Assert.AreEqual(new CurlSocket(4), ((SocketCallbackConnection)cleared).Socket);
        Assert.IsTrue(plain.Disposed);
        Assert.AreEqual(1, closes);
    }

    [TestMethod]
    public async Task ClearTlsAsync_InnerGivesNone_GivesNone()
    {
        SocketCallbackConnection connection = new(new FakeConnection(), new CurlSocketCloser(new CurlSocket(4), null));

        Assert.IsNull(await connection.ClearTlsAsync(sendCloseNotifyFirst: false, CancellationToken.None));
    }

    private static SocketCallbackConnector Create(IConnector inner, EasyCallbackOptions callbacks) =>
        new(inner, callbacks, new CurlSocketHandles(), new FrozenTimeProvider());

    /// <summary>A clock that never moves, so a refusal reports 0 ms.</summary>
    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }

    /// <summary>Answers every connect with one result.</summary>
    private sealed class FakeConnector(ConnectResult result, MultiplexedConnectResult? quic = null) : IConnector
    {
        public FakeConnector(IConnection connection)
            : this(ConnectResult.Connected(connection))
        {
        }

        public FakeConnector(MultiplexedConnectResult quic)
            : this(ConnectResult.Failed(CurlExitCode.CouldntConnect, "TCP is not scripted"), quic)
        {
        }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(result);

        public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(quic!);
    }

    /// <summary>A connection that answers every member with something other than the interface's default.</summary>
    private sealed class FakeConnection : IConnection
    {
        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint { get; init; }

        public EndPoint? LocalEndPoint { get; init; }

        public bool IsSharedWithAnotherTransfer => true;

        public bool HasPeerClosed => true;

        public IConnection? Plain { get; init; }

        public bool Disposed { get; private set; }

        public int Writes { get; private set; }

        public int Flushes { get; private set; }

        public bool MarkedReusable { get; private set; }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(3);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Writes++;
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return ValueTask.CompletedTask;
        }

        public void MarkReusable() => MarkedReusable = true;

        public ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) => ValueTask.FromResult(Plain);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A QUIC connection that answers every member with something of its own and records its close.</summary>
    private sealed class FakeMultiplexedConnection : IMultiplexedConnection
    {
        public EndPoint? RemoteEndPoint { get; init; }

        public EndPoint? LocalEndPoint { get; init; }

        public string ApplicationProtocol => "h3";

        public long? BidirectionalStreamLimit => 7;

        public TimeSpan? PeerIdleTimeout => TimeSpan.FromSeconds(30);

        public ScriptedMultiplexedStream Bidirectional { get; } = new(0, []);

        public ScriptedMultiplexedStream Unidirectional { get; } = new(2, []);

        public ScriptedMultiplexedStream Accepted { get; } = new(3, []);

        public long? CloseCode { get; private set; }

        public bool Disposed { get; private set; }

        public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IMultiplexedStream>(Bidirectional);

        public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IMultiplexedStream>(Unidirectional);

        public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IMultiplexedStream>(Accepted);

        public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken)
        {
            CloseCode = applicationErrorCode;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
