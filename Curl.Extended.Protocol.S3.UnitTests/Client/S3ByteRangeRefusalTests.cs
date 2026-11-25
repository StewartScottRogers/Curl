using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>Checks that <c>-r</c> and <c>-C</c> are refused where an <c>s3://</c> transfer cannot send them.</summary>
[TestClass]
public sealed class S3ByteRangeRefusalTests
{
    private const string ObjectUrl = "s3://bucket/big.bin";
    private const string Why = " on an upload, list or delete; only a download sends a range";

    [TestMethod]
    [DataRow(ObjectUrl, true, null)]
    [DataRow("s3://bucket/logs/", false, null)]
    [DataRow("s3://bucket", false, null)]
    [DataRow(ObjectUrl, false, "DELETE")]
    public async Task ExecuteAsync_RangeTextWhereNoRangeIsSent_FailsBeforeConnecting(string url, bool upload, string? method)
    {
        await AssertRefusedAsync(Context(url, upload, method, rangeText: "0-9"), "-r/--range");
    }

    [TestMethod]
    [DataRow(ObjectUrl, true, null)]
    [DataRow("s3://bucket/logs/", false, null)]
    [DataRow(ObjectUrl, false, "DELETE")]
    public async Task ExecuteAsync_ResumeFromWhereNoRangeIsSent_FailsBeforeConnecting(string url, bool upload, string? method)
    {
        await AssertRefusedAsync(Context(url, upload, method, resumeFrom: 7), "-C/--continue-at");
    }

    [TestMethod]
    public async Task ExecuteAsync_ParsedRangeOnAnUpload_FailsBeforeConnecting()
    {
        await AssertRefusedAsync(Context(ObjectUrl, upload: true, range: ByteRange.Bounded(0, 9)), "-r/--range");
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeUploadFromUnknownOffset_FailsBeforeConnecting()
    {
        await AssertRefusedAsync(Context(ObjectUrl, upload: true, resumeUnknown: true), "-C/--continue-at");
    }

    [TestMethod]
    [DataRow(true, null)]
    [DataRow(false, "DELETE")]
    public async Task ExecuteAsync_ResumeFromZeroWhereNoRangeIsSent_Transfers(bool upload, string? method)
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok(string.Empty));

        TransferResult result = await RunAsync(connector, Context(ObjectUrl, upload, method, resumeFrom: 0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, connector.Connections);
    }

    private static async Task AssertRefusedAsync(TransferContext context, string option)
    {
        QueuedS3Connector connector = new();

        TransferResult result = await RunAsync(connector, context);

        Assert.AreEqual((CurlExitCode.NotBuiltIn, "s3:// does not support " + option + Why), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Connections);
    }

    private static async Task<TransferResult> RunAsync(QueuedS3Connector connector, TransferContext context)
    {
        S3ProtocolHandler handler = new(connector, name => name == "AWS_ENDPOINT_URL" ? "http://minio:9000" : null, 5 * 1024 * 1024);
        return await handler.ExecuteAsync(context);
    }

    private static TransferContext Context(
        string url,
        bool upload = false,
        string? method = null,
        long? resumeFrom = null,
        string? rangeText = null,
        ByteRange? range = null,
        bool resumeUnknown = false) => new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload ? new MemoryStream([1, 2, 3]) : null,
            Http = method is null ? null : new Curl.Protocol.Abstractions.HttpRequestOptions { CustomMethod = method },
            Credentials = new NetworkCredential("AKID", "secret"),
            ResumeFrom = resumeFrom,
            RangeText = rangeText,
            Range = range,
            ResumeUploadFromUnknownOffset = resumeUnknown,
        };
}
