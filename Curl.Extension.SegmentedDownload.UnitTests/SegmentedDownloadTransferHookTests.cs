using System.Text;

namespace Curl.Extension.SegmentedDownload.UnitTests;

[TestClass]
public sealed class SegmentedDownloadTransferHookTests
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.com/file");

    [TestMethod]
    public async Task ExecuteAsync_APlainGet_AsksForTheFirstByteThenForEachRangeConditionalOnTheEntityTag_Rfc9110Sections14_2And13_1_5()
    {
        var origin = new RangeOrigin(Alphabet);
        var context = Context();

        var result = await new SegmentedDownloadTransferHook(3).ExecuteAsync(context, origin.Invoke);

        var requests = origin.Requests.ToArray();
        Assert.HasCount(4, requests);
        CollectionAssert.AreEqual(new[] { "Range: bytes=0-0" }, requests[0].Http!.Headers.ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "bytes=0-8", "bytes=9-17", "bytes=18-25" },
            requests.Skip(1).Select(request => RangeOrigin.Field(request, "Range")).ToArray());
        Assert.IsTrue(requests.Skip(1).All(request => RangeOrigin.Field(request, "If-Range") == "\"v1\""));
        Assert.AreEqual(Alphabet, Text(context.Output));
        Assert.AreEqual("HTTP/1.1 206 X\r\nETag: \"v1\"\r\nContent-Range: bytes 0-8/26\r\n\r\n", Text(context.HeaderOutput!));
        Assert.AreEqual(Text(context.HeaderOutput!), Text(context.DumpHeaderOutput!));
        Assert.AreEqual(26, result.BytesTransferred);
        Assert.AreEqual(206, result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("W/\"v1\"")]
    public async Task ExecuteAsync_NoStrongEntityTag_SendsNoIfRange_Rfc9110Section13_1_5(string? tag)
    {
        var origin = new RangeOrigin(Alphabet) { ETag = tag };
        var context = Context(dumpHeaders: false);

        await new SegmentedDownloadTransferHook(2).ExecuteAsync(context, origin.Invoke);

        Assert.IsTrue(origin.Requests.All(request => RangeOrigin.Field(request, "If-Range") is null));
        Assert.AreEqual(Alphabet, Text(context.Output));
        Assert.IsNull(context.DumpHeaderOutput);
    }

    [TestMethod]
    public async Task ExecuteAsync_AServerThatIgnoresRange_IsWrittenAsReceivedFromOneRequest_Rfc9110Section14_2()
    {
        var origin = new RangeOrigin(Alphabet) { IgnoresRange = true };
        var context = Context();

        var result = await new SegmentedDownloadTransferHook(4).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(1, origin.Requests);
        Assert.AreEqual(Alphabet, Text(context.Output));
        Assert.AreEqual("HTTP/1.1 200 X\r\nETag: \"v1\"\r\n\r\n", Text(context.HeaderOutput!));
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow(416, "bytes */0")]
    [DataRow(206, "bytes 0-0/*")]
    public async Task ExecuteAsync_AFirstResponseWithoutAKnownLength_RunsTheTransferAsCurlDoes_Rfc9110Section14_4(int code, string contentRange)
    {
        var origin = new RangeOrigin(Alphabet)
        {
            Rewrite = (range, response) => range is null ? response : response with { Code = code, Fields = [new("Content-Range", contentRange)], Body = string.Empty },
        };
        var context = Context();

        await new SegmentedDownloadTransferHook(4).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(2, origin.Requests);
        Assert.IsEmpty(origin.Requests.Last().Http!.Headers);
        Assert.AreEqual(Alphabet, Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedFirstRequest_RunsTheTransferAsCurlDoes()
    {
        var origin = new RangeOrigin(Alphabet)
        {
            Rewrite = (range, response) => range is null ? response : response with { Result = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused") },
        };

        await new SegmentedDownloadTransferHook(4).ExecuteAsync(Context(), origin.Invoke);

        Assert.HasCount(2, origin.Requests);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFirstResponseWithoutAReport_RunsTheTransferAsCurlDoes()
    {
        var origin = new RangeOrigin(Alphabet)
        {
            Rewrite = (range, response) => range is null ? response : response with { Result = TransferResult.Success(1) },
        };

        await new SegmentedDownloadTransferHook(4).ExecuteAsync(Context(), origin.Invoke);

        Assert.HasCount(2, origin.Requests);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedSegment_FailsTheTransferWithNothingWritten()
    {
        var origin = new RangeOrigin(Alphabet)
        {
            Rewrite = (range, response) => range == "bytes=13-25" ? response with { Result = TransferResult.Failure(CurlExitCode.PartialFile, "cut") } : response,
        };
        var context = Context();

        var result = await new SegmentedDownloadTransferHook(2).ExecuteAsync(context, origin.Invoke);

        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual(0, context.Output.Length);
        Assert.AreEqual(0, context.HeaderOutput!.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARepresentationThatChangedUnderIfRange_WritesTheWhole200InPlaceOfTheParts_Rfc9110Section13_1_5()
    {
        var origin = new RangeOrigin(Alphabet);
        origin.Rewrite = (range, response) =>
        {
            origin.ETag = "\"v2\"";
            return response;
        };
        var context = Context();

        var result = await new SegmentedDownloadTransferHook(2).ExecuteAsync(context, origin.Invoke);

        Assert.AreEqual(Alphabet, Text(context.Output));
        Assert.AreEqual("HTTP/1.1 200 X\r\nETag: \"v2\"\r\n\r\n", Text(context.HeaderOutput!));
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow("code")]
    [DataRow("content-range")]
    [DataRow("length")]
    [DataRow("etag")]
    [DataRow("last-modified")]
    public async Task ExecuteAsync_ASegmentThatIsNotExactlyItsRangeOfTheSameRepresentation_RunsTheTransferAsCurlDoes_Rfc9110Section15_3_7(string defect)
    {
        var origin = new RangeOrigin(Alphabet);
        origin.Rewrite = (range, response) => range == "bytes=13-25" ? Spoil(response, defect) : response;
        var context = Context();

        await new SegmentedDownloadTransferHook(2).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(4, origin.Requests);
        Assert.IsEmpty(origin.Requests.Last().Http!.Headers);
        Assert.AreEqual(Alphabet, Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_OneSegment_RunsTheTransferAsCurlDoes()
    {
        var origin = new RangeOrigin(Alphabet);
        var context = Context();

        await new SegmentedDownloadTransferHook(1).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(1, origin.Requests);
        Assert.AreSame(context, origin.Requests.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotAPlainGetOfTheWholeResource_RunsAsCurlRunsIt()
    {
        TransferContext[] contexts =
        [
            new() { Url = Url, Output = Stream.Null },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions { CustomMethod = "PUT" } },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions { Body = new BytesBody(new byte[] { 1 }, "text/plain") } },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), PostData = new byte[] { 1 } },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), PostWithoutData = true },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), Upload = Stream.Null },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), NoBody = true },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), Range = ByteRange.Bounded(0, 1) },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), ResumeFrom = 3 },
            new() { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions(), TimeCondition = new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfModifiedSince) },
        ];

        foreach (var context in contexts)
        {
            ITransferContext? sent = null;

            await new SegmentedDownloadTransferHook(4).ExecuteAsync(context, request =>
            {
                sent = request;
                return ValueTask.FromResult(TransferResult.Success(0));
            });

            Assert.AreSame(context, sent);
        }
    }

    private static RangeOrigin.Response Spoil(RangeOrigin.Response response, string defect) => defect switch
    {
        "code" => response with { Code = 299 },
        "content-range" => response with { Fields = [.. response.Fields.Where(field => field.Key != "Content-Range"), new("Content-Range", "bytes 13-24/26")] },
        "length" => response with { Body = response.Body[1..] },
        "etag" => response with { Fields = [.. response.Fields.Where(field => field.Key != "ETag"), new("ETag", "\"v9\"")] },
        _ => response with { Fields = [.. response.Fields, new("Last-Modified", "Wed, 28 Oct 2026 12:00:00 GMT")] },
    };

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static TransferContext Context(bool dumpHeaders = true) => new()
    {
        Url = Url,
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = dumpHeaders ? new MemoryStream() : null,
        Http = new HttpRequestOptions(),
    };


}
