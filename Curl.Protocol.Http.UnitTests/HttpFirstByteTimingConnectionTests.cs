using System.Text;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpFirstByteTimingConnection" />: the moment the first response byte
/// arrived, taken once, and everything else passed through to the connection it wraps.
/// </summary>
[TestClass]
public sealed class HttpFirstByteTimingConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_TakesTheMomentOfTheFirstReadThatReturnsBytesOnly()
    {
        ScriptedConnection inner = new(Encoding.Latin1.GetBytes("ab"), 1, null);
        HttpFirstByteTimingConnection connection = new(inner, new SteppingTimeProvider(100));
        byte[] buffer = new byte[8];
        Diagnostics.Arrange("scripted bytes, read size, clock step", "\"ab\", 1, 100");

        Diagnostics.Act("first byte before reading", connection.FirstByteReceived?.ToString() ?? "(none)");
        Assert.IsNull(connection.FirstByteReceived);
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Diagnostics.Act("first byte after the first read", connection.FirstByteReceived);
        Assert.AreEqual(100L, connection.FirstByteReceived);
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));

        Diagnostics.Bytes("buffer", buffer);
        Diagnostics.Assert("first byte after every read", 100L, connection.FirstByteReceived);
        Assert.AreEqual(100L, connection.FirstByteReceived);
        Assert.AreEqual("b", Encoding.Latin1.GetString(buffer, 0, 1));
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionClosesAtOnce_TakesNoMoment()
    {
        HttpFirstByteTimingConnection connection = new(new ScriptedConnection([], 1, null), new SteppingTimeProvider(100));
        Diagnostics.Arrange("scripted bytes", "none");

        int read = await connection.ReadAsync(new byte[8], CancellationToken.None);

        Diagnostics.Act("read, first byte", $"{read}, {connection.FirstByteReceived?.ToString() ?? "(none)"}");
        Diagnostics.Assert("read, first byte", "0, (none)", $"{read}, {connection.FirstByteReceived?.ToString() ?? "(none)"}");
        Assert.AreEqual(0, read);
        Assert.IsNull(connection.FirstByteReceived);
    }

    [TestMethod]
    public async Task WritesFlushesAndProperties_PassThroughAndDisposingLeavesTheConnectionOpen()
    {
        ScriptedConnection inner = new([], 1, null);
        HttpFirstByteTimingConnection connection = new(inner, new SteppingTimeProvider(100));
        Diagnostics.Arrange("written", "GET");

        await connection.WriteAsync("GET"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Diagnostics.Bytes("inner written", inner.Written);
        Diagnostics.Act("inner disposed", inner.IsDisposed);
        Diagnostics.Assert("inner written", "GET", Encoding.Latin1.GetString(inner.Written));
        Assert.AreEqual("GET", Encoding.Latin1.GetString(inner.Written));
        Assert.AreEqual(inner.IsSecure, connection.IsSecure);
        Assert.AreEqual(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.IsFalse(inner.IsDisposed);
    }
}
