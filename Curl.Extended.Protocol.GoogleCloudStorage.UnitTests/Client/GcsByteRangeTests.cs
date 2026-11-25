using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// <c>-r</c> and <c>-C</c> on <c>gs://</c> (ADR-1354): an object download sends a <c>Range</c> header, as Cloud
/// Storage's "GET Object" takes it, and an upload or a listing refuses them before any request.
/// </summary>
[TestClass]
public sealed class GcsByteRangeTests
{
    private const string Emulator = "http://emulator:4443";
    private const string Whole = "0123456789abcdefghijklmnopqrst";

    [TestMethod]
    public async Task ExecuteAsync_Range_SendsTheRangeHeaderAndWritesOnlyThoseBytes()
    {
        QueuedGcsConnector connector = new(Partial("abcdefghij"));
        MemoryStream output = new();

        TransferResult result = await Run(connector, Context(output, rangeText: "10-19"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10, result.BytesTransferred);
        Assert.AreEqual("abcdefghij", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("GET /bucket/k HTTP/1.1\r\nhost: emulator:4443\r\nrange: bytes=10-19\r\n\r\n", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeWithHmacKey_SignsTheRangeHeader()
    {
        QueuedGcsConnector connector = new(Partial("abcdefghij"));

        await Run(connector, Context(new MemoryStream(), rangeText: "10-19", credentials: new NetworkCredential("GOOGID", "secret")));

        Assert.Contains("range: bytes=10-19\r\n", connector.Connections[0].Written);
        Assert.Contains("SignedHeaders=host;range;x-goog-content-sha256;x-goog-date, Signature=", connector.Connections[0].Written);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("0-4")]
    public async Task ExecuteAsync_Resume_AppendsTheRestSoThePartialFileMatchesTheObject(string? rangeText)
    {
        QueuedGcsConnector connector = new(Partial(Whole[10..]));
        MemoryStream output = new();
        output.Write(Encoding.UTF8.GetBytes(Whole[..10]));

        TransferResult result = await Run(connector, Context(output, resumeFrom: 10, rangeText: rangeText));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(20, result.BytesTransferred);
        Assert.AreEqual(Whole, Encoding.UTF8.GetString(output.ToArray()));
        Assert.Contains("\r\nrange: bytes=10-\r\n", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromOffsetZero_SendsNoRangeAndWritesTheWholeObject()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok(Whole));
        MemoryStream output = new();

        TransferResult result = await Run(connector, Context(output, resumeFrom: 0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Whole, Encoding.UTF8.GetString(output.ToArray()));
        Assert.DoesNotContain("range:", connector.Connections[0].Written);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 0\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789")]
    public async Task ExecuteAsync_ResumeWithNothingLeft_SucceedsAndWritesNothing(string response)
    {
        QueuedGcsConnector connector = new(response);
        MemoryStream output = new();

        TransferResult result = await Run(connector, Context(output, resumeFrom: 10));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.BytesTransferred);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 11\r\n\r\n0123456789a")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n0123456789")]
    public async Task ExecuteAsync_ResumeAnsweredWithTheWholeObject_FailsWithExit33AndWritesNothing(string response)
    {
        QueuedGcsConnector connector = new(response);
        MemoryStream output = new();

        TransferResult result = await Run(connector, Context(output, resumeFrom: 10));

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
        Assert.AreEqual("HTTP server does not seem to support byte ranges. Cannot resume.", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeNotSatisfiable_FailsWithExit22()
    {
        QueuedGcsConnector connector = new("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 0\r\n\r\n");

        TransferResult result = await Run(connector, Context(new MemoryStream(), rangeText: "99-"));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Cloud Storage returned error 416", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("gs://bucket/k", true, "0-9", null, false, "-r/--range")]
    [DataRow("gs://bucket/k", true, null, 5L, false, "-C/--continue-at")]
    [DataRow("gs://bucket/k", true, null, null, true, "-C/--continue-at")]
    [DataRow("gs://bucket/", false, "0-9", null, false, "-r/--range")]
    [DataRow("gs://bucket/dir/", false, null, 5L, false, "-C/--continue-at")]
    public async Task ExecuteAsync_ByteRangeOnAnUploadOrListing_FailsWithExit4BeforeAnyConnection(
        string url, bool upload, string? rangeText, long? resumeFrom, bool unknownOffset, string option)
    {
        QueuedGcsConnector connector = new();
        TransferContext context = Context(new MemoryStream(), url, rangeText, resumeFrom, upload: upload ? new MemoryStream() : null, unknownOffset: unknownOffset);

        TransferResult result = await Run(connector, context);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual($"gs:// does not support {option} on an upload or a listing; only an object download takes a byte range", result.ErrorMessage);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public void RefusalOf_ParsedRangeWithoutItsText_IsRefused()
    {
        TransferContext context = new() { Url = CurlUrl.Parse("gs://bucket/"), Output = new MemoryStream(), Range = ByteRange.Bounded(0, 9) };

        Assert.IsNotNull(GcsByteRangeRefusal.RefusalOf(context));
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingResumedFromOffsetZero_Lists()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok("<ListBucketResult><IsTruncated>false</IsTruncated></ListBucketResult>"));

        TransferResult result = await Run(connector, Context(new MemoryStream(), "gs://bucket/", resumeFrom: 0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
    }

    private static string Partial(string body) =>
        $"HTTP/1.1 206 Partial Content\r\nContent-Length: {body.Length}\r\n\r\n{body}";

    private static ValueTask<TransferResult> Run(QueuedGcsConnector connector, TransferContext context) =>
        new GcsProtocolHandler(connector, _ => Emulator).ExecuteAsync(context);

    private static TransferContext Context(
        Stream output,
        string url = "gs://bucket/k",
        string? rangeText = null,
        long? resumeFrom = null,
        NetworkCredential? credentials = null,
        Stream? upload = null,
        bool unknownOffset = false) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            RangeText = rangeText,
            ResumeFrom = resumeFrom,
            Credentials = credentials,
            Upload = upload,
            ResumeUploadFromUnknownOffset = unknownOffset,
        };
}
