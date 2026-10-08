using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// TLS 1.2, 1.1 and 1.0 record protection: each layout rebuilt here from its RFC with the
/// BCL and <c>Curl.Cryptography</c>, round trips for every suite family and version, and
/// the failures that must all be <c>bad_record_mac</c>.
/// </summary>
[TestClass]
public sealed class Tls12RecordProtectionTests
{
    private static readonly byte[] MacKey = Enumerable.Range(0x40, 20).Select(value => (byte)value).ToArray();

    private static readonly byte[] AesKey = Enumerable.Range(0x60, 16).Select(value => (byte)value).ToArray();

    private static readonly byte[] Content = "The quick brown fox jumps"u8.ToArray();

    public static IEnumerable<object[]> EverySuiteFamily =>
    [
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes256Cbc, Tls12MacAlgorithm.HmacSha1, true],
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.Camellia128Cbc, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacMd5, false],
        [TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls11, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1, true],
        [TlsProtocolVersion.Tls11, Tls12BulkCipher.Camellia256Cbc, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls11, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha256, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Cbc, Tls12MacAlgorithm.HmacSha384, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Cbc, Tls12MacAlgorithm.HmacSha1, true],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Camellia128Cbc, Tls12MacAlgorithm.HmacSha256, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Camellia256Cbc, Tls12MacAlgorithm.HmacSha256, true],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha256, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Gcm, Tls12MacAlgorithm.None, true],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aria128Gcm, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aria256Gcm, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Ccm, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Ccm, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Ccm8, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Ccm8, Tls12MacAlgorithm.None, false],
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacMd5, false],
        [TlsProtocolVersion.Tls10, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls11, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacSha1, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacMd5, false],
        [TlsProtocolVersion.Tls12, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacSha1, true],
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DynamicData(nameof(EverySuiteFamily))]
    public void RecordsRoundTripThroughTheReadState(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, bool encryptThenMac)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm, encryptThenMac);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);
        byte[][] writes = [[], [0x42], Content, RandomNumberGenerator.GetBytes(20000)];
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("writes", string.Join(", ", writes.Select(write => $"{write.Length} bytes")));

        using (Diagnostics.Phase("protect and unprotect every write"))
        {
            foreach (byte[] write in writes)
            {
                List<byte> received = [];
                foreach ((TlsContentType contentType, TlsProtocolVersion recordVersion, byte[] fragment) in Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, write)))
                {
                    Assert.AreEqual(TlsContentType.ApplicationData, contentType);
                    Assert.AreEqual(version, recordVersion);
                    received.AddRange(reader.Unprotect(contentType, fragment).Value);
                }

                Diagnostics.Diff($"round trip of {write.Length} bytes", write, received.ToArray());
                CollectionAssert.AreEqual(write, received);
            }
        }

        Diagnostics.Act("sequence numbers", $"writer {writer.SequenceNumber}, reader {reader.SequenceNumber}");
        Assert.AreEqual(writer.SequenceNumber, reader.SequenceNumber);
    }

    [TestMethod]
    [DynamicData(nameof(EverySuiteFamily))]
    public void AFlippedBitAnywhereIsBadRecordMac(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, bool encryptThenMac)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm, encryptThenMac);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance, insertEmptyFragment: false);
        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("fragment", $"{fragment.Length} bytes");

        foreach (int position in new[] { 0, fragment.Length / 2, fragment.Length - 1 })
        {
            using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);
            byte[] tampered = fragment.ToArray();
            tampered[position] ^= 0x01;

            TlsAlertDescription? alert = reader.Unprotect(TlsContentType.Handshake, tampered).Alert;
            Diagnostics.Act($"bit flipped at {position}", alert);
            Diagnostics.Assert($"alert for bit flipped at {position}", TlsAlertDescription.BadRecordMac, alert);
            Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert, $"bit flipped at {position}");
        }
    }

    [TestMethod]
    [DynamicData(nameof(EverySuiteFamily))]
    public void AnotherSequenceNumberOrContentTypeIsBadRecordMac(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, bool encryptThenMac)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm, encryptThenMac);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance, insertEmptyFragment: false);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);
        byte[] first = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        byte[] second = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("records", "two handshake records; the first read as an alert, the second read as sequence number 0");

        TlsAlertDescription? wrongType = reader.Unprotect(TlsContentType.Alert, first).Alert;
        TlsAlertDescription? wrongSequence = Tls12RecordReadState.Create(parameters, keys).Unprotect(TlsContentType.Handshake, second).Alert;
        Diagnostics.Act("alerts", $"another content type {wrongType}, another sequence number {wrongSequence}");

        Diagnostics.Assert("alerts", $"{TlsAlertDescription.BadRecordMac}, {TlsAlertDescription.BadRecordMac}", $"{wrongType}, {wrongSequence}");
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, wrongType);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, wrongSequence);
    }

    [TestMethod]
    public void Tls12CbcSealsIvThenContentMacAndPaddingUnderTheExplicitIv()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        byte[] iv = Enumerable.Range(0x80, 16).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []), Tls12Records.Replay(iv));
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("explicit iv", Convert.ToHexString(iv));

        byte[] records = writer.Protect(TlsContentType.ApplicationData, Content);
        Diagnostics.Act("records", $"{records.Length} bytes");
        Diagnostics.Bytes("records", records);

        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length), .. Content]);
        byte[] plaintext = [.. Content, .. mac, .. Enumerable.Repeat((byte)2, 3)];
        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        byte[] fragment = [.. iv, .. aes.EncryptCbc(plaintext, iv, PaddingMode.None)];
        Diagnostics.Diff("records", (byte[])[0x17, 0x03, 0x03, 0x00, (byte)fragment.Length, .. fragment], records);
        CollectionAssert.AreEqual((byte[])[0x17, 0x03, 0x03, 0x00, (byte)fragment.Length, .. fragment], records);
    }

    [TestMethod]
    public void Tls10CbcChainsTheLastCiphertextBlockIntoTheNextRecord()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        byte[] keyBlockIv = Enumerable.Range(0xA0, 16).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, keyBlockIv), Tls12Records.Replay());
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("key block iv", Convert.ToHexString(keyBlockIv));

        byte[] first = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        byte[] second = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        Diagnostics.Act("fragments", $"first {first.Length} bytes, second {second.Length} bytes");

        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        Diagnostics.Diff("second fragment", aes.EncryptCbc(MacThenPad(1, TlsContentType.Handshake, TlsProtocolVersion.Tls10), first[^16..], PaddingMode.None), second);
        CollectionAssert.AreEqual(aes.EncryptCbc(MacThenPad(0, TlsContentType.Handshake, TlsProtocolVersion.Tls10), keyBlockIv, PaddingMode.None), first);
        CollectionAssert.AreEqual(aes.EncryptCbc(MacThenPad(1, TlsContentType.Handshake, TlsProtocolVersion.Tls10), first[^16..], PaddingMode.None), second);
    }

    [TestMethod]
    public void EncryptThenMacAppendsTheMacOverTheIvAndCiphertext()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, EncryptThenMac: true);
        byte[] iv = Enumerable.Range(0x80, 16).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []), Tls12Records.Replay(iv));
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("explicit iv", Convert.ToHexString(iv));

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;
        Diagnostics.Act("fragment", $"{fragment.Length} bytes");

        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        byte[] encrypted = [.. iv, .. aes.EncryptCbc((byte[])[.. Content, .. Enumerable.Repeat((byte)6, 7)], iv, PaddingMode.None)];
        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, encrypted.Length), .. encrypted]);
        Diagnostics.Diff("fragment", (byte[])[.. encrypted, .. mac], fragment);
        CollectionAssert.AreEqual((byte[])[.. encrypted, .. mac], fragment);
    }

    [TestMethod]
    public void GcmSendsTheSequenceNumberAsTheExplicitNonceAfterTheSalt()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None);
        byte[] salt = [0xC0, 0xC1, 0xC2, 0xC3];
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys([], AesKey, salt), Tls12Records.Replay());
        writer.Protect(TlsContentType.Handshake, [1]);
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("salt", Convert.ToHexString(salt));

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;
        Diagnostics.Act("fragment", $"{fragment.Length} bytes, explicit nonce {Convert.ToHexString(fragment[..8])}");

        byte[] explicitNonce = [0, 0, 0, 0, 0, 0, 0, 1];
        byte[] ciphertext = new byte[Content.Length];
        byte[] tag = new byte[16];
        using AesGcm aesGcm = new(AesKey, 16);
        aesGcm.Encrypt((byte[])[.. salt, .. explicitNonce], Content, ciphertext, tag, Tls12Records.AdditionalData(1, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length));
        Diagnostics.Diff("fragment", (byte[])[.. explicitNonce, .. ciphertext, .. tag], fragment);
        CollectionAssert.AreEqual((byte[])[.. explicitNonce, .. ciphertext, .. tag], fragment);
    }

    [TestMethod]
    public void ChaCha20Poly1305XorsTheSequenceNumberIntoTheIvAndSendsNoExplicitNonce()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None);
        byte[] key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        byte[] iv = Enumerable.Range(0xE0, 12).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys([], key, iv), Tls12Records.Replay());
        writer.Protect(TlsContentType.Handshake, [1]);
        writer.Protect(TlsContentType.Handshake, [2]);
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("iv", Convert.ToHexString(iv));

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;
        Diagnostics.Act("fragment", $"{fragment.Length} bytes");

        byte[] nonce = iv.ToArray();
        nonce[11] ^= 2;
        byte[] ciphertext = new byte[Content.Length];
        byte[] tag = new byte[16];
        using AeadChaCha20Poly1305 aead = new(key);
        aead.Encrypt(nonce, Content, ciphertext, tag, Tls12Records.AdditionalData(2, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length));
        Diagnostics.Diff("fragment", (byte[])[.. ciphertext, .. tag], fragment);
        CollectionAssert.AreEqual((byte[])[.. ciphertext, .. tag], fragment);
    }

    [TestMethod]
    [DataRow(Tls12BulkCipher.Aes128Ccm, 16, 16)]
    [DataRow(Tls12BulkCipher.Aes256Ccm, 32, 16)]
    [DataRow(Tls12BulkCipher.Aes128Ccm8, 16, 8)]
    [DataRow(Tls12BulkCipher.Aes256Ccm8, 32, 8)]
    public void CcmSendsTheSequenceNumberAsTheExplicitNonceAfterTheSaltAndEndsWithItsTag(Tls12BulkCipher bulkCipher, int keyLength, int tagLength)
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, bulkCipher, Tls12MacAlgorithm.None);
        byte[] key = Enumerable.Range(0x10, keyLength).Select(value => (byte)value).ToArray();
        byte[] salt = [0xC0, 0xC1, 0xC2, 0xC3];
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys([], key, salt), Tls12Records.Replay());
        writer.Protect(TlsContentType.Handshake, [1]);
        Diagnostics.Arrange("parameters", $"{Describe(parameters)}, key {keyLength} bytes, tag {tagLength} bytes");

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;
        Diagnostics.Act("fragment", $"{fragment.Length} bytes, explicit nonce {Convert.ToHexString(fragment[..8])}");

        byte[] explicitNonce = [0, 0, 0, 0, 0, 0, 0, 1];
        byte[] ciphertext = new byte[Content.Length];
        byte[] tag = new byte[tagLength];
        using AeadAesCcm aesCcm = new(key);
        aesCcm.Encrypt((byte[])[.. salt, .. explicitNonce], Content, ciphertext, tag, Tls12Records.AdditionalData(1, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length));
        Diagnostics.Diff("fragment", (byte[])[.. explicitNonce, .. ciphertext, .. tag], fragment);
        CollectionAssert.AreEqual((byte[])[.. explicitNonce, .. ciphertext, .. tag], fragment);
    }

    [TestMethod]
    public void Rc4EncryptsTheContentAndItsMacWithAKeyStreamThatRunsOnAcrossRecords()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls11, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []), Tls12Records.Replay());
        Diagnostics.Arrange("parameters", Describe(parameters));

        byte[] first = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        byte[] second = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;
        Diagnostics.Act("fragments", $"first {first.Length} bytes, second {second.Length} bytes");

        using Rc4 rc4 = new(AesKey);
        byte[] expected = [.. Content, .. Mac(0, TlsContentType.Handshake, TlsProtocolVersion.Tls11), .. Content, .. Mac(1, TlsContentType.ApplicationData, TlsProtocolVersion.Tls11)];
        rc4.ApplyKeyStream(expected, expected);
        Diagnostics.Diff("both fragments", expected, (byte[])[.. first, .. second]);
        CollectionAssert.AreEqual(expected, (byte[])[.. first, .. second]);
    }

    [TestMethod]
    public void NullWithAMacSendsTheContentThenItsMac()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, [], []), Tls12Records.Replay());
        Diagnostics.Arrange("parameters", Describe(parameters));

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.Alert, Content))[0].Fragment;
        Diagnostics.Act("fragment", $"{fragment.Length} bytes");
        Diagnostics.Bytes("fragment", fragment);

        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.Alert, TlsProtocolVersion.Tls12, Content.Length), .. Content]);
        Diagnostics.Diff("fragment", (byte[])[.. Content, .. mac], fragment);
        CollectionAssert.AreEqual((byte[])[.. Content, .. mac], fragment);
    }

    [TestMethod]
    public void ThePlaintextStateSendsAndReceivesTheContentAsItIs()
    {
        using Tls12RecordWriteState writer = Tls12RecordWriteState.CreatePlaintext(TlsProtocolVersion.Tls10);
        using Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls10);
        Diagnostics.Arrange("states", "plaintext writer and reader for TLS 1.0");

        byte[] records = writer.Protect(TlsContentType.Handshake, Content);
        byte[]? received = reader.Unprotect(TlsContentType.Handshake, Content).Value;
        Diagnostics.Bytes("records", records);
        Diagnostics.Act("received", received is null ? "nothing" : $"{received.Length} bytes");

        Diagnostics.Diff("records", (byte[])[0x16, 0x03, 0x01, 0x00, (byte)Content.Length, .. Content], records);
        Diagnostics.Assert("inserts empty fragment", false, writer.InsertsEmptyFragment);
        CollectionAssert.AreEqual((byte[])[0x16, 0x03, 0x01, 0x00, (byte)Content.Length, .. Content], records);
        CollectionAssert.AreEqual(Content, received);
        Assert.IsFalse(writer.InsertsEmptyFragment);
    }

    [TestMethod]
    public void ContentOverTwoToTheFourteenIsFragmented()
    {
        using Tls12RecordWriteState writer = Tls12RecordWriteState.CreatePlaintext(TlsProtocolVersion.Tls12);
        Diagnostics.Arrange("content", $"{(2 * 16384) + 1} bytes through a plaintext TLS 1.2 writer");

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> records = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, new byte[(2 * 16384) + 1]));
        Diagnostics.Act("fragment lengths", string.Join(", ", records.Select(record => record.Fragment.Length)));

        Diagnostics.Assert("sequence number", 3UL, writer.SequenceNumber);
        CollectionAssert.AreEqual(new[] { 16384, 16384, 1 }, records.Select(record => record.Fragment.Length).ToArray());
        Assert.AreEqual(3UL, writer.SequenceNumber);
    }

    [TestMethod]
    public void AReceivedFragmentOverTheCiphertextLimitIsRecordOverflow()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls12);
        Diagnostics.Arrange("fragment", $"{16384 + 2049} bytes into a plaintext TLS 1.2 reader");

        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, new byte[16384 + 2049]).Alert;
        Diagnostics.Act("result", $"alert {alert}, sequence number {reader.SequenceNumber}");

        Diagnostics.Assert("alert", TlsAlertDescription.RecordOverflow, alert);
        Assert.AreEqual(TlsAlertDescription.RecordOverflow, alert);
        Assert.AreEqual(0UL, reader.SequenceNumber);
    }

    [TestMethod]
    public void ReceivedContentOverTwoToTheFourteenIsRecordOverflow()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls12);
        Diagnostics.Arrange("fragments", "16385 bytes, then 16384 bytes, into a plaintext TLS 1.2 reader");

        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, new byte[16385]).Alert;
        byte[]? largest = reader.Unprotect(TlsContentType.ApplicationData, new byte[16384]).Value;
        Diagnostics.Act("results", $"alert {alert}, then {largest?.Length.ToString() ?? "no"} bytes");

        Diagnostics.Assert("alert", TlsAlertDescription.RecordOverflow, alert);
        Assert.AreEqual(TlsAlertDescription.RecordOverflow, alert);
        Assert.HasCount(16384, largest!);
    }

    [TestMethod]
    [DataRow(Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None, 24)]
    [DataRow(Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None, 16)]
    [DataRow(Tls12BulkCipher.Aes128Ccm8, Tls12MacAlgorithm.None, 16)]
    [DataRow(Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.HmacSha1, 20)]
    [DataRow(Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1, 20)]
    public void AFragmentShorterThanItsTagOrMacIsBadRecordMac(Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, int shortestLength)
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, bulkCipher, macAlgorithm);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, Tls12Records.RandomKeys(parameters));
        Diagnostics.Arrange("parameters", Describe(parameters));
        Diagnostics.Arrange("fragment", $"{shortestLength - 1} bytes, one short of {shortestLength}");

        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, new byte[shortestLength - 1]).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    public void RecordsProtectedWithAnotherKeyAreBadRecordMac()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aria128Gcm, Tls12MacAlgorithm.None);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, Tls12Records.RandomKeys(parameters));

        Diagnostics.Arrange("parameters", $"{Describe(parameters)}; writer and reader given different random keys");

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;
        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    private static string Describe(Tls12RecordProtectionParameters parameters) =>
        $"{parameters.Version}, {parameters.BulkCipher}, {parameters.MacAlgorithm}, encrypt-then-MAC {parameters.EncryptThenMac}";

    private static byte[] MacThenPad(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version) =>
        [.. Content, .. Mac(sequenceNumber, contentType, version), .. Enumerable.Repeat((byte)2, 3)];

    private static byte[] Mac(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version) =>
        HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(sequenceNumber, contentType, version, Content.Length), .. Content]);
}
