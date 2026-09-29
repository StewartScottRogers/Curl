namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbReadRequest" /> against the measured read, and the offset past 4 GiB
/// that curl splits into its low and high words.
/// </summary>
[TestClass]
public sealed class SmbReadRequestTests
{
    [TestMethod]
    public void Encode_FirstRead_IsCurlsBytes()
    {
        byte[] request = SmbReadRequest.Encode(0x0064, 0x0007, 0x4001, 0);

        CollectionAssert.AreEqual(SmbRecordedExchange.ReadRequest, request);
    }

    [TestMethod]
    public void Encode_OffsetPast4GiB_SplitsItIntoTheLowAndHighWords()
    {
        byte[] request = SmbReadRequest.Encode(0x0064, 0x0007, 0x4001, 0x1_2345_8000);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x80, 0x45, 0x23 }, request[43..47]);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }, request[57..61]);
    }
}
