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

    private static AesSha1KerberosEncryption Aes128() =>
        (AesSha1KerberosEncryption)KerberosEncryption.Create(KerberosEncryptionType.Aes128CtsHmacSha196, new FixedKerberosRandomSource(Confounder));

    private static AesSha1KerberosEncryption Aes256() =>
        (AesSha1KerberosEncryption)KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, new FixedKerberosRandomSource(Confounder));
}
