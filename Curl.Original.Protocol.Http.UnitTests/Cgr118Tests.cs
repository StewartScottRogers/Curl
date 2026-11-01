using System.Text;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-118 (BL-3647): a header line that starts with white space
/// and has no header before it to fold into is a header of its own, and curl 8.22.0 (mingw,
/// Schannel) writes it to <c>-i</c> and <c>-D</c> as received, its leading blanks kept, while
/// its name loses them; measured on 2026-10-26 with <c>Record-CurlExchange.ps1</c> (BL-3647 Notes).
/// </summary>
[TestClass]
public sealed class Cgr118Tests
{
    private static readonly int[] ChunkSizes = [1, 7, 65536];

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n Date: Tue, 09 Nov 2010 14:49:00 GMT\r\nServer: x\r\nContent-Length: 6\r\n\r\n", "Date=Tue, 09 Nov 2010 14:49:00 GMT,Server=x,Content-Length=6", DisplayName = "One space")]
    [DataRow("HTTP/1.1 200 OK\r\n\tX-A: one\r\nServer: x\r\nContent-Length: 6\r\n\r\n", "X-A=one,Server=x,Content-Length=6", DisplayName = "Tab")]
    [DataRow("HTTP/1.1 200 OK\r\n  X-Fold: a\r\nContent-Length: 2\r\n\r\n", "X-Fold=a,Content-Length=2", DisplayName = "Two spaces")]
    [DataRow("HTTP/1.1 200 OK\r\n\tX: a\r\nContent-Length: 2\r\n\r\n", "X=a,Content-Length=2", DisplayName = "Tab, short")]
    public async Task ReadAsync_FirstHeaderStartsWithBlanks_WritesTheLineAsReceivedAndNamesItWithoutThem(string response, string expectedHeaders)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            HttpResponseHead head = await ReadAsync(response, chunkSize);

            Assert.AreEqual(response, Encoding.Latin1.GetString(head.HeadBytes.Span), $"Chunk size {chunkSize}");
            Assert.AreEqual(expectedHeaders, string.Join(",", head.Headers.Select(header => $"{header.Name}={header.Value}")), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ReadAsync_FirstHeaderStartsWithBlanksAndIsFolded_KeepsTheBlanksBeforeTheFoldedLine()
    {
        // curl 8.22.0 wrote " X-Fold: a b\r\n" for " X-Fold: a\r\n  b\r\n".
        foreach (int chunkSize in ChunkSizes)
        {
            HttpResponseHead head = await ReadAsync("HTTP/1.1 200 OK\r\n X-Fold: a\r\n  b\r\nContent-Length: 2\r\n\r\n", chunkSize);

            Assert.AreEqual("HTTP/1.1 200 OK\r\n X-Fold: a b\r\nContent-Length: 2\r\n\r\n", Encoding.Latin1.GetString(head.HeadBytes.Span), $"Chunk size {chunkSize}");
            Assert.AreEqual("X-Fold", head.Headers[0].Name, $"Chunk size {chunkSize}");
            Assert.AreEqual("a b", head.Headers[0].Value, $"Chunk size {chunkSize}");
        }
    }

    private static async Task<HttpResponseHead> ReadAsync(string response, int chunkSize) =>
        await new HttpResponseHeadReader(new ScriptedConnection(Encoding.Latin1.GetBytes(response), chunkSize)).ReadAsync(CancellationToken.None);
}
