using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins BL-5110: <see cref="PooledConnection.ShutDownSendingAsync" /> reaches the connection the
/// pool opened, so a plain passive FTP upload sends its FIN at once.
/// </summary>
[TestClass]
public sealed class PooledConnectionShutDownSendingForwardingTests
{
    [TestMethod]
    public async Task ShutDownSendingAsync_OnAPooledConnection_ShutsTheWrappedConnectionDown()
    {
        var inner = new ShutDownCountingConnection();
        await using var pool = new PoolingConnector(new SingleConnector(inner), new ManualTimeProvider());
        var result = await pool.ConnectAsync(new ConnectTarget("origin.example", 21, false) { PoolScheme = "ftp" }, CancellationToken.None);

        await result.Connection!.ShutDownSendingAsync(CancellationToken.None);

        Assert.AreEqual(1, inner.ShutDowns);
    }

    private sealed class SingleConnector(IConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connection, new ConnectTimings(1, null, 1, null)));
    }

    private sealed class ShutDownCountingConnection : IConnection
    {
        public int ShutDowns { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
        {
            ShutDowns++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
