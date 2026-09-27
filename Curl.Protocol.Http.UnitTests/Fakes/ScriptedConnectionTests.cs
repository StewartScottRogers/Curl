using System.Text;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Pins <see cref="ScriptedConnection" />, the fake connection every HTTP test replays a recorded exchange through.
/// </summary>
[TestClass]
public sealed class ScriptedConnectionTests
{
    [TestMethod]
    public async Task ReadAsync_ChunkSizeChosen_ReplaysResponseInChunksOfAtMostThatSizeThenReportsClose()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("abcde"), 2);
        byte[] buffer = new byte[10];

        List<string> reads = [];
        int read;
        while ((read = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reads.Add(Encoding.ASCII.GetString(buffer, 0, read));
        }

        CollectionAssert.AreEqual(new[] { "ab", "cd", "e" }, reads);
        Assert.AreEqual(4, connection.ReadCount);
    }

    [TestMethod]
    public async Task ReadAsync_BufferSmallerThanChunk_FillsOnlyTheBuffer()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("abcde"), 100);

        int read = await connection.ReadAsync(new byte[3], CancellationToken.None);

        Assert.AreEqual(3, read);
    }

    [TestMethod]
    public async Task ReadAsync_ResponseSpentWithFailureChosen_ThrowsTheFailure()
    {
        IOException failure = new("reset");
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("a"), 8, failureAfterResponse: failure);
        byte[] buffer = new byte[8];

        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await connection.ReadAsync(buffer, CancellationToken.None));

        Assert.AreSame(failure, thrown);
    }

    [TestMethod]
    public async Task ReadAsync_ExpectedRequestWritten_ReadsAndRecordsTheWrites()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
        await using ScriptedConnection connection = new([], 1, request);

        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        int read = await connection.ReadAsync(new byte[1], CancellationToken.None);

        Assert.AreEqual(0, read);
        CollectionAssert.AreEqual(request, connection.Written);
    }

    [TestMethod]
    public async Task ReadAsync_RequestDiffersFromExpected_FailsTheTest()
    {
        ScriptedConnection connection = new([], 1, Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"));
        await connection.WriteAsync(Encoding.ASCII.GetBytes("GET /x HTTP/1.1\r\n\r\n"), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<AssertFailedException>(
            async () => await connection.ReadAsync(new byte[1], CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_LaterReads_DoNotCheckTheRequestAgain()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("ab"), 1, request);
        await connection.WriteAsync(request, CancellationToken.None);

        await connection.ReadAsync(new byte[1], CancellationToken.None);
        await connection.WriteAsync(request, CancellationToken.None);
        int read = await connection.ReadAsync(new byte[1], CancellationToken.None);

        Assert.AreEqual(1, read);
    }

    [TestMethod]
    public async Task DisposeAsync_Called_IsRecordedAndConnectionHasNoAddressOrTls()
    {
        ScriptedConnection connection = new([], 1);

        await connection.DisposeAsync();

        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ReadAndWrite_TokenCancelled_ThrowOperationCanceled()
    {
        ScriptedConnection connection = new([], 1);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connection.ReadAsync(new byte[1], cancelled.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connection.WriteAsync(new byte[1], cancelled.Token));
    }

    [TestMethod]
    public void Constructor_ChunkSizeBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScriptedConnection([], 0));
    }
}
