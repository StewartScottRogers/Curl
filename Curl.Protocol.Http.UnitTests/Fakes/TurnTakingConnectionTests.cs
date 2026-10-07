using System.Text;
using Curl.Testing;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Proves <see cref="TurnTakingConnection" /> holds each response back until its request.
/// </summary>
[TestClass]
public sealed class TurnTakingConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_ServesEachResponseOnlyAfterTheNextWrite()
    {
        await using TurnTakingConnection connection = new(2, "abc", "d");
        byte[] buffer = new byte[8];
        Diagnostics.Arrange("responses", "abc, d");
        Diagnostics.Arrange("chunk size", 2);

        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));
        await connection.WriteAsync("r1"u8.ToArray(), CancellationToken.None);
        await connection.WriteAsync("+"u8.ToArray(), CancellationToken.None);
        Assert.AreEqual(2, await connection.ReadAsync(buffer, CancellationToken.None));
        await connection.WriteAsync("early"u8.ToArray(), CancellationToken.None);
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual((byte)'c', buffer[0]);
        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));
        await connection.WriteAsync("r2"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        await connection.WriteAsync("r3"u8.ToArray(), CancellationToken.None);
        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));

        Diagnostics.Act("written", connection.Written);
        Diagnostics.Assert("written", "r1+earlyr2r3", connection.Written);
        Assert.AreEqual("r1+earlyr2r3", connection.Written);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
    }

    [TestMethod]
    public async Task DisposeAsync_MarksItDisposedAndKeepsWhatWasWritten()
    {
        TurnTakingConnection connection = new(1);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("x"), CancellationToken.None);
        Diagnostics.Arrange("written before dispose", "x");

        await connection.DisposeAsync();

        Diagnostics.Act("disposed and written", $"{connection.IsDisposed}, {connection.Written}");
        Diagnostics.Assert("written", "x", connection.Written);
        Assert.IsTrue(connection.IsDisposed);
        Assert.AreEqual("x", connection.Written);
    }
}
