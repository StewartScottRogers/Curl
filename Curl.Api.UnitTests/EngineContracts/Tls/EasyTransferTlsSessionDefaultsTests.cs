using Curl.Api.Cancellation;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Tls;

namespace Curl.Api.EngineContracts.Tls;

// IEasyTransfer's TLS session members default to CURLE_NOT_BUILT_IN, as a libcurl without SSLS-EXPORT answers
// curl_easy_ssls_import and curl_easy_ssls_export (BL-2786).
[TestClass]
public sealed class EasyTransferTlsSessionDefaultsTests
{
    [TestMethod]
    public void ImportTlsSession_DefaultMember_AnswersNotBuiltIn()
    {
        IEasyTransfer easy = new CancellableEasyTransfer();

        CurlCode code = easy.ImportTlsSession("example.test:443", null, [0x01]);

        Assert.AreEqual(CurlCode.NotBuiltIn, code);
    }

    [TestMethod]
    public void ExportTlsSessions_DefaultMember_AnswersNotBuiltInAndCallsNothing()
    {
        IEasyTransfer easy = new CancellableEasyTransfer();
        int calls = 0;

        CurlCode code = easy.ExportTlsSessions(_ => { calls++; return CurlCode.Ok; });

        Assert.AreEqual(CurlCode.NotBuiltIn, code);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void ExportedTlsSession_Members_KeepWhatTheyWereGiven()
    {
        ExportedTlsSession session = new("h:443", [1], [2], 3, 0x0304, "h2", 16384);

        Assert.AreEqual("h:443", session.SessionKey);
        Assert.AreEqual(3, session.ValidUntil);
        Assert.AreEqual(0x0304, session.IetfTlsId);
        Assert.AreEqual("h2", session.Alpn);
        Assert.AreEqual(16384u, session.MaxEarlyData);
    }
}
