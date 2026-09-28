using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="ConnectResult" /> may be built, and the invariant that
/// a connection is present exactly when the exit code is <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class ConnectResultTests
{
    [TestMethod]
    public void Connected_WithNullConnection_ThrowsArgumentNullException()
    {
        IConnection? connection = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ConnectResult.Connected(connection!));

        Assert.AreEqual("connection", exception.ParamName);
    }

    [TestMethod]
    public void Connected_WithConnection_ExposesItWithOkAndNoMessage()
    {
        var connection = new UnusedConnection();

        var result = ConnectResult.Connected(connection);

        Assert.AreSame(connection, result.Connection);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectResult.Failed(CurlExitCode.Ok, "unused"));

        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithCouldntResolveHost_ExposesCodeAndMessageWithNoConnection()
    {
        var result = ConnectResult.Failed(
            CurlExitCode.CouldntResolveHost,
            "Could not resolve host: nonexistent.invalid");

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithCouldntConnect_ExposesCodeAndMessageWithNoConnection()
    {
        var result = ConnectResult.Failed(
            CurlExitCode.CouldntConnect,
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server");

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server",
            result.ErrorMessage);
    }

    [TestMethod]
    public void Connected_WithConnectionOnly_LeavesTimingsEndPointAndConnectCodeAtTheirDefaults()
    {
        var result = ConnectResult.Connected(new UnusedConnection());

        Assert.IsNull(result.Timings);
        Assert.IsNull(result.LocalEndPoint);
        Assert.AreEqual(0, result.ProxyConnectResponseCode);
    }

    [TestMethod]
    public void Connected_WithTimingsOnly_CarriesThemAndDefaultsTheRest()
    {
        var connection = new UnusedConnection();
        var timings = new ConnectTimings(100, 150, 200, null);

        var result = ConnectResult.Connected(connection, timings);

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
        var timings = new ConnectTimings(100, null, 200, 300);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);

        var result = ConnectResult.Connected(new UnusedConnection(), timings, localEndPoint, 200);

        Assert.AreSame(timings, result.Timings);
        Assert.AreSame(localEndPoint, result.LocalEndPoint);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
    }

    [TestMethod]
    public void Connected_WithPeerCertificates_CarriesThemInOrder()
    {
        ReadOnlyMemory<byte>[] certificates = [new byte[] { 0x30, 0x01 }, new byte[] { 0x30, 0x02 }];

        var result = ConnectResult.Connected(new UnusedConnection(), null, null, 0, certificates);

        Assert.AreSame(certificates, result.PeerCertificates);
    }

    [TestMethod]
    public void Connected_WithoutReuseArguments_IsNotReusedAndNumberedZero()
    {
        var shortResult = ConnectResult.Connected(new UnusedConnection());
        var longResult = ConnectResult.Connected(new UnusedConnection(), null, null, 0, null);

        Assert.IsFalse(shortResult.IsReused);
        Assert.AreEqual(0L, shortResult.ConnectionNumber);
        Assert.IsFalse(longResult.IsReused);
        Assert.AreEqual(0L, longResult.ConnectionNumber);
    }

    [TestMethod]
    public void Connected_WithReuseArguments_CarriesThemAsGiven()
    {
        var result = ConnectResult.Connected(
            new UnusedConnection(),
            null,
            isReused: true,
            connectionNumber: 7);

        Assert.IsTrue(result.IsReused);
        Assert.AreEqual(7L, result.ConnectionNumber);
    }

    [TestMethod]
    public void MarkReusable_OnConnectionThatDoesNotOverrideIt_DoesNotThrow()
    {
        IConnection connection = new UnusedConnection();

        connection.MarkReusable();

        Assert.IsFalse(connection.IsSecure);
    }

    [TestMethod]
    public void Connected_WithTimingsAndNullConnection_ThrowsArgumentNullException()
    {
        IConnection? connection = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ConnectResult.Connected(connection!, new ConnectTimings(0, null, 0, null)));

        Assert.AreEqual("connection", exception.ParamName);
    }

    [TestMethod]
    public void Failed_Always_LeavesTimingsEndPointAndConnectCodeAtTheirDefaults()
    {
        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed");

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
        Assert.IsFalse(ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed").IsConnectionRefused);
        Assert.IsFalse(ConnectResult.Connected(new UnusedConnection()).IsConnectionRefused);
    }

    [TestMethod]
    public void Refused_Always_IsCouldntConnectMarkedRefusedWithNoConnection()
    {
        var result = ConnectResult.Refused("Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Failed_WithoutTimings_CarriesNoTimings()
    {
        Assert.IsNull(ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed").Timings);
        Assert.IsNull(ConnectResult.Refused("refused").Timings);
    }

    [TestMethod]
    public void Failed_WithTimings_CarriesThemWithNoConnection()
    {
        var timings = new ConnectTimings(10, 20, null, null);

        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "failed", timings);

        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsNull(result.Connection);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public void Failed_WithTimingsAndOk_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectResult.Failed(CurlExitCode.Ok, "unused", new ConnectTimings(10, 20, null, null)));

        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Refused_WithTimings_CarriesThemMarkedRefused()
    {
        var timings = new ConnectTimings(10, 20, null, null);

        var result = ConnectResult.Refused("refused", timings);

        Assert.AreSame(timings, result.Timings);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Failed_WithAConnectionNumber_CarriesIt()
    {
        var result = ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: x", null, connectionNumber: 3);

        Assert.AreEqual(3L, result.ConnectionNumber);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public void Refused_WithAConnectionNumber_CarriesItMarkedRefused()
    {
        var result = ConnectResult.Refused("refused", null, connectionNumber: 1);

        Assert.AreEqual(1L, result.ConnectionNumber);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public void Refused_WithoutAConnectionNumber_NumbersItZero()
    {
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
