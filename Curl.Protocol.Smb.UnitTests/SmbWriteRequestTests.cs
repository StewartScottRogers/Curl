using System.Text;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbWriteRequest" /> against the measured write, and the offset past 4 GiB
/// that curl splits into its low and high words.
/// </summary>
[TestClass]
public sealed class SmbWriteRequestTests
{
    [TestMethod]
    public void Encode_FirstWrite_IsCurlsBytes()
    {
        byte[] data = Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent);

        byte[] request = SmbWriteRequest.Encode(0x0064, 0x0007, 0x4001, 0, data.Length, data);

        CollectionAssert.AreEqual(SmbRecordedExchange.WriteRequest, request);
    }

    [TestMethod]
    public void Encode_FewerBytesThanDeclared_DeclaresTheLengthAndSendsWhatItHas()
    {
        byte[] request = SmbWriteRequest.Encode(0x0064, 0x0007, 0x4001, 5, 6, []);

        CollectionAssert.AreEqual(SmbRecordedExchange.ShortWriteRequest, request);
    }

    [TestMethod]
    public void Encode_OffsetPast4GiB_SplitsItIntoTheLowAndHighWords()
    {
        byte[] request = SmbWriteRequest.Encode(0x0064, 0x0007, 0x4001, 0x1_2345_8000, 0, []);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x80, 0x45, 0x23 }, request[43..47]);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }, request[61..65]);
    }
}
