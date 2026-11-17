using System.Text;

namespace Curl.Extension.SegmentedDownload.UnitTests;

[TestClass]
public sealed class SegmentedDownloadWeakValidatorTests
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    [TestMethod]
    public async Task ExecuteAsync_AWeakETag_SendsOnePlainGetAndWritesThatResponse_Rfc9110Section15_3_7_3()
    {
        var origin = new RangeOrigin(Alphabet) { ETag = "W/\"same\"", ChangesEveryRequest = true };
        var context = Context();

        await new SegmentedDownloadTransferHook(4).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(2, origin.Requests);
        Assert.IsEmpty(origin.Requests.Last().Http!.Headers);
        Assert.AreEqual("cdefghijklmnopqrstuvwxyzab", Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_OnlyALastModified_SendsOnePlainGetAndWritesThatResponse_Rfc9110Section15_3_7_3()
    {
        var origin = new RangeOrigin(Alphabet) { ETag = null, LastModified = "Wed, 28 Oct 2026 12:00:00 GMT", ChangesEveryRequest = true };
        var context = Context();

        await new SegmentedDownloadTransferHook(4).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(2, origin.Requests);
        Assert.IsEmpty(origin.Requests.Last().Http!.Headers);
        Assert.AreEqual("cdefghijklmnopqrstuvwxyzab", Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_AStrongETag_StillSendsEachRangeWithIfRange_Rfc9110Section13_1_5()
    {
        var origin = new RangeOrigin(Alphabet) { ETag = "\"s\"" };
        var context = Context();

        await new SegmentedDownloadTransferHook(4).ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(5, origin.Requests);
        Assert.AreEqual(4, origin.Requests.Count(request => RangeOrigin.Field(request, "If-Range") == "\"s\""));
        Assert.AreEqual(Alphabet, Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoDumpedHeaderOutput_JoinsTheSegmentsAndLeavesItUnset_Adr1087()
    {
        var origin = new RangeOrigin(Alphabet);
        var context = Context(dumpHeaders: false);

        await new SegmentedDownloadTransferHook(2).ExecuteAsync(context, origin.Invoke);

        Assert.AreEqual(Alphabet, Text(context.Output));
        Assert.IsNull(context.DumpHeaderOutput);
    }

    private static TransferContext Context(bool dumpHeaders = true) => new()
    {
        Url = CurlUrl.Parse("http://example.com/file"),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = dumpHeaders ? new MemoryStream() : null,
        Http = new HttpRequestOptions(),
    };

    private static string Text(Stream stream) => Encoding.ASCII.GetString(((MemoryStream)stream).ToArray());
}
