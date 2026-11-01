using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the chunk framing curl 8.22.0 sent for <c>-H "Transfer-Encoding: chunked" -F a=b</c> (BL-3210 case 4):
/// the whole 0x95-byte multipart body in one chunk, with no empty chunk before it, then <c>0</c>.
/// </summary>
[TestClass]
public sealed class HttpRequestBodyWriterMimeChunkTests
{
    [TestMethod]
    public async Task WriteAsync_ChunkedMultipartBodyReadInOnce_WritesOneChunkThenTheLastChunk()
    {
        byte[] body = Encoding.Latin1.GetBytes(new string('x', 0x95));
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        await writer.WriteAsync(new StreamBody(new MemoryStream(body), body.Length, "multipart/form-data"), true, CancellationToken.None);

        Assert.AreEqual("95\r\n" + new string('x', 0x95) + "\r\n0\r\n\r\n", Encoding.Latin1.GetString(connection.Written));
    }
}
