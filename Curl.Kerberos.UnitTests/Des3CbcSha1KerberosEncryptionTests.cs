using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Checks <c>des3-cbc-sha1</c> against RFC 3961 appendix A.3 (DR and DK) and A.4
/// (string-to-key), MIT Kerberos' <c>t_decrypt.c</c> (decryption, key usages 0 to 4) and
/// <c>t_cksums.c</c> (<c>hmac-sha1-des3-kd</c>), and round trips of encryption and checksums.
/// </summary>
[TestClass]
public sealed class Des3CbcSha1KerberosEncryptionTests
{
    private const string ChecksumKey = "7A 25 DF 89 92 29 6D CE DA 0E 13 5B C4 04 6E 23 75 B3 C1 4C 98 FB C1 62";

    // RFC 3961 appendix A.3.
    [TestMethod]
    [DataRow("dce06b1f64c857a11c3db57c51899b2cc1791008ce973b92", "0000000155", "935079d14490a75c3093c4a6e8c3b049c71e6ee705", "925179d04591a79b5d3192c4a7e9c289b049c71f6ee604cd")]
    [DataRow("5e13d31c70ef765746578531cb51c15bf11ca82c97cee9f2", "00000001aa", "9f58e5a047d894101c469845d67ae3c5249ed812f2", "9e58e5a146d9942a101c469845d67a20e3c4259ed913f207")]
    [DataRow("98e6fd8a04a4b6859b75a176540b9752bad3ecd610a252bc", "0000000155", "12fff90c773f956d13fc2ca0d0840349dbd39908eb", "13fef80d763e94ec6d13fd2ca1d085070249dad39808eabf")]
    [DataRow("622aec25a2fe2cad7094680b7c64940280084c1a7cec92b5", "00000001aa", "f8debf05b097e7dc0603686aca35d91fd9a5516a70", "f8dfbf04b097e6d9dc0702686bcb3489d91fd9a4516b703e")]
    [DataRow("d3f8298ccb166438dcb9b93ee5a7629286a491f838f802fb", "6b65726265726f73", "2270db565d2a3d64cfbfdc5305d4f778a6de42d9da", "2370da575d2a3da864cebfdc5204d56df779a7df43d9da43")]
    [DataRow("c1081649ada74362e6a1459d01dfd30d67c2234c940704da", "0000000155", "348056ec98fcc517171d2b4d7a9493af482d999175", "348057ec98fdc48016161c2a4c7a943e92ae492c989175f7")]
    [DataRow("5d154af238f46713155719d55e2f1f790dd661f279a7917c", "00000001aa", "a8818bc367dadacbe9a6c84627fb60c294b01215e5", "a8808ac267dada3dcbe9a7c84626fbc761c294b01315e5c1")]
    [DataRow("798562e049852f57dc8c343ba17f2ca1d97394efc8adc443", "0000000155", "c813f88b3be2b2f75424ce9175fbc8483b88c8713a", "c813f88a3be3b334f75425ce9175fbe3c8493b89c8703b49")]
    [DataRow("26dce334b545292f2feab9a8701a89a4b99eb9942cecd016", "00000001aa", "f58efc6f83f93e55e695fd252cf8fe59f7d5ba37ec", "f48ffd6e83f83e7354e694fd252cf83bfe58f7d5ba37ec5d")]
    public void DeriveRandomAndDeriveKey_Rfc3961AppendixA3_MatchVectors(string key, string constant, string expectedRandom, string expectedKey)
    {
        CollectionAssert.AreEqual(Hex.Bytes(expectedRandom), Des3CbcSha1KerberosEncryption.DeriveRandom(Hex.Bytes(key), Hex.Bytes(constant)));
        CollectionAssert.AreEqual(Hex.Bytes(expectedKey), Des3CbcSha1KerberosEncryption.DeriveKey(Hex.Bytes(key), Hex.Bytes(constant)));
    }

    // RFC 3961 appendix A.4; its g-clef is U+1D11E, printed there as U+1011E (erratum 2090).
    [TestMethod]
    [DataRow("password", "ATHENA.MIT.EDUraeburn", "850bb51358548cd05e86768c313e3bfef7511937dcf72c3e")]
    [DataRow("potatoe", "WHITEHOUSE.GOVdanny", "dfcd233dd0a43204ea6dc437fb15e061b02979c1f74f377a")]
    [DataRow("penny", "EXAMPLE.COMbuckaroo", "6d2fcdf2d6fbbc3ddcadb5da5710a23489b0d3b69d5d9d4a")]
    [DataRow("ß", "ATHENA.MIT.EDUJurišić", "16d5a40e1ce3bacb61b9dce00470324c831973a7b952feb0")]
    [DataRow("\U0001D11E", "EXAMPLE.COMpianist", "85763726585dbc1cce6ec43e1f751f07f1c4cbb098f40b19")]
    public void StringToKey_Rfc3961AppendixA4_MatchesVector(string password, string salt, string expected)
    {
        byte[] key = Encryption().StringToKey(password, Encoding.UTF8.GetBytes(salt), []);

        CollectionAssert.AreEqual(Hex.Bytes(expected), key);
    }

