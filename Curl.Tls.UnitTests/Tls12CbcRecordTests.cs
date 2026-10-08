using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// CBC records in detail: the padding checked without a branch on the decrypted bytes, bad
/// padding and a bad MAC both ending in <c>bad_record_mac</c>, malformed lengths, and
/// TLS 1.0's empty-fragment BEAST countermeasure (ADR-0150).
/// </summary>
[TestClass]
public sealed class Tls12CbcRecordTests
{
    private static readonly byte[] MacKey = Enumerable.Range(0x40, 20).Select(value => (byte)value).ToArray();

    private static readonly byte[] AesKey = Enumerable.Range(0x60, 16).Select(value => (byte)value).ToArray();

    private static readonly byte[] Iv = Enumerable.Range(0x80, 16).Select(value => (byte)value).ToArray();

    private static readonly byte[] Content = "The quick brown fox jumps"u8.ToArray();

    private static readonly Tls12RecordProtectionParameters Tls12AesSha1 = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void LongerPaddingThanNeededIsAccepted()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));
        Diagnostics.Arrange("record protection", Tls12AesSha1);
        Diagnostics.Arrange("padding", "35 bytes of 34");

        byte[] fragment = EncryptWithPadding(Content, Mac(Content), Enumerable.Repeat((byte)34, 35).ToArray());
        Diagnostics.Bytes("fragment", fragment);
        TlsDecodeResult<byte[]> opened = reader.Unprotect(TlsContentType.ApplicationData, fragment);
        Diagnostics.Act("alert", opened.Alert);

        Diagnostics.Diff("content", Content, opened.Succeeded ? opened.Value : []);
        CollectionAssert.AreEqual(Content, opened.Value);
    }

    [TestMethod]
    [DataRow(Tls12MacAlgorithm.HmacSha1)]
    [DataRow(Tls12MacAlgorithm.HmacSha256)]
    [DataRow(Tls12MacAlgorithm.HmacSha384)]
    public void EveryPaddingLengthOpensUnderTheBclHmacAndABadMacOrPaddingIsBadRecordMac(Tls12MacAlgorithm macAlgorithm)
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Cbc, macAlgorithm);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordCipher cipher = Tls12RecordCipher.Create(parameters, keys, null);
        int macLength = CryptographicOperations.HmacData(parameters.MacHash, keys.MacKey, ReadOnlySpan<byte>.Empty).Length;
        Diagnostics.Arrange("record protection", parameters);
        Diagnostics.Arrange("MAC length", macLength);
        Diagnostics.Arrange("padding lengths", $"0 to {Tls12CbcPadding.MaximumPaddingLength}");
        int openedCount = 0;
        int rejectedCount = 0;
        using (Diagnostics.Phase("open every padding length"))
        {
            for (int paddingLength = 0; paddingLength <= Tls12CbcPadding.MaximumPaddingLength; paddingLength++)
            {
                int contentLength = 40 + ((16 - ((40 + macLength + paddingLength + 1) % 16)) % 16);
                byte[] content = Enumerable.Range(paddingLength, contentLength).Select(value => (byte)value).ToArray();
                byte[] mac = CryptographicOperations.HmacData(parameters.MacHash, keys.MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, contentLength), .. content]);
                byte[] padding = Enumerable.Repeat((byte)paddingLength, paddingLength + 1).ToArray();
                byte[] badMac = [.. mac];
                badMac[^1] ^= 0x80;
                byte[] badPadding = [.. padding];
                badPadding[0] = (byte)(paddingLength == 0 ? 200 : paddingLength ^ 1);

                TlsDecodeResult<byte[]> opened = Open(cipher, keys.Key, content, mac, padding);
                TlsDecodeResult<byte[]> withBadMac = Open(cipher, keys.Key, content, badMac, padding);
                TlsDecodeResult<byte[]> withBadPadding = Open(cipher, keys.Key, content, mac, badPadding);
                openedCount += opened.Succeeded ? 1 : 0;
                rejectedCount += (withBadMac.Alert == TlsAlertDescription.BadRecordMac ? 1 : 0) + (withBadPadding.Alert == TlsAlertDescription.BadRecordMac ? 1 : 0);

                CollectionAssert.AreEqual(content, opened.Value, $"padding {paddingLength}");
                Assert.AreEqual(TlsAlertDescription.BadRecordMac, withBadMac.Alert, $"padding {paddingLength}");
                Assert.AreEqual(TlsAlertDescription.BadRecordMac, withBadPadding.Alert, $"padding {paddingLength}");
            }
        }

        Diagnostics.Act("records opened", openedCount);
        Diagnostics.Assert("records opened", Tls12CbcPadding.MaximumPaddingLength + 1, openedCount);
        Diagnostics.Assert("bad MAC or padding records answered bad_record_mac", 2 * (Tls12CbcPadding.MaximumPaddingLength + 1), rejectedCount);
    }

    [TestMethod]
    public void ABadPaddingByteWithAGoodMacIsBadRecordMac()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));
        Diagnostics.Arrange("padding", "2, 7, 2");

        byte[] fragment = EncryptWithPadding(Content, Mac(Content), [2, 7, 2]);
        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    public void GoodPaddingWithABadMacIsBadRecordMac()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));
        byte[] mac = Mac(Content);
        mac[0] ^= 1;
        Diagnostics.Arrange("MAC with its first bit flipped", Convert.ToHexStringLower(mac));
        Diagnostics.Arrange("padding", "2, 2, 2");

        byte[] fragment = EncryptWithPadding(Content, mac, [2, 2, 2]);
        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    public void APaddingLengthLongerThanTheRecordIsBadRecordMac()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));
        Diagnostics.Arrange("padding", "12 bytes of 200");

        byte[] fragment = EncryptWithPadding([], Mac([]), Enumerable.Repeat((byte)200, 12).ToArray());
        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    [DataRow(16 + 16)]
    [DataRow(16 + 33)]
    public void AFragmentTooShortOrNotWholeBlocksIsBadRecordMac(int length)
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));
        Diagnostics.Arrange("fragment length", length);

        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, new byte[length]).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    [DataRow(16 + 20)]
    [DataRow(16 + 17 + 20)]
    public void AnEncryptThenMacFragmentTooShortOrNotWholeBlocksIsBadRecordMac(int length)
    {
        Tls12RecordProtectionParameters parameters = Tls12AesSha1 with { EncryptThenMac = true };
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []));
        Diagnostics.Arrange("record protection", parameters);
        Diagnostics.Arrange("fragment length", length);

        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, new byte[length]).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    public void EncryptThenMacWithAGoodMacOverBadPaddingIsBadRecordMac()
    {
        Tls12RecordProtectionParameters parameters = Tls12AesSha1 with { EncryptThenMac = true };
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []));
        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        byte[] encrypted = [.. Iv, .. aes.EncryptCbc((byte[])[.. Content, 1, 1, 1, 1, 1, 1, 6], Iv, PaddingMode.None)];
        byte[] mac = HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, encrypted.Length), .. encrypted]);
        Diagnostics.Arrange("padding", "1, 1, 1, 1, 1, 1, 6");
        Diagnostics.Bytes("IV and ciphertext", encrypted);
        Diagnostics.Arrange("MAC over the ciphertext", Convert.ToHexStringLower(mac));

        TlsAlertDescription? alert = reader.Unprotect(TlsContentType.ApplicationData, [.. encrypted, .. mac]).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 9, 3, 3, 3, 3 }, 1u, 1)]
    [DataRow(new byte[] { 0 }, 1u, 0)]
    [DataRow(new byte[] { 9, 9, 3, 2, 3 }, 0u, 5)]
    [DataRow(new byte[] { 4, 4, 4, 4 }, 0u, 4)]
    public void CheckReadsThePaddingAsAMask(byte[] plaintext, uint good, int unpaddedLength)
    {
        Diagnostics.Arrange("plaintext", Convert.ToHexStringLower(plaintext));

        uint mask = Tls12CbcPadding.Check(plaintext, 0, out int actualLength);
        Diagnostics.Act("mask", $"0x{mask:x8}");
        Diagnostics.Act("unpadded length", actualLength);

        Diagnostics.Assert("mask", good == 1 ? uint.MaxValue : 0u, mask);
        Diagnostics.Assert("unpadded length", unpaddedLength, actualLength);
        Assert.AreEqual(good == 1 ? uint.MaxValue : 0u, mask);
        Assert.AreEqual(unpaddedLength, actualLength);
    }

    [TestMethod]
    public void CheckAcceptsTheLongestPaddingAndRejectsItWhenTheMacDoesNotFit()
    {
        byte[] plaintext = [.. new byte[20], .. Enumerable.Repeat((byte)255, 256)];
        Diagnostics.Arrange("plaintext", "20 zero bytes then 256 bytes of 255");

        uint fitsMask = Tls12CbcPadding.Check(plaintext, 20, out int unpaddedLength);
        uint tooLongMask = Tls12CbcPadding.Check(plaintext, 21, out int wholeLength);
        Diagnostics.Act("mask with a 20-byte MAC", $"0x{fitsMask:x8}");
        Diagnostics.Act("mask with a 21-byte MAC", $"0x{tooLongMask:x8}");

        Diagnostics.Assert("unpadded length with a 20-byte MAC", 20, unpaddedLength);
        Diagnostics.Assert("length with a 21-byte MAC", 276, wholeLength);
        Assert.AreEqual(uint.MaxValue, fitsMask);
        Assert.AreEqual(20, unpaddedLength);
        Assert.AreEqual(0u, tooLongMask);
        Assert.AreEqual(276, wholeLength);
    }

    [TestMethod]
    public void Tls10CbcSendsAnEmptyRecordBeforeApplicationDataByDefault()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);
        Diagnostics.Arrange("record protection", parameters);

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> records = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content));
        Diagnostics.Act("records written", records.Count);
        byte[] first = reader.Unprotect(TlsContentType.ApplicationData, records[0].Fragment).Value;
        byte[] second = reader.Unprotect(TlsContentType.ApplicationData, records[1].Fragment).Value;

        Diagnostics.Assert("inserts the empty fragment", true, writer.InsertsEmptyFragment);
        Diagnostics.Assert("first record's content length", 0, first.Length);
        Diagnostics.Diff("second record's content", Content, second);
        Assert.IsTrue(writer.InsertsEmptyFragment);
        Assert.HasCount(2, records);
        Assert.IsEmpty(first);
        CollectionAssert.AreEqual(Content, second);
    }

    [TestMethod]
    public void Tls10CbcSendsApplicationDataWholeWhenTheSplitIsOff()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance, insertEmptyFragment: false);
        Diagnostics.Arrange("record protection", parameters);
        Diagnostics.Arrange("insert empty fragment", false);

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> records = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content));
        int recordCount = records.Count;
        Diagnostics.Act("records written", recordCount);

        Diagnostics.Assert("inserts the empty fragment", false, writer.InsertsEmptyFragment);
        Diagnostics.Assert("records written", 1, recordCount);
        Assert.IsFalse(writer.InsertsEmptyFragment);
        Assert.HasCount(1, records);
    }

    [TestMethod]
    public void Tls10CbcSendsNoEmptyRecordBeforeHandshakeOrEmptyApplicationData()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance);
        Diagnostics.Arrange("record protection", parameters);

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> handshakeRecords = Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content));
        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> emptyApplicationDataRecords = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, []));
        Diagnostics.Act("handshake records written", handshakeRecords.Count);
        Diagnostics.Act("empty application data records written", emptyApplicationDataRecords.Count);

        Diagnostics.Assert("records written for each", "1, 1", $"{handshakeRecords.Count}, {emptyApplicationDataRecords.Count}");
        Assert.HasCount(1, handshakeRecords);
        Assert.HasCount(1, emptyApplicationDataRecords);
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1)]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1)]
    public void OnlyTls10CbcInsertsTheEmptyRecord(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance);
        Diagnostics.Arrange("record protection", parameters);

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> records = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content));
        Diagnostics.Act("records written", records.Count);

        Diagnostics.Assert("inserts the empty fragment", false, writer.InsertsEmptyFragment);
        Diagnostics.Assert("records written", 1, records.Count);
        Assert.IsFalse(writer.InsertsEmptyFragment);
        Assert.HasCount(1, records);
    }

    [TestMethod]
    public void AReadOnlyCbcCipherRefusesToSealWithAnExplicitIv()
    {
        using Tls12RecordCipher cipher = Tls12RecordCipher.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []), null);
        Diagnostics.Arrange("record protection", Tls12AesSha1);
        Diagnostics.Arrange("random source", "none");

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(() => cipher.Seal(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content));
        Diagnostics.Act("exception", thrown.GetType().Name);

        Diagnostics.Assert("exception", nameof(InvalidOperationException), thrown.GetType().Name);
    }

    private static byte[] Mac(byte[] content) =>
        HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, content.Length), .. content]);

    private static TlsDecodeResult<byte[]> Open(Tls12RecordCipher cipher, byte[] key, byte[] content, byte[] mac, byte[] padding)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        byte[] fragment = [.. Iv, .. aes.EncryptCbc((byte[])[.. content, .. mac, .. padding], Iv, PaddingMode.None)];
        return cipher.Open(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, fragment);
    }

    private static byte[] EncryptWithPadding(byte[] content, byte[] mac, byte[] padding)
    {
        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        return [.. Iv, .. aes.EncryptCbc((byte[])[.. content, .. mac, .. padding], Iv, PaddingMode.None)];
    }
}
