using System.Text;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpFirstByteTimingConnection" />: the moment the first response byte
/// arrived, taken once, and everything else passed through to the connection it wraps.
/// </summary>
[TestClass]
public sealed class HttpFirstByteTimingConnectionTests
{
    [TestMethod]
    public async Task ReadAsync_TakesTheMomentOfTheFirstReadThatReturnsBytesOnly()
    {
        ScriptedConnection inner = new(Encoding.Latin1.GetBytes("ab"), 1, null);
        HttpFirstByteTimingConnection connection = new(inner, new SteppingTimeProvider(100));
        byte[] buffer = new byte[8];

        Assert.IsNull(connection.FirstByteReceived);
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(100L, connection.FirstByteReceived);
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));

        Assert.AreEqual(100L, connection.FirstByteReceived);
        Assert.AreEqual("b", Encoding.Latin1.GetString(buffer, 0, 1));
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionClosesAtOnce_TakesNoMoment()
    {
        HttpFirstByteTimingConnection connection = new(new ScriptedConnection([], 1, null), new SteppingTimeProvider(100));

        Assert.AreEqual(0, await connection.ReadAsync(new byte[8], CancellationToken.None));

        Assert.IsNull(connection.FirstByteReceived);
    }

    [TestMethod]
    public async Task WritesFlushesAndProperties_PassThroughAndDisposingLeavesTheConnectionOpen()
    {
        ScriptedConnection inner = new([], 1, null);
        HttpFirstByteTimingConnection connection = new(inner, new SteppingTimeProvider(100));

        await connection.WriteAsync("GET"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.AreEqual("GET", Encoding.Latin1.GetString(inner.Written));
        Assert.AreEqual(inner.IsSecure, connection.IsSecure);
        Assert.AreEqual(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.IsFalse(inner.IsDisposed);
    }
}
