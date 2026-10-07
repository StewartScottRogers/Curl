using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Checks that <see cref="PooledConnection" /> forwards every member to the connection it
/// wraps; how it returns to its pool on dispose is in <see cref="PoolingConnectorTests" />.
/// </summary>
[TestClass]
public sealed class PooledConnectionTests
{
    private readonly FakeConnector _inner = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadWriteAndFlush_ForwardToTheUnderlyingConnection()
    {
        await using var pool = new PoolingConnector(_inner, new ManualTimeProvider());
        var result = await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false), CancellationToken.None);
        var connection = (PooledConnection)result.Connection!;
        var underlying = _inner.Opened[0];

        await connection.WriteAsync(new byte[] { 7, 8 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var read = await connection.ReadAsync(new byte[4], CancellationToken.None);

        Diagnostics.Arrange("target", "origin.example:80, TLS False");
        Diagnostics.Bytes("written", underlying.Written.ToArray());
        Diagnostics.Act("bytes read", read);
        Diagnostics.Assert("flush count", 1, underlying.FlushCount);

        CollectionAssert.AreEqual(new byte[] { 7, 8 }, underlying.Written);
        Assert.AreEqual(1, underlying.FlushCount);
        Assert.AreEqual(0, read);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsNull(connection.LocalEndPoint);
        Assert.AreEqual(0L, connection.ConnectionNumber);
    }
}
