using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Checks <c>aes128-cts-hmac-sha1-96</c> and <c>aes256-cts-hmac-sha1-96</c> against RFC 3962
/// appendix B's string-to-key and AES-CTS vectors, and their encryption, checksum and PRF
/// against RFC 3961's simplified profile built from the same primitives.
/// </summary>
[TestClass]
public sealed class AesSha1KerberosEncryptionTests
{
    private const string SixtyFourXs = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

    private static readonly byte[] Confounder = Hex.Bytes("00112233445566778899aabbccddeeff");

    // RFC 3962 appendix B.
    [TestMethod]
    [DataRow(1, "password", "ATHENA.MIT.EDUraeburn", "42263c6e89f4fc28b8df68ee09799f15", "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161")]
    [DataRow(2, "password", "ATHENA.MIT.EDUraeburn", "c651bf29e2300ac27fa469d693bdda13", "a2e16d16b36069c135d5e9d2e25f896102685618b95914b467c67622225824ff")]
    [DataRow(1200, "password", "ATHENA.MIT.EDUraeburn", "4c01cd46d632d01e6dbe230a01ed642a", "55a6ac740ad17b4846941051e1e8b0a7548d93b0ab30a8bc3ff16280382b8c2a")]
    [DataRow(1200, SixtyFourXs, "pass phrase equals block size", "59d1bb789a828b1aa54ef9c2883f69ed", "89adee3608db8bc71f1bfbfe459486b05618b70cbae22092534e56c553ba4b34")]
    [DataRow(1200, SixtyFourXs + "X", "pass phrase exceeds block size", "cb8005dc5f90179a7f02104c0018751d", "d78c5c9cb872a8c9dad4697f0bb5b2d21496c82beb2caeda2112fceea057401b")]
    [DataRow(50, "\U0001D11E", "EXAMPLE.COMpianist", "f149c1f2e154a73452d43e7fe62a56e5", "4b6d9839f84406df1f09cc166db4b83c571848b784a3d6bdc346589a3e393f9e")]
    public void StringToKey_Rfc3962AppendixB_MatchesVector(int iterationCount, string password, string salt, string key128, string key256)
    {
        byte[] parameters = [0, 0, (byte)(iterationCount >> 8), (byte)iterationCount];

        CollectionAssert.AreEqual(Hex.Bytes(key128), Aes128().StringToKey(password, Encoding.UTF8.GetBytes(salt), parameters));
        CollectionAssert.AreEqual(Hex.Bytes(key256), Aes256().StringToKey(password, Encoding.UTF8.GetBytes(salt), parameters));
    }

    // RFC 3962 appendix B, the binary salt from [PECMS].
    [TestMethod]
    public void StringToKey_Rfc3962AppendixBBinarySalt_MatchesVector()
    {
        byte[] salt = Hex.Bytes("1234567878563412");
        byte[] parameters = [0, 0, 0, 5];

        CollectionAssert.AreEqual(Hex.Bytes("e9b23d52273747dd5c35cb55be619d8e"), Aes128().StringToKey("password", salt, parameters));
        CollectionAssert.AreEqual(Hex.Bytes("97a4e786be20d81a382d5ebc96d5909cabcdadc87ca48f574504159f16c36e31"), Aes256().StringToKey("password", salt, parameters));
    }

