using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Libcurl.Info;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-248: <c>CURLINFO_HTTPAUTH_USED</c> is 64 for a Bearer request and 128 for an AWS SigV4 one, as libcurl 8.22.0 answers.
/// </summary>
[TestClass]
public sealed class Cgr248Tests
{
    private static object? Read(TransferInfo info, EasyOptions options) =>
        EasyInfoReaders.Create()[CurlInfo.HttpauthUsed](info, options);

    private static TransferInfo Sent(long used = 0) => new() { RequestSize = 100, HttpAuthUsed = used };

    [TestMethod]
    public void HttpauthUsed_BearerTokenSet_Is64() =>
        Assert.AreEqual(64L, Read(Sent(), new EasyOptions { Authentication = new EasyAuthenticationOptions { XOAuth2Bearer = "tok" } }));

    [TestMethod]
    public void HttpauthUsed_AwsSigV4Set_Is128() =>
        Assert.AreEqual(128L, Read(Sent(), new EasyOptions { Authentication = new EasyAuthenticationOptions { AwsSigV4 = "aws:amz:us-east-1:s3" } }));

    [TestMethod]
    public void HttpauthUsed_NothingSet_Is0() => Assert.AreEqual(0L, Read(Sent(), new EasyOptions()));

    [TestMethod]
    public void HttpauthUsed_EngineReportedBasic_StaysBasic() =>
        Assert.AreEqual(1L, Read(Sent(1), new EasyOptions { Authentication = new EasyAuthenticationOptions { XOAuth2Bearer = "tok" } }));

    [TestMethod]
    public void HttpauthUsed_NoRequestSent_Is0() =>
        Assert.AreEqual(0L, Read(new TransferInfo(), new EasyOptions { Authentication = new EasyAuthenticationOptions { XOAuth2Bearer = "tok" } }));
}
