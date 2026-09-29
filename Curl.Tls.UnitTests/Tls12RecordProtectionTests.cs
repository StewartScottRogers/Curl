using System.Security.Cryptography;
using Curl.Cryptography;

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
    ];

    [TestMethod]
    [DynamicData(nameof(EverySuiteFamily))]
    public void RecordsRoundTripThroughTheReadState(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, bool encryptThenMac)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm, encryptThenMac);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);
        byte[][] writes = [[], [0x42], Content, RandomNumberGenerator.GetBytes(20000)];

        foreach (byte[] write in writes)
        {
            List<byte> received = [];
            foreach ((TlsContentType contentType, TlsProtocolVersion recordVersion, byte[] fragment) in Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, write)))
            {
                Assert.AreEqual(TlsContentType.ApplicationData, contentType);
                Assert.AreEqual(version, recordVersion);
                received.AddRange(reader.Unprotect(contentType, fragment).Value);
            }

            CollectionAssert.AreEqual(write, received);
        }

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

        foreach (int position in new[] { 0, fragment.Length / 2, fragment.Length - 1 })
        {
            using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);
            byte[] tampered = fragment.ToArray();
            tampered[position] ^= 0x01;

            Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.Handshake, tampered).Alert, $"bit flipped at {position}");
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

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.Alert, first).Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, Tls12RecordReadState.Create(parameters, keys).Unprotect(TlsContentType.Handshake, second).Alert);
    }

    [TestMethod]
    public void Tls12CbcSealsIvThenContentMacAndPaddingUnderTheExplicitIv()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        byte[] iv = Enumerable.Range(0x80, 16).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []), Tls12Records.Replay(iv));

        byte[] records = writer.Protect(TlsContentType.ApplicationData, Content);

        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length), .. Content]);
        byte[] plaintext = [.. Content, .. mac, .. Enumerable.Repeat((byte)2, 3)];
        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        byte[] fragment = [.. iv, .. aes.EncryptCbc(plaintext, iv, PaddingMode.None)];
        CollectionAssert.AreEqual((byte[])[0x17, 0x03, 0x03, 0x00, (byte)fragment.Length, .. fragment], records);
    }

    [TestMethod]
    public void Tls10CbcChainsTheLastCiphertextBlockIntoTheNextRecord()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        byte[] keyBlockIv = Enumerable.Range(0xA0, 16).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, keyBlockIv), Tls12Records.Replay());

        byte[] first = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;
        byte[] second = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content))[0].Fragment;

        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        CollectionAssert.AreEqual(aes.EncryptCbc(MacThenPad(0, TlsContentType.Handshake, TlsProtocolVersion.Tls10), keyBlockIv, PaddingMode.None), first);
        CollectionAssert.AreEqual(aes.EncryptCbc(MacThenPad(1, TlsContentType.Handshake, TlsProtocolVersion.Tls10), first[^16..], PaddingMode.None), second);
    }

    [TestMethod]
    public void EncryptThenMacAppendsTheMacOverTheIvAndCiphertext()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, EncryptThenMac: true);
        byte[] iv = Enumerable.Range(0x80, 16).Select(value => (byte)value).ToArray();
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []), Tls12Records.Replay(iv));

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;

        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        byte[] encrypted = [.. iv, .. aes.EncryptCbc((byte[])[.. Content, .. Enumerable.Repeat((byte)6, 7)], iv, PaddingMode.None)];
        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, encrypted.Length), .. encrypted]);
        CollectionAssert.AreEqual((byte[])[.. encrypted, .. mac], fragment);
    }

    [TestMethod]
    public void GcmSendsTheSequenceNumberAsTheExplicitNonceAfterTheSalt()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None);
        byte[] salt = [0xC0, 0xC1, 0xC2, 0xC3];
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys([], AesKey, salt), Tls12Records.Replay());
        writer.Protect(TlsContentType.Handshake, [1]);

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;

        byte[] explicitNonce = [0, 0, 0, 0, 0, 0, 0, 1];
        byte[] ciphertext = new byte[Content.Length];
        byte[] tag = new byte[16];
        using AesGcm aesGcm = new(AesKey, 16);
        aesGcm.Encrypt((byte[])[.. salt, .. explicitNonce], Content, ciphertext, tag, Tls12Records.AdditionalData(1, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length));
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

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;

        byte[] nonce = iv.ToArray();
        nonce[11] ^= 2;
        byte[] ciphertext = new byte[Content.Length];
        byte[] tag = new byte[16];
        using AeadChaCha20Poly1305 aead = new(key);
        aead.Encrypt(nonce, Content, ciphertext, tag, Tls12Records.AdditionalData(2, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content.Length));
        CollectionAssert.AreEqual((byte[])[.. ciphertext, .. tag], fragment);
    }

    [TestMethod]
    public void NullWithAMacSendsTheContentThenItsMac()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, new Tls12WriteKeys(MacKey, [], []), Tls12Records.Replay());

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.Alert, Content))[0].Fragment;

        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.Alert, TlsProtocolVersion.Tls12, Content.Length), .. Content]);
        CollectionAssert.AreEqual((byte[])[.. Content, .. mac], fragment);
    }

    [TestMethod]
    public void ThePlaintextStateSendsAndReceivesTheContentAsItIs()
    {
        using Tls12RecordWriteState writer = Tls12RecordWriteState.CreatePlaintext(TlsProtocolVersion.Tls10);
        using Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls10);

        byte[] records = writer.Protect(TlsContentType.Handshake, Content);

        CollectionAssert.AreEqual((byte[])[0x16, 0x03, 0x01, 0x00, (byte)Content.Length, .. Content], records);
        CollectionAssert.AreEqual(Content, reader.Unprotect(TlsContentType.Handshake, Content).Value);
        Assert.IsFalse(writer.InsertsEmptyFragment);
    }

    [TestMethod]
    public void ContentOverTwoToTheFourteenIsFragmented()
    {
        using Tls12RecordWriteState writer = Tls12RecordWriteState.CreatePlaintext(TlsProtocolVersion.Tls12);

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> records = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, new byte[(2 * 16384) + 1]));

        CollectionAssert.AreEqual(new[] { 16384, 16384, 1 }, records.Select(record => record.Fragment.Length).ToArray());
        Assert.AreEqual(3UL, writer.SequenceNumber);
    }

    [TestMethod]
    public void AReceivedFragmentOverTheCiphertextLimitIsRecordOverflow()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls12);

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, reader.Unprotect(TlsContentType.ApplicationData, new byte[16384 + 2049]).Alert);
        Assert.AreEqual(0UL, reader.SequenceNumber);
    }

    [TestMethod]
    public void ReceivedContentOverTwoToTheFourteenIsRecordOverflow()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls12);

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, reader.Unprotect(TlsContentType.ApplicationData, new byte[16385]).Alert);
        Assert.HasCount(16384, reader.Unprotect(TlsContentType.ApplicationData, new byte[16384]).Value);
    }

    [TestMethod]
    [DataRow(Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None, 24)]
    [DataRow(Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None, 16)]
    [DataRow(Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1, 20)]
    public void AFragmentShorterThanItsTagOrMacIsBadRecordMac(Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, int shortestLength)
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, bulkCipher, macAlgorithm);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, Tls12Records.RandomKeys(parameters));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, new byte[shortestLength - 1]).Alert);
    }

    [TestMethod]
    public void RecordsProtectedWithAnotherKeyAreBadRecordMac()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aria128Gcm, Tls12MacAlgorithm.None);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, Tls12Records.RandomKeys(parameters));

        byte[] fragment = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content))[0].Fragment;

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert);
    }

    private static byte[] MacThenPad(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version)
    {
        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(sequenceNumber, contentType, version, Content.Length), .. Content]);
        return [.. Content, .. mac, .. Enumerable.Repeat((byte)2, 3)];
    }
}
