using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Checks <see cref="ServerHelloReplayStream" />: bytes read before <see cref="ServerHelloReplayStream.Replay" />
/// are read again after it, then the transport's; writes, flushes and disposal reach the transport.
/// </summary>
[TestClass]
public sealed class ServerHelloReplayStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadsBeforeReplayAreReadAgainThenTheTransportsBytes()
    {
        byte[] transportBytes = [1, 2, 3, 4, 5, 6, 7, 8];
        Diagnostics.Bytes("transport", transportBytes);
        Diagnostics.Arrange("reads", "3 bytes, 1 byte, Replay, 2 bytes, up to 4 bytes, up to 4 bytes, 1 byte");
        using MemoryStream transport = new(transportBytes);
        using ServerHelloReplayStream stream = new(transport);
        byte[] first = new byte[3];
        byte[] again = new byte[2];
        byte[] rest = new byte[4];
        byte[] end = new byte[4];

        await stream.ReadExactlyAsync(first);
        Assert.AreEqual(1, stream.Read(new byte[1], 0, 1));
        stream.Replay();
        Assert.AreEqual(2, await stream.ReadAsync(again, 0, 2, CancellationToken.None));
        int restRead = stream.Read(rest);
        int endRead = await stream.ReadAsync(end);

        Diagnostics.Act("bytes read after replay", $"{restRead}, then {endRead}");
        Diagnostics.Diff("first read", new byte[] { 1, 2, 3 }, first);
        Diagnostics.Diff("read again after replay", new byte[] { 1, 2 }, again);
        Diagnostics.Diff("rest of the replayed bytes", new byte[] { 3, 4, 0, 0 }, rest);
        Diagnostics.Diff("transport's bytes after the replay", new byte[] { 5, 6, 7, 8 }, end);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, first);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, again);
        Assert.AreEqual(2, restRead);
        CollectionAssert.AreEqual(new byte[] { 3, 4, 0, 0 }, rest);
        Assert.AreEqual(4, endRead);
        CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, end);
        Assert.AreEqual(0, stream.Read(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task WritesAndFlushesReachTheTransport()
    {
        using MemoryStream transport = new();
        using ServerHelloReplayStream stream = new(transport);
        Diagnostics.Arrange("writes", "1 by Write(array), 2 by WriteAsync(memory), 3 by WriteAsync(array), then Flush and FlushAsync");

        stream.Write([1], 0, 1);
        await stream.WriteAsync(new byte[] { 2 }.AsMemory());
        await stream.WriteAsync([3], 0, 1, CancellationToken.None);
        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);

        Diagnostics.Act("can read, write, seek", $"{stream.CanRead}, {stream.CanWrite}, {stream.CanSeek}");
        Diagnostics.Diff("transport", new byte[] { 1, 2, 3 }, transport.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, transport.ToArray());
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void SeekingIsNotSupported()
    {
        using ServerHelloReplayStream stream = new(new MemoryStream());
        Diagnostics.Arrange("transport", "an empty MemoryStream");

        Diagnostics.Act("calls", "Length, Position get and set, Seek, SetLength");
        Diagnostics.Assert("exception each call must throw", nameof(NotSupportedException), "checked by Assert.ThrowsExactly below");
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void DisposingDisposesTheTransport()
    {
        MemoryStream transport = new();
        ServerHelloReplayStream stream = new(transport);
        Diagnostics.Arrange("transport", "an empty MemoryStream");

        stream.Dispose();

        Diagnostics.Act("transport can read", transport.CanRead);
        Diagnostics.Assert("transport can read", false, transport.CanRead);
        Assert.IsFalse(transport.CanRead);
    }
}
