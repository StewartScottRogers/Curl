using Curl.Extended.Protocol.DnsQuery.Transport.Https;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Https;

/// <summary>Checks that the connection is an asynchronous, unseekable read-write stream for the HTTP/2 layer.</summary>
[TestClass]
public sealed class DohConnectionStreamTests
{
    [TestMethod]
    public async Task ReadAndWrite_GoToTheConnection()
    {
        var server = new ScriptedDnsServer((_, _) => [], [1, 2]);
        IConnection connection = (await server.ConnectAsync(new ConnectTarget("h", 443, UseTls: false), CancellationToken.None)).Connection!;
        using var stream = new DohConnectionStream(connection);
        byte[] buffer = new byte[4];

        int read = await stream.ReadAsync(buffer.AsMemory());
        await stream.WriteAsync(new byte[] { 3 }.AsMemory());
        await stream.FlushAsync();

        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 3 }, server.TcpWritten.ToArray());
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public async Task SynchronousAndSeekingMembers_AreNotSupported()
    {
        var server = new ScriptedDnsServer((_, _) => [], []);
        using var stream = new DohConnectionStream((await server.ConnectAsync(new ConnectTarget("h", 443, UseTls: false), CancellationToken.None)).Connection!);

        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(stream.Flush);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
