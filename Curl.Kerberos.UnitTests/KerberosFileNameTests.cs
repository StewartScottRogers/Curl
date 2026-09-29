namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosFileName" /> splits a name at its first colon as MIT's
/// <c>krb5_cc_resolve</c> does, and that the other small types say what they hold.
/// </summary>
[TestClass]
public sealed class KerberosFileNameTests
{
    [TestMethod]
    [DataRow("FILE:/tmp/krb5cc_1000", "FILE", "/tmp/krb5cc_1000")]
    [DataRow("/tmp/krb5cc_1000", "FILE", "/tmp/krb5cc_1000")]
    [DataRow("DIR::/run/user/1000/krb5cc/tkt", "DIR", ":/run/user/1000/krb5cc/tkt")]
    [DataRow("KEYRING:persistent:1000", "KEYRING", "persistent:1000")]
    [DataRow("KCM:", "KCM", "")]
    [DataRow(":x", "", "x")]
    public void Parse_Name_SplitsAtTheFirstColon(string name, string type, string residual)
    {
        Assert.AreEqual(new KerberosFileName(type, residual), KerberosFileName.Parse(name));
    }

    [TestMethod]
    public void KerberosFileException_Error_IsInTheMessage()
    {
        KerberosFileException failure = new(KerberosFileError.UnknownVersion);

        Assert.AreEqual(KerberosFileError.UnknownVersion, failure.Error);
        Assert.AreEqual("Kerberos file could not be read or written: UnknownVersion.", failure.Message);
    }

    [TestMethod]
    public void KerberosPrincipal_SingleComponent_PrintsAsKlistDoes()
    {
        Assert.AreEqual("alice@EXAMPLE.TEST", new KerberosPrincipal(1, "EXAMPLE.TEST", ["alice"]).ToString());
    }
}
