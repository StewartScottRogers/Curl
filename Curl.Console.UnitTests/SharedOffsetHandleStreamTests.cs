using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="SharedOffsetHandleStream" /> hands its bytes to the shared-offset write:
/// until every byte is written, failing when a write moves none, and nothing it cannot do.
/// </summary>
[TestClass]
public sealed class SharedOffsetHandleStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Write_PartialWrites_WritesTheRestUntilEveryByteIsWritten()
    {
        List<string> calls = [];
        using SharedOffsetHandleStream stream = new(7, (handle, bytes) =>
        {
            calls.Add($"{handle}:{Convert.ToHexString(bytes)}");
            return Math.Min(2, bytes.Length);
        });
        Diagnostics.Arrange("write", "moves at most 2 bytes a call");

        stream.Write([1, 2, 3, 4, 5, 9], 0, 5);
        Diagnostics.Act("calls", string.Join(' ', calls));

        Diagnostics.Assert("calls", "7:0102030405 7:030405 7:05", string.Join(' ', calls));
        CollectionAssert.AreEqual(new[] { "7:0102030405", "7:030405", "7:05" }, calls);
    }

    [TestMethod]
    public void Write_WriteMovesNoByte_ThrowsIOException()
    {
        using SharedOffsetHandleStream stream = new(7, (_, _) => 0);
        Diagnostics.Arrange("write", "moves no byte");

        IOException thrown = Assert.ThrowsExactly<IOException>(() => stream.Write([1], 0, 1));
        Diagnostics.Act("message", thrown.Message);

        Diagnostics.Assert("message", SharedOffsetHandleStream.NothingWrittenMessage, thrown.Message);
        Assert.AreEqual(SharedOffsetHandleStream.NothingWrittenMessage, thrown.Message);
    }

    [TestMethod]
    public void Write_Empty_CallsNoWrite()
    {
        int calls = 0;
        using SharedOffsetHandleStream stream = new(7, (_, bytes) => ++calls);

        stream.Write([], 0, 0);
        Diagnostics.Act("calls", calls);

        Diagnostics.Assert("calls", 0, calls);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task WriteAsync_ArrayAndMemory_WriteTheirBytes()
    {
        List<byte> written = [];
        using SharedOffsetHandleStream stream = new(7, (_, bytes) =>
        {
            written.AddRange(bytes.ToArray());
            return bytes.Length;
        });

        await stream.WriteAsync(new byte[] { 0, 1, 2 }, 1, 2, TestContext.CancellationToken);
        await stream.WriteAsync(new byte[] { 3 }.AsMemory(), TestContext.CancellationToken);
        Diagnostics.Act("written", Convert.ToHexString(written.ToArray()));

        Diagnostics.Assert("written", "010203", Convert.ToHexString(written.ToArray()));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, written);
    }

    [TestMethod]
    public async Task WriteAsync_Cancelled_ThrowsWithoutWriting()
    {
        int calls = 0;
        using SharedOffsetHandleStream stream = new(7, (_, bytes) => ++calls);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await stream.WriteAsync(new byte[] { 1 }.AsMemory(), cancelled.Token));
        Diagnostics.Act("calls", calls);

        Diagnostics.Assert("calls", 0, calls);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Capabilities_WriteOnly_EverythingElseIsNotSupported()
    {
        using SharedOffsetHandleStream stream = new(7, (_, bytes) => bytes.Length);
        Diagnostics.Act("read / seek / write", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");

        stream.Flush();
        Diagnostics.Assert("read / seek / write", "False / False / True", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
