using System.IO.Compression;
using System.Text;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <c>%{size_download}</c> under <c>--tr-encoding</c> when a transfer-coded body fails to
/// decode partway (BL-5120): the bytes decoded and delivered before the error are counted.
/// </summary>
[TestClass]
public sealed class TransferEncodingPartialDecodeSizeDownloadTests
{
    private static readonly byte[] Plain = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("hello world ", 50)));

    [TestMethod]
    public async Task CopyAsync_GzipThenTrailingJunk_Exit23AndCountsTheDecodedBody()
    {
        byte[] chunk = Join(Gzip(Plain), Encoding.ASCII.GetBytes("TRAILINGJUNK"));

        (HttpResponseBodyReader reader, byte[] written, HttpTransferException failure) = await ReadFailingAsync(chunk);

        Assert.AreEqual(23, (int)failure.ExitCode);
        Assert.AreEqual(600, reader.BytesWritten);
        CollectionAssert.AreEqual(Plain, written);
    }

    [TestMethod]
    public async Task CopyAsync_TwoGzipMembers_Exit23AndCountsTheFirstMember()
    {
        byte[] chunk = Join(Gzip(Plain[..100]), Gzip(Plain[100..]));

        (HttpResponseBodyReader reader, byte[] written, HttpTransferException failure) = await ReadFailingAsync(chunk);

        Assert.AreEqual(23, (int)failure.ExitCode);
        Assert.AreEqual(100, reader.BytesWritten);
        CollectionAssert.AreEqual(Plain[..100], written);
    }

    [TestMethod]
    public async Task CopyAsync_BodyThatFailsBeforeAnyByteDecodes_CountsNothing()
    {
        (HttpResponseBodyReader reader, byte[] written, _) = await ReadFailingAsync(Encoding.ASCII.GetBytes("this is not gzip at all"));

        Assert.AreEqual(0, reader.BytesWritten);
        Assert.IsEmpty(written);
    }

    private static async Task<(HttpResponseBodyReader Reader, byte[] Written, HttpTransferException Failure)> ReadFailingAsync(byte[] chunk)
    {
        byte[] body = Join(Encoding.ASCII.GetBytes($"{chunk.Length:x}\r\n"), chunk, Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"));
        byte[] response = Join(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n"), body);
        ScriptedConnection connection = new(response, 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        MemoryStream output = new();
        HttpResponseBodyReader reader = new(connection) { DecodesTransferCoding = true };

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await reader.CopyAsync(head, false, output, false, true, CancellationToken.None));

        return (reader, output.ToArray(), failure);
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
