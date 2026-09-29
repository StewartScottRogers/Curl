namespace Curl.Kerberos;

/// <summary>
/// Pins <see cref="KeytabReader" /> to the keytab MIT's <c>kadmin.local ktadd</c> wrote
/// (<see cref="RecordedKerberosFiles" />), and to built files for holes, the 8-bit key
/// version number and the failures.
/// </summary>
[TestClass]
public sealed class KeytabReaderTests
{
    private static readonly DateTimeOffset WrittenAt = DateTimeOffset.FromUnixTimeSeconds(1790650057);

    [TestMethod]
    public void Read_RecordedKeytab_GivesBothKeysKlistShowed()
    {
        using Keytab keytab = KeytabReader.Read(RecordedKerberosFiles.HttpServiceKeytab);

        Assert.HasCount(2, keytab.Entries);
        AssertEntry(keytab.Entries[0], 18, "601fea012fabf038ba449cbddf548bc71da37762c12643f9b8c92e65d7754e6c");
        AssertEntry(keytab.Entries[1], 17, "4af9eed7b18540effef29446c3e4e677");
    }

    [TestMethod]
    public void Dispose_RecordedKeytab_ZeroesEveryKey()
    {
        Keytab keytab = KeytabReader.Read(RecordedKerberosFiles.HttpServiceKeytab);

        keytab.Dispose();

        foreach (KeytabEntry entry in keytab.Entries)
        {
            Assert.IsFalse(entry.Key.Value.ContainsAnyExcept((byte)0));
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(6)]
    [DataRow(50)]
    [DataRow(97)]
    [DataRow(150)]
    public void Read_TruncatedRecordedKeytab_FailsAsTruncated(int length)
    {
        byte[] truncated = RecordedKerberosFiles.HttpServiceKeytab[..length];

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(truncated));

        Assert.AreEqual(KerberosFileError.Truncated, failure.Error);
    }

    [TestMethod]
    [DataRow((byte)0x05, (byte)0x01)]
    [DataRow((byte)0x05, (byte)0x04)]
    [DataRow((byte)0x06, (byte)0x02)]
    public void Read_OtherVersion_FailsAsUnknownVersion(byte marker, byte version)
    {
        byte[] bytes = [marker, version, .. RecordedKerberosFiles.HttpServiceKeytab[2..]];

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(bytes));

        Assert.AreEqual(KerberosFileError.UnknownVersion, failure.Error);
    }

    [TestMethod]
    public void Read_HoleBetweenEntries_SkipsTheHole()
    {
        byte[] first = Entry(kvno8: 3, kvno32: null);
        byte[] bytes = new BigEndianBytes().Byte(0x05).Byte(0x02)
            .Int32(first.Length).Raw(first)
            .Int32(-6).Raw(new byte[6])
            .Int32(first.Length).Raw(first)
            .ToArray();

        using Keytab keytab = KeytabReader.Read(bytes);

        Assert.HasCount(2, keytab.Entries);
    }

    [TestMethod]
    public void Read_HoleRunningPastTheEnd_FailsAsTruncated()
    {
        byte[] bytes = new BigEndianBytes().Byte(0x05).Byte(0x02).Int32(-6).Raw(new byte[5]).ToArray();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(bytes));

        Assert.AreEqual(KerberosFileError.Truncated, failure.Error);
    }

    [TestMethod]
    public void Read_ZeroSize_EndsTheKeytab()
    {
        byte[] first = Entry(kvno8: 3, kvno32: null);
        byte[] bytes = new BigEndianBytes().Byte(0x05).Byte(0x02)
            .Int32(first.Length).Raw(first)
            .Int32(0).Raw(0xFF, 0xFF, 0xFF, 0xFF, 0xFF)
            .ToArray();

        using Keytab keytab = KeytabReader.Read(bytes);

        Assert.HasCount(1, keytab.Entries);
    }

    [TestMethod]
    public void Read_FewerThanFourBytesAfterAnEntry_EndsTheKeytab()
    {
        byte[] first = Entry(kvno8: 3, kvno32: null);
        byte[] bytes = new BigEndianBytes().Byte(0x05).Byte(0x02).Int32(first.Length).Raw(first).Raw(0, 0, 0).ToArray();

        using Keytab keytab = KeytabReader.Read(bytes);

        Assert.HasCount(1, keytab.Entries);
    }

    [TestMethod]
    public void Read_VersionHeaderOnly_IsAnEmptyKeytab()
    {
        using Keytab keytab = KeytabReader.Read([0x05, 0x02]);

        Assert.IsEmpty(keytab.Entries);
    }

    [TestMethod]
    [DataRow(7, null, 7u)]
    [DataRow(7, 0u, 7u)]
    [DataRow(44, 300u, 300u)]
    public void Read_KeyVersionNumber_Uses32BitFieldWhenPresentAndNotZero(int kvno8, uint? kvno32, uint expected)
    {
        byte[] entry = Entry((byte)kvno8, kvno32);
        byte[] bytes = new BigEndianBytes().Byte(0x05).Byte(0x02).Int32(entry.Length).Raw(entry).ToArray();

        using Keytab keytab = KeytabReader.Read(bytes);

        KeytabEntry read = keytab.Entries.Single();
        Assert.AreEqual(expected, read.KeyVersionNumber);
        Assert.AreEqual("alice@EXAMPLE.TEST", read.Principal.ToString());
        Assert.AreEqual(23, read.Key.EncryptionType);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, read.Key.Value.ToArray());
    }

    private static byte[] Entry(byte kvno8, uint? kvno32)
    {
        BigEndianBytes entry = new BigEndianBytes().UInt16(1).String16("EXAMPLE.TEST").String16("alice")
            .Int32(1).UInt32(1_000_000).Byte(kvno8).UInt16(23).Bytes16(1, 2, 3, 4);
        if (kvno32 is uint value)
        {
            entry.UInt32(value);
        }

        return entry.ToArray();
    }

    private static void AssertEntry(KeytabEntry entry, int encryptionType, string keyHex)
    {
        Assert.AreEqual("HTTP/server.example.test@EXAMPLE.TEST", entry.Principal.ToString());
        Assert.AreEqual(1, entry.Principal.NameType);
        Assert.AreEqual(WrittenAt, entry.Timestamp);
        Assert.AreEqual(2u, entry.KeyVersionNumber);
        Assert.AreEqual(encryptionType, entry.Key.EncryptionType);
        Assert.AreEqual(keyHex, Convert.ToHexStringLower(entry.Key.Value));
    }
}
