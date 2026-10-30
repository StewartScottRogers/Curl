namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class CapturedResponseTests
{
    [TestMethod]
    public void Code_AFailedTransferOrOneWithoutAReport_IsZero()
    {
        Assert.AreEqual(0, new CapturedResponse(TransferResult.Failure(CurlExitCode.CouldntConnect, "refused"), [], [], []).Code);
        Assert.AreEqual(0, new CapturedResponse(TransferResult.Success(0), [], [], []).Code);
    }

    [TestMethod]
    public async Task CaptureAsync_AContextDumpingHeaders_CapturesTheDumpedHeadersToo()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null, HeaderOutput = Stream.Null, DumpHeaderOutput = new MemoryStream() };

        var captured = await CapturedResponse.CaptureAsync(context, async capturing =>
        {
            await capturing.DumpHeaderOutput!.WriteAsync("HTTP/1.1 200 OK\r\n"u8.ToArray());
            return TransferResult.Success(0);
        });

        CollectionAssert.AreEqual("HTTP/1.1 200 OK\r\n"u8.ToArray(), captured.DumpedHeaders);
        Assert.AreEqual(0, context.DumpHeaderOutput.Length);
    }
}