    [TestMethod]
    public void StringToKey_NoParameters_Uses4096Iterations()
    {
        byte[] salt = Encoding.UTF8.GetBytes("ATHENA.MIT.EDUraeburn");

        CollectionAssert.AreEqual(Aes128().StringToKey("password", salt, [0, 0, 0x10, 0]), Aes128().StringToKey("password", salt, []));
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0 })]
    [DataRow(new byte[] { 1, 0, 0, 0 })]
    [DataRow(new byte[] { 0, 0x10, 0 })]
    public void StringToKey_BadParameters_ThrowsBadStringToKeyParameters(byte[] parameters)
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Aes128().StringToKey("password", [], parameters));

        Assert.AreEqual(KerberosCryptographyError.BadStringToKeyParameters, exception.Error);
    }

    [TestMethod]
    public void StringToKey_LargestIterationCountMitAllows_IsAccepted()
    {
        // 2^24 - 1 is MIT's largest; only its acceptance is checked, from a cheap
        // prefix of the work, so the test does not run 16 million iterations.
        Assert.AreEqual(0xffffff, KerberosEncryption.ReadIterationCount([0, 0xff, 0xff, 0xff], 4096));
    }

    // RFC 3962 appendix B, AES-CTS with the 128-bit key "chicken teriyaki" and an all-zero IV.
    [TestMethod]
    [DataRow("4920776f756c64206c696b652074686520", "c6353568f2bf8cb4d8a580362da7ff7f97")]
    [DataRow("4920776f756c64206c696b65207468652047656e6572616c20476175277320", "fc00783e0efdb2c1d445d4c8eff7ed2297687268d6ecccc0c07b25e25ecfe5")]
    [DataRow("4920776f756c64206c696b65207468652047656e6572616c20476175277320436869636b656e2c20706c656173652c", "97687268d6ecccc0c07b25e25ecfe584b3fffd940c16a18c1b5549d2f838029e39312523a78662d5be7fcbcc98ebf5")]
    [DataRow("4920776f756c64206c696b65207468652047656e6572616c20476175277320436869636b656e2c20706c656173652c20616e6420776f6e746f6e20736f75702e", "97687268d6ecccc0c07b25e25ecfe58439312523a78662d5be7fcbcc98ebf5a84807efe836ee89a526730dbc2f7bc8409dad8bbb96c4cdc03bc103e1a194bbd8")]
    public void AesCts_Rfc3962AppendixB_MatchesVector(string input, string output)
    {
        byte[] plaintext = Hex.Bytes(input);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] decrypted = new byte[plaintext.Length];
        byte[] key = Encoding.ASCII.GetBytes("chicken teriyaki");

        KerberosAesCts.Encrypt(key, plaintext, ciphertext);
        KerberosAesCts.Decrypt(key, ciphertext, decrypted);

        CollectionAssert.AreEqual(Hex.Bytes(output), ciphertext);
        CollectionAssert.AreEqual(plaintext, decrypted);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    [DataRow(16)]
    [DataRow(37)]
    public void Encrypt_Aes256_IsCtsOfConfounderAndPlaintextThenHmacSha196(int plaintextLength)
    {
        byte[] key = Aes256().StringToKey("password", Encoding.UTF8.GetBytes("ATHENA.MIT.EDUraeburn"), [0, 0, 0, 1]);
        byte[] plaintext = [.. Enumerable.Range(0, plaintextLength).Select(value => (byte)value)];
        AesSha1KerberosEncryption encryption = Aes256();
        byte[] confounded = [.. Confounder, .. plaintext];
        byte[] expected = new byte[confounded.Length];
        KerberosAesCts.Encrypt(encryption.DeriveKey(key, [0, 0, 0, 3, 0xAA]), confounded, expected);
        byte[] hmac = HMACSHA1.HashData(encryption.DeriveKey(key, [0, 0, 0, 3, 0x55]), confounded);

        byte[] ciphertext = encryption.Encrypt(key, 3, plaintext);

        CollectionAssert.AreEqual((byte[])[.. expected, .. hmac[..12]], ciphertext);
        CollectionAssert.AreEqual(plaintext, encryption.Decrypt(key, 3, ciphertext));
    }

    [TestMethod]
    public void Decrypt_Aes128RoundTrip_ReturnsPlaintext()
    {
        byte[] key = Hex.Bytes("42263c6e89f4fc28b8df68ee09799f15");
        byte[] plaintext = Encoding.ASCII.GetBytes("I would like the General Gau's Chicken, please.");

        byte[] ciphertext = Aes128().Encrypt(key, 11, plaintext);

        Assert.HasCount(16 + plaintext.Length + 12, ciphertext);
        CollectionAssert.AreEqual(plaintext, Aes128().Decrypt(key, 11, ciphertext));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(20)]
    [DataRow(40)]
    public void Decrypt_TamperedCiphertext_ThrowsIntegrityCheckFailed(int tamperedIndex)
    {
        byte[] key = Hex.Bytes("42263c6e89f4fc28b8df68ee09799f15");
        byte[] ciphertext = Aes128().Encrypt(key, 11, Encoding.ASCII.GetBytes("a message of some length"));
        ciphertext[tamperedIndex] ^= 1;

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Aes128().Decrypt(key, 11, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    [TestMethod]
    public void Decrypt_WrongUsage_ThrowsIntegrityCheckFailed()
    {
        byte[] key = Hex.Bytes("42263c6e89f4fc28b8df68ee09799f15");
        byte[] ciphertext = Aes128().Encrypt(key, 11, [1, 2, 3]);

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Aes128().Decrypt(key, 12, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    [TestMethod]
    public void Decrypt_ShorterThanConfounderAndHmac_ThrowsCiphertextTooShort()
    {
        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Aes128().Decrypt(new byte[16], 1, new byte[27]));

        Assert.AreEqual(KerberosCryptographyError.CiphertextTooShort, exception.Error);
    }

    [TestMethod]
    public void ComputeChecksum_IsHmacSha196UnderTheChecksumKey()
    {
        byte[] key = Hex.Bytes("fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161");
        byte[] data = Encoding.ASCII.GetBytes("checksummed data");
        AesSha1KerberosEncryption encryption = Aes256();
        byte[] expected = HMACSHA1.HashData(encryption.DeriveKey(key, [0, 0, 0, 15, 0x99]), data)[..12];

        byte[] checksum = encryption.ComputeChecksum(key, 15, data);

        CollectionAssert.AreEqual(expected, checksum);
        Assert.IsTrue(encryption.VerifyChecksum(key, 15, data, checksum));
        Assert.IsFalse(encryption.VerifyChecksum(key, 16, data, checksum));
    }

    // MIT Kerberos t_cksums.c at commit 50588db5d26e81f3d564d1f69435af34ae80d9b2: its
    // CKSUMTYPE_HMAC_SHA1_96_AES128 and CKSUMTYPE_HMAC_SHA1_96_AES256 cases.
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "eight nine ten eleven twelve thirteen", 3, "9062430C8CDA3388922E6D6A509F5B7A", "01A4B088D45628F6946614E3")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fourteen", 4, "B1AE4CD8462AFF1677053CC9279AAC30B796FB81CE21474DD3DDBCFEA4EC76D7", "E08739E3279E2903EC8E3836")]
    public void ComputeChecksum_MitTCksumsCase_MatchesVectorAndVerifies(KerberosEncryptionType encryptionType, string data, int usage, string key, string expected)
    {
        KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(Confounder));
        byte[] published = Hex.Bytes(expected);
        byte[] flipped = Hex.Bytes(expected);
        flipped[^1] ^= 0x01;

        byte[] checksum = encryption.ComputeChecksum(Hex.Bytes(key), usage, Encoding.ASCII.GetBytes(data));

        CollectionAssert.AreEqual(published, checksum);
        Assert.IsTrue(encryption.VerifyChecksum(Hex.Bytes(key), usage, Encoding.ASCII.GetBytes(data), published));
        Assert.IsFalse(encryption.VerifyChecksum(Hex.Bytes(key), usage, Encoding.ASCII.GetBytes(data), flipped));
    }

    [TestMethod]
    public void ComputePseudoRandom_IsSha1BlockEncryptedUnderDkPrf()
    {
        byte[] key = Hex.Bytes("42263c6e89f4fc28b8df68ee09799f15");
        AesSha1KerberosEncryption encryption = Aes128();
        using Aes aes = Aes.Create();
        aes.SetKey(encryption.DeriveKey(key, "prf"u8));
        byte[] expected = aes.EncryptEcb(SHA1.HashData("test"u8)[..16], PaddingMode.None);

        CollectionAssert.AreEqual(expected, encryption.ComputePseudoRandom(key, "test"u8));
    }

    // MIT Kerberos t_decrypt.c at commit 50588db5d26e81f3d564d1f69435af34ae80d9b2: its five
    // aes128-cts-hmac-sha1-96 and five aes256-cts-hmac-sha1-96 cases, key usages 0 to 4.
    [TestMethod]
    [DataRow(128, "", 0, "5A5C0F0BA54F3828B2195E66CA24A289", "49FF8E11C173D9583A3254FBE7B1F1DF36C538E8416784A1672E6676")]
    [DataRow(128, "1", 1, "98450E3F3BAA13F5C99BEB936981B06F", "F86742F537B35DC2174A4DBAA920FAF9042090B065E1EBB1CAD9A65394")]
    [DataRow(128, "9 bytesss", 2, "9062430C8CDA3388922E6D6A509F5B7A", "68FB9679601F45C78857B2BF820FD6E53ECA8D42FD4B1D7024A09205ABB7CD2EC26C355D2F")]
    [DataRow(128, "13 bytes byte", 3, "033EE6502C54FD23E27791E987983827", "EC366D0327A933BF49330E650E49BC6B974637FE80BF532FE51795B4809718E6194724DB948D1FD637")]
    [DataRow(128, "30 bytes bytes bytes bytes byt", 4, "DCEEB70B3DE76562E689226C76429148", "C96081032D5D8EEB7E32B4089F789D0FAA481DEA74C0F97CBF3146DDFCF8E800156ECB532FC203E30FF600B63B350939FECE510F02D7FF1E7BAC")]
    [DataRow(256, "", 0, "17F275F2954F2ED1F90C377BA7F4D6A369AA0136E0BF0C927AD6133C693759A9", "E5094C55EE7B38262E2B044280B069379A95BF95BD8376FB3281B435")]
    [DataRow(256, "1", 1, "B9477E1FF0329C0050E20CE6C72D2DFF27E8FE541AB0954429A9CB5B4F7B1E2A", "406150B97AEB76D43B36B62CC1ECDFBE6F40E95755E0BEB5C27825F3A4")]
    [DataRow(256, "9 bytesss", 2, "B1AE4CD8462AFF1677053CC9279AAC30B796FB81CE21474DD3DDBCFEA4EC76D7", "09957AA25FCAF88F7B39E4406E633012D5FEA21853F6478DA7065CAEF41FD454A40824EEC5")]
    [DataRow(256, "13 bytes byte", 3, "E5A72BE9B7926C1225BAFEF9C1872E7BA4CDB2B17893D84ABD90ACDD8764D966", "D8F1AAFEEC84587CC3E700A774E56651A6D693E174EC4473B5E6D96F80297A653FB818AD893E719F96")]
    [DataRow(256, "30 bytes bytes bytes bytes byt", 4, "F1C795E9248A09338D82C3F8D5B567040B0110736845041347235B1404231398", "D1137A4D634CFECE924DBC3BF6790648BD5CFF7DE0E7B99460211D0DAEF3D79A295C688858F3B34B9CBD6EEBAE81DAF6B734D4D498B6714F1C1D")]
    public void Decrypt_MitTDecryptCase_GivesThePlaintext(int keyBits, string plaintext, int usage, string key, string ciphertext)
    {
        AesSha1KerberosEncryption encryption = keyBits == 128 ? Aes128() : Aes256();

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(plaintext), encryption.Decrypt(Hex.Bytes(key), usage, Hex.Bytes(ciphertext)));
    }

    // The aes256 "13 bytes byte" case above with one bit of its MAC flipped.
    [TestMethod]
    public void Decrypt_MitTDecryptCaseWithOneBitFlipped_ThrowsIntegrityCheckFailed()
    {
        byte[] ciphertext = Hex.Bytes("D8F1AAFEEC84587CC3E700A774E56651A6D693E174EC4473B5E6D96F80297A653FB818AD893E719F96");
        ciphertext[^1] ^= 0x01;

        KerberosCryptographyException exception = Assert.ThrowsExactly<KerberosCryptographyException>(() => Aes256().Decrypt(Hex.Bytes("E5A72BE9B7926C1225BAFEF9C1872E7BA4CDB2B17893D84ABD90ACDD8764D966"), 3, ciphertext));

        Assert.AreEqual(KerberosCryptographyError.IntegrityCheckFailed, exception.Error);
    }

    private static AesSha1KerberosEncryption Aes128() =>
        (AesSha1KerberosEncryption)KerberosEncryption.Create(KerberosEncryptionType.Aes128CtsHmacSha196, new FixedKerberosRandomSource(Confounder));

    private static AesSha1KerberosEncryption Aes256() =>
        (AesSha1KerberosEncryption)KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, new FixedKerberosRandomSource(Confounder));
}
