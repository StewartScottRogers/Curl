using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="IConnection" />, which let an implementation
/// leave them out.
/// </summary>
[TestClass]
public sealed class IConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LocalEndPoint_WhenNotOverridden_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("connection", nameof(MinimalConnection));

        var localEndPoint = connection.LocalEndPoint;

        diagnostics.Act("local end point", localEndPoint?.ToString() ?? "null");
        diagnostics.Assert("local end point is null", true, localEndPoint is null);
        Assert.IsNull(localEndPoint);
    }

    [TestMethod]
    public void MarkReusable_WhenNotOverridden_DoesNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("connection", nameof(MinimalConnection));

        connection.MarkReusable();

        diagnostics.Act("is secure", connection.IsSecure);
        diagnostics.Assert("is secure", false, connection.IsSecure);
        Assert.IsFalse(connection.IsSecure);
    }

    [TestMethod]
    public void Session_WhenNotOverridden_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("connection", nameof(MinimalConnection));

        var session = connection.Session;

        diagnostics.Act("session", session?.GetType().Name ?? "null");
        diagnostics.Assert("session is null", true, session is null);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void TryHoldSession_WhenNotOverridden_ReturnsFalseAndHoldsNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("session", nameof(UnusedSession));

        var isHeld = connection.TryHoldSession(new UnusedSession());

        diagnostics.Act("is held", isHeld);
        diagnostics.Act("held session", connection.Session?.GetType().Name ?? "null");
        diagnostics.Assert("is held", false, isHeld);
        Assert.IsFalse(isHeld);
        Assert.IsNull(connection.Session);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ClearTlsAsync_WhenNotOverridden_ReturnsNull(bool sendCloseNotifyFirst)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("send close notify first", sendCloseNotifyFirst);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst, CancellationToken.None);

        diagnostics.Act("plaintext", plaintext?.GetType().Name ?? "null");
        diagnostics.Assert("plaintext is null", true, plaintext is null);
        Assert.IsNull(plaintext);
    }

    [TestMethod]
    public void IsSharedWithAnotherTransfer_WhenNotOverridden_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("connection", nameof(MinimalConnection));

        var isShared = connection.IsSharedWithAnotherTransfer;

        diagnostics.Act("is shared", isShared);
        diagnostics.Assert("is shared", false, isShared);
        Assert.IsFalse(isShared);
    }

    [TestMethod]
    public void HasPeerClosed_WhenNotOverridden_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnection connection = new MinimalConnection();
        diagnostics.Arrange("connection", nameof(MinimalConnection));

        var hasPeerClosed = connection.HasPeerClosed;

        diagnostics.Act("has peer closed", hasPeerClosed);
        diagnostics.Assert("has peer closed", false, hasPeerClosed);
        Assert.IsFalse(hasPeerClosed);
    }

    [TestMethod]
    public void ConcurrentTransferLimit_WhenNotOverridden_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IConnectionSession session = new UnusedSession();
        diagnostics.Arrange("session", nameof(UnusedSession));

        var limit = session.ConcurrentTransferLimit;

        diagnostics.Act("limit", limit?.ToString() ?? "null");
        diagnostics.Assert("limit is null", true, limit is null);
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
