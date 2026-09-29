namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KeytabStore" /> names the default keytab as MIT does
/// (<c>KRB5_KTNAME</c>, then <c>FILE:/etc/krb5.keytab</c>) and reads only <c>FILE:</c> and
/// <c>WRFILE:</c> keytabs, through the file seam.
/// </summary>
[TestClass]
public sealed class KeytabStoreTests
{
    [TestMethod]
    public void DefaultKeytabName_Krb5KtnameSet_IsKrb5Ktname()
    {
        KeytabStore store = Store(new InMemoryKerberosFiles(), "FILE:/srv/http.keytab");

        Assert.AreEqual("FILE:/srv/http.keytab", store.DefaultKeytabName());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DefaultKeytabName_Krb5KtnameUnsetOrEmpty_IsEtcKrb5Keytab(string? krb5Ktname)
    {
        KeytabStore store = Store(new InMemoryKerberosFiles(), krb5Ktname);

        Assert.AreEqual("FILE:/etc/krb5.keytab", store.DefaultKeytabName());
    }

    [TestMethod]
    public void ReadDefault_NoKrb5Ktname_ReadsEtcKrb5Keytab()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/etc/krb5.keytab", RecordedKeytab());

        using Keytab keytab = Store(files, null).ReadDefault();

        Assert.HasCount(2, keytab.Entries);
    }

    [TestMethod]
    [DataRow("FILE:/srv/http.keytab")]
    [DataRow("WRFILE:/srv/http.keytab")]
    [DataRow("/srv/http.keytab")]
    public void Read_FileWritableFileOrBarePath_ReadsThatFileAndZeroesItsBytes(string keytabName)
    {
        byte[] bytes = RecordedKeytab();
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/srv/http.keytab", bytes);

        using Keytab keytab = Store(files, null).Read(keytabName);

        Assert.HasCount(2, keytab.Entries);
        Assert.IsFalse(bytes.AsSpan().ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Read_UnreadableFile_ZeroesItsBytesAndFails()
    {
        byte[] bytes = [0x05, 0x01];
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/srv/http.keytab", bytes);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(files, null).Read("/srv/http.keytab"));

        Assert.AreEqual(KerberosFileError.UnknownVersion, failure.Error);
        Assert.IsFalse(bytes.AsSpan().ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Read_NoSuchFile_FailsAsNotFound()
    {
        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(
            () => Store(new InMemoryKerberosFiles(), null).Read("FILE:/etc/krb5.keytab"));

        Assert.AreEqual(KerberosFileError.NotFound, failure.Error);
    }

    [TestMethod]
    [DataRow("MEMORY:x")]
    [DataRow("KDB:")]
    public void Read_OtherKeytabType_FailsAsUnsupportedType(string keytabName)
    {
        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(
            () => Store(new InMemoryKerberosFiles(), null).Read(keytabName));

        Assert.AreEqual(KerberosFileError.UnsupportedType, failure.Error);
    }

    private static byte[] RecordedKeytab() => (byte[])RecordedKerberosFiles.HttpServiceKeytab.Clone();

    private static KeytabStore Store(InMemoryKerberosFiles files, string? krb5Ktname) =>
        new(files, name => name == KeytabStore.KeytabNameVariable ? krb5Ktname : "unexpected");
}
