using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class PrerequestConnectorTests
{
    private static readonly ConnectTarget Target = new("h", 80, false);

    private static readonly IPEndPoint Remote = new(IPAddress.Parse("192.0.2.7"), 8080);

    private static readonly IPEndPoint Local = new(IPAddress.IPv6Loopback, 50123);

    [TestMethod]
    public void Over_NoCallback_GivesTheConnectorItself()
    {
        FixedConnector connector = new(ConnectResult.Connected(new EndPointConnection(Remote, Local)));

        Assert.AreSame(connector, PrerequestConnector.Over(connector, prerequest: null));
        Assert.IsInstanceOfType<PrerequestConnector>(PrerequestConnector.Over(connector, (_, _, _, _) => PrerequestResult.Ok));
    }

    [TestMethod]
    public async Task ConnectAsync_CallbackAnswersOk_GivesTheConnectionAndPassesThePeerAndLocalEndPoints()
    {
        List<string> calls = [];
        EndPointConnection connection = new(Remote, Local);
        ConnectResult inner = ConnectResult.Connected(connection);
        PrerequestConnector connector = new(new FixedConnector(inner), Recording(calls, PrerequestResult.Ok));

        ConnectResult result = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(inner, result);
        Assert.IsFalse(connection.Disposed);
        CollectionAssert.AreEqual(new[] { "192.0.2.7 ::1 8080 50123" }, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_CallbackAborts_ClosesTheConnectionAndFailsWithAbortedByCallback()
    {
        EndPointConnection connection = new(Remote, Local);
        PrerequestConnector connector = new(new FixedConnector(ConnectResult.Connected(connection)), (_, _, _, _) => PrerequestResult.Abort);

        ConnectResult result = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual("operation aborted by pre-request callback", result.ErrorMessage);
        Assert.IsTrue(connection.Disposed);
    }

    [TestMethod]
    public async Task ConnectAsync_InnerConnectFails_GivesTheFailureWithoutCallingTheCallback()
    {
        List<string> calls = [];
        ConnectResult failed = ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused");
        PrerequestConnector connector = new(new FixedConnector(failed), Recording(calls, PrerequestResult.Ok));

        Assert.AreSame(failed, await connector.ConnectAsync(Target, CancellationToken.None));
        Assert.IsEmpty(calls);
    }

    [TestMethod]
    public async Task ConnectAsync_EndPointsAreNotIp_PassesEmptyAddressesAndZeroPorts()
    {
        List<string> calls = [];
        PrerequestConnector connector = new(new FixedConnector(ConnectResult.Connected(new EndPointConnection(new DnsEndPoint("h", 80), null))), Recording(calls, PrerequestResult.Ok));

        await connector.ConnectAsync(Target, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "  0 0" }, calls);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_CallbackAborts_ClosesTheSessionAndFailsWithAbortedByCallback()
    {
        EndPointConnection connection = new(Remote, Local);
        PrerequestConnector connector = new(new FixedConnector(ConnectResult.Connected(connection)), (_, _, _, _) => PrerequestResult.Abort);

        ConnectResult result = await connector.ConnectMultiplexedSessionAsync(Target, _ => connection, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.AbortedByCallback, result.ExitCode);
        Assert.IsTrue(connection.Disposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_PassesThroughWithoutCallingTheCallback()
    {
        List<string> calls = [];
        PrerequestConnector connector = new(new FixedConnector(ConnectResult.Connected(new EndPointConnection(Remote, Local))), Recording(calls, PrerequestResult.Abort));

        MultiplexedConnectResult result = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsEmpty(calls);
    }

    private static PrerequestCallback Recording(List<string> calls, PrerequestResult answer) => (primaryIp, localIp, primaryPort, localPort) =>
    {
        calls.Add($"{primaryIp} {localIp} {primaryPort} {localPort}");
        return answer;
    };

    /// <summary>A connector that answers every connect with one result; QUIC keeps the interface's default refusal.</summary>
    private sealed class FixedConnector(ConnectResult result) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(result);

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    /// <summary>A connection that carries no bytes and reports the end points it was given.</summary>
    private sealed class EndPointConnection(EndPoint? remote, EndPoint? local) : IConnection
    {
        public bool Disposed { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => remote;

        public EndPoint? LocalEndPoint => local;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
