using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Checks <c>camellia128-cts-cmac</c> and <c>camellia256-cts-cmac</c> against every sample in
/// RFC 6803 section 10: string-to-key, key derivation, encryption and checksums. The RFC
/// prints no key usage for its encryptions; they are MIT's <c>t_decrypt.c</c> cases, whose
/// five plaintexts per type use key usages 0 to 4 in order.
/// </summary>
[TestClass]
public sealed class CamelliaCmacKerberosEncryptionTests
{
    private const string Camellia128BaseKey = "57 D0 29 72 98 FF D9 D3 5D E5 A4 7F B4 BD E2 4B";

    private const string Camellia256BaseKey = "B9 D6 82 8B 20 56 B7 BE 65 6D 88 A1 23 B1 FA C6 82 14 AC 2B 72 7E CF 5F 69 AF E0 C4 DF 2A 6D 2C";

    private const string Password64 = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

    [TestMethod]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "password", "ATHENA.MIT.EDUraeburn", 1, "57 D0 29 72 98 FF D9 D3 5D E5 A4 7F B4 BD E2 4B")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "password", "ATHENA.MIT.EDUraeburn", 1, Camellia256BaseKey)]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "password", "ATHENA.MIT.EDUraeburn", 2, "73 F1 B5 3A A0 F3 10 F9 3B 1D E8 CC AA 0C B1 52")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "password", "ATHENA.MIT.EDUraeburn", 2, "83 FC 58 66 E5 F8 F4 C6 F3 86 63 C6 5C 87 54 9F 34 2B C4 7E D3 94 DC 9D 3C D4 D1 63 AD E3 75 E3")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "password", "ATHENA.MIT.EDUraeburn", 1200, "8E 57 11 45 45 28 55 57 5F D9 16 E7 B0 44 87 AA")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "password", "ATHENA.MIT.EDUraeburn", 1200, "77 F4 21 A6 F2 5E 13 83 95 E8 37 E5 D8 5D 38 5B 4C 1B FD 77 2E 11 2C D9 20 8C E7 2A 53 0B 15 E6")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, Password64, "pass phrase equals block size", 1200, "8B F6 C3 EF 70 9B 98 1D BB 58 5D 08 68 43 BE 05")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, Password64, "pass phrase equals block size", 1200, "11 9F E2 A1 CB 0B 1B E0 10 B9 06 7A 73 DB 63 ED 46 65 B4 E5 3A 98 D1 78 03 5D CF E8 43 A6 B9 B0")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, Password64 + "X", "pass phrase exceeds block size", 1200, "57 52 AC 8D 6A D1 CC FE 84 30 B3 12 87 1C 2F 74")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, Password64 + "X", "pass phrase exceeds block size", 1200, "61 4D 5D FC 0B A6 D3 90 B4 12 B8 9A E4 D5 B0 88 B6 12 B3 16 51 09 94 67 9D DB 43 83 C7 12 6D DF")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "\U0001D11E", "EXAMPLE.COMpianist", 50, "CC 75 C7 FD 26 0F 1C 16 58 01 1F CC 0D 56 06 16")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "\U0001D11E", "EXAMPLE.COMpianist", 50, "16 3B 76 8C 6D B1 48 B4 EE C7 16 3D F5 AE D7 0E 20 6B 68 CE C0 78 BC 06 9E D6 8A 7E D3 6B 1E CC")]
    public void StringToKey_Rfc6803Sample_MatchesVector(KerberosEncryptionType encryptionType, string password, string salt, int iterationCount, string expected)
    {
        byte[] key = Encryption(encryptionType).StringToKey(password, Encoding.UTF8.GetBytes(salt), IterationCount(iterationCount));

        CollectionAssert.AreEqual(Hex.Bytes(expected), key);
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "00 49 8F D9 16 BF C1 C2 B1 03 1C 17 08 01 B3 81")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "11 08 3A 00 BD FE 6A 41 B2 F1 97 16 D6 20 2F 0A FA 94 28 9A FE 8B 27 A0 49 BD 28 B1 D7 6C 38 9A")]
    public void StringToKey_Rfc6803BinarySalt_MatchesVector(KerberosEncryptionType encryptionType, string expected)
    {
        byte[] key = Encryption(encryptionType).StringToKey("password", Hex.Bytes("12 34 56 78 78 56 34 12"), IterationCount(5));

        CollectionAssert.AreEqual(Hex.Bytes(expected), key);
    }

    [TestMethod]
    public void StringToKey_DefaultIterationCount_Is32768()
    {
        KerberosEncryption encryption = Encryption(KerberosEncryptionType.Camellia128CtsCmac);

        CollectionAssert.AreEqual(
            encryption.StringToKey("password", "EXAMPLE.COMuser"u8, IterationCount(32768)),
            encryption.StringToKey("password", "EXAMPLE.COMuser"u8, []));
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, Camellia128BaseKey, 0x99, "D1 55 77 5A 20 9D 05 F0 2B 38 D4 2A 38 9E 5A 56")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, Camellia128BaseKey, 0xAA, "64 DF 83 F8 5A 53 2F 17 57 7D 8C 37 03 57 96 AB")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, Camellia128BaseKey, 0x55, "3E 4F BD F3 0F B8 25 9C 42 5C B6 C9 6F 1F 46 35")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, Camellia256BaseKey, 0x99, "E4 67 F9 A9 55 2B C7 D3 15 5A 62 20 AF 9C 19 22 0E EE D4 FF 78 B0 D1 E6 A1 54 49 91 46 1A 9E 50")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, Camellia256BaseKey, 0xAA, "41 2A EF C3 62 A7 28 5F C3 96 6C 6A 51 81 E7 60 5A E6 75 23 5B 6D 54 9F BF C9 AB 66 30 A4 C6 04")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, Camellia256BaseKey, 0x55, "FA 62 4F A0 E5 23 99 3F A3 88 AE FD C6 7E 67 EB CD 8C 08 E8 A0 24 6B 1D 73 B0 D1 DD 9F C5 82 B0")]
    public void DeriveKey_Rfc6803KeyUsage2_MatchesVector(KerberosEncryptionType encryptionType, string baseKey, int purpose, string expected)
    {
        byte[] derived = ((CamelliaCmacKerberosEncryption)Encryption(encryptionType)).DeriveKey(Hex.Bytes(baseKey), [0, 0, 0, 2, (byte)purpose]);

        CollectionAssert.AreEqual(Hex.Bytes(expected), derived);
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "1D C4 6A 8D 76 3F 4F 93 74 2B CB A3 38 75 76 C3", 0, "", "B6 98 22 A1 9A 6B 09 C0 EB C8 55 7D 1F 1B 6C 0A",
        "C4 66 F1 87 10 69 92 1E DB 7C 6F DE 24 4A 52 DB 0B A1 0E DC 19 7B DB 80 06 65 8C A3 CC CE 6E B8")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "50 27 BC 23 1D 0F 3A 9D 23 33 3F 1C A6 FD BE 7C", 1, "1", "6F 2F C3 C2 A1 66 FD 88 98 96 7A 83 DE 95 96 D9",
        "84 2D 21 FD 95 03 11 C0 DD 46 4A 3F 4B E8 D6 DA 88 A5 6D 55 9C 9B 47 D3 F9 A8 50 67 AF 66 15 59 B8")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "A1 BB 61 E8 05 F9 BA 6D DE 8F DB DD C0 5C DE A0", 2, "9 bytesss", "A5 B4 A7 1E 07 7A EE F9 3C 87 63 C1 8F DB 1F 10",
        "61 9F F0 72 E3 62 86 FF 0A 28 DE B3 A3 52 EC 0D 0E DF 5C 51 60 D6 63 C9 01 75 8C CF 9D 1E D3 3D 71 DB 8F 23 AA BF 83 48 A0")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "2C A2 7A 5F AF 55 32 24 45 06 43 4E 1C EF 66 76", 3, "13 bytes byte", "19 FE E4 0D 81 0C 52 4B 5B 22 F0 18 74 C6 93 DA",
        "B8 EC A3 16 7A E6 31 55 12 E5 9F 98 A7 C5 00 20 5E 5F 63 FF 3B B3 89 AF 1C 41 A2 1D 64 0D 86 15 C9 ED 3F BE B0 5A B6 AC B6 76 89 B5 EA")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "78 24 F8 C1 6F 83 FF 35 4C 6B F7 51 5B 97 3F 43", 4, "30 bytes bytes bytes bytes byt", "CA 7A 7A B4 BE 19 2D AB D6 03 50 6D B1 9C 39 E2",
        "A2 6A 39 05 A4 FF D5 81 6B 7B 1E 27 38 0D 08 09 0C 8E C1 F3 04 49 6E 1A BD CD 2B DC D1 DF FC 66 09 89 E1 17 A7 13 DD BB 57 A4 14 6C 15 87 CB A4 35 66 65 59 1D 22 40 28 2F 58 42 B1 05 A5")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "B6 1C 86 CC 4E 5D 27 57 54 5A D4 23 39 9F B7 03 1E CA B9 13 CB B9 00 BD 7A 3C 6D D8 BF 92 01 5B", 0, "", "3C BB D2 B4 59 17 94 10 67 F9 65 99 BB 98 92 6C",
        "03 88 6D 03 31 0B 47 A6 D8 F0 6D 7B 94 D1 DD 83 7E CC E3 15 EF 65 2A FF 62 08 59 D9 4A 25 92 66")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "1B 97 FE 0A 19 0E 20 21 EB 30 75 3E 1B 6E 1E 77 B0 75 4B 1D 68 46 10 35 58 64 10 49 63 46 38 33", 1, "1", "DE F4 87 FC EB E6 DE 63 46 D4 DA 45 21 BB A2 D2",
        "2C 9C 15 70 13 3C 99 BF 6A 34 BC 1B 02 12 00 2F D1 94 33 87 49 DB 41 35 49 7A 34 7C FC D9 D1 8A 12")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "32 16 4C 5B 43 4D 1D 15 38 E4 CF D9 BE 80 40 FE 8C 4A C7 AC C4 B9 3D 33 14 D2 13 36 68 14 7A 05", 2, "9 bytesss", "AD 4F F9 04 D3 4E 55 53 84 B1 41 00 FC 46 5F 88",
        "9C 6D E7 5F 81 2D E7 ED 0D 28 B2 96 35 57 A1 15 64 09 98 27 5B 0A F5 15 27 09 91 3F F5 2A 2A 9C 8E 63 B8 72 F9 2E 64 C8 39")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "B0 38 B1 32 CD 8E 06 61 22 67 FA B7 17 00 66 D8 8A EC CB A0 B7 44 BF C6 0D C8 9B CA 18 2D 07 15", 3, "13 bytes byte", "CF 9B CA 6D F1 14 4E 0C 0A F9 B8 F3 4C 90 D5 14",
        "EE EC 85 A9 81 3C DC 53 67 72 AB 9B 42 DE FC 57 06 F7 26 E9 75 DD E0 5A 87 EB 54 06 EA 32 4C A1 85 C9 98 6B 42 AA BE 79 4B 84 82 1B EE")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "CC FC D3 49 BF 4C 66 77 E8 6E 4B 02 B8 EA B9 24 A5 46 AC 73 1C F9 BF 69 89 B9 96 E7 D6 BF BB A7", 4, "30 bytes bytes bytes bytes byt", "64 4D EF 38 DA 35 00 72 75 87 8D 21 68 55 E2 28",
        "0E 44 68 09 85 85 5F 2D 1F 18 12 52 9C A8 3B FD 8E 34 9D E6 FD 9A DA 0B AA A0 48 D6 8E 26 5F EB F3 4A D1 25 5A 34 49 99 AD 37 14 68 87 A6 C6 84 57 31 AC 7F 46 37 6A 05 04 CD 06 57 14 74")]
    public void Encrypt_Rfc6803SampleEncryption_MatchesVectorAndDecrypts(KerberosEncryptionType encryptionType, string baseKey, int usage, string plaintext, string confounder, string expected)
    {
        KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(Hex.Bytes(confounder)));

        byte[] ciphertext = encryption.Encrypt(Hex.Bytes(baseKey), usage, Encoding.ASCII.GetBytes(plaintext));

        CollectionAssert.AreEqual(Hex.Bytes(expected), ciphertext);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(plaintext), encryption.Decrypt(Hex.Bytes(baseKey), usage, ciphertext));
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "1D C4 6A 8D 76 3F 4F 93 74 2B CB A3 38 75 76 C3", 7, "abcdefghijk", "11 78 E6 C5 C4 7A 8C 1A E0 C4 B9 C7 D4 EB 7B 6B")]
    [DataRow(KerberosEncryptionType.Camellia128CtsCmac, "50 27 BC 23 1D 0F 3A 9D 23 33 3F 1C A6 FD BE 7C", 8, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "D1 B3 4F 70 04 A7 31 F2 3A 0C 00 BF 6C 3F 75 3A")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "B6 1C 86 CC 4E 5D 27 57 54 5A D4 23 39 9F B7 03 1E CA B9 13 CB B9 00 BD 7A 3C 6D D8 BF 92 01 5B", 9, "123456789", "87 A1 2C FD 2B 96 21 48 10 F0 1C 82 6E 77 44 B1")]
    [DataRow(KerberosEncryptionType.Camellia256CtsCmac, "32 16 4C 5B 43 4D 1D 15 38 E4 CF D9 BE 80 40 FE 8C 4A C7 AC C4 B9 3D 33 14 D2 13 36 68 14 7A 05", 10, "!@#$%^&*()!@#$%^&*()!@#$%^&*()", "3F A0 B4 23 55 E5 2B 18 91 87 29 4A A2 52 AB 64")]
    public void ComputeChecksum_Rfc6803SampleChecksum_MatchesVector(KerberosEncryptionType encryptionType, string baseKey, int usage, string plaintext, string expected)
    {
        KerberosEncryption encryption = Encryption(encryptionType);

        CollectionAssert.AreEqual(Hex.Bytes(expected), encryption.ComputeChecksum(Hex.Bytes(baseKey), usage, Encoding.ASCII.GetBytes(plaintext)));
        Assert.IsTrue(encryption.VerifyChecksum(Hex.Bytes(baseKey), usage, Encoding.ASCII.GetBytes(plaintext), Hex.Bytes(expected)));
    }

    [TestMethod]
    public void ComputePseudoRandom_IsCmacUnderThePrfKey()
    {
        CamelliaCmacKerberosEncryption encryption = (CamelliaCmacKerberosEncryption)Encryption(KerberosEncryptionType.Camellia256CtsCmac);
        byte[] pseudoRandomKey = encryption.DeriveKey(Hex.Bytes(Camellia256BaseKey), "prf"u8);

        byte[] output = encryption.ComputePseudoRandom(Hex.Bytes(Camellia256BaseKey), "test"u8);

        CollectionAssert.AreEqual(KerberosCamelliaCmac.Compute(pseudoRandomKey, "test"u8), output);
        Assert.HasCount(16, output);
    }

    [TestMethod]
    public void CmacCompute_EmptyMessage_PadsWithTheSecondSubkeyNotTheFirst()
    {
        // SP 800-38B: an empty message is one padded block 80 00 .. 00 masked with K2, so its
        // tag differs from that of the whole block 80 00 .. 00, which is masked with K1.
        byte[] paddedBlock = new byte[16];
        paddedBlock[0] = 0x80;

        byte[] emptyTag = KerberosCamelliaCmac.Compute(Hex.Bytes(Camellia128BaseKey), []);

        Assert.HasCount(16, emptyTag);
        CollectionAssert.AreNotEqual(KerberosCamelliaCmac.Compute(Hex.Bytes(Camellia128BaseKey), paddedBlock), emptyTag);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(20)]
    [DataRow(37)]
    public void Decrypt_TamperedCiphertext_ThrowsIntegrityCheckFailed(int tamperedIndex)
    {
        KerberosEncryption encryption = Encryption(KerberosEncryptionType.Camellia128CtsCmac);
        byte[] ciphertext = encryption.Encrypt(Hex.Bytes(Camellia128BaseKey), 2, Encoding.ASCII.GetBytes("tamper"));
        ciphertext[tamperedIndex] ^= 0x80;

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => encryption.Decrypt(Hex.Bytes(Camellia128BaseKey), 2, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    [TestMethod]
    public void Decrypt_ShorterThanConfounderAndMac_ThrowsCiphertextTooShort()
    {
        KerberosEncryption encryption = Encryption(KerberosEncryptionType.Camellia256CtsCmac);

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => encryption.Decrypt(Hex.Bytes(Camellia256BaseKey), 2, new byte[31]));

        Assert.AreEqual(KerberosCryptographyError.CiphertextTooShort, exception.Error);
    }

    private static byte[] IterationCount(int count) => [(byte)(count >> 24), (byte)(count >> 16), (byte)(count >> 8), (byte)count];

    private static KerberosEncryption Encryption(KerberosEncryptionType encryptionType) =>
        KerberosEncryption.Create(encryptionType, new SystemKerberosRandomSource());
}
