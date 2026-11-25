using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;

namespace Curl.Engine;

[TestClass]
public sealed class TransferHeaderStoreHttp2TrailerTests
{
    private const int Last = ITransferHeaders.LastRequest;

    [TestMethod]
    public void RecordLine_Http2TrailerAfterTheHeadsEmptyLine_IsATrailerAndNotAHeader()
    {
        TransferHeaderStore store = new();
        foreach (string line in new[] { "HTTP/2 200 ", "x-h: head", string.Empty, "x-t: trailerval" })
        {
            store.RecordLine(line);
        }

        Assert.AreEqual(CurlHeaderCode.Missing, store.Find("x-t", 0, CurlHeaderOrigin.Header, Last, out _));
        Assert.AreEqual(CurlHeaderCode.Ok, store.Find("x-t", 0, CurlHeaderOrigin.Trailer, Last, out CurlHeader? trailer));
        Assert.AreEqual("trailerval", trailer!.Value);
        Assert.AreEqual(CurlHeaderCode.Ok, store.Find("x-h", 0, CurlHeaderOrigin.Header, Last, out _));
    }
}
