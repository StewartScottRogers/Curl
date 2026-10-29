namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferContextOutputsTests
{
    [TestMethod]
    public void WithOutputs_GivesTheContextWithOnlyItsOutputsReplaced()
    {
        TransferContext own = Context();
        MemoryStream output = new();
        MemoryStream headerOutput = new();

        ITransferContext given = own.WithOutputs(output, headerOutput, null);

        Assert.AreSame(output, given.Output);
        Assert.AreSame(headerOutput, given.HeaderOutput);
        Assert.IsNull(given.DumpHeaderOutput);
        Assert.AreSame(own.Http, given.Http);
        Assert.AreSame(own.Url, given.Url);
    }

    [TestMethod]
    public void WithOutputs_WithoutAContextOrOutput_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TransferContextOutputs.WithOutputs(null!, new MemoryStream(), null, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => Context().WithOutputs(null!, null, null));
    }

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("http://example.com/page"),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = new HttpRequestOptions { Headers = ["X-Curl: own"] },
    };
}
