namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosEncryptionTypeList" /> resolves encryption type relations
/// to the numbers MIT 1.22.1's <c>kinit</c> offered for them, measured from the AS-REQ it
/// sent with that relation as <c>permitted_enctypes</c> (ADR-0209), plus the examples MIT's
/// <c>krb5.conf</c> documentation gives (<c>DEFAULT -rc4</c>, <c>des3 DEFAULT</c>) and the
/// remaining aliases and stray signs of MIT's name table.
/// </summary>
[TestClass]
public sealed class KerberosEncryptionTypeListTests
{
    [TestMethod]
    [DataRow("DEFAULT", new[] { 18, 17, 20, 19, 25, 26 })]
    [DataRow("DEFAULT -aes", new[] { 25, 26 })]
    [DataRow("des3 DEFAULT", new[] { 16, 18, 17, 20, 19, 25, 26 })]
    [DataRow("DEFAULT -rc4", new[] { 18, 17, 20, 19, 25, 26 })]
    [DataRow("DEFAULT +rc4 -camellia", new[] { 18, 17, 20, 19, 23 })]
    [DataRow("camellia aes", new[] { 26, 25, 18, 17, 20, 19 })]
    [DataRow("rc4 des3", new[] { 23, 16 })]
    [DataRow("arcfour-hmac-exp rc4-hmac des3-hmac-sha1 aes128-sha2 aes256-sha2 aes128-cts aes256-sha1 camellia128-cts camellia256-cts-cmac", new[] { 23, 16, 19, 20, 17, 18, 25, 26 })]
    [DataRow("AES256-CTS Default -AES128-CTS", new[] { 18, 20, 19, 25, 26 })]
    [DataRow("des-cbc-crc bogus aes128-cts", new[] { 17 })]
    [DataRow("des des-cbc-md5 aes128-cts", new[] { 17 })]
    [DataRow("+aes -aes256-cts aes256-cts", new[] { 17, 20, 19, 18 })]
    [DataRow("des3-cbc-raw des3-cbc-sha1-kd arcfour-hmac-md5 aes128-sha1 aes256-cts-hmac-sha1-96", new[] { 16, 23, 17, 18 })]
    [DataRow("des3-cbc-sha1 arcfour-hmac rc4-hmac-exp arcfour-hmac-md5-exp aes128-cts-hmac-sha1-96 aes256-cts aes128-cts-hmac-sha256-128 aes256-cts-hmac-sha384-192 camellia128-cts-cmac camellia256-cts", new[] { 16, 23, 17, 18, 19, 20, 25, 26 })]
    [DataRow("aes -aes - + -bogus", new int[0])]
    [DataRow("bogus", new int[0])]
    public void Resolve_WeakCryptoNotAllowed_GivesMitsList(string relation, int[] expected)
    {
        IReadOnlyList<int> types = KerberosEncryptionTypeList.Resolve(relation.Split(' '), allowWeakCrypto: false);

        CollectionAssert.AreEqual(expected, types.ToArray());
    }

    [TestMethod]
    public void Resolve_WeakCryptoAllowed_ListsArcfourExport()
    {
        IReadOnlyList<int> types = KerberosEncryptionTypeList.Resolve(["arcfour-hmac-exp", "aes128-cts"], allowWeakCrypto: true);

        CollectionAssert.AreEqual(new[] { KerberosEncryptionTypeList.WeakArcfourExport, 17 }, types.ToArray());
    }

    [TestMethod]
    public void Resolve_NullWords_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KerberosEncryptionTypeList.Resolve(null!, allowWeakCrypto: false));
}
