using System.Text;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpPrefixedConnection" />: its prefix is read first, however small the
/// reads, then the connection behind it, which takes every write.
/// </summary>
[TestClass]
public sealed class HttpPrefixedConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_SmallReads_GiveThePrefixThenTheRest()
    {
        HttpPrefixedConnection connection = new("abc"u8.ToArray(), new ScriptedConnection("de"u8.ToArray(), 65536));
        MemoryStream read = new();
        byte[] buffer = new byte[2];
        Diagnostics.Arrange("prefix, rest, buffer size", "abc, de, 2");

        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            read.Write(buffer, 0, count);
        }

        Diagnostics.Act("read", Encoding.Latin1.GetString(read.ToArray()));
        Diagnostics.Assert("read", "abcde", Encoding.Latin1.GetString(read.ToArray()));
        Assert.AreEqual("abcde", Encoding.Latin1.GetString(read.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_GoToTheConnectionBehind()
    {
        ScriptedConnection rest = new([], 1);
        HttpPrefixedConnection connection = new(ReadOnlyMemory<byte>.Empty, rest);
        Diagnostics.Arrange("written", "x");

        await connection.WriteAsync("x"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        Diagnostics.Bytes("connection behind written", rest.Written);
        Diagnostics.Act("connection behind written", Encoding.Latin1.GetString(rest.Written));
        Diagnostics.Diff("connection behind written", "x"u8, rest.Written);
        CollectionAssert.AreEqual("x"u8.ToArray(), rest.Written);
    }

    [TestMethod]
    public async Task Properties_AreTheConnectionBehinds_AndDisposingLeavesItOpen()
    {
        ScriptedConnection rest = new([], 1) { IsSecure = true };
        HttpPrefixedConnection connection = new(ReadOnlyMemory<byte>.Empty, rest);
        Diagnostics.Arrange("connection behind", "secure, no end points");

        await connection.DisposeAsync();

        Diagnostics.Act("secure, behind disposed", $"{connection.IsSecure}, {rest.IsDisposed}");
        Diagnostics.Assert("secure, behind disposed", "True, False", $"{connection.IsSecure}, {rest.IsDisposed}");
        Assert.IsTrue(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsNull(connection.LocalEndPoint);
        Assert.IsFalse(rest.IsDisposed);
    }
}
