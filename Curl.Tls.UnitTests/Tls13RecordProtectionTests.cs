using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Checks <see cref="Tls13RecordProtection" /> beyond RFC 8448's records: fragmentation at
/// 2^14, padding a peer adds, and each limit and failure RFC 8446 section 5 names.
/// </summary>
[TestClass]
public sealed class Tls13RecordProtectionTests
{
    private static readonly byte[] Secret = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    [TestMethod]
    [DataRow((ushort)0x1301, true)]
    [DataRow((ushort)0x1302, true)]
    [DataRow((ushort)0x1303, true)]
    [DataRow((ushort)0x1304, true)]
    [DataRow((ushort)0x1305, true)]
    [DataRow((ushort)0x1306, false)]
    [DataRow((ushort)0x1300, false)]
    public void CanProtectNamesTheFiveTls13Suites(int cipherSuite, bool expected) =>
        Assert.AreEqual(expected, Tls13RecordProtection.CanProtect((ushort)cipherSuite));

    [TestMethod]
    public void CreateRefusesAnUnknownSuiteAndMissingArguments()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Tls13RecordProtection.Create(new Tls13CipherSuite(0x1306, Tls13KeySchedule.Sha256, 16), Secret));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls13RecordProtection.Create(null!, Secret));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls13RecordProtection.Create(Tls13CipherSuite.Aes128GcmSha256, null!));
    }

    [TestMethod]
    public void ProtectSplitsContentIntoRecordsOfAtMostTwoToTheFourteen()
    {
        using Tls13RecordProtection writer = Tls13RecordProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Secret);
        using Tls13RecordProtection reader = Tls13RecordProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Secret);
        byte[] content = RandomNumberGenerator.GetBytes((2 * Tls13RecordProtection.MaximumPlaintextLength) + 1);

        byte[] records = writer.Protect(TlsContentType.ApplicationData, content);
        int firstLength = 5 + Tls13RecordProtection.MaximumPlaintextLength + 17;
        Tls13RecordContent first = reader.Unprotect(records.AsSpan(0, firstLength)).Value;
        Tls13RecordContent second = reader.Unprotect(records.AsSpan(firstLength, firstLength)).Value;
        Tls13RecordContent third = reader.Unprotect(records.AsSpan(2 * firstLength)).Value;

        Assert.AreEqual(3ul, writer.SequenceNumber);
        Assert.HasCount(5 + 1 + 17, records[(2 * firstLength)..]);
        CollectionAssert.AreEqual(content, (byte[])[.. first.Content, .. second.Content, .. third.Content]);
    }

    [TestMethod]
    public void EmptyContentIsOneEmptyRecord()
    {
        using Tls13RecordProtection writer = Tls13RecordProtection.Create(Tls13CipherSuite.Aes256GcmSha384, [.. Secret, .. Secret[..16]]);

        byte[] record = writer.Protect(TlsContentType.ApplicationData, []);

        CollectionAssert.AreEqual(new byte[] { 0x17, 0x03, 0x03, 0x00, 0x11 }, record[..5]);
        Assert.HasCount(5 + 17, record);
    }

    [TestMethod]
    public void UnprotectRemovesThePaddingAPeerAdds()
    {
        using Tls13RecordProtection reader = Create();

        Tls13RecordContent content = reader.Unprotect(Seal([7, 8, 0x17, 0, 0, 0])).Value;

        Assert.AreEqual(TlsContentType.ApplicationData, content.Type);
        CollectionAssert.AreEqual(new byte[] { 7, 8 }, content.Content);
    }

    [TestMethod]
    public void UnprotectRefusesContentOverTwoToTheFourteen()
    {
        using Tls13RecordProtection reader = Create();

        TlsDecodeResult<Tls13RecordContent> result = reader.Unprotect(Seal([.. new byte[Tls13RecordProtection.MaximumPlaintextLength + 1], 0x17]));

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, result.Alert);
    }

    [TestMethod]
    public void UnprotectRefusesAFragmentOverTheCiphertextLimit()
    {
        using Tls13RecordProtection reader = Create();

        TlsDecodeResult<Tls13RecordContent> result = reader.Unprotect(new byte[5 + Tls13RecordProtection.MaximumCiphertextLength + 1]);

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, result.Alert);
        Assert.AreEqual(0ul, reader.SequenceNumber);
    }

    [TestMethod]
    public void UnprotectCallsAFragmentShorterThanATagABadRecordMac()
    {
        using Tls13RecordProtection reader = Create();

        TlsDecodeResult<Tls13RecordContent> result = reader.Unprotect([0x17, 0x03, 0x03, 0x00, 0x0f, .. new byte[15]]);

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, result.Alert);
        Assert.AreEqual(1ul, reader.SequenceNumber);
    }

    [TestMethod]
    [DataRow((ushort)0x1304, 16)]
    [DataRow((ushort)0x1305, 8)]
    public void CcmRecordsCarryTheSuitesTagAndOpenOnTheOtherSide(int cipherSuite, int tagLength)
    {
        Tls13CipherSuite suite = Tls13CipherSuite.Find((ushort)cipherSuite)!;
        using Tls13RecordProtection writer = Tls13RecordProtection.Create(suite, Secret);
        using Tls13RecordProtection reader = Tls13RecordProtection.Create(suite, Secret);

        byte[] record = writer.Protect(TlsContentType.Handshake, [1, 2, 3]);
        Tls13RecordContent content = reader.Unprotect(record).Value;

        Assert.HasCount(5 + 3 + 1 + tagLength, record);
        Assert.AreEqual(TlsContentType.Handshake, content.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, content.Content);
    }

    [TestMethod]
    public void Ccm8FragmentShorterThanItsEightByteTagIsABadRecordMac()
    {
        using Tls13RecordProtection reader = Tls13RecordProtection.Create(Tls13CipherSuite.Aes128Ccm8Sha256, Secret);

        TlsDecodeResult<Tls13RecordContent> result = reader.Unprotect([0x17, 0x03, 0x03, 0x00, 0x07, .. new byte[7]]);

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, result.Alert);
        Assert.AreEqual(1ul, reader.SequenceNumber);
    }

    [TestMethod]
    public void Ccm8RecordWithACorruptedTagIsABadRecordMac()
    {
        using Tls13RecordProtection writer = Tls13RecordProtection.Create(Tls13CipherSuite.Aes128Ccm8Sha256, Secret);
        using Tls13RecordProtection reader = Tls13RecordProtection.Create(Tls13CipherSuite.Aes128Ccm8Sha256, Secret);
        byte[] record = writer.Protect(TlsContentType.ApplicationData, [4, 5, 6]);
        record[^1] ^= 0x01;

        TlsDecodeResult<Tls13RecordContent> result = reader.Unprotect(record);

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, result.Alert);
    }

    private static Tls13RecordProtection Create() => Tls13RecordProtection.Create(Tls13CipherSuite.Aes128GcmSha256, Secret);

    /// <summary>Protects an inner plaintext exactly as given, padding included, as the first record under <see cref="Secret" />.</summary>
    private static byte[] Seal(byte[] innerPlaintext)
    {
        Tls13TrafficKeys keys = Tls13KeySchedule.Sha256.DeriveTrafficKeys(Secret, 16);
        byte[] record = new byte[5 + innerPlaintext.Length + 16];
        record[0] = 0x17;
        record[1] = 0x03;
        record[2] = 0x03;
        record[3] = (byte)((innerPlaintext.Length + 16) >> 8);
        record[4] = (byte)(innerPlaintext.Length + 16);
        using AesGcm aes = new(keys.Key, 16);
        aes.Encrypt(keys.Iv, innerPlaintext, record.AsSpan(5, innerPlaintext.Length), record.AsSpan(5 + innerPlaintext.Length), record.AsSpan(0, 5));
        return record;
    }
}
