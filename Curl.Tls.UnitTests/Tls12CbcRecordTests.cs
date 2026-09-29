using System.Security.Cryptography;

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

    [TestMethod]
    public void LongerPaddingThanNeededIsAccepted()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));

        byte[] fragment = EncryptWithPadding(Content, Mac(Content), Enumerable.Repeat((byte)34, 35).ToArray());

        CollectionAssert.AreEqual(Content, reader.Unprotect(TlsContentType.ApplicationData, fragment).Value);
    }

    [TestMethod]
    public void ABadPaddingByteWithAGoodMacIsBadRecordMac()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));

        byte[] fragment = EncryptWithPadding(Content, Mac(Content), [2, 7, 2]);

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert);
    }

    [TestMethod]
    public void GoodPaddingWithABadMacIsBadRecordMac()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));
        byte[] mac = Mac(Content);
        mac[0] ^= 1;

        byte[] fragment = EncryptWithPadding(Content, mac, [2, 2, 2]);

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert);
    }

    [TestMethod]
    public void APaddingLengthLongerThanTheRecordIsBadRecordMac()
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));

        byte[] fragment = EncryptWithPadding([], Mac([]), Enumerable.Repeat((byte)200, 12).ToArray());

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, fragment).Alert);
    }

    [TestMethod]
    [DataRow(16 + 16)]
    [DataRow(16 + 33)]
    public void AFragmentTooShortOrNotWholeBlocksIsBadRecordMac(int length)
    {
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, new byte[length]).Alert);
    }

    [TestMethod]
    [DataRow(16 + 20)]
    [DataRow(16 + 17 + 20)]
    public void AnEncryptThenMacFragmentTooShortOrNotWholeBlocksIsBadRecordMac(int length)
    {
        Tls12RecordProtectionParameters parameters = Tls12AesSha1 with { EncryptThenMac = true };
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, new Tls12WriteKeys(MacKey, AesKey, []));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, new byte[length]).Alert);
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

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, reader.Unprotect(TlsContentType.ApplicationData, [.. encrypted, .. mac]).Alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 9, 3, 3, 3, 3 }, 1u, 1)]
    [DataRow(new byte[] { 0 }, 1u, 0)]
    [DataRow(new byte[] { 9, 9, 3, 2, 3 }, 0u, 5)]
    [DataRow(new byte[] { 4, 4, 4, 4 }, 0u, 4)]
    public void CheckReadsThePaddingAsAMask(byte[] plaintext, uint good, int unpaddedLength)
    {
        uint mask = Tls12CbcPadding.Check(plaintext, 0, out int actualLength);

        Assert.AreEqual(good == 1 ? uint.MaxValue : 0u, mask);
        Assert.AreEqual(unpaddedLength, actualLength);
    }

    [TestMethod]
    public void CheckAcceptsTheLongestPaddingAndRejectsItWhenTheMacDoesNotFit()
    {
        byte[] plaintext = [.. new byte[20], .. Enumerable.Repeat((byte)255, 256)];

        Assert.AreEqual(uint.MaxValue, Tls12CbcPadding.Check(plaintext, 20, out int unpaddedLength));
        Assert.AreEqual(20, unpaddedLength);
        Assert.AreEqual(0u, Tls12CbcPadding.Check(plaintext, 21, out int wholeLength));
        Assert.AreEqual(276, wholeLength);
    }

    [TestMethod]
    public void Tls10CbcSendsAnEmptyRecordBeforeApplicationDataByDefault()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState reader = Tls12RecordReadState.Create(parameters, keys);

        List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> records = Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content));

        Assert.IsTrue(writer.InsertsEmptyFragment);
        Assert.HasCount(2, records);
        Assert.IsEmpty(reader.Unprotect(TlsContentType.ApplicationData, records[0].Fragment).Value);
        CollectionAssert.AreEqual(Content, reader.Unprotect(TlsContentType.ApplicationData, records[1].Fragment).Value);
    }

    [TestMethod]
    public void Tls10CbcSendsApplicationDataWholeWhenTheSplitIsOff()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance, insertEmptyFragment: false);

        Assert.IsFalse(writer.InsertsEmptyFragment);
        Assert.HasCount(1, Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content)));
    }

    [TestMethod]
    public void Tls10CbcSendsNoEmptyRecordBeforeHandshakeOrEmptyApplicationData()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance);

        Assert.HasCount(1, Tls12Records.Split(writer.Protect(TlsContentType.Handshake, Content)));
        Assert.HasCount(1, Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, [])));
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1)]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacSha1)]
    public void OnlyTls10CbcInsertsTheEmptyRecord(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm);
        using Tls12RecordWriteState writer = Tls12RecordWriteState.Create(parameters, Tls12Records.RandomKeys(parameters), SystemTlsRandomSource.Instance);

        Assert.IsFalse(writer.InsertsEmptyFragment);
        Assert.HasCount(1, Tls12Records.Split(writer.Protect(TlsContentType.ApplicationData, Content)));
    }

    [TestMethod]
    public void AReadOnlyCbcCipherRefusesToSealWithAnExplicitIv()
    {
        using Tls12RecordCipher cipher = Tls12RecordCipher.Create(Tls12AesSha1, new Tls12WriteKeys(MacKey, AesKey, []), null);

        Assert.ThrowsExactly<InvalidOperationException>(() => cipher.Seal(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, Content));
    }

    private static byte[] Mac(byte[] content) =>
        HMACSHA1.HashData(MacKey, (byte[])[.. Tls12Records.AdditionalData(0, TlsContentType.ApplicationData, TlsProtocolVersion.Tls12, content.Length), .. content]);

    private static byte[] EncryptWithPadding(byte[] content, byte[] mac, byte[] padding)
    {
        using Aes aes = Aes.Create();
        aes.Key = AesKey;
        return [.. Iv, .. aes.EncryptCbc((byte[])[.. content, .. mac, .. padding], Iv, PaddingMode.None)];
    }
}
