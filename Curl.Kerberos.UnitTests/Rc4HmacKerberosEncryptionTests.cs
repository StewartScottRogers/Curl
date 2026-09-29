using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// Checks <c>rc4-hmac</c> against RFC 4757: the section 2 string-to-key vector, the
/// section 5 message layout rebuilt from its primitives, a round trip with a known key,
/// the <c>hmac-md5</c> checksum and the PRF.
/// </summary>
[TestClass]
public sealed class Rc4HmacKerberosEncryptionTests
{
    // RFC 4757 section 2: String2Key("foo").
    private static readonly byte[] FooKey = Hex.Bytes("ac8e657f83df82beea5d43bdaf7800cc");

    private static readonly byte[] Confounder = Hex.Bytes("0102030405060708");

    [TestMethod]
    public void StringToKey_Rfc4757Section2Foo_MatchesVector()
    {
        CollectionAssert.AreEqual(FooKey, Rc4Hmac().StringToKey("foo", Encoding.UTF8.GetBytes("IGNORED.SALT"), [9, 9]));
    }

    [TestMethod]
    [DataRow(3, 8)]
    [DataRow(9, 8)]
    [DataRow(23, 13)]
    [DataRow(7, 7)]
    public void MessageType_KeyUsage_MapsAsMitKerberos(int usage, int messageType)
    {
        Assert.AreEqual(messageType, Rc4HmacKerberosEncryption.MessageType(usage));
    }

    [TestMethod]
    public void Encrypt_IsChecksumThenRc4OfConfounderAndPlaintext()
    {
        byte[] plaintext = Encoding.ASCII.GetBytes("rc4-hmac plaintext");
        byte[] messageType = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(messageType, 8);
        byte[] usageKey = HMACMD5.HashData(FooKey, messageType);
        byte[] body = [.. Confounder, .. plaintext];
        byte[] checksum = HMACMD5.HashData(usageKey, body);
        using (Rc4 rc4 = new(HMACMD5.HashData(usageKey, checksum)))
        {
            rc4.ApplyKeyStream(body, body);
        }

        byte[] ciphertext = Rc4Hmac().Encrypt(FooKey, 3, plaintext);

        CollectionAssert.AreEqual((byte[])[.. checksum, .. body], ciphertext);
    }

    [TestMethod]
    public void Decrypt_RoundTripWithFooKey_ReturnsPlaintext()
    {
        byte[] plaintext = Encoding.ASCII.GetBytes("a ticket's encrypted part");

        byte[] ciphertext = Rc4Hmac().Encrypt(FooKey, 2, plaintext);

        Assert.HasCount(16 + 8 + plaintext.Length, ciphertext);
        CollectionAssert.AreEqual(plaintext, Rc4Hmac().Decrypt(FooKey, 2, ciphertext));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(18)]
    [DataRow(30)]
    public void Decrypt_TamperedCiphertext_ThrowsIntegrityCheckFailed(int tamperedIndex)
    {
        byte[] ciphertext = Rc4Hmac().Encrypt(FooKey, 2, Encoding.ASCII.GetBytes("tampered message"));
        ciphertext[tamperedIndex] ^= 1;

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Rc4Hmac().Decrypt(FooKey, 2, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    [TestMethod]
    public void Decrypt_ShorterThanChecksumAndConfounder_ThrowsCiphertextTooShort()
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Rc4Hmac().Decrypt(FooKey, 2, new byte[23]));

        Assert.AreEqual(KerberosCryptographyError.CiphertextTooShort, exception.Error);
    }

    [TestMethod]
    public void ComputeChecksum_IsHmacMd5UnderTheSigningKey()
    {
        byte[] data = Encoding.ASCII.GetBytes("checksummed");
        byte[] signingKey = HMACMD5.HashData(FooKey, "signaturekey\0"u8);
        byte[] expected = HMACMD5.HashData(signingKey, MD5.HashData((byte[])[13, 0, 0, 0, .. data]));

        byte[] checksum = Rc4Hmac().ComputeChecksum(FooKey, 23, data);

        CollectionAssert.AreEqual(expected, checksum);
        Assert.IsTrue(Rc4Hmac().VerifyChecksum(FooKey, 23, data, checksum));
    }

    [TestMethod]
    public void ComputePseudoRandom_IsHmacSha1()
    {
        CollectionAssert.AreEqual(HMACSHA1.HashData(FooKey, "test"u8), Rc4Hmac().ComputePseudoRandom(FooKey, "test"u8));
    }

    private static KerberosEncryption Rc4Hmac() =>
        KerberosEncryption.Create(KerberosEncryptionType.Rc4Hmac, new FixedKerberosRandomSource(Confounder));
}
