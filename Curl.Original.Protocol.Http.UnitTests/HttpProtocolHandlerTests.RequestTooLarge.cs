using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// A request head of 1 MiB or more, which curl 8.22.0 refuses with exit 100 and
/// <c>HTTP request too large</c> after connecting and before sending a byte (measured,
/// BL-2167 Notes; upstream test3229).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_RequestHeadOfOneMebibyte_FailsWithTooLargeAndSendsNothing()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536);
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { Headers = [HttpRequestHeadFormatterTests.LargeHeader(HttpRequestHeadFormatter.MaximumRequestHeadLength)] };
        Diagnostics.Arrange("request head length", HttpRequestHeadFormatter.MaximumRequestHeadLength);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Http = options, Events = events });

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.TooLarge, result.ExitCode);
        Diagnostics.Assert("bytes sent", 0, connection.Written.Length);
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual("HTTP request too large", result.ErrorMessage);
        Assert.IsEmpty(connection.Written);
        Assert.Contains("HTTP request too large", events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_RequestHeadOneByteUnderOneMebibyte_SendsIt()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536);
        HttpRequestOptions options = new() { Headers = [HttpRequestHeadFormatterTests.LargeHeader(HttpRequestHeadFormatter.MaximumRequestHeadLength - 1)] };

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Http = options });

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(HttpRequestHeadFormatter.MaximumRequestHeadLength - 1, connection.Written);
    }
}
