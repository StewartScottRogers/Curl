using System.Text;
using Curl.Extension.WebDavTools.Propfind;
using Curl.Extension.WebDavTools.Transfer;

namespace Curl.Extension.WebDavTools.UnitTests.Transfer;

[TestClass]
public sealed class WebDavListTransferHookTests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.com/dav/");

    [TestMethod]
    public async Task ExecuteAsync_SendsAPropfindWithDepth1AndThePropBody_Rfc4918Sections9_1And10_2()
    {
        var origin = new DavOrigin();
        origin.Responses["PROPFIND /dav/"] = (207, DavOrigin.Multistatus(DavOrigin.Collection("/dav/")));

        await new WebDavListTransferHook().ExecuteAsync(Context(new MemoryStream()), origin.Invoke);

        var request = origin.Requests.Single();
        Assert.AreEqual("PROPFIND", request.Http!.CustomMethod);
        Assert.AreEqual("1", DavOrigin.Field(request, "Depth"));
        Assert.AreEqual("application/xml; charset=utf-8", DavOrigin.Field(request, "Content-Type"));
        Assert.AreEqual(WebDavPropfindRequest.Body, Encoding.UTF8.GetString(request.PostData!.Value.Span));
        Assert.AreEqual(Url, request.Url);
    }

    [TestMethod]
    public async Task ExecuteAsync_A207Multistatus_WritesOneTabSeparatedLinePerResponse_Rfc4918Section13()
    {
        var origin = new DavOrigin();
        origin.Responses["PROPFIND /dav/"] = (207, DavOrigin.Multistatus(
            DavOrigin.Collection("/dav/"),
            DavOrigin.File("/dav/a%20b.txt", 12, "Wed, 28 Oct 2026 10:00:00 GMT"),
            DavOrigin.File("/dav/c.txt", 3, null)));
        var output = new MemoryStream();
        var headers = new MemoryStream();

        var result = await new WebDavListTransferHook().ExecuteAsync(Context(output, headers), origin.Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(207, result.Report!.ResponseCode);
        Assert.AreEqual(
            "collection\t-\t-\t/dav/\nfile\t12\tWed, 28 Oct 2026 10:00:00 GMT\t/dav/a b.txt\nfile\t3\t-\t/dav/c.txt\n",
            Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("HTTP/1.1 207 X\r\n\r\n", Encoding.ASCII.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnAnswerThatIsNot207_FailsWithExit22AndWritesNothing()
    {
        var output = new MemoryStream();

        var result = await new WebDavListTransferHook().ExecuteAsync(Context(output), new DavOrigin().Invoke);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The server answered PROPFIND with 404, not 207 Multistatus", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ASuccessWithNoReport_FailsWithExit22()
    {
        var result = await new WebDavListTransferHook().ExecuteAsync(Context(new MemoryStream()), _ => ValueTask.FromResult(TransferResult.Success(0)));

        Assert.AreEqual("The server answered PROPFIND with 0, not 207 Multistatus", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_A207BodyTheReaderRefuses_FailsWithExit8()
    {
        var origin = new DavOrigin();
        origin.Responses["PROPFIND /dav/"] = (207, "<not-multistatus/>");

        var result = await new WebDavListTransferHook().ExecuteAsync(Context(new MemoryStream()), origin.Invoke);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "The 207 Multistatus body was refused: ");
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedTransfer_GivesItsResult()
    {
        var origin = new DavOrigin();
        var failure = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");
        origin.Failures["PROPFIND /dav/"] = failure;

        Assert.AreSame(failure, await new WebDavListTransferHook().ExecuteAsync(Context(new MemoryStream()), origin.Invoke));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_FailsWithExit1WithoutContactingTheServer()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://example.com/dav/"), Output = Stream.Null };

        var result = await new WebDavListTransferHook().ExecuteAsync(context, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(WebDavCollectionListing.NotHttpMessage, result.ErrorMessage);
    }

    private static TransferContext Context(Stream output, Stream? headers = null) =>
        new() { Url = Url, Output = output, HeaderOutput = headers, Http = new HttpRequestOptions() };
}
