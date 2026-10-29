namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-196: a transfer URL of exactly 8,000,000 bytes fails as out of memory and a longer one as a bad
/// function argument, as curl 8.22.0 does, not as a malformed URL.
/// </summary>
[TestClass]
public sealed class Cgr196Tests
{
    private const string Prefix = "http://127.0.0.1:20200/?";

    private static string UrlOfLength(int length) => Prefix + new string('a', length - Prefix.Length);

    [TestMethod]
    public void TransferLengthRejection_UrlOfExactlyTheLimit_IsAtLengthLimit()
    {
        CurlUrlRejection? rejection = CurlUrl.TransferLengthRejection(UrlOfLength(8_000_000));

        Assert.AreEqual(CurlUrlRejection.UrlAtLengthLimit, rejection);
        Assert.AreEqual("Out of memory", rejection!.Value.ToCurlMessage());
    }

    [TestMethod]
    public void TransferLengthRejection_UrlAboveTheLimit_IsTooLong()
    {
        CurlUrlRejection? rejection = CurlUrl.TransferLengthRejection(UrlOfLength(8_000_001));

        Assert.AreEqual(CurlUrlRejection.UrlTooLong, rejection);
        Assert.AreEqual("A libcurl function was given a bad argument", rejection!.Value.ToCurlMessage());
    }

    [TestMethod]
    public void TransferLengthRejection_UrlJustBelowTheLimit_HasNoLengthRejection() =>
        Assert.IsNull(CurlUrl.TransferLengthRejection(UrlOfLength(7_999_999)));
}
