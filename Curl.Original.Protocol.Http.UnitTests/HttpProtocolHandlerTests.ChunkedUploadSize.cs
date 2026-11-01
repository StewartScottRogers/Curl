using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// CURLINFO_REQUEST_SIZE and CURLINFO_SIZE_UPLOAD_T of a chunked POSTFIELDS upload, against libcurl 8.22.0 in the
/// easytest.py case post-chunked-header (BL-3331, BL-3320): the chunk framing of the request body is
/// counted in both.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ChunkedPostOfOneByte_CountsTheTenFramingBytesInTheRequestSize()
    {
        // libcurl: 147 against 137 for the same wire bytes; "1\r\n" + "\r\n" + "0\r\n\r\n" is the 10.
        const string response = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";
        HttpRequestOptions options = new()
        {
            Headers = ["Transfer-Encoding: chunked"],
            Body = new BytesBody(Encoding.Latin1.GetBytes("x"), "application/x-www-form-urlencoded"),
        };
        Diagnostics.Arrange("request", "POST of x with Transfer-Encoding: chunked");

        ScriptedConnection connection = Connection(response, 65536);
        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EncodingContext(new MemoryStream(), options));

        WriteResult(result);
        Diagnostics.Assert("request size", (long)connection.Written.Length, result.Report!.RequestSize);
        Assert.AreEqual((long)connection.Written.Length, result.Report!.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedPostOfOneByte_CountsTheTenFramingBytesInTheUploadSize()
    {
        // libcurl 8.22.0: CURLINFO_SIZE_UPLOAD_T 11 = "1\r\nx\r\n0\r\n\r\n" (BL-3320).
        const string response = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";
        HttpRequestOptions options = new()
        {
            Headers = ["Transfer-Encoding: chunked"],
            Body = new BytesBody(Encoding.Latin1.GetBytes("x"), "application/x-www-form-urlencoded"),
        };
        Diagnostics.Arrange("request", "POST of x with Transfer-Encoding: chunked");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536))).ExecuteAsync(EncodingContext(new MemoryStream(), options));

        WriteResult(result);
        Diagnostics.Assert("upload size", 11L, result.Report!.UploadSize);
        Assert.AreEqual(11L, result.Report!.UploadSize);
    }
}
