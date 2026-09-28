using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Checks that <see cref="PooledConnection" /> forwards every member to the connection it
/// wraps; how it returns to its pool on dispose is in <see cref="PoolingConnectorTests" />.
/// </summary>
[TestClass]
public sealed class PooledConnectionTests
{
    private readonly FakeConnector _inner = new();

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

        CollectionAssert.AreEqual(new byte[] { 7, 8 }, underlying.Written);
        Assert.AreEqual(1, underlying.FlushCount);
        Assert.AreEqual(0, read);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.AreEqual(0L, connection.ConnectionNumber);
    }
}
