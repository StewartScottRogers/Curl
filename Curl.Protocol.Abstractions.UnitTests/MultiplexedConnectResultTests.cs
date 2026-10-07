using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="MultiplexedConnectResult" /> may be built, and the
/// invariant that a connection is present exactly when the exit code is
/// <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class MultiplexedConnectResultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Connected_WithNullConnection_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IMultiplexedConnection? connection = null;
        diagnostics.Arrange("connection", connection);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => MultiplexedConnectResult.Connected(connection!, null));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "connection", exception.ParamName);
        Assert.AreEqual("connection", exception.ParamName);
    }

    [TestMethod]
    public void Connected_WithConnectionAndTimings_CarriesThemWithOkAndNoMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var connection = new UnusedMultiplexedConnection();
        var timings = new ConnectTimings(100, 150, 300, 300);
        diagnostics.Arrange("timings", timings);

        var result = MultiplexedConnectResult.Connected(connection, timings);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(connection, result.Connection);
        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => MultiplexedConnectResult.Failed(CurlExitCode.Ok, "unused"));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "exitCode", exception.ParamName);
        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithQuicConnectError_CarriesCodeAndMessageWithNoConnectionOrTimings()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.QuicConnectError);

        var result = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "QUIC connection lacks 3 uni streams to run HTTP/3");

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Diff("error message", "QUIC connection lacks 3 uni streams to run HTTP/3", result.ErrorMessage!);
        Assert.IsNull(result.Connection);
        Assert.IsNull(result.Timings);
        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual("QUIC connection lacks 3 uni streams to run HTTP/3", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenNotOverridden_FailsWithCouldntConnect()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnector connector = new TcpOnlyConnector();
        diagnostics.Arrange("target", "example.com:443 tls");

        var result = await connector.ConnectMultiplexedAsync(new ConnectTarget("example.com", 443, true), CancellationToken.None);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Diff("error message", "QUIC is not available on this connector", result.ErrorMessage!);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public void ToConnectResult_OfASuccess_CarriesTheSessionTimingsLocalEndPointAndProtocol()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var quic = new UnusedMultiplexedConnection { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 4433) };
        var timings = new ConnectTimings(1, null, 2, 2);
        var session = new SessionConnection();
        IMultiplexedConnection? opened = null;
        diagnostics.Arrange("local end point", quic.LocalEndPoint);
        diagnostics.Arrange("timings", timings);

        var result = MultiplexedConnectResult.Connected(quic, timings).ToConnectResult(connection =>
        {
            opened = connection;
            return session;
        });

        diagnostics.Act("application protocol", result.ApplicationProtocol);
        diagnostics.Act("local end point", result.LocalEndPoint);
        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Assert("application protocol", "h3", result.ApplicationProtocol);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.QuicConnectError);

        var result = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "no").ToConnectResult(_ => throw new AssertFailedException("opened"));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual("no", result.ErrorMessage);
    }

    [TestMethod]
    public void ToConnectResult_WithoutASessionBuilder_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var result = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "no");
        diagnostics.Arrange("session builder", null);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => result.ToConnectResult(null!));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenNotOverridden_GivesTheQuicConnectAsAConnectResult()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnector connector = new TcpOnlyConnector();
        diagnostics.Arrange("target", "example.com:443 tls");

        var result = await connector.ConnectMultiplexedSessionAsync(new ConnectTarget("example.com", 443, true), _ => new SessionConnection(), CancellationToken.None);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Diff("error message", "QUIC is not available on this connector", result.ErrorMessage!);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("QUIC is not available on this connector", result.ErrorMessage);
    }

    [TestMethod]
    public void BidirectionalStreamLimit_WhenNotOverridden_IsUnknown()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IMultiplexedConnection quic = new UnusedMultiplexedConnection();
        diagnostics.Arrange("connection", quic.GetType().Name);

        diagnostics.Act("bidirectional stream limit", quic.BidirectionalStreamLimit);
        diagnostics.Assert("bidirectional stream limit", null, quic.BidirectionalStreamLimit);
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
