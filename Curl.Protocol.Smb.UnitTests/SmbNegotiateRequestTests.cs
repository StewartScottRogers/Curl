namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbNegotiateRequest" /> to the bytes curl sends, measured on 2026-09-29.
/// </summary>
[TestClass]
public sealed class SmbNegotiateRequestTests
{
    [TestMethod]
    public void Encode_IsCurlsMeasuredBytes()
    {
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, SmbNegotiateRequest.Encode());
    }

}
