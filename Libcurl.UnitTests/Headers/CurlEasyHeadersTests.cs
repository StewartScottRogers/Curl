using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Libcurl.Easy;
using Libcurl.Headers;
using Libcurl.Options;

namespace Libcurl.UnitTests.Headers;

[TestClass]
public sealed class CurlEasyHeadersTests
{
    private static readonly CurlHeader ContentType = new("Content-Type", "text/plain", 1, 0, CurlHeaderOrigin.Header);

    private const CurlHeaderOrigin MarkerBit = (CurlHeaderOrigin)(1 << 27);

    private static (CurlEasy Easy, FakeTransferHeaders Headers) NewHandle()
    {
        var transfer = new FakeHeadersTransfer();
        var easy = new CurlEasy(transfer, new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>()));
        return (easy, transfer.FakeHeaders);
    }

    [TestMethod]
    public void Header_Ok_AnswersTheEnginesHeaderAndPassesEveryArgument()
    {
        var (easy, headers) = NewHandle();
        headers.TryGetResult = CurlHeaderCode.Ok;
        headers.Header = ContentType;

        var code = easy.Header("content-type", 2, CurlHeaderOrigin.Header | CurlHeaderOrigin.Trailer, 1, out var header);

        Assert.AreEqual(CurlHeaderCode.Ok, code);
        Assert.AreEqual(ContentType with { Origin = CurlHeaderOrigin.Header | MarkerBit }, header);
        Assert.AreEqual(("content-type", 2, CurlHeaderOrigin.Header | CurlHeaderOrigin.Trailer, 1), headers.LastTryGet);
    }

    [TestMethod]
    [DataRow(CurlHeaderCode.BadIndex)]
    [DataRow(CurlHeaderCode.Missing)]
    [DataRow(CurlHeaderCode.NoHeaders)]
    [DataRow(CurlHeaderCode.NoRequest)]
    [DataRow(CurlHeaderCode.BadArgument)]
    public void Header_EngineFails_AnswersItsCodeAndNoHeader(CurlHeaderCode result)
    {
        var (easy, headers) = NewHandle();
        headers.TryGetResult = result;

        var code = easy.Header("Server", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out var header);

        Assert.AreEqual(result, code);
        Assert.IsNull(header);
    }

    [TestMethod]
    public void Header_NullName_AnswersBadArgumentWithoutTheEngine()
    {
        var (easy, headers) = NewHandle();

        var code = easy.Header(null, 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out var header);

        Assert.AreEqual(CurlHeaderCode.BadArgument, code);
        Assert.IsNull(header);
        Assert.AreEqual(0, headers.CallCount);
    }

    [TestMethod]
    public void Header_NullHandle_AnswersBadArgument()
    {
        var code = ((CurlEasy?)null).Header("Server", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out var header);

        Assert.AreEqual(CurlHeaderCode.BadArgument, code);
        Assert.IsNull(header);
    }

    [TestMethod]
    public void Header_CleanedUpHandle_AnswersBadArgumentWithoutTheEngine()
    {
        var (easy, headers) = NewHandle();
        easy.Dispose();

        var code = easy.Header("Server", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out var header);

        Assert.AreEqual(CurlHeaderCode.BadArgument, code);
        Assert.IsNull(header);
        Assert.AreEqual(0, headers.CallCount);
    }

    [TestMethod]
    public void NextHeader_AnswersTheEnginesHeaderAndPassesEveryArgument()
    {
        var (easy, headers) = NewHandle();
        headers.Header = ContentType;
        var previous = new CurlHeader("Server", "x", 1, 0, CurlHeaderOrigin.Header);

        var next = easy.NextHeader(CurlHeaderOrigin.Informational, 0, previous);

        Assert.AreEqual(ContentType with { Origin = CurlHeaderOrigin.Header | MarkerBit }, next);
        Assert.AreEqual((CurlHeaderOrigin.Informational, 0, previous), headers.LastNext);
    }

    [TestMethod]
    public void NextHeader_PreviousCarriesTheMarkerBit_PassesTheEnginesOwnHeaderBack()
    {
        var (easy, headers) = NewHandle();
        headers.Header = ContentType;
        var first = easy.NextHeader(CurlHeaderOrigin.Header, 0, null);

        _ = easy.NextHeader(CurlHeaderOrigin.Header, 0, first);

        Assert.AreSame(ContentType, headers.LastNext!.Value.Previous);
    }

    [TestMethod]
    public void NextHeader_NoMore_AnswersNull()
    {
        var (easy, _) = NewHandle();

        Assert.IsNull(easy.NextHeader(CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, null));
    }

    [TestMethod]
    public void NextHeader_NullHandle_AnswersNull()
    {
        Assert.IsNull(((CurlEasy?)null).NextHeader(CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, null));
    }

    [TestMethod]
    public void NextHeader_CleanedUpHandle_AnswersNullWithoutTheEngine()
    {
        var (easy, headers) = NewHandle();
        headers.Header = ContentType;
        easy.Dispose();

        Assert.IsNull(easy.NextHeader(CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, null));
        Assert.AreEqual(0, headers.CallCount);
    }
}
