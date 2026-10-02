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

    // MIT Kerberos t_cksums.c at commit 50588db5d26e81f3d564d1f69435af34ae80d9b2: its
    // CKSUMTYPE_HMAC_MD5_ARCFOUR case, key usage 6.
    [TestMethod]
    public void ComputeChecksum_MitTCksumsCase_MatchesVectorAndVerifies()
    {
        byte[] key = Hex.Bytes("F7D3A155AF5E238A0B7A871A96BA2AB2");
        byte[] data = Encoding.ASCII.GetBytes("seventeen eighteen nineteen twenty");
        byte[] published = Hex.Bytes("EB38CC97E2230F59DA4117DC5859D7EC");
        byte[] flipped = Hex.Bytes("EB38CC97E2230F59DA4117DC5859D7EC");
        flipped[^1] ^= 0x01;

        byte[] checksum = Rc4Hmac().ComputeChecksum(key, 6, data);

        CollectionAssert.AreEqual(published, checksum);
        Assert.IsTrue(Rc4Hmac().VerifyChecksum(key, 6, data, published));
        Assert.IsFalse(Rc4Hmac().VerifyChecksum(key, 6, data, flipped));
    }

    // MIT Kerberos t_decrypt.c at commit 50588db5d26e81f3d564d1f69435af34ae80d9b2: its five
    // rc4-hmac (ENCTYPE_ARCFOUR_HMAC) cases, key usages 0 to 4.
    [TestMethod]
    [DataRow("", 0, "F81FEC39255F5784E850C4377C88BD85", "02C1EB15586144122EC717763DD348BF00434DDC6585954C")]
    [DataRow("1", 1, "67D1300D281223867F9647FF48721273", "6156E0CC04E0A0874F9FDA008F498A7ADBBC80B70B14DDDBC0")]
    [DataRow("9 bytesss", 2, "3E40AB6093695281B3AC1A9304224D98", "0F9AD121D99D4A09448E4F1F718C4F5CBE6096262C66F29DF232A87C9F98755D55")]
    [DataRow("13 bytes byte", 3, "4BA2FBF0379FAED87A254D3B353D5A7E", "612C57568B17A70352BAE8CF26FB9459A6F3353CD35FD439DB3107CBEC765D326DFC04C1DD")]
    [DataRow("30 bytes bytes bytes bytes byt", 4, "68F263DB3FCE15D031C9EAB02D67107A", "95F9047C3AD75891C2E9B04B16566DC8B6EB9CE4231AFB2542EF87A7B5A0F260A99F0460508DE0CECC632D07C354124E46C5D2234EB8")]
    public void Decrypt_MitTDecryptCase_GivesThePlaintext(string plaintext, int usage, string key, string ciphertext)
    {
        byte[] decrypted = Rc4Hmac().Decrypt(Hex.Bytes(key), usage, Hex.Bytes(ciphertext));

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(plaintext), decrypted);
    }

    // The "9 bytesss" case above with one bit of its encrypted plaintext flipped.
    [TestMethod]
    public void Decrypt_MitTDecryptCaseWithOneBitFlipped_ThrowsIntegrityCheckFailed()
    {
        byte[] ciphertext = Hex.Bytes("0F9AD121D99D4A09448E4F1F718C4F5CBE6096262C66F29DF232A87C9F98755D55");
        ciphertext[^1] ^= 0x01;

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Rc4Hmac().Decrypt(Hex.Bytes("3E40AB6093695281B3AC1A9304224D98"), 2, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    private static KerberosEncryption Rc4Hmac() =>
        KerberosEncryption.Create(KerberosEncryptionType.Rc4Hmac, new FixedKerberosRandomSource(Confounder));
}
