namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferContextRequestSigningTests
{
    private static readonly CurlUrl Other = CurlUrl.Parse("http://example.com/other");

    [TestMethod]
    public void SignedHeadersFor_NoSignerOnTheTransfer_GivesTheHeadersThemselves()
    {
        var context = Context().WithRequestHeaders(["X-A: 1"]);
        IReadOnlyList<string> headers = ["X-A: 1"];

        var signed = context.SignedHeadersFor("POST", Other, headers);

        Assert.AreSame(headers, signed);
    }

    [TestMethod]
    public void SignedHeadersFor_SignersUnderOtherWrappers_AppliesEachWithTheRequestsMethodAndUrl()
    {
        var own = Context();
        var context = own
            .WithRequestSigner((method, url, headers) => [.. headers, $"Inner: {method} {url.AbsolutePath}"])
            .WithRequest(own.Url, "PUT", null)
            .WithRequestSigner((_, _, headers) => [.. headers, "Outer: 1"])
            .WithRequestHeaders(["X-A: 1"]);

        var signed = context.SignedHeadersFor("POST", Other, ["X-A: 1"]);

        CollectionAssert.AreEqual(new[] { "X-A: 1", "Outer: 1", "Inner: POST /other" }, signed.ToArray());
    }

    [TestMethod]
    public void WithRequestSigner_ATransfer_ChangesNothingElseAndWrapsIt()
    {
        var context = Context();

        var signing = context.WithRequestSigner((_, _, headers) => headers);

        Assert.AreSame(context, ((IWrappingTransferContext)signing).Inner);
        Assert.AreSame(context.Http, signing.Http);
        Assert.AreSame(context.Output, signing.Output);
    }

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("http://example.com/own"),
        Output = new MemoryStream(),
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions(),
    };
}
