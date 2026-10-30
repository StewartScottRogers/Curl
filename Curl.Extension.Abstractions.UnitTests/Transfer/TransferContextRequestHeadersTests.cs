namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferContextRequestHeadersTests
{
    [TestMethod]
    public void WithRequestHeaders_HttpTransfer_AddsTheHeadersAfterItsOwn()
    {
        MemoryStream output = new();
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = output, Http = new HttpRequestOptions { Headers = ["X-Curl: own"] } };

        ITransferContext given = own.WithRequestHeaders(["Authorization: Bearer token"]);

        CollectionAssert.AreEqual(new[] { "X-Curl: own", "Authorization: Bearer token" }, given.Http!.Headers.ToArray());
        Assert.AreSame(output, given.Output);
        Assert.AreSame(own.Url, given.Url);
        CollectionAssert.AreEqual(new[] { "X-Curl: own" }, own.Http.Headers.ToArray());
    }

    [TestMethod]
    public void WithRequestHeaders_TransferThatIsNotHttp_GivesTheContextItself()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("ftp://example.com/"), Output = new MemoryStream() };

        ITransferContext given = own.WithRequestHeaders(["Authorization: Bearer token"]);

        Assert.AreSame(own, given);
    }

    [TestMethod]
    public void WithRequestHeaders_WithoutAContextOrHeaders_Throws()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream() };

        Assert.ThrowsExactly<ArgumentNullException>(() => TransferContextRequestHeaders.WithRequestHeaders(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => own.WithRequestHeaders(null!));
    }
}
