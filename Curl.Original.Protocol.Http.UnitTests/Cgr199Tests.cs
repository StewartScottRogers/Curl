using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-199 (BL-3728): curl 8.22.0 sends an in-memory body forced to chunked in the pieces
/// of its upload buffer, so a 70000-byte body after a 158-byte head goes out as chunks of
/// 65366 (<c>ff56</c>) and 4634 (<c>121a</c>) bytes, not as one chunk.
/// </summary>
[TestClass]
public sealed class Cgr199Tests
{
    [TestMethod]
    public async Task WriteAsync_ChunkedBytesBodyLargerThanTheUploadBuffer_SendsItInChunksOfTheBufferRoom()
    {
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { SharedHeadLength = 158 };
        byte[] body = new byte[70000];
        Array.Fill(body, (byte)'x');

        await writer.WriteAsync(new BytesBody(body, "a/b"), true, CancellationToken.None);

        string expected = "ff56\r\n" + new string('x', 65366) + "\r\n121a\r\n" + new string('x', 4634) + "\r\n0\r\n\r\n";
        Assert.AreEqual(expected, Encoding.Latin1.GetString(connection.Written));
    }
}
