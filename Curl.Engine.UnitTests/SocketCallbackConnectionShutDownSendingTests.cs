using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins BL-5110: <see cref="SocketCallbackConnection.ShutDownSendingAsync" /> reaches the
/// connection it wraps.
/// </summary>
[TestClass]
public sealed class SocketCallbackConnectionShutDownSendingTests
{
    [TestMethod]
    public async Task ShutDownSendingAsync_OnTheWrapper_ShutsTheInnerConnectionDown()
    {
        var inner = new ShutDownCountingConnection();
        SocketCallbackConnection connection = new(inner, new CurlSocketCloser(new CurlSocket(3), null));

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
