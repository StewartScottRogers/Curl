using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="IConnection" />, which let an implementation
/// leave them out.
/// </summary>
[TestClass]
public sealed class IConnectionTests
{
    [TestMethod]
    public void LocalEndPoint_WhenNotOverridden_ReturnsNull()
    {
        IConnection connection = new MinimalConnection();

        var localEndPoint = connection.LocalEndPoint;

        Assert.IsNull(localEndPoint);
    }

    [TestMethod]
    public void MarkReusable_WhenNotOverridden_DoesNothing()
    {
        IConnection connection = new MinimalConnection();

        connection.MarkReusable();

        Assert.IsFalse(connection.IsSecure);
    }

    [TestMethod]
    public void Session_WhenNotOverridden_ReturnsNull()
    {
        IConnection connection = new MinimalConnection();

        var session = connection.Session;

        Assert.IsNull(session);
    }

    [TestMethod]
    public void TryHoldSession_WhenNotOverridden_ReturnsFalseAndHoldsNothing()
    {
        IConnection connection = new MinimalConnection();

        var isHeld = connection.TryHoldSession(new UnusedSession());

        Assert.IsFalse(isHeld);
        Assert.IsNull(connection.Session);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ClearTlsAsync_WhenNotOverridden_ReturnsNull(bool sendCloseNotifyFirst)
    {
        IConnection connection = new MinimalConnection();

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst, CancellationToken.None);

        Assert.IsNull(plaintext);
    }

    [TestMethod]
    public void IsSharedWithAnotherTransfer_WhenNotOverridden_ReturnsFalse()
    {
        IConnection connection = new MinimalConnection();

        var isShared = connection.IsSharedWithAnotherTransfer;

        Assert.IsFalse(isShared);
    }

    [TestMethod]
    public void ConcurrentTransferLimit_WhenNotOverridden_ReturnsNull()
    {
        IConnectionSession session = new UnusedSession();

        var limit = session.ConcurrentTransferLimit;

        Assert.IsNull(limit);
    }

    private sealed class UnusedSession : IConnectionSession
    {
        public ValueTask ShutDownAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MinimalConnection : IConnection
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
