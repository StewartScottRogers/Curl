using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>Checks that <c>-r</c> and <c>-C</c> send a signed <c>Range</c> header on an <c>s3://</c> download, as the https path does.</summary>
[TestClass]
public sealed class S3RangeDownloadTests
{
    private const string Partial = "HTTP/1.1 206 Partial Content\r\nContent-Range: bytes 0-9/100\r\nContent-Length: 10\r\n\r\n0123456789";

    [TestMethod]
    public async Task ExecuteAsync_RangeText_SendsSignedRangeAndWritesTheBody()
    {
        QueuedS3Connector connector = new(Partial);
        MemoryStream output = new();

        TransferResult result = await RunAsync(connector, output, rangeText: "0-9");

        Assert.AreEqual((CurlExitCode.Ok, 10L), (result.ExitCode, result.BytesTransferred));
        Assert.AreEqual("0123456789", Encoding.UTF8.GetString(output.ToArray()));
        string sent = connector.Connections[0].Written;
        Assert.Contains("range: bytes=0-9\r\n", sent);
        Assert.Contains("SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeTextAnswered200_WritesTheWholeBody()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok("whole"));
        MemoryStream output = new();

        TransferResult result = await RunAsync(connector, output, rangeText: "0-1");

        Assert.AreEqual((CurlExitCode.Ok, 5L), (result.ExitCode, result.BytesTransferred));
        Assert.AreEqual("whole", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeTextAnswered416_FailsWithExit22()
    {
        QueuedS3Connector connector = new("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 0\r\n\r\n");

        TransferResult result = await RunAsync(connector, new MemoryStream(), rangeText: "500-");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFrom_SendsOffsetRangeAndAppends()
    {
        QueuedS3Connector connector = new("HTTP/1.1 206 Partial Content\r\nContent-Length: 3\r\n\r\nxyz");
        MemoryStream output = new();

        TransferResult result = await RunAsync(connector, output, resumeFrom: 7);

        Assert.AreEqual((CurlExitCode.Ok, 3L), (result.ExitCode, result.BytesTransferred));
        Assert.AreEqual("xyz", Encoding.UTF8.GetString(output.ToArray()));
        Assert.Contains("range: bytes=7-\r\n", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromZero_SendsNoRange()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok("all"));

        TransferResult result = await RunAsync(connector, new MemoryStream(), resumeFrom: 0);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.DoesNotContain("range:", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered416_HasNothingLeft()
    {
        QueuedS3Connector connector = new("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 0\r\n\r\n");

        TransferResult result = await RunAsync(connector, new MemoryStream(), resumeFrom: 7);

        Assert.AreEqual((CurlExitCode.Ok, 0L), (result.ExitCode, result.BytesTransferred));
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered200OfTheHeldSize_HasNothingLeft()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok("1234567"));
        MemoryStream output = new();

        TransferResult result = await RunAsync(connector, output, resumeFrom: 7);

        Assert.AreEqual((CurlExitCode.Ok, 0L), (result.ExitCode, result.BytesTransferred));
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered200OfAnotherSize_FailsWithRangeError()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok("whole object"));

        TransferResult result = await RunAsync(connector, new MemoryStream(), resumeFrom: 7);

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
    }

    private static async Task<TransferResult> RunAsync(QueuedS3Connector connector, Stream output, long? resumeFrom = null, string? rangeText = null)
    {
        S3ProtocolHandler handler = new(connector, name => name == "AWS_ENDPOINT_URL" ? "http://minio:9000" : null, 5 * 1024 * 1024);
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("s3://bucket/big.bin"),
            Output = output,
            Credentials = new NetworkCredential("AKID", "secret"),
            ResumeFrom = resumeFrom,
            RangeText = rangeText,
            TimeProvider = new FixedTimeProvider(),
        });
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);
    }
}
