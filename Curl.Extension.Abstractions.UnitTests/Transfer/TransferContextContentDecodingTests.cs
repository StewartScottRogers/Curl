namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferContextContentDecodingTests
{
    [TestMethod]
    public void WithContentDecodingOff_CompressedHttpTransfer_TurnsCompressedOffAndKeepsTheRest()
    {
        MemoryStream output = new();
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = output, Http = new HttpRequestOptions { Compressed = true, Headers = ["X-Curl: own"] } };

        ITransferContext given = own.WithContentDecodingOff();

        Assert.IsFalse(given.Http!.Compressed);
        CollectionAssert.AreEqual(new[] { "X-Curl: own" }, given.Http.Headers.ToArray());
        Assert.AreSame(output, given.Output);
        Assert.IsTrue(own.Http.Compressed);
    }

    [TestMethod]
    public void WithContentDecodingOff_TransferThatIsNotHttp_GivesTheContextItself()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("ftp://example.com/"), Output = new MemoryStream() };

        Assert.AreSame(own, own.WithContentDecodingOff());
        Assert.ThrowsExactly<ArgumentNullException>(() => TransferContextContentDecoding.WithContentDecodingOff(null!));
    }
}
