using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="ConnectResult" /> may be built, and the invariant that
/// a connection is present exactly when the exit code is <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class ConnectResultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Connected_WithNullConnection_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection? connection = null;
        diagnostics.Arrange("connection", connection);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ConnectResult.Connected(connection!));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "connection", exception.ParamName);
        Assert.AreEqual("connection", exception.ParamName);
    }

    [TestMethod]
    public void Connected_WithConnection_ExposesItWithOkAndNoMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var connection = new UnusedConnection();
        diagnostics.Arrange("connection", connection.GetType().Name);

        var result = ConnectResult.Connected(connection);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(connection, result.Connection);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectResult.Failed(CurlExitCode.Ok, "unused"));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "exitCode", exception.ParamName);
        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithCouldntResolveHost_ExposesCodeAndMessageWithNoConnection()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntResolveHost);

        var result = ConnectResult.Failed(
            CurlExitCode.CouldntResolveHost,
            "Could not resolve host: nonexistent.invalid");

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Diff("error message", "Could not resolve host: nonexistent.invalid", result.ErrorMessage!);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithCouldntConnect_ExposesCodeAndMessageWithNoConnection()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var result = ConnectResult.Failed(
            CurlExitCode.CouldntConnect,
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server");

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Diff(
            "error message",
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server",
            result.ErrorMessage!);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server",
            result.ErrorMessage);
    }

    [TestMethod]
    public void Connected_WithConnectionOnly_LeavesTimingsEndPointAndConnectCodeAtTheirDefaults()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "connection only");

        var result = ConnectResult.Connected(new UnusedConnection());

        diagnostics.Act("timings", result.Timings);
        diagnostics.Act("local end point", result.LocalEndPoint);
        diagnostics.Act("proxy connect response code", result.ProxyConnectResponseCode);
        diagnostics.Assert("proxy connect response code", 0, result.ProxyConnectResponseCode);
        Assert.IsNull(result.Timings);
        Assert.IsNull(result.LocalEndPoint);
        Assert.AreEqual(0, result.ProxyConnectResponseCode);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "default")]
    [DataRow(3, DisplayName = "3 headers")]
    public void Connected_WithAConnectReplyHeaderCount_CarriesItAndDefaultsToZero(int count)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connect reply headers stored", count);

        var result = count == 0
            ? ConnectResult.Connected(new UnusedConnection())
            : ConnectResult.Connected(new UnusedConnection(), null, connectReplyHeadersStored: count);

        diagnostics.Act("connect reply headers stored", result.ConnectReplyHeadersStored);
        diagnostics.Assert("connect reply headers stored", count, result.ConnectReplyHeadersStored);
        Assert.AreEqual(count, result.ConnectReplyHeadersStored);
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "default")]
    [DataRow(61L, DisplayName = "61 bytes")]
    public void Connected_WithProxyConnectHeaderBytes_CarriesThemAndDefaultsToZero(long bytes)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy connect header bytes", bytes);

        var result = bytes == 0
            ? ConnectResult.Connected(new UnusedConnection())
            : ConnectResult.Connected(new UnusedConnection(), null, proxyConnectHeaderBytes: bytes);

        diagnostics.Assert("proxy connect header bytes", bytes, result.ProxyConnectHeaderBytes);
        Assert.AreEqual(bytes, result.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public void Connected_WithTimingsOnly_CarriesThemAndDefaultsTheRest()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var connection = new UnusedConnection();
        var timings = new ConnectTimings(100, 150, 200, null);
        diagnostics.Arrange("timings", timings);

        var result = ConnectResult.Connected(connection, timings);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("timings", result.Timings);
        diagnostics.Act("peer certificate count", result.PeerCertificates.Count);
        diagnostics.Assert("proxy connect response code", 0, result.ProxyConnectResponseCode);
        Assert.AreSame(connection, result.Connection);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreSame(timings, result.Timings);
        Assert.IsNull(result.LocalEndPoint);
        Assert.AreEqual(0, result.ProxyConnectResponseCode);
        Assert.IsEmpty(result.PeerCertificates);
    }

    [TestMethod]
    public void Connected_WithEveryMeasurement_CarriesThemAll()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var timings = new ConnectTimings(100, null, 200, 300);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
        diagnostics.Arrange("timings", timings);
        diagnostics.Arrange("local end point", localEndPoint);

        var result = ConnectResult.Connected(new UnusedConnection(), timings, localEndPoint, 200);

        diagnostics.Act("local end point", result.LocalEndPoint);
        diagnostics.Act("proxy connect response code", result.ProxyConnectResponseCode);
        diagnostics.Assert("proxy connect response code", 200, result.ProxyConnectResponseCode);
        Assert.AreSame(timings, result.Timings);
        Assert.AreSame(localEndPoint, result.LocalEndPoint);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
    }

    [TestMethod]
    public void Connected_WithPeerCertificates_CarriesThemInOrder()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ReadOnlyMemory<byte>[] certificates = [new byte[] { 0x30, 0x01 }, new byte[] { 0x30, 0x02 }];
        diagnostics.Arrange("certificate count", certificates.Length);

        var result = ConnectResult.Connected(new UnusedConnection(), null, null, 0, certificates);

        diagnostics.Act("peer certificate count", result.PeerCertificates.Count);
        diagnostics.Assert("peer certificate count", certificates.Length, result.PeerCertificates.Count);
        Assert.AreSame(certificates, result.PeerCertificates);
    }

    [TestMethod]
    public void Connected_WithoutReuseArguments_IsNotReusedAndNumberedZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reuse arguments", "none");

        var shortResult = ConnectResult.Connected(new UnusedConnection());
        var longResult = ConnectResult.Connected(new UnusedConnection(), null, null, 0, null);

        diagnostics.Act("short overload is reused", shortResult.IsReused);
        diagnostics.Act("short overload connection number", shortResult.ConnectionNumber);
        diagnostics.Act("long overload is reused", longResult.IsReused);
        diagnostics.Act("long overload connection number", longResult.ConnectionNumber);
        diagnostics.Assert("connection number", 0L, longResult.ConnectionNumber);
        Assert.IsFalse(shortResult.IsReused);
        Assert.AreEqual(0L, shortResult.ConnectionNumber);
        Assert.IsFalse(longResult.IsReused);
        Assert.AreEqual(0L, longResult.ConnectionNumber);
    }

    [TestMethod]
    public void Connected_WithReuseArguments_CarriesThemAsGiven()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("is reused", true);
        diagnostics.Arrange("connection number", 7L);

        var result = ConnectResult.Connected(
            new UnusedConnection(),
            null,
            isReused: true,
            connectionNumber: 7);

        diagnostics.Act("is reused", result.IsReused);
        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Assert("connection number", 7L, result.ConnectionNumber);
        Assert.IsTrue(result.IsReused);
        Assert.AreEqual(7L, result.ConnectionNumber);
    }

    [TestMethod]
    public void Connected_WithoutApplicationProtocol_AgreedNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("application protocol", null);

        var result = ConnectResult.Connected(new UnusedConnection());

        diagnostics.Act("application protocol", result.ApplicationProtocol);
        diagnostics.Assert("application protocol", null, result.ApplicationProtocol);
        Assert.IsNull(result.ApplicationProtocol);
    }

    [TestMethod]
    public void Connected_WithApplicationProtocol_CarriesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("application protocol", "h2");

        var result = ConnectResult.Connected(new UnusedConnection(), null, applicationProtocol: "h2");

        diagnostics.Act("application protocol", result.ApplicationProtocol);
        diagnostics.Assert("application protocol", "h2", result.ApplicationProtocol);
        Assert.AreEqual("h2", result.ApplicationProtocol);
    }

    [TestMethod]
    public void Connected_WithoutUnixSocketPath_WentOverTcp()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("unix socket path", null);

        var result = ConnectResult.Connected(new UnusedConnection());

        diagnostics.Act("unix socket path", result.UnixSocketPath);
        diagnostics.Assert("unix socket path", null, result.UnixSocketPath);
        Assert.IsNull(result.UnixSocketPath);
    }

    [TestMethod]
    public void Connected_WithUnixSocketPath_CarriesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("unix socket path", "/run/S.sock");

        var result = ConnectResult.Connected(new UnusedConnection(), null, unixSocketPath: "/run/S.sock");

        diagnostics.Act("unix socket path", result.UnixSocketPath);
        diagnostics.Assert("unix socket path", "/run/S.sock", result.UnixSocketPath);
        Assert.AreEqual("/run/S.sock", result.UnixSocketPath);
    }

    [TestMethod]
    public void Connected_WithoutMappedDestination_NamesNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mapped destination", "none");

        var result = ConnectResult.Connected(new UnusedConnection());

        diagnostics.Act("mapped host", result.MappedHost);
        diagnostics.Act("mapped port", result.MappedPort);
        diagnostics.Assert("mapped port", 0, result.MappedPort);
        Assert.IsNull(result.MappedHost);
        Assert.AreEqual(0, result.MappedPort);
    }

    [TestMethod]
    public void Connected_WithMappedDestination_CarriesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mapped host", "127.0.0.1");
        diagnostics.Arrange("mapped port", 18499);

        var result = ConnectResult.Connected(new UnusedConnection(), null, mappedHost: "127.0.0.1", mappedPort: 18499);

        diagnostics.Act("mapped host", result.MappedHost);
        diagnostics.Act("mapped port", result.MappedPort);
        diagnostics.Assert("mapped port", 18499, result.MappedPort);
        Assert.AreEqual("127.0.0.1", result.MappedHost);
        Assert.AreEqual(18499, result.MappedPort);
    }

    [TestMethod]
    public void Failed_Always_NamesNoMappedDestination()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused");

        diagnostics.Act("mapped host", result.MappedHost);
        diagnostics.Act("mapped port", result.MappedPort);
        diagnostics.Assert("mapped port", 0, result.MappedPort);
        Assert.IsNull(result.MappedHost);
        Assert.AreEqual(0, result.MappedPort);
    }

    [TestMethod]
    public void Failed_Always_NamesNoUnixSocketPath()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused");

        diagnostics.Act("unix socket path", result.UnixSocketPath);
        diagnostics.Assert("unix socket path", null, result.UnixSocketPath);
        Assert.IsNull(result.UnixSocketPath);
    }

    [TestMethod]
    public void Failed_Always_AgreedNoApplicationProtocol()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused");

        diagnostics.Act("application protocol", result.ApplicationProtocol);
        diagnostics.Assert("application protocol", null, result.ApplicationProtocol);
        Assert.IsNull(result.ApplicationProtocol);
    }

    [TestMethod]
    public void MarkReusable_OnConnectionThatDoesNotOverrideIt_DoesNotThrow()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new UnusedConnection();
        diagnostics.Arrange("connection", connection.GetType().Name);

        connection.MarkReusable();

        diagnostics.Act("is secure", connection.IsSecure);
        diagnostics.Assert("is secure", false, connection.IsSecure);
        Assert.IsFalse(connection.IsSecure);
    }

    [TestMethod]
    public void Connected_WithTimingsAndNullConnection_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection? connection = null;
        diagnostics.Arrange("connection", connection);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ConnectResult.Connected(connection!, new ConnectTimings(0, null, 0, null)));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "connection", exception.ParamName);
        Assert.AreEqual("connection", exception.ParamName);
    }

    [TestMethod]
    public void Failed_Always_LeavesTimingsEndPointAndConnectCodeAtTheirDefaults()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed");

        diagnostics.Act("peer certificate count", result.PeerCertificates.Count);
        diagnostics.Act("timings", result.Timings);
        diagnostics.Act("is reused", result.IsReused);
        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Assert("connection number", 0L, result.ConnectionNumber);
        Assert.IsEmpty(result.PeerCertificates);
        Assert.IsNull(result.Timings);
        Assert.IsNull(result.LocalEndPoint);
        Assert.AreEqual(0, result.ProxyConnectResponseCode);
        Assert.IsFalse(result.IsReused);
        Assert.AreEqual(0L, result.ConnectionNumber);
    }

    [TestMethod]
    public void Failed_Always_IsNotConnectionRefused()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var failed = ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed");
        var connected = ConnectResult.Connected(new UnusedConnection());

        diagnostics.Act("failed is connection refused", failed.IsConnectionRefused);
        diagnostics.Act("connected is connection refused", connected.IsConnectionRefused);
        diagnostics.Assert("failed is connection refused", false, failed.IsConnectionRefused);
        Assert.IsFalse(ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed").IsConnectionRefused);
        Assert.IsFalse(ConnectResult.Connected(new UnusedConnection()).IsConnectionRefused);
    }

    [TestMethod]
    public void Refused_Always_IsCouldntConnectMarkedRefusedWithNoConnection()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server");

        var result = ConnectResult.Refused("Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server");

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("is connection refused", result.IsConnectionRefused);
        diagnostics.Diff(
            "error message",
            "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server",
            result.ErrorMessage!);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Failed_WithoutTimings_CarriesNoTimings()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timings", null);

        var failed = ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed");
        var refused = ConnectResult.Refused("refused");

        diagnostics.Act("failed timings", failed.Timings);
        diagnostics.Act("refused timings", refused.Timings);
        diagnostics.Assert("failed timings", null, failed.Timings);
        Assert.IsNull(ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed").Timings);
        Assert.IsNull(ConnectResult.Refused("refused").Timings);
    }

    [TestMethod]
    public void Failed_WithTimings_CarriesThemWithNoConnection()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var timings = new ConnectTimings(10, 20, null, null);
        diagnostics.Arrange("timings", timings);

        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed", timings);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("is connection refused", result.IsConnectionRefused);
        diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsNull(result.Connection);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public void Failed_WithTimingsAndOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectResult.Failed(CurlExitCode.Ok, "unused", new ConnectTimings(10, 20, null, null)));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "exitCode", exception.ParamName);
        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Refused_WithTimings_CarriesThemMarkedRefused()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var timings = new ConnectTimings(10, 20, null, null);
        diagnostics.Arrange("timings", timings);

        var result = ConnectResult.Refused("refused", timings);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("is connection refused", result.IsConnectionRefused);
        diagnostics.Assert("is connection refused", true, result.IsConnectionRefused);
        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Failed_WithAConnectionNumber_CarriesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connection number", 3L);

        var result = ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: x", null, connectionNumber: 3);

        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Assert("connection number", 3L, result.ConnectionNumber);
        Assert.AreEqual(3L, result.ConnectionNumber);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Refused_WithAConnectionNumber_CarriesItMarkedRefused()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connection number", 1L);

        var result = ConnectResult.Refused("refused", null, connectionNumber: 1);

        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Act("is connection refused", result.IsConnectionRefused);
        diagnostics.Assert("connection number", 1L, result.ConnectionNumber);
        Assert.AreEqual(1L, result.ConnectionNumber);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public void Refused_WithoutAConnectionNumber_NumbersItZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connection number", null);

        long connectionNumber = ConnectResult.Refused("refused", new ConnectTimings(10, 20, null, null)).ConnectionNumber;

        diagnostics.Act("connection number", connectionNumber);
        diagnostics.Assert("connection number", 0L, connectionNumber);
        Assert.AreEqual(0L, ConnectResult.Refused("refused", new ConnectTimings(10, 20, null, null)).ConnectionNumber);
    }

    private sealed class UnusedConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask FlushAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
