using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpEarlyResponseConnection" />: the status line read while a body read stays
/// pending, and the replay of every byte that read took. Every reply is read with 1-byte reads and
/// with one read.
/// </summary>
[TestClass]
public sealed class HttpEarlyResponseConnectionTests
{
    private const string Reply = "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n";

    private static readonly int[] ChunkSizes = [1, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(Reply, 401, DisplayName = "Status line")]
    [DataRow("not a status line\r\n", null, DisplayName = "Not a status line")]
    [DataRow("HTTP/1.1 200", null, DisplayName = "Connection ends before a line feed")]
    public async Task ReadStatusCodeAsync_Reply_GivesTheFirstLinesStatusCode(string reply, int? expected)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            HttpEarlyResponseConnection connection = new(new ScriptedConnection(Encoding.Latin1.GetBytes(reply), chunkSize));
            Diagnostics.Arrange("reply, chunk size", $"{reply.ReplaceLineEndings("|")}, {chunkSize}");

            int? statusCode = await connection.ReadStatusCodeAsync(CancellationToken.None);

            Diagnostics.Assert("status code", expected, statusCode);
            Assert.AreEqual(expected, statusCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ReadStatusCodeAsync_LineFillsTheBuffer_GivesNoStatusCode()
    {
        byte[] reply = Encoding.Latin1.GetBytes(new string('x', HttpLineReader.MaximumLineLength + 1));
        HttpEarlyResponseConnection connection = new(new ScriptedConnection(reply, 65536));
        Diagnostics.Arrange("reply", $"{reply.Length} bytes with no line feed");

        int? statusCode = await connection.ReadStatusCodeAsync(CancellationToken.None);

        Diagnostics.Assert("status code", null, statusCode);
        Assert.IsNull(statusCode);
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheStatusLineWasRead_ReplaysEveryByteThenReadsOn()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            HttpEarlyResponseConnection connection = new(new ScriptedConnection(Encoding.Latin1.GetBytes(Reply), chunkSize));
            Diagnostics.Arrange("reply, chunk size", $"{Reply.ReplaceLineEndings("|")}, {chunkSize}");

            await connection.ReadStatusCodeAsync(CancellationToken.None);
            string read = await ReadAllAsync(connection);

            Diagnostics.Assert("read", Reply, read);
            Assert.AreEqual(Reply, read, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task Members_WrappedConnection_PassThroughAndDisposeLeavesItOpen()
    {
        ScriptedConnection inner = new(Encoding.Latin1.GetBytes(Reply), 65536) { IsSecure = true };
        HttpEarlyResponseConnection connection = new(inner);
        Diagnostics.Arrange("wrapped connection", "secure, no status line read");

        await connection.WriteAsync("abc"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();
        string read = await ReadAllAsync(connection);

        Diagnostics.Assert("written, read, secure, end point, disposed", $"abc, {Reply}, True, , False", $"{Encoding.Latin1.GetString(inner.Written)}, {read}, {connection.IsSecure}, {connection.RemoteEndPoint}, {inner.IsDisposed}");
        Assert.AreEqual("abc", Encoding.Latin1.GetString(inner.Written));
        Assert.AreEqual(Reply, read);
        Assert.IsTrue(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        Assert.IsFalse(inner.IsDisposed);
    }

    private static async Task<string> ReadAllAsync(HttpEarlyResponseConnection connection)
    {
        byte[] buffer = new byte[7];
        StringBuilder read = new();
        int length;
        while ((length = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            read.Append(Encoding.Latin1.GetString(buffer, 0, length));
        }

        return read.ToString();
    }
}