    [TestMethod]
    public void StringToKey_ParametersGiven_ThrowsBadStringToKeyParameters()
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Encryption().StringToKey("password", "EXAMPLE.COMuser"u8, [0, 0, 16, 0]));

        Assert.AreEqual(KerberosCryptographyError.BadStringToKeyParameters, exception.Error);
    }

    [TestMethod]
    public void RandomToKey_AllOnes_SpreadsTheLowBitsIntoTheEighthByteWithOddParity()
    {
        byte[] key = Des3CbcSha1KerberosEncryption.RandomToKey(Enumerable.Repeat((byte)0xFF, 21).ToArray());

        CollectionAssert.AreEqual(Hex.Bytes("FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE FE"), key);
    }

    [TestMethod]
    public void RandomToKey_AlternatingBits_SetsEachByteToOddParity()
    {
        byte[] key = Des3CbcSha1KerberosEncryption.RandomToKey([.. Enumerable.Repeat<byte>(0x01, 7), .. Enumerable.Repeat<byte>(0x02, 7), .. Enumerable.Repeat<byte>(0x00, 7)]);

        CollectionAssert.AreEqual(Hex.Bytes("01 01 01 01 01 01 01 FE 02 02 02 02 02 02 02 01 01 01 01 01 01 01 01 01"), key);
    }

    // MIT Kerberos t_decrypt.c: its five des3-cbc-sha1 cases, key usages 0 to 4.
    [TestMethod]
    [DataRow("", 0, ChecksumKey, "548AF4D504F7D723303F12175FE8386B7B5335A967BAD61F3BF0B143")]
    [DataRow("1", 1, "BC0783891513D5CE57BC138FD3C11AE6404523853229 62B6", "9C3C1DBA4747D85AF2916E4745F2DCE38046796E5104BCCDFB669A91D44BC356660945C7")]
    [DataRow("9 bytesss", 2, "2FD0F725CE04100D2FC8A18098831F850B45D9EF850BD920", "CF9144EBC8697981075A8BAD8D74E5D7D591EB7D9770C7ADA25EE8C5B3D69444DFEC79A5B7A01482D9AF74E6")]
    [DataRow("13 bytes byte", 3, "0DD52094E0F41CECCB5BE510A764B35176E3981332F1E598", "839A17081ECBAFBCDC91B88C6955DD3C4514023CF177B77BF0D0177A16F705E849CB7781D76A316B193F8D30")]
    [DataRow("30 bytes bytes bytes bytes byt", 4, "F11686CBBC9E23EA54FECD2A3DCDFB20B6FE98BF2645C4C4", "89433E83FD0EA3666CFFCD18D8DEEBC53B9A34EDBEB159D9F667C6C2B9A964401D55E7E9C68D648D65C3AA84FFA3790C14A864DA8073A9A95C4BA2BC")]
    public void Decrypt_MitTDecryptCase_GivesThePlaintextAndItsZeroPadding(string plaintext, int usage, string key, string ciphertext)
    {
        byte[] decrypted = Encryption().Decrypt(Hex.Bytes(key), usage, Hex.Bytes(ciphertext));

        byte[] expected = Encoding.ASCII.GetBytes(plaintext);
        CollectionAssert.AreEqual(expected, decrypted[..expected.Length]);
        Assert.AreEqual(0, (decrypted.Length + 8) % 8);
        Assert.IsTrue(decrypted[expected.Length..].All(value => value == 0));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1")]
    [DataRow("30 bytes bytes bytes bytes byt")]
    public void Encrypt_FixedConfounder_PadsToWholeBlocksAndDecryptsBack(string plaintext)
    {
        byte[] key = Hex.Bytes(ChecksumKey);
        KerberosEncryption encryption = Encryption();
        byte[] message = Encoding.ASCII.GetBytes(plaintext);

        byte[] ciphertext = encryption.Encrypt(key, 7, message);
        byte[] decrypted = encryption.Decrypt(key, 7, ciphertext);

        Assert.AreEqual(0, (ciphertext.Length - 20) % 8);
        Assert.AreEqual(((message.Length + 7) / 8 * 8) + 8 + 20, ciphertext.Length);
        CollectionAssert.AreEqual(message, decrypted[..message.Length]);
    }

    [TestMethod]
    public void Decrypt_AlteredCiphertext_ThrowsIntegrityCheckFailed()
    {
        byte[] key = Hex.Bytes(ChecksumKey);
        KerberosEncryption encryption = Encryption();
        byte[] ciphertext = encryption.Encrypt(key, 3, "13 bytes byte"u8);
        ciphertext[0] ^= 1;

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => encryption.Decrypt(key, 3, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    [TestMethod]
    public void Decrypt_ShorterThanConfounderAndHmac_ThrowsCiphertextTooShort()
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Encryption().Decrypt(Hex.Bytes(ChecksumKey), 1, new byte[27]));

        Assert.AreEqual(KerberosCryptographyError.CiphertextTooShort, exception.Error);
    }

    [TestMethod]
    public void Decrypt_EncryptedPartNotWholeBlocks_ThrowsCiphertextNotWholeBlocks()
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Encryption().Decrypt(Hex.Bytes(ChecksumKey), 1, new byte[29]));

        Assert.AreEqual(KerberosCryptographyError.CiphertextNotWholeBlocks, exception.Error);
    }

    // MIT Kerberos t_cksums.c: hmac-sha1-des3-kd of "six seven", key usage 2.
    [TestMethod]
    public void ComputeChecksum_MitTCksumsCase_MatchesVectorAndVerifies()
    {
        byte[] key = Hex.Bytes(ChecksumKey);
        KerberosEncryption encryption = Encryption();

        byte[] checksum = encryption.ComputeChecksum(key, 2, "six seven"u8);

        CollectionAssert.AreEqual(Hex.Bytes("0E EF C9 C3 E0 49 AA BC 1B A5 C4 01 67 7D 9A B6 99 08 2B B4"), checksum);
        Assert.IsTrue(encryption.VerifyChecksum(key, 2, "six seven"u8, checksum));
        Assert.IsFalse(encryption.VerifyChecksum(key, 3, "six seven"u8, checksum));
    }

    [TestMethod]
    public void ComputePseudoRandom_Input_IsTheFirstTwoBlocksOfItsSha1EncryptedUnderTheDerivedPrfKey()
    {
        byte[] key = Hex.Bytes(ChecksumKey);

        byte[] output = Encryption().ComputePseudoRandom(key, "prf input"u8);

        using TripleDES tripleDes = TripleDES.Create();
        tripleDes.SetKey(Des3CbcSha1KerberosEncryption.DeriveKey(key, "prf"u8));
        byte[] expected = tripleDes.EncryptCbc(SHA1.HashData("prf input"u8)[..16], new byte[8], PaddingMode.None);
        CollectionAssert.AreEqual(expected, output);
    }

    // BL-1649, ADR-0424: a key whose first and second, or second and third, 8-byte parts are
    // equal apart from their parity bits is single DES, which .NET's TripleDES refuses; Curl
    // refuses it first, as WeakKey.
    [TestMethod]
    [DataRow("000000000000000000000000000000000000000000000000")]
    [DataRow("0123456789ABCDEF0123456789ABCDEFFEDCBA9876543210")]
    [DataRow("FEDCBA98765432100123456789ABCDEF0123456789ABCDEF")]
    [DataRow("0123456789ABCDEF0022446688AACCEEFEDCBA9876543210")]
    public void EncryptDecryptChecksumAndPseudoRandom_WeakKey_ThrowWeakKey(string weakKey)
    {
        byte[] key = Hex.Bytes(weakKey);
        KerberosEncryption encryption = Encryption();

        Action[] operations =
        [
            () => encryption.Encrypt(key, 1, []),
            () => encryption.Decrypt(key, 1, new byte[28]),
            () => encryption.ComputeChecksum(key, 1, "data"u8),
            () => encryption.ComputePseudoRandom(key, "prf input"u8),
        ];
        foreach (Action operation in operations)
        {
            KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(operation);
            Assert.AreEqual(KerberosCryptographyError.WeakKey, exception.Error);
        }
    }

    [TestMethod]
    public void CreateTripleDes_StrongKey_EncryptsAsTripleDes()
    {
        byte[] key = Hex.Bytes(ChecksumKey);

        using TripleDES created = Des3CbcSha1KerberosEncryption.CreateTripleDes(key);

        using TripleDES expected = TripleDES.Create();
        expected.SetKey(key);
        CollectionAssert.AreEqual(expected.EncryptEcb(new byte[8], PaddingMode.None), created.EncryptEcb(new byte[8], PaddingMode.None));
    }

    private static KerberosEncryption Encryption() =>
        KerberosEncryption.Create(KerberosEncryptionType.Des3CbcSha1, new FixedKerberosRandomSource([.. Enumerable.Range(1, 8).Select(value => (byte)value)]));
}
