namespace Curl.Kerberos;

/// <summary>
/// Checks what every <see cref="KerberosEncryption" /> shares: <see cref="KerberosEncryption.Create" />'s
/// types and their sizes, the key-size and password checks, and the production random source.
/// </summary>
[TestClass]
public sealed class KerberosEncryptionTests
{
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, 15, 16, 12)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, 16, 32, 12)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, 19, 16, 16)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, 20, 32, 24)]
    [DataRow(KerberosEncryptionType.Rc4Hmac, -138, 16, 16)]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, 17, 16, 16)]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, 18, 32, 16)]
    public void Create_KnownType_HasItsChecksumTypeAndSizes(KerberosEncryptionType encryptionType, int checksumType, int keySize, int checksumSize)
    {
        KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new SystemKerberosRandomSource());

        Assert.AreEqual(encryptionType, encryption.EncryptionType);
        Assert.AreEqual(checksumType, encryption.ChecksumType);
        Assert.AreEqual(keySize, encryption.KeySize);
        Assert.AreEqual(checksumSize, encryption.ChecksumSize);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(16)]
    [DataRow(24)]
    public void Create_UnknownType_ThrowsUnsupportedEncryptionType(int encryptionType)
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => KerberosEncryption.Create((KerberosEncryptionType)encryptionType, new SystemKerberosRandomSource()));

        Assert.AreEqual(KerberosCryptographyError.UnsupportedEncryptionType, exception.Error);
        Assert.AreEqual("Kerberos cryptography failed: UnsupportedEncryptionType.", exception.Message);
    }

    [TestMethod]
    public void EveryOperation_WrongKeySize_ThrowsArgumentException()
    {
        KerberosEncryption encryption = KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, new SystemKerberosRandomSource());
        byte[] key = new byte[16];

        Assert.ThrowsExactly<ArgumentException>(() => encryption.Encrypt(key, 1, []));
        Assert.ThrowsExactly<ArgumentException>(() => encryption.Decrypt(key, 1, new byte[64]));
        Assert.ThrowsExactly<ArgumentException>(() => encryption.ComputeChecksum(key, 1, []));
        Assert.ThrowsExactly<ArgumentException>(() => encryption.VerifyChecksum(key, 1, [], new byte[12]));
        Assert.ThrowsExactly<ArgumentException>(() => encryption.ComputePseudoRandom(key, []));
    }

    [TestMethod]
    public void StringToKey_NullPassword_ThrowsArgumentNullException()
    {
        KerberosEncryption encryption = KerberosEncryption.Create(KerberosEncryptionType.Rc4Hmac, new SystemKerberosRandomSource());

        Assert.ThrowsExactly<ArgumentNullException>(() => encryption.StringToKey(null!, [], []));
    }

    [TestMethod]
    public void Encrypt_SystemRandomSource_UsesAFreshConfounderEachTime()
    {
        KerberosEncryption encryption = KerberosEncryption.Create(KerberosEncryptionType.Aes128CtsHmacSha196, new SystemKerberosRandomSource());
        byte[] key = new byte[16];

        CollectionAssert.AreNotEqual(encryption.Encrypt(key, 1, [1, 2, 3]), encryption.Encrypt(key, 1, [1, 2, 3]));
    }
}
