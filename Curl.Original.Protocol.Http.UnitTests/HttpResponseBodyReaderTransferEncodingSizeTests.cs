using System.IO.Compression;
using System.Text;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <c>%{size_download}</c> under <c>--tr-encoding</c> (BL-5036): a Transfer-Encoding
/// coding is counted after it is decoded, a Content-Encoding coding still as received.
/// </summary>
[TestClass]
public sealed class HttpResponseBodyReaderTransferEncodingSizeTests
{
    private static readonly byte[] Plain = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("hello world ", 50)));

    [TestMethod]
    public async Task CopyAsync_GzipTransferEncodingChunked_CountsTheDecodedBody()
    {
        byte[] gzip = Gzip(Plain);
        byte[] body = Join(Encoding.ASCII.GetBytes($"{gzip.Length:x}\r\n"), gzip, Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"));

        (HttpResponseBodyReader reader, byte[] written) = await ReadAsync("Transfer-Encoding: gzip, chunked\r\n", body, decodeContent: false, decodeTransfer: true);

        Assert.AreEqual(600, reader.BytesWritten);
        CollectionAssert.AreEqual(Plain, written);
    }

    [TestMethod]
    public async Task CopyAsync_GzipContentEncodingCompressed_CountsTheEncodedBody()
    {
        byte[] gzip = Gzip(Plain);

        (HttpResponseBodyReader reader, byte[] written) = await ReadAsync($"Content-Length: {gzip.Length}\r\nContent-Encoding: gzip\r\n", gzip, decodeContent: true, decodeTransfer: false);

        Assert.AreEqual(gzip.Length, reader.BytesWritten);
        CollectionAssert.AreEqual(Plain, written);
    }

    [TestMethod]
    public async Task CopyAsync_PlainChunkedWithTrEncoding_CountsTheDechunkedBody()
    {
        (HttpResponseBodyReader reader, byte[] written) = await ReadAsync("Transfer-Encoding: chunked\r\n", Encoding.ASCII.GetBytes("5\r\nhello\r\n0\r\n\r\n"), decodeContent: false, decodeTransfer: true);

        Assert.AreEqual(5, reader.BytesWritten);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(written));
    }

    private static async Task<(HttpResponseBodyReader Reader, byte[] Written)> ReadAsync(string headers, byte[] body, bool decodeContent, bool decodeTransfer)
    {
        byte[] response = Join(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n{headers}\r\n"), body);
        ScriptedConnection connection = new(response, 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        MemoryStream output = new();
        HttpResponseBodyReader reader = new(connection) { DecodesTransferCoding = decodeTransfer };

        await reader.CopyAsync(head, false, output, decodeContent, decodeTransfer, CancellationToken.None);

        return (reader, output.ToArray());
    }

    private static byte[] Gzip(byte[] plain)
    {
        using MemoryStream compressed = new();
        using (GZipStream stream = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            stream.Write(plain);
        }

        return compressed.ToArray();
    }

    private static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
