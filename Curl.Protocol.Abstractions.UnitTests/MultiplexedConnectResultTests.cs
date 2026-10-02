using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="MultiplexedConnectResult" /> may be built, and the
/// invariant that a connection is present exactly when the exit code is
/// <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class MultiplexedConnectResultTests
{
    [TestMethod]
    public void Connected_WithNullConnection_ThrowsArgumentNullException()
    {
        IMultiplexedConnection? connection = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => MultiplexedConnectResult.Connected(connection!, null));

        Assert.AreEqual("connection", exception.ParamName);
    }

    [TestMethod]
    public void Connected_WithConnectionAndTimings_CarriesThemWithOkAndNoMessage()
    {
        var connection = new UnusedMultiplexedConnection();
        var timings = new ConnectTimings(100, 150, 300, 300);

        var result = MultiplexedConnectResult.Connected(connection, timings);

        Assert.AreSame(connection, result.Connection);
        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => MultiplexedConnectResult.Failed(CurlExitCode.Ok, "unused"));

        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithQuicConnectError_CarriesCodeAndMessageWithNoConnectionOrTimings()
    {
        var result = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "QUIC connection lacks 3 uni streams to run HTTP/3");

        Assert.IsNull(result.Connection);
        Assert.IsNull(result.Timings);
        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual("QUIC connection lacks 3 uni streams to run HTTP/3", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenNotOverridden_FailsWithCouldntConnect()
    {
        IConnector connector = new TcpOnlyConnector();

        var result = await connector.ConnectMultiplexedAsync(new ConnectTarget("example.com", 443, true), CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public void ToConnectResult_OfASuccess_CarriesTheSessionTimingsLocalEndPointAndProtocol()
    {
        var quic = new UnusedMultiplexedConnection { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 4433) };
        var timings = new ConnectTimings(1, null, 2, 2);
        var session = new SessionConnection();
        IMultiplexedConnection? opened = null;

        var result = MultiplexedConnectResult.Connected(quic, timings).ToConnectResult(connection =>
        {
            opened = connection;
            return session;
        });

        Assert.AreSame(quic, opened);
        Assert.AreSame(session, result.Connection);
        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4433), result.LocalEndPoint);
        Assert.AreEqual("h3", result.ApplicationProtocol);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public void ToConnectResult_OfAFailure_CarriesItsExitCodeAndMessageAndOpensNoSession()
    {
        var result = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "no").ToConnectResult(_ => throw new AssertFailedException("opened"));

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual("no", result.ErrorMessage);
    }

    [TestMethod]
    public void ToConnectResult_WithoutASessionBuilder_Throws()
    {
        var result = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "no");

        Assert.ThrowsExactly<ArgumentNullException>(() => result.ToConnectResult(null!));
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenNotOverridden_GivesTheQuicConnectAsAConnectResult()
    {
        IConnector connector = new TcpOnlyConnector();

        var result = await connector.ConnectMultiplexedSessionAsync(new ConnectTarget("example.com", 443, true), _ => new SessionConnection(), CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public void BidirectionalStreamLimit_WhenNotOverridden_IsUnknown()
    {
        IMultiplexedConnection quic = new UnusedMultiplexedConnection();

        Assert.IsNull(quic.BidirectionalStreamLimit);
    }

    private sealed class SessionConnection : IConnection
    {
        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask FlushAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TcpOnlyConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedMultiplexedConnection : IMultiplexedConnection
    {
        public EndPoint? RemoteEndPoint => null;

        public EndPoint? LocalEndPoint { get; init; }

        public string ApplicationProtocol => "h3";

        public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
