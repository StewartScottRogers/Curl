using System.Text;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpPrefixedConnection" />: its prefix is read first, however small the
/// reads, then the connection behind it, which takes every write.
/// </summary>
[TestClass]
public sealed class HttpPrefixedConnectionTests
{
    [TestMethod]
    public async Task ReadAsync_SmallReads_GiveThePrefixThenTheRest()
    {
        HttpPrefixedConnection connection = new("abc"u8.ToArray(), new ScriptedConnection("de"u8.ToArray(), 65536));
        MemoryStream read = new();
        byte[] buffer = new byte[2];

        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            read.Write(buffer, 0, count);
        }

        Assert.AreEqual("abcde", Encoding.Latin1.GetString(read.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_GoToTheConnectionBehind()
    {
        ScriptedConnection rest = new([], 1);
        HttpPrefixedConnection connection = new(ReadOnlyMemory<byte>.Empty, rest);

        await connection.WriteAsync("x"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual("x"u8.ToArray(), rest.Written);
    }

    [TestMethod]
    public async Task Properties_AreTheConnectionBehinds_AndDisposingLeavesItOpen()
    {
        ScriptedConnection rest = new([], 1) { IsSecure = true };
        HttpPrefixedConnection connection = new(ReadOnlyMemory<byte>.Empty, rest);

        await connection.DisposeAsync();

        Assert.IsTrue(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsNull(connection.LocalEndPoint);
        Assert.IsFalse(rest.IsDisposed);
    }
}
