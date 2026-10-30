namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class SignedCertificateTimestampTests
{
    [TestMethod]
    public void Parse_Rfc6962Section3_2_Version1_ReadsEveryField()
    {
        byte[] serialized = [0, .. Enumerable.Repeat((byte)7, 32), 0, 0, 0, 0, 0, 0, 1, 2, 0, 1, 9, 4, 3, 0, 2, 0xAB, 0xCD];

        var timestamp = SignedCertificateTimestamp.Parse(serialized)!;

        CollectionAssert.AreEqual(Enumerable.Repeat((byte)7, 32).ToArray(), timestamp.LogId.ToArray());
        Assert.AreEqual(258UL, timestamp.Timestamp);
        CollectionAssert.AreEqual(new byte[] { 9 }, timestamp.Extensions.ToArray());
        Assert.AreEqual((byte)4, timestamp.HashAlgorithm);
        Assert.AreEqual((byte)3, timestamp.SignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, timestamp.Signature.ToArray());
    }

    [TestMethod]
    public void Parse_Rfc6962Section3_2_BytesPastTheSignature_Throws()
    {
        byte[] serialized = [0, .. new byte[32], 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 3, 0, 0, 0xFF];

        Assert.ThrowsExactly<FormatException>(() => SignedCertificateTimestamp.Parse(serialized));
    }

    [TestMethod]
    public void ParseList_Rfc6962Section3_3_BytesPastTheList_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => SignedCertificateTimestamp.ParseList(new byte[] { 0, 0, 0 }));
    }

    [TestMethod]
    public void ParseList_Rfc6962Section3_3_EmptyList_GivesNone()
    {
        Assert.IsEmpty(SignedCertificateTimestamp.ParseList(new byte[] { 0, 0 }));
    }

    [TestMethod]
    public void SignedDataFor_Rfc6962Section3_2_LaysOutThePrecertificateEntry()
    {
        var timestamp = new SignedCertificateTimestamp(new byte[32], 0x0102, new byte[] { 0xEE }, 4, 3, Array.Empty<byte>());

        var data = timestamp.SignedDataFor([0xAA, 0xBB], [0x30, 0x00]);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 0, 1, 0xAA, 0xBB, 0, 0, 2, 0x30, 0x00, 0, 1, 0xEE }, data);
    }
}
