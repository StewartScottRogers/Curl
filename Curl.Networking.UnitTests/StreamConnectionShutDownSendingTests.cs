using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="StreamConnection.ShutDownSendingAsync" /> runs its send-side shutdown on
/// the stream it wraps, and that <see cref="TcpIoTraceConnection" /> passes the call through
/// (BL-3179, ADR-0770). The socket shutdown itself is a thin adapter measured by the Integration run.
/// </summary>
[TestClass]
public sealed class StreamConnectionShutDownSendingTests
{
    [TestMethod]
    public async Task ShutDownSendingAsync_RunsTheSendSideShutdownOnTheWrappedStream()
    {
        using var stream = new MemoryStream();
        Stream? shutDown = null;
        await using var connection = new StreamConnection(stream, remoteEndPoint: null) { ShutDownSendSide = s => shutDown = s };

        await connection.ShutDownSendingAsync(CancellationToken.None);

        Assert.AreSame(stream, shutDown);
    }

    [TestMethod]
    public async Task ShutDownSendingAsync_OverAStreamWithNoSocket_DoesNothing()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        await using var connection = new StreamConnection(stream, remoteEndPoint: null);

        await connection.ShutDownSendingAsync(CancellationToken.None);

        Assert.AreEqual(0, stream.Position);
    }

    [TestMethod]
    public async Task TcpIoTraceConnection_ShutDownSendingAsync_PassesThroughToTheInnerConnection()
    {
        var inner = new ShutDownCountingConnection();
        var connection = new TcpIoTraceConnection(inner, NoTransferEvents.Instance, TcpIoTraceConnection.HttpLines);

        await connection.ShutDownSendingAsync(CancellationToken.None);

        Assert.AreEqual(1, inner.ShutDowns);
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
