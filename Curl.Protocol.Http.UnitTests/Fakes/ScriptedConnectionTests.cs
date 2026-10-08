using System.Text;
using Curl.Testing;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Pins <see cref="ScriptedConnection" />, the fake connection every HTTP test replays a recorded exchange through.
/// </summary>
[TestClass]
public sealed class ScriptedConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_ChunkSizeChosen_ReplaysResponseInChunksOfAtMostThatSizeThenReportsClose()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("abcde"), 2);
        byte[] buffer = new byte[10];
        Diagnostics.Arrange("scripted response", "abcde");
        Diagnostics.Arrange("chunk size", 2);

        List<string> reads = [];
        int read;
        while ((read = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reads.Add(Encoding.ASCII.GetString(buffer, 0, read));
        }

        Diagnostics.Act("reads", string.Join(" | ", reads));
        Diagnostics.Act("read count", connection.ReadCount);
        Diagnostics.Assert("read count", 4, connection.ReadCount);
        CollectionAssert.AreEqual(new[] { "ab", "cd", "e" }, reads);
        Assert.AreEqual(4, connection.ReadCount);
    }

    [TestMethod]
    public async Task ReadAsync_BufferSmallerThanChunk_FillsOnlyTheBuffer()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("abcde"), 100);
        Diagnostics.Arrange("scripted response", "abcde");
        Diagnostics.Arrange("chunk size and buffer size", "100 and 3");

        int read = await connection.ReadAsync(new byte[3], CancellationToken.None);

        Diagnostics.Act("bytes read", read);
        Diagnostics.Assert("bytes read", 3, read);
        Assert.AreEqual(3, read);
    }

    [TestMethod]
    public async Task ReadAsync_ResponseSpentWithFailureChosen_ThrowsTheFailure()
    {
        IOException failure = new("reset");
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("a"), 8, failureAfterResponse: failure);
        byte[] buffer = new byte[8];
        Diagnostics.Arrange("scripted response", "a, then IOException: reset");

        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await connection.ReadAsync(buffer, CancellationToken.None));

        Diagnostics.Act("exception", thrown.Message);
        Diagnostics.Assert("is the scripted failure", true, ReferenceEquals(failure, thrown));
        Assert.AreSame(failure, thrown);
    }

    [TestMethod]
    public async Task ReadAsync_ExpectedRequestWritten_ReadsAndRecordsTheWrites()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
        await using ScriptedConnection connection = new([], 1, request);
        Diagnostics.Bytes("expected request", request);
        Diagnostics.Arrange("scripted response", "empty");

        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        int read = await connection.ReadAsync(new byte[1], CancellationToken.None);

        Diagnostics.Act("bytes read", read);
        Diagnostics.Bytes("written", connection.Written);
        Diagnostics.Diff("written", request, connection.Written.ToArray());
        Assert.AreEqual(0, read);
        CollectionAssert.AreEqual(request, connection.Written);
    }

    [TestMethod]
    public async Task ReadAsync_RequestDiffersFromExpected_FailsTheTest()
    {
        ScriptedConnection connection = new([], 1, Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"));
        await connection.WriteAsync(Encoding.ASCII.GetBytes("GET /x HTTP/1.1\r\n\r\n"), CancellationToken.None);
        Diagnostics.Arrange("expected request line", "GET / HTTP/1.1");
        Diagnostics.Arrange("written request line", "GET /x HTTP/1.1");

        AssertFailedException thrown = await Assert.ThrowsExactlyAsync<AssertFailedException>(
            async () => await connection.ReadAsync(new byte[1], CancellationToken.None));

        Diagnostics.Act("exception", thrown.GetType().Name);
        Diagnostics.Assert("exception", nameof(AssertFailedException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ReadAsync_LaterReads_DoNotCheckTheRequestAgain()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("ab"), 1, request);
        await connection.WriteAsync(request, CancellationToken.None);
        Diagnostics.Arrange("scripted response", "ab, in 1-byte chunks");
        Diagnostics.Bytes("expected request", request);

        await connection.ReadAsync(new byte[1], CancellationToken.None);
        await connection.WriteAsync(request, CancellationToken.None);
        int read = await connection.ReadAsync(new byte[1], CancellationToken.None);

        Diagnostics.Act("bytes read on the second read", read);
        Diagnostics.Assert("bytes read on the second read", 1, read);
        Assert.AreEqual(1, read);
    }

    [TestMethod]
    public async Task DisposeAsync_Called_IsRecordedAndConnectionHasNoAddressOrTls()
    {
        ScriptedConnection connection = new([], 1);
        Diagnostics.Arrange("scripted response", "empty");

        await connection.DisposeAsync();

        Diagnostics.Act("disposed, secure, has remote end point", $"{connection.IsDisposed}, {connection.IsSecure}, {connection.RemoteEndPoint is not null}");
        Diagnostics.Assert("disposed", true, connection.IsDisposed);
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
        Diagnostics.Arrange("token", "cancelled before the read and the write");

        OperationCanceledException readThrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connection.ReadAsync(new byte[1], cancelled.Token));
        OperationCanceledException writeThrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connection.WriteAsync(new byte[1], cancelled.Token));

        Diagnostics.Act("read and write exceptions", $"{readThrown.GetType().Name}, {writeThrown.GetType().Name}");
        Diagnostics.Assert("write exception", nameof(OperationCanceledException), writeThrown.GetType().Name);
    }

    [TestMethod]
    public void Constructor_ChunkSizeBelowOne_Throws()
    {
        Diagnostics.Arrange("chunk size", 0);

        ArgumentOutOfRangeException thrown = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScriptedConnection([], 0));

        Diagnostics.Act("exception parameter", thrown.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), thrown.GetType().Name);
    }
}
